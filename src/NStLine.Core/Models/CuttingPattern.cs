namespace NStLine.Core.Models;

public sealed class CuttingPattern
{
    public int Multiplicity { get; set; }
    public IReadOnlyList<PlacedPart> Parts { get; set; } = Array.Empty<PlacedPart>();
    public double Remainder { get; set; }
}
