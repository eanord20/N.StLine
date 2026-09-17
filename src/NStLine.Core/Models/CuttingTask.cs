namespace NStLine.Core.Models;

public sealed class CuttingTask
{
    public LinearMaterial Material { get; set; } = null!;
    public double BarLength { get; set; }
    public double CutWidth { get; set; }
    public IReadOnlyList<PartRequirement> Parts { get; set; } = Array.Empty<PartRequirement>();
}
