using NStLine.Core.Contracts;
using NStLine.Core.Models;

namespace NStLine.Cutting.Services;

public sealed class ProjectCuttingEngine : IProjectCuttingEngine
{
    private readonly ICuttingEngine _cuttingEngine;

    public ProjectCuttingEngine(ICuttingEngine cuttingEngine)
    {
        _cuttingEngine = cuttingEngine;
    }

    public ProjectCuttingResult Solve(
        ProjectCuttingTask task,
        CuttingOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(options);

        if (task.Materials.Count == 0)
            throw new ArgumentException("At least one material is required.", nameof(task));

        var materialsById = task.Materials.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var unknownMaterial = task.Parts.FirstOrDefault(x => !materialsById.ContainsKey(x.MaterialId));
        if (unknownMaterial != null)
            throw new ArgumentException($"Part material '{unknownMaterial.MaterialId}' is missing.", nameof(task));

        var nonEmptyMaterials = task.Materials
            .Select(material => new
            {
                Material = material,
                Parts = task.Parts.Where(part => part.MaterialId == material.Id).ToList()
            })
            .Where(x => x.Parts.Count > 0)
            .ToList();

        if (nonEmptyMaterials.Count == 0)
            throw new ArgumentException("At least one part is required.", nameof(task));

        var results = new List<MaterialCuttingResult>(nonEmptyMaterials.Count);
        for (var index = 0; index < nonEmptyMaterials.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = nonEmptyMaterials[index];
            var cuttingTask = new CuttingTask
            {
                Material = group.Material,
                BarLength = group.Material.StandardLength,
                CutWidth = group.Material.CutWidth,
                Parts = group.Parts
            };
            var result = _cuttingEngine.Solve(cuttingTask, options, null, cancellationToken);
            results.Add(new MaterialCuttingResult
            {
                Material = group.Material,
                Task = cuttingTask,
                Result = result
            });
            progress?.Report((index + 1.0) / nonEmptyMaterials.Count);
        }

        return new ProjectCuttingResult { Materials = results };
    }
}
