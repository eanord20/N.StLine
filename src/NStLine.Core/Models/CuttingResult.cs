namespace NStLine.Core.Models;

public sealed class CuttingResult
{
    public CalculationStatus Status { get; set; }
    public BestFoundReason BestFoundReason { get; set; } = BestFoundReason.None;
    public IReadOnlyList<BarCutting> Bars { get; set; } = Array.Empty<BarCutting>();
    public long ExploredNodes { get; set; }
    public long GeneratedPatterns { get; set; }
    public long PrunedByBarBound { get; set; }
    public long PrunedByRemainderBound { get; set; }
    public long PrunedByDominance { get; set; }

    public int TotalBarCount => Bars.Count;
    public double TotalBarLength => Bars.Sum(b => b.BarLength);
    public double TotalPartLength => Bars.Sum(b => b.PartLength);
    public double TotalCutLoss => Bars.Sum(b => b.CutLoss);
    public double TotalRemainder => Bars.Sum(b => b.Remainder);

    public double UtilizationPercent { get; set; }
    public IReadOnlyList<CuttingPattern> Patterns { get; set; } = Array.Empty<CuttingPattern>();
    public string? Method { get; set; }
    public double ElapsedSeconds { get; set; }
}
