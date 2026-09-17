using NStLine.Core.Models;

namespace NStLine.App.Models;

public sealed class ProjectData
{
    public LinearMaterial? Material { get; set; }
    public double BarLength { get; set; }
    public double CutWidth { get; set; }
    public List<PartRequirement> Parts { get; set; } = new();
}
