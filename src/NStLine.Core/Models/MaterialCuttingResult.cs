namespace NStLine.Core.Models;

public sealed class MaterialCuttingResult
{
    public LinearMaterial Material { get; set; } = null!;
    public CuttingTask Task { get; set; } = null!;
    public CuttingResult Result { get; set; } = null!;
}
