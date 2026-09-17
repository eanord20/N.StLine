namespace NStLine.Core.Models;

public sealed class BarCutting
{
    public int Index { get; set; }
    public double BarLength { get; set; }
    public IReadOnlyList<PlacedPart> Parts { get; set; } = Array.Empty<PlacedPart>();
    public int CutCount => Parts.Count;
    public double CutLoss { get; set; }
    public double Remainder { get; set; }

    public double PartLength => Parts.Sum(p => p.Source.Length);
    public double UsedLength => PartLength + CutLoss;
}
