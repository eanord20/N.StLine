using NStLine.App.Models;
using NStLine.Core.Models;

namespace NStLine.App.Services;

public interface IReportService
{
    void GenerateReport(string outputPath, ProjectCuttingTask task, ProjectCuttingResult result,
        ReportGenerationOptions? options = null);
}
