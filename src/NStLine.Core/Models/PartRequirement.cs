namespace NStLine.Core.Models;

public sealed class PartRequirement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MaterialId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Length { get; set; }
    public int Quantity { get; set; } = 1;
}
