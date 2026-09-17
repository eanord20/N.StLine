namespace NStLine.Core.Models;

public sealed class ProjectCuttingTask
{
    public IReadOnlyList<LinearMaterial> Materials { get; set; } = Array.Empty<LinearMaterial>();
    public IReadOnlyList<PartRequirement> Parts { get; set; } = Array.Empty<PartRequirement>();
}
