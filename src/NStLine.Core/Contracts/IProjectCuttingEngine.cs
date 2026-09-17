using NStLine.Core.Models;

namespace NStLine.Core.Contracts;

public interface IProjectCuttingEngine
{
    ProjectCuttingResult Solve(
        ProjectCuttingTask task,
        CuttingOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
