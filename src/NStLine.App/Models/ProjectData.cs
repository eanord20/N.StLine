using System.Text.Json.Serialization;
using NStLine.Core.Models;

namespace NStLine.App.Models;

public sealed class ProjectData
{
    public List<LinearMaterial> Materials { get; set; } = new();
    public List<PartRequirement> Parts { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinearMaterial? Material { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double? BarLength { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double? CutWidth { get; set; }
}
