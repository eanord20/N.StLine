namespace NStLine.Core.Models;

public sealed class CuttingResult
{
    public CalculationStatus Status { get; set; }
    public IReadOnlyList<BarCutting> Bars { get; set; } = Array.Empty<BarCutting>();

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
