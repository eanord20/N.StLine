namespace NStLine.Core.Models;

public sealed class CuttingOptions
{
    public double TimeLimitSeconds { get; set; } = 30.0;
    public int MaxPatternCount { get; set; } = 100_000;
    public double ToleranceMm { get; set; } = 0.01;
}
