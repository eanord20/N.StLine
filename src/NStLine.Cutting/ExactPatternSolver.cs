using System.Diagnostics;
using NStLine.Core.Models;

namespace NStLine.Cutting;

internal sealed class ExactPatternSolver
{
    private readonly CuttingTask _task;
    private readonly CuttingOptions _options;
    private readonly IReadOnlyList<PartRequirement> _parts;
    private readonly int _partTypeCount;
    private readonly List<Pattern> _patterns = new();
    private readonly int[] _maxCountsPerType;
    private int[][]? _suffixMaxCounts;

    private int _bestBarCount = int.MaxValue;
    private double _bestRemainder = double.MaxValue;
    private List<(Pattern Pattern, int Multiplicity)> _bestSolution = new();
    private long _nodeCount;
    private long _prunedByBarBound;
    private long _prunedByRemainderBound;
    private long _prunedByDominance;
    private readonly Stopwatch _stopwatch = new();
    private readonly TimeSpan _timeLimit;
    private readonly CancellationToken _cancellationToken;
    private bool _patternLimitReached;
    private bool _timeLimitReached;
    private readonly Dictionary<StateKey, PrefixCost> _stateCache = new(StateKeyComparer.Instance);
    private const int MaxStateCacheEntries = 200_000;

    public ExactPatternSolver(CuttingTask task, CuttingOptions options, CancellationToken cancellationToken)
    {
        _task = task;
        _options = options;
        _cancellationToken = cancellationToken;
        _parts = task.Parts
            .OrderByDescending(part => part.Length)
            .ThenByDescending(part => part.Quantity)
            .ThenBy(part => part.Id, StringComparer.Ordinal)
            .ToList();
        _partTypeCount = task.Parts.Count;
        _maxCountsPerType = new int[_partTypeCount];
        _timeLimit = TimeSpan.FromSeconds(options.TimeLimitSeconds);
    }

    public bool SearchCompleted { get; private set; }
    public bool PatternLimitReached => _patternLimitReached;
    public bool TimeLimitReached => _timeLimitReached;
    public long ExploredNodes => _nodeCount;
    public long GeneratedPatterns => _patterns.Count;
    public long PrunedByBarBound => _prunedByBarBound;
    public long PrunedByRemainderBound => _prunedByRemainderBound;
    public long PrunedByDominance => _prunedByDominance;
    public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    public void SetIncumbent(int barCount, double totalRemainder)
    {
        _bestBarCount = barCount;
        _bestRemainder = totalRemainder;
    }

    public CuttingResult? Solve()
    {
        if (!_stopwatch.IsRunning)
            _stopwatch.Start();

        var demand = _parts.Select(p => p.Quantity).ToArray();
        var chosen = new Stack<(Pattern Pattern, int Multiplicity)>();

        try
        {
            if (!GeneratePatterns())
                return null;

            if (_patterns.Count == 0)
                return null;

        ComputeMaxCounts();
        SortPatterns();
        BuildSuffixMaxCounts();
            Search(0, demand, 0, 0.0, chosen);
            SearchCompleted = true;
        }
        catch (OperationCanceledException)
        {
            // best effort result will be returned below
        }

        if (_bestSolution.Count == 0)
            return null;

        var status = _cancellationToken.IsCancellationRequested || _stopwatch.Elapsed >= _timeLimit
            ? CalculationStatus.BestFound
            : CalculationStatus.Optimal;

        return BuildResult(status, "Exact branch-and-bound");
    }

    private bool GeneratePatterns()
    {
        var counts = new int[_partTypeCount];
        GenerateRecursive(0, 0.0, 0, counts);
        return _patterns.Count <= _options.MaxPatternCount;
    }

    private void GenerateRecursive(int index, double usedLength, int partCount, int[] counts)
    {
        if (_stopwatch.Elapsed >= _timeLimit)
        {
            _timeLimitReached = true;
            throw new OperationCanceledException();
        }

        if (index == _partTypeCount)
        {
            if (partCount == 0)
                return;

            double remainder = _task.BarLength - usedLength - partCount * _task.CutWidth;
            if (remainder >= -_options.ToleranceMm)
            {
                remainder = Math.Max(0.0, remainder);
                _patterns.Add(new Pattern(counts.ToArray(), usedLength, partCount, remainder));
            }
            return;
        }

        double length = _parts[index].Length;
        double denominator = length + _task.CutWidth;
        if (denominator <= 0)
            denominator = length;

        int maxCount = 0;
        if (denominator > 0)
        {
            double available = _task.BarLength - usedLength - partCount * _task.CutWidth;
            maxCount = (int)Math.Floor(available / denominator);
        }
        maxCount = Math.Min(_parts[index].Quantity, Math.Max(0, maxCount));

        for (int c = maxCount; c >= 0; c--)
        {
            if (_cancellationToken.IsCancellationRequested)
                return;

            if (_patterns.Count > _options.MaxPatternCount)
            {
                _patternLimitReached = true;
                return;
            }

            counts[index] = c;
            GenerateRecursive(index + 1,
                usedLength + c * length,
                partCount + c,
                counts);
        }

        counts[index] = 0;
    }

    private void ComputeMaxCounts()
    {
        for (int i = 0; i < _partTypeCount; i++)
        {
            int max = 0;
            foreach (var pattern in _patterns)
            {
                if (pattern.Counts[i] > max)
                    max = pattern.Counts[i];
            }
            _maxCountsPerType[i] = max;
        }
    }

    private void SortPatterns()
    {
        // Prefer patterns that use more material and have fewer cuts for the same length.
        _patterns.Sort((a, b) =>
        {
            int cmp = b.UsedLength.CompareTo(a.UsedLength);
            if (cmp != 0) return cmp;
            return a.Remainder.CompareTo(b.Remainder);
        });
    }

    private void Search(int patternIndex, int[] remaining, int barCount, double totalRemainder,
        Stack<(Pattern Pattern, int Multiplicity)> chosen)
    {
        if (_cancellationToken.IsCancellationRequested || _stopwatch.Elapsed >= _timeLimit)
        {
            if (_stopwatch.Elapsed >= _timeLimit)
                _timeLimitReached = true;
            throw new OperationCanceledException();
        }

        // Check if all demand satisfied before running out of patterns.
        bool allZero = true;
        for (int i = 0; i < _partTypeCount; i++)
        {
            if (remaining[i] != 0)
            {
                allZero = false;
                break;
            }
        }

        if (allZero)
        {
            UpdateBest(barCount, totalRemainder, chosen);
            return;
        }

        if (patternIndex >= _patterns.Count)
            return;

        _nodeCount++;
        if (_nodeCount % 1024 == 0)
        {
            if (_cancellationToken.IsCancellationRequested || _stopwatch.Elapsed >= _timeLimit)
            {
                if (_stopwatch.Elapsed >= _timeLimit)
                    _timeLimitReached = true;
                throw new OperationCanceledException();
            }
        }

        // Lower bound on additional bars.
        int lowerBound = ComputeLowerBound(remaining, patternIndex);
        if (lowerBound == int.MaxValue)
        {
            _prunedByBarBound++;
            return;
        }
        int projectedBars = barCount + lowerBound;

        if (projectedBars > _bestBarCount)
        {
            _prunedByBarBound++;
            return;
        }

        if (projectedBars == _bestBarCount && totalRemainder >= _bestRemainder)
        {
            _prunedByRemainderBound++;
            return;
        }

        var stateKey = new StateKey(patternIndex, remaining);
        var prefixCost = new PrefixCost(barCount, totalRemainder);
        if (_stateCache.TryGetValue(stateKey, out var seenPrefixCost))
        {
            if (seenPrefixCost.Dominates(prefixCost))
            {
                _prunedByDominance++;
                return;
            }

            if (prefixCost.Dominates(seenPrefixCost))
                _stateCache[stateKey] = prefixCost;
        }
        else if (_stateCache.Count < MaxStateCacheEntries)
        {
            _stateCache.Add(stateKey, prefixCost);
        }

        var pattern = _patterns[patternIndex];

        // Maximum multiplicity of this pattern limited by remaining demand.
        int maxMult = int.MaxValue;
        for (int i = 0; i < _partTypeCount; i++)
        {
            if (pattern.Counts[i] == 0)
                continue;
            int allowed = remaining[i] / pattern.Counts[i];
            if (allowed < maxMult)
                maxMult = allowed;
        }
        if (maxMult == int.MaxValue)
            maxMult = 0;

        // Try using the pattern as many times as possible, then fewer, then zero.
        for (int mult = maxMult; mult >= 0; mult--)
        {
            if (mult > 0)
            {
                for (int i = 0; i < _partTypeCount; i++)
                    remaining[i] -= mult * pattern.Counts[i];
                chosen.Push((pattern, mult));
            }

            Search(patternIndex + 1, remaining, barCount + mult, totalRemainder + mult * pattern.Remainder, chosen);

            if (mult > 0)
            {
                chosen.Pop();
                for (int i = 0; i < _partTypeCount; i++)
                    remaining[i] += mult * pattern.Counts[i];
            }
        }
    }

    private void BuildSuffixMaxCounts()
    {
        if (_patterns.Count == 0)
        {
            _suffixMaxCounts = null;
            return;
        }

        var cells = (long)_patterns.Count * _partTypeCount;
        if (cells > 8_000_000)
        {
            _suffixMaxCounts = null;
            return;
        }

        _suffixMaxCounts = new int[_patterns.Count + 1][];
        _suffixMaxCounts[_patterns.Count] = new int[_partTypeCount];
        for (var patternIndex = _patterns.Count - 1; patternIndex >= 0; patternIndex--)
        {
            var current = new int[_partTypeCount];
            var suffixNext = _suffixMaxCounts[patternIndex + 1];
            var counts = _patterns[patternIndex].Counts;
            for (var partIndex = 0; partIndex < _partTypeCount; partIndex++)
                current[partIndex] = Math.Max(suffixNext[partIndex], counts[partIndex]);
            _suffixMaxCounts[patternIndex] = current;
        }
    }

    private int ComputeLowerBound(int[] remaining, int patternIndex)
    {
        double totalPartLength = 0.0;
        int totalParts = 0;
        int perTypeMax = 0;
        var maxCounts = _suffixMaxCounts != null && patternIndex < _suffixMaxCounts.Length
            ? _suffixMaxCounts[patternIndex]
            : _maxCountsPerType;

        for (int i = 0; i < _partTypeCount; i++)
        {
            if (remaining[i] <= 0)
                continue;

            totalPartLength += remaining[i] * _parts[i].Length;
            totalParts += remaining[i];

            if (maxCounts[i] > 0)
            {
                int barsForType = (int)Math.Ceiling((double)remaining[i] / maxCounts[i]);
                if (barsForType > perTypeMax)
                    perTypeMax = barsForType;
            }
            else
            {
                return int.MaxValue;
            }
        }

        double totalNeeded = totalPartLength + totalParts * _task.CutWidth;
        int lengthBound = (int)Math.Ceiling(totalNeeded / _task.BarLength);

        return Math.Max(lengthBound, perTypeMax);
    }

    private void UpdateBest(int barCount, double totalRemainder, IEnumerable<(Pattern Pattern, int Multiplicity)> chosen)
    {
        if (barCount < _bestBarCount || (barCount == _bestBarCount && totalRemainder < _bestRemainder))
        {
            _bestBarCount = barCount;
            _bestRemainder = totalRemainder;
            _bestSolution = chosen.ToList();
        }
    }

    private CuttingResult BuildResult(CalculationStatus status, string method)
    {
        var bars = new List<BarCutting>();
        int index = 1;

        foreach (var (pattern, multiplicity) in _bestSolution)
        {
            for (int m = 0; m < multiplicity; m++)
            {
                var placedParts = new List<PlacedPart>();
                for (int i = 0; i < _partTypeCount; i++)
                {
                    for (int c = 0; c < pattern.Counts[i]; c++)
                    {
                        placedParts.Add(new PlacedPart
                        {
                            Source = _parts[i],
                            CopyIndex = c + 1
                        });
                    }
                }

                bars.Add(new BarCutting
                {
                    Index = index++,
                    BarLength = _task.BarLength,
                    Parts = placedParts,
                    CutLoss = pattern.PartCount * _task.CutWidth,
                    Remainder = pattern.Remainder
                });
            }
        }

        var result = new CuttingResult
        {
            Status = status,
            BestFoundReason = status == CalculationStatus.BestFound && _timeLimitReached
                ? BestFoundReason.TimeLimitReached
                : BestFoundReason.None,
            Bars = bars,
            Method = method,
            ElapsedSeconds = _stopwatch.Elapsed.TotalSeconds,
            ExploredNodes = _nodeCount,
            GeneratedPatterns = _patterns.Count,
            PrunedByBarBound = _prunedByBarBound,
            PrunedByRemainderBound = _prunedByRemainderBound,
            PrunedByDominance = _prunedByDominance
        };

        result.Patterns = GroupPatterns(result.Bars).ToList();
        result.UtilizationPercent = result.TotalBarLength > 0
            ? result.TotalPartLength / result.TotalBarLength * 100.0
            : 0.0;

        return result;
    }

    private static IEnumerable<CuttingPattern> GroupPatterns(IReadOnlyList<BarCutting> bars)
    {
        var groups = bars.GroupBy(GetPatternKey);
        foreach (var group in groups)
        {
            yield return new CuttingPattern
            {
                Multiplicity = group.Count(),
                Parts = group.First().Parts,
                Remainder = group.First().Remainder
            };
        }
    }

    private static string GetPatternKey(BarCutting bar)
    {
        var ids = string.Join(",", bar.Parts.Select(p => p.Source.Id));
        return $"{ids}|{bar.Remainder:F4}";
    }

    private sealed class Pattern
    {
        public int[] Counts { get; }
        public double UsedLength { get; }
        public int PartCount { get; }
        public double Remainder { get; }

        public Pattern(int[] counts, double usedLength, int partCount, double remainder)
        {
            Counts = counts;
            UsedLength = usedLength;
            PartCount = partCount;
            Remainder = remainder;
        }
    }

    private readonly record struct PrefixCost(int BarCount, double TotalRemainder)
    {
        public bool Dominates(PrefixCost other)
            => BarCount < other.BarCount || (BarCount == other.BarCount && TotalRemainder <= other.TotalRemainder);
    }

    private readonly struct StateKey
    {
        public int PatternIndex { get; }
        public int[] Remaining { get; }

        public StateKey(int patternIndex, int[] remaining)
        {
            PatternIndex = patternIndex;
            Remaining = (int[])remaining.Clone();
        }
    }

    private sealed class StateKeyComparer : IEqualityComparer<StateKey>
    {
        public static StateKeyComparer Instance { get; } = new();

        public bool Equals(StateKey x, StateKey y)
        {
            if (x.PatternIndex != y.PatternIndex || x.Remaining.Length != y.Remaining.Length)
                return false;

            for (var index = 0; index < x.Remaining.Length; index++)
            {
                if (x.Remaining[index] != y.Remaining[index])
                    return false;
            }

            return true;
        }

        public int GetHashCode(StateKey obj)
        {
            var hash = new HashCode();
            hash.Add(obj.PatternIndex);
            foreach (var value in obj.Remaining)
                hash.Add(value);
            return hash.ToHashCode();
        }
    }
}
