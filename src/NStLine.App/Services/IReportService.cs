using NStLine.Core.Models;

namespace NStLine.App.Services;

public interface IReportService
{
    void GenerateReport(string outputPath, ProjectCuttingTask task, ProjectCuttingResult result);
}
