using NStLine.Core.Models;

namespace NStLine.Cutting;

internal static class FirstFitDecreasingSolver
{
    public static CuttingResult Solve(CuttingTask task, CuttingOptions options)
    {
        var copies = task.Parts
            .SelectMany(p => Enumerable.Range(1, p.Quantity)
                .Select(i => new PlacedPart { Source = p, CopyIndex = i }))
            .OrderByDescending(p => p.Source.Length)
            .ThenBy(p => p.Source.Name)
            .ToList();

        var bars = new List<BarState>();

        foreach (var part in copies)
        {
            bool placed = false;
            foreach (var bar in bars)
            {
                if (bar.TryPlace(part, task.CutWidth))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                var newBar = new BarState(task.BarLength);
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
            var bar = bars[i];
            int cutCount = bar.CutCount;
            double partLength = bar.UsedPartLength;
            double cutLoss = bar.CutLoss(task.CutWidth);
            double remainder = task.BarLength - partLength - cutLoss;

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
        public double Length { get; }
        public List<PlacedPart> Parts { get; } = new();
        public int CutCount { get; private set; }
        public double UsedPartLength { get; private set; }

        public BarState(double length)
        {
            Length = length;
        }

        public double CutLoss(double cutWidth) => CutCount * cutWidth;

        public bool TryPlace(PlacedPart part, double cutWidth)
        {
            int newCutCount = CutCount + 1;
            double newPartLength = UsedPartLength + part.Source.Length;
            double newUsedLength = newPartLength + newCutCount * cutWidth;

            if (newUsedLength <= Length + 1e-9)
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
