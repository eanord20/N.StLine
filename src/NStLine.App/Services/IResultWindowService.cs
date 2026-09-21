using NStLine.Core.Models;

namespace NStLine.App.Services;

public interface IResultWindowService
{
    void Show(ProjectCuttingTask task, ProjectCuttingResult result);
}
