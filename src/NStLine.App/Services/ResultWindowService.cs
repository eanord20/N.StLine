using NStLine.App.ViewModels;
using NStLine.App.Views;
using NStLine.Core.Models;
using Application = System.Windows.Application;

namespace NStLine.App.Services;

public sealed class ResultWindowService : IResultWindowService
{
    private readonly IReportService _reportService;

    public ResultWindowService(IReportService reportService)
    {
        _reportService = reportService;
    }

    public void Show(ProjectCuttingTask task, ProjectCuttingResult result)
    {
        var window = new ResultWindow(new ResultWindowViewModel(task, result, _reportService))
        {
            Owner = Application.Current?.MainWindow
        };
        window.Show();
    }
}
