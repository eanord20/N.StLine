using NStLine.Core.Contracts;
using NStLine.Core.Models;

namespace NStLine.Cutting.Services;

public sealed class CuttingEngine : ICuttingEngine
{
    public CuttingResult Solve(CuttingTask task, CuttingOptions options, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Validate(task);

        // Try exact solver first if the pattern space is not too large.
        var exactSolver = new ExactPatternSolver(task, options, cancellationToken);
        CuttingResult? exactResult;

        try
        {
            exactResult = exactSolver.Solve();
        }
        catch (OperationCanceledException)
        {
            exactResult = null;
        }

        var heuristicResult = FirstFitDecreasingSolver.Solve(task, options);

        if (exactResult == null)
            return heuristicResult;

        // If the exact solver proved optimality, prefer it even if the heuristic is equally good.
        if (exactResult.Status == CalculationStatus.Optimal)
            return exactResult;

        // Both ran but neither is proven optimal: pick the better result, keeping the honest status.
        bool exactBetter = exactResult.TotalBarCount < heuristicResult.TotalBarCount ||
                           (exactResult.TotalBarCount == heuristicResult.TotalBarCount &&
                            exactResult.TotalRemainder < heuristicResult.TotalRemainder);

        return exactBetter ? exactResult : heuristicResult;
    }

    private static void Validate(CuttingTask task)
    {
        if (task.Material == null)
            throw new ArgumentException("Material is not selected.", nameof(task));

        if (task.BarLength <= 0)
            throw new ArgumentException("Bar length must be greater than zero.", nameof(task));

        if (task.CutWidth < 0 || task.CutWidth >= task.BarLength)
            throw new ArgumentException("Cut width must be non-negative and less than bar length.", nameof(task));

        if (task.Parts == null || task.Parts.Count == 0)
            throw new ArgumentException("At least one part is required.", nameof(task));

        foreach (var part in task.Parts)
        {
            if (part == null)
                continue;

            if (part.Length <= 0)
                throw new ArgumentException($"Part '{part.Name}' length must be greater than zero.", nameof(task));

            if (part.Quantity <= 0)
                throw new ArgumentException($"Part '{part.Name}' quantity must be greater than zero.", nameof(task));

            if (part.Length + task.CutWidth > task.BarLength)
                throw new ArgumentException($"Part '{part.Name}' does not fit into a single bar.", nameof(task));
        }
    }
}
