namespace NStLine.Core.Models;

public sealed class ProjectCuttingResult
{
    public IReadOnlyList<MaterialCuttingResult> Materials { get; set; } = Array.Empty<MaterialCuttingResult>();

    public CalculationStatus Status => Materials.All(x => x.Result.Status == CalculationStatus.Optimal)
        ? CalculationStatus.Optimal
        : CalculationStatus.BestFound;
    public int TotalBarCount => Materials.Sum(x => x.Result.TotalBarCount);
    public double TotalBarLength => Materials.Sum(x => x.Result.TotalBarLength);
    public double TotalPartLength => Materials.Sum(x => x.Result.TotalPartLength);
    public double TotalCutLoss => Materials.Sum(x => x.Result.TotalCutLoss);
    public double TotalRemainder => Materials.Sum(x => x.Result.TotalRemainder);
    public int DistinctPatternCount => Materials.Sum(x => x.Result.Patterns.Count);
    public double UtilizationPercent => TotalBarLength > 0 ? TotalPartLength / TotalBarLength * 100.0 : 0.0;
}
