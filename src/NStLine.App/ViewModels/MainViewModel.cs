using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using NStLine.App.Commands;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.Core.Constants;
using NStLine.Core.Contracts;
using NStLine.Core.Models;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace NStLine.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private readonly IMaterialsLibraryService _materialsLibraryService;
    private readonly IProjectFileService? _projectFileService;
    private readonly ICuttingEngine _cuttingEngine;

    private LinearMaterial? _selectedMaterial;
    private double _barLength;
    private double _cutWidth;
    private CuttingResult? _result;
    private string _statusText = string.Empty;
    private double _progressValue;
    private bool _isBusy;
    private PartRow? _selectedPart;

    public MainViewModel(ICuttingEngine cuttingEngine,
                         IMaterialsLibraryService? materialsLibraryService = null,
                         IProjectFileService? projectFileService = null)
    {
        _cuttingEngine = cuttingEngine;
        _materialsLibraryService = materialsLibraryService ?? new MaterialsLibraryService();
        _projectFileService = projectFileService;

        AddRowCommand = new RelayCommand(_ => AddRow());
        DeleteRowCommand = new RelayCommand(_ => DeleteRow(), _ => SelectedPart != null);
        PasteFromClipboardCommand = new RelayCommand(_ => PasteFromClipboard());
        CalculateCommand = new RelayCommand(async _ => await CalculateAsync(), _ => CanCalculate());
        CopyResultsCommand = new RelayCommand(_ => CopyResults(), _ => Result != null);
        OpenMaterialsCommand = new RelayCommand(_ => OpenMaterials());
        ClearAllCommand = new RelayCommand(_ => ClearAll(), _ => Parts.Count > 0 || Result != null);
        SaveProjectCommand = new RelayCommand(_ => SaveProject(), _ => CanSaveProject());
        OpenProjectCommand = new RelayCommand(_ => OpenProject());
        SetLanguageCommand = new RelayCommand(lang => SetLanguage(lang?.ToString() ?? "en"));
        SetThemeCommand = new RelayCommand(theme =>
        {
            if (Enum.TryParse<AppTheme>(theme?.ToString() ?? "Light", true, out var t))
                SetTheme(t);
        });

        LoadLibrary();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PartRow> Parts { get; } = new();
    public ObservableCollection<LinearMaterial> LibraryMaterials { get; } = new();

    public LinearMaterial? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            _selectedMaterial = value;
            OnPropertyChanged();
            if (value != null && _barLength <= 0)
                BarLength = value.StandardLength;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public double BarLength
    {
        get => _barLength;
        set
        {
            _barLength = value;
            OnPropertyChanged();
            ValidateParts();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public double CutWidth
    {
        get => _cutWidth;
        set
        {
            _cutWidth = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public CuttingResult? Result
    {
        get => _result;
        private set
        {
            _result = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set { _progressValue = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public PartRow? SelectedPart
    {
        get => _selectedPart;
        set
        {
            _selectedPart = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand AddRowCommand { get; }
    public ICommand DeleteRowCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand CalculateCommand { get; }
    public ICommand CopyResultsCommand { get; }
    public ICommand OpenMaterialsCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand SetThemeCommand { get; }

    public string WindowTitle => _localization.WindowTitle;
    public string ButtonMaterials => _localization.ButtonMaterials;
    public string ButtonPaste => _localization.ButtonPaste;
    public string ButtonCalculate => _localization.ButtonCalculate;
    public string ButtonCopyResults => _localization.ButtonCopyResults;
    public string ButtonClear => _localization.ButtonClear;
    public string ButtonAddRow => _localization.ButtonAddRow;
    public string LabelMaterial => _localization.LabelMaterial;
    public string LabelBarLength => _localization.LabelBarLength;
    public string LabelCutWidth => _localization.LabelCutWidth;
    public string StatusOptimal => _localization.StatusOptimal;
    public string StatusBestFound => _localization.StatusBestFound;

    private void LoadLibrary()
    {
        var materials = _materialsLibraryService.LoadAll();
        LibraryMaterials.Clear();
        foreach (var material in materials)
            LibraryMaterials.Add(material);
    }

    private void AddRow()
    {
        Parts.Add(new PartRow { RowNumber = Parts.Count + 1, Quantity = 1 });
        RenumberRows();
    }

    private void DeleteRow()
    {
        if (SelectedPart != null)
        {
            Parts.Remove(SelectedPart);
            RenumberRows();
        }
    }

    private void RenumberRows()
    {
        for (int i = 0; i < Parts.Count; i++)
            Parts[i].RowNumber = i + 1;
    }

    private void PasteFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText())
                return;

            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var cells = line.Split('\t').Select(c => c.Trim()).ToArray();
                if (cells.Length < 2)
                    continue;

                string name = cells[0];
                if (!double.TryParse(cells[1], out var length))
                    continue;

                int quantity = 1;
                if (cells.Length >= 3 && !int.TryParse(cells[2], out quantity))
                    continue;

                var row = new PartRow
                {
                    Name = name,
                    Length = length,
                    Quantity = Math.Max(1, quantity),
                    RowNumber = Parts.Count + 1
                };

                ValidateRow(row);
                Parts.Add(row);
            }

            RenumberRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ValidateParts()
    {
        foreach (var row in Parts)
            ValidateRow(row);
    }

    private void ValidateRow(PartRow row)
    {
        if (row.Length <= 0)
            row.SetError("Length must be greater than zero.");
        else if (row.Quantity <= 0)
            row.SetError("Quantity must be greater than zero.");
        else if (BarLength > 0 && row.Length + CutWidth > BarLength)
            row.SetError("Part does not fit into the bar.");
        else
            row.ClearError();
    }

    private bool CanCalculate()
    {
        if (IsBusy || SelectedMaterial == null || BarLength <= 0 || CutWidth < 0)
            return false;
        if (Parts.Count == 0)
            return false;
        return Parts.All(p => !p.HasError);
    }

    private async Task CalculateAsync()
    {
        IsBusy = true;
        ProgressValue = 0;
        StatusText = "Calculating...";
        Result = null;

        try
        {
            var task = new CuttingTask
            {
                Material = SelectedMaterial!,
                BarLength = BarLength,
                CutWidth = CutWidth,
                Parts = Parts.Select(p => p.ToPartRequirement()).ToList()
            };

            var options = AppConstants.DefaultCuttingOptions;
            var progress = new Progress<double>(v => ProgressValue = v * 100);

            Result = await Task.Run(() => _cuttingEngine.Solve(task, options, progress));
            StatusText = Result.Status == CalculationStatus.Optimal
                ? StatusOptimal
                : StatusBestFound;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    private void CopyResults()
    {
        if (Result == null)
            return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{StatusText}");
        sb.AppendLine($"{_localization.LabelTotalBars}: {Result.TotalBarCount}");
        sb.AppendLine($"{_localization.LabelTotalPartLength}: {Result.TotalPartLength:F1}");
        sb.AppendLine($"{_localization.LabelTotalCutLoss}: {Result.TotalCutLoss:F1}");
        sb.AppendLine($"{_localization.LabelTotalRemainder}: {Result.TotalRemainder:F1}");
        sb.AppendLine($"{_localization.LabelUtilization}: {Result.UtilizationPercent:F1}%");
        sb.AppendLine();

        foreach (var bar in Result.Bars)
        {
            var parts = string.Join(" + ", bar.Parts.Select(p => $"{p.Source.Name} {p.Source.Length:F1}"));
            sb.AppendLine($"{bar.BarLength:F1} ({bar.CutCount} cuts) = {parts} + {bar.Remainder:F1}");
        }

        Clipboard.SetText(sb.ToString());
    }

    private void OpenMaterials()
    {
        var dialog = new Views.MaterialsWindow(LibraryMaterials)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            var updated = dialog.ViewModel.Materials;
            _materialsLibraryService.SaveAll(updated);
            LoadLibrary();

            if (SelectedMaterial != null && !LibraryMaterials.Any(m => m.Id == SelectedMaterial.Id))
                SelectedMaterial = null;
        }
    }

    private void ClearAll()
    {
        Parts.Clear();
        Result = null;
        StatusText = string.Empty;
    }

    private bool CanSaveProject()
    {
        return _projectFileService != null && Parts.Count > 0 && SelectedMaterial != null;
    }

    private void SaveProject()
    {
        // Placeholder: implemented in Phase 5.
    }

    private void OpenProject()
    {
        // Placeholder: implemented in Phase 5.
    }

    private void SetLanguage(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return;

        _localization.Culture = culture;
        OnPropertyChanged(string.Empty);
    }

    private void SetTheme(AppTheme theme)
    {
        ThemeService.Apply(theme);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
