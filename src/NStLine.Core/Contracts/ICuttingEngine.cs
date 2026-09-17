using NStLine.Core.Models;

namespace NStLine.Core.Contracts;

public interface ICuttingEngine
{
    CuttingResult Solve(CuttingTask task,
                          CuttingOptions options,
                          IProgress<double>? progress = null,
                          CancellationToken cancellationToken = default);
}
