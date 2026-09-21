using System.Text;
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
    {
        var text = new StringBuilder();
        text.AppendLine(StatusText);
        text.AppendLine($"{LocalizationService.Instance.LabelTotalBars}: {Result.TotalBarCount}");
        text.AppendLine($"{LocalizationService.Instance.LabelTotalPartLength}: {Result.TotalPartLength:0.##}");
        text.AppendLine($"{LocalizationService.Instance.LabelTotalCutLoss}: {Result.TotalCutLoss:0.##}");
        text.AppendLine($"{LocalizationService.Instance.LabelTotalRemainder}: {Result.TotalRemainder:0.##}");
        text.AppendLine($"{LocalizationService.Instance.LabelUtilization}: {Result.UtilizationPercent:0.##}%");

        foreach (var materialResult in Materials)
        {
            text.AppendLine();
            text.AppendLine(materialResult.Material.Name);
            foreach (var bar in materialResult.Result.Bars)
            {
                var parts = CuttingTextFormatter.FormatGroupedParts(bar.Parts);
                text.AppendLine($"{bar.BarLength:0.##} ({bar.CutCount}) = {parts} + {bar.Remainder:0.##}");
            }
        }

        return text.ToString();
    }

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
