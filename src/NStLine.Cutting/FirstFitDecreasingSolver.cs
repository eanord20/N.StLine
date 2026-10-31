using System.Numerics;
using NStLine.Core.Models;

namespace NStLine.Cutting;

internal static class FirstFitDecreasingSolver
{
    public static CuttingResult Solve(CuttingTask task, CuttingOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copies = task.Parts
            .OrderByDescending(part => part.Length)
            .ThenBy(part => part.Name)
            .SelectMany(part => Enumerable.Range(1, part.Quantity)
                .Select(index => new PlacedPart { Source = part, CopyIndex = index }));

        var lengthScale = new LengthScale(task.Parts.Select(part => part.Length).Append(task.BarLength).Append(task.CutWidth).ToArray());
        var bars = new List<BarState>();

        foreach (var part in copies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool placed = false;
            foreach (var bar in bars)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (bar.TryPlace(part, task.CutWidth))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                var newBar = new BarState(task.BarLength, lengthScale);
                if (!newBar.TryPlace(part, task.CutWidth))
                {
                    // Should be caught by validation; defensive fallback to a zero-remainder bar.
                    newBar.Parts.Add(part);
                }
                bars.Add(newBar);
            }
        }

        var resultBars = new List<BarCutting>();
        for (int i = 0; i < bars.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bar = bars[i];
            int cutCount = bar.CutCount;
            var partLength = bar.UsedPartLength;
            double cutLoss = bar.CutLoss(task.CutWidth);
            var remainder = lengthScale.ToMillimeters(lengthScale[task.BarLength] - partLength - cutCount * lengthScale[task.CutWidth]);

            resultBars.Add(new BarCutting
            {
                Index = i + 1,
                BarLength = task.BarLength,
                Parts = bar.Parts.ToList(),
                CutLoss = cutLoss,
                Remainder = remainder
            });
        }

        var result = new CuttingResult
        {
            Status = CalculationStatus.BestFound,
            Bars = resultBars,
            Method = "First Fit Decreasing",
            ElapsedSeconds = 0
        };

        result.Patterns = GroupPatterns(resultBars).ToList();
        result.UtilizationPercent = result.TotalBarLength > 0
            ? result.TotalPartLength / result.TotalBarLength * 100.0
            : 0.0;

        return result;
    }

    private static IEnumerable<CuttingPattern> GroupPatterns(IReadOnlyList<BarCutting> bars)
    {
        return bars
            .GroupBy(b => GetPatternKey(b))
            .Select(g => new CuttingPattern
            {
                Multiplicity = g.Count(),
                Parts = g.First().Parts,
                Remainder = g.First().Remainder
            });
    }

    private static string GetPatternKey(BarCutting bar)
    {
        var ids = string.Join(",", bar.Parts.Select(p => p.Source.Id));
        return $"{ids}|{bar.Remainder:F4}";
    }

    private sealed class BarState
    {
        private readonly LengthScale _lengthScale;
        public BigInteger Length { get; }
        public List<PlacedPart> Parts { get; } = new();
        public int CutCount { get; private set; }
        public BigInteger UsedPartLength { get; private set; }

        public BarState(double length, LengthScale lengthScale)
        {
            _lengthScale = lengthScale;
            Length = lengthScale[length];
        }

        public double CutLoss(double cutWidth) => CutCount * cutWidth;

        public bool TryPlace(PlacedPart part, double cutWidth)
        {
            int newCutCount = CutCount + 1;
            var newPartLength = UsedPartLength + _lengthScale[part.Source.Length];
            var newUsedLength = newPartLength + newCutCount * _lengthScale[cutWidth];

            if (newUsedLength <= Length)
            {
                Parts.Add(part);
                CutCount = newCutCount;
                UsedPartLength = newPartLength;
                return true;
            }

            return false;
        }
    }
}
