using System.Windows.Input;
using NStLine.App.Commands;
using NStLine.App.Formatters;
using NStLine.App.Services;
using NStLine.Core.Models;
using Clipboard = System.Windows.Clipboard;

namespace NStLine.App.ViewModels;

public sealed class ResultWindowViewModel
{
    private readonly IReportService? _reportService;

    public ResultWindowViewModel(
        ProjectCuttingTask task,
        ProjectCuttingResult result,
        IReportService? reportService)
    {
        Task = task;
        Result = result;
        _reportService = reportService;
        CopyCommand = new RelayCommand(_ => Clipboard.SetText(BuildCopyText()));
        ExportDocxCommand = new RelayCommand(_ => ExportDocx(), _ => _reportService != null);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? CloseRequested;

    public ProjectCuttingTask Task { get; }
    public ProjectCuttingResult Result { get; }
    public IReadOnlyList<MaterialCuttingResult> Materials => Result.Materials;
    public string StatusText => Result.Status == CalculationStatus.Optimal
        ? LocalizationService.Instance.StatusOptimal
        : LocalizationService.Instance.StatusBestFound;

    public ICommand CopyCommand { get; }
    public ICommand ExportDocxCommand { get; }
    public ICommand CloseCommand { get; }

    public string BuildCopyText()
        => CuttingTextFormatter.FormatProjectResult(Result, LocalizationService.Instance);

    private void ExportDocx()
    {
        if (_reportService == null)
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "DOCX document (*.docx)|*.docx",
            DefaultExt = ".docx"
        };
        if (dialog.ShowDialog() == true)
            _reportService.GenerateReport(dialog.FileName, Task, Result);
    }
}
