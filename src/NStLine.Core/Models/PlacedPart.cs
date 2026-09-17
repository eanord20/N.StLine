namespace NStLine.Core.Models;

public sealed class PlacedPart
{
    public PartRequirement Source { get; set; } = null!;
    public int CopyIndex { get; set; }
}
