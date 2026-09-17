namespace NStLine.Core.Models;

public sealed class LinearMaterial
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public double StandardLength { get; set; }
    public double CutWidth { get; set; }
}
