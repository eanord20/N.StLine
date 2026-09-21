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
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace NStLine.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const int MaxUndoDepth = 50;

    private readonly LocalizationService _localization = LocalizationService.Instance;
    private readonly IMaterialsLibraryService _materialsLibraryService;
    private readonly IProjectFileService? _projectFileService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IProjectCuttingEngine _cuttingEngine;
    private readonly IResultWindowService? _resultWindowService;
    private readonly PartRowsUndoHistory _undoHistory = new(MaxUndoDepth);

    private string? _activeMaterialId;
    private string _statusText = string.Empty;
    private double _progressValue;
    private bool _isBusy;
    private PartRow? _selectedPart;
    private AppTheme _currentTheme = AppTheme.Light;
    private PartRowsSnapshot? _pendingEditSnapshot;

    public MainViewModel(
        IProjectCuttingEngine cuttingEngine,
        IMaterialsLibraryService? materialsLibraryService = null,
        IProjectFileService? projectFileService = null,
        IAppSettingsService? appSettingsService = null,
        IResultWindowService? resultWindowService = null)
    {
        _cuttingEngine = cuttingEngine;
        _materialsLibraryService = materialsLibraryService ?? new MaterialsLibraryService();
        _projectFileService = projectFileService;
        _appSettingsService = appSettingsService ?? new AppSettingsService();
        _resultWindowService = resultWindowService;

        AddRowCommand = new RelayCommand(_ => AddRow(), _ => ActiveMaterial != null);
        DeleteRowCommand = new RelayCommand(
            row => DeleteRow(row as PartRow),
            row => row is PartRow || SelectedPart != null);
        UndoCommand = new RelayCommand(_ => Undo(), _ => _undoHistory.CanUndo);
        PasteFromClipboardCommand = new RelayCommand(_ => PasteFromClipboard(), _ => ActiveMaterial != null);
        CalculateCommand = new RelayCommand(async _ => await CalculateAsync(), _ => CanCalculate());
        OpenMaterialsCommand = new RelayCommand(_ => OpenMaterials());
        AddMaterialCommand = new RelayCommand(AddMaterial, material => material is LinearMaterial);
        RemoveMaterialCommand = new RelayCommand(RemoveMaterial, id => id is string);
        SetActiveMaterialCommand = new RelayCommand(SetActiveMaterial, id => id is string);
        ClearAllCommand = new RelayCommand(_ => ClearAll(), _ => Parts.Count > 0 || ProjectMaterials.Count > 0);
        SaveProjectCommand = new RelayCommand(_ => SaveProject(), _ => CanSaveProject());
        OpenProjectCommand = new RelayCommand(_ => OpenProject());
        SetLanguageCommand = new RelayCommand(lang => SetLanguage(lang?.ToString() ?? "en"));
        SetThemeCommand = new RelayCommand(theme =>
        {
            if (Enum.TryParse<AppTheme>(theme?.ToString() ?? "Light", true, out var t))
                SetTheme(t);
        });

        LoadLibrary();
        LoadSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PartRow> Parts { get; } = new();
    public ObservableCollection<LinearMaterial> LibraryMaterials { get; } = new();
    public ObservableCollection<LinearMaterial> ProjectMaterials { get; } = new();

    public string? ActiveMaterialId
    {
        get => _activeMaterialId;
        set
        {
            if (_activeMaterialId == value)
                return;

            _activeMaterialId = value;
            SelectedPart = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveMaterial));
            OnPropertyChanged(nameof(ActiveParts));
            OnPropertyChanged(nameof(HasActiveParts));
            ValidateParts();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public LinearMaterial? ActiveMaterial => ProjectMaterials.FirstOrDefault(x => x.Id == ActiveMaterialId);
    public IEnumerable<PartRow> ActiveParts => Parts.Where(x => x.MaterialId == ActiveMaterialId);
    public bool HasProjectMaterials => ProjectMaterials.Count > 0;
    public bool HasActiveParts => ActiveParts.Any();
    public IEnumerable<LinearMaterial> AvailableLibraryMaterials
        => LibraryMaterials.Where(x => ProjectMaterials.All(projectMaterial => projectMaterial.Id != x.Id));

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
    public ICommand UndoCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand CalculateCommand { get; }
    public ICommand OpenMaterialsCommand { get; }
    public ICommand AddMaterialCommand { get; }
    public ICommand RemoveMaterialCommand { get; }
    public ICommand SetActiveMaterialCommand { get; }
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
    public string ButtonExportDocx => _localization.ButtonExportDocx;
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

    private void AddMaterial(object? parameter)
    {
        if (parameter is not LinearMaterial material || ProjectMaterials.Any(x => x.Id == material.Id))
            return;

        ProjectMaterials.Add(CloneMaterial(material));
        ActiveMaterialId = material.Id;
        OnPropertyChanged(nameof(HasProjectMaterials));
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
    }

    private void RemoveMaterial(object? parameter)
    {
        if (parameter is not string materialId)
            return;

        var material = ProjectMaterials.FirstOrDefault(x => x.Id == materialId);
        if (material == null)
            return;

        var rows = Parts.Where(x => x.MaterialId == materialId).ToList();
        if (rows.Count > 0 && MessageBox.Show(
                string.Format(_localization.ConfirmRemoveMaterial, material.Name),
                _localization.WindowTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        foreach (var row in rows)
            Parts.Remove(row);
        ProjectMaterials.Remove(material);
        ActiveMaterialId = ProjectMaterials.FirstOrDefault()?.Id;
        RenumberRows();
        OnPropertyChanged(nameof(HasProjectMaterials));
        OnPropertyChanged(nameof(HasActiveParts));
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
        OnPropertyChanged(nameof(ActiveParts));
    }

    private void SetActiveMaterial(object? parameter)
    {
        if (parameter is string materialId && ProjectMaterials.Any(x => x.Id == materialId))
            ActiveMaterialId = materialId;
    }

    private static LinearMaterial CloneMaterial(LinearMaterial material)
        => new()
        {
            Id = material.Id,
            Name = material.Name,
            StandardLength = material.StandardLength,
            CutWidth = material.CutWidth
        };

    private void LoadSettings()
    {
        var settings = _appSettingsService.Load();
        _localization.Culture = settings.Culture;
        if (Enum.TryParse<AppTheme>(settings.Theme, true, out var theme))
        {
            _currentTheme = theme;
            ThemeService.Apply(theme);
        }
        OnPropertyChanged(string.Empty);
    }

    private void SaveSettings()
    {
        _appSettingsService.Save(new AppSettings
        {
            Culture = _localization.Culture,
            Theme = _currentTheme.ToString()
        });
    }

    private void AddRow()
    {
        if (ActiveMaterialId == null)
            return;

        PushUndoSnapshot();
        var number = Parts.Count(x => x.MaterialId == ActiveMaterialId) + 1;
        var row = new PartRow
        {
            MaterialId = ActiveMaterialId,
            Name = $"Part {number}",
            RowNumber = number,
            Quantity = 1
        };
        ValidateRow(row);
        Parts.Add(row);
        RenumberRows();
        OnPropertyChanged(nameof(ActiveParts));
        OnPropertyChanged(nameof(HasActiveParts));
    }

    private void DeleteRow(PartRow? row)
    {
        var target = row ?? SelectedPart;
        if (target == null)
            return;

        PushUndoSnapshot();
        Parts.Remove(target);
        if (ReferenceEquals(SelectedPart, target))
            SelectedPart = null;
        RenumberRows();
        OnPropertyChanged(nameof(ActiveParts));
        OnPropertyChanged(nameof(HasActiveParts));
    }

    private void RenumberRows()
    {
        foreach (var materialGroup in Parts.GroupBy(x => x.MaterialId))
        {
            var number = 1;
            foreach (var row in materialGroup)
                row.RowNumber = number++;
        }
    }

    private void PasteFromClipboard()
    {
        try
        {
            if (ActiveMaterialId == null || !Clipboard.ContainsText())
                return;

            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var snapshot = CaptureSnapshot();
            var originalCount = Parts.Count;
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

                var rowNumber = Parts.Count(x => x.MaterialId == ActiveMaterialId) + 1;
                var row = new PartRow
                {
                    MaterialId = ActiveMaterialId,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Part {rowNumber}" : name,
                    Length = length,
                    Quantity = Math.Max(1, quantity),
                    RowNumber = rowNumber
                };

                ValidateRow(row);
                Parts.Add(row);
            }

            if (Parts.Count > originalCount)
            {
                _undoHistory.Push(snapshot.Rows, snapshot.ActiveMaterialId);
                RaiseUndoCanExecuteChanged();
            }
            RenumberRows();
            OnPropertyChanged(nameof(ActiveParts));
            OnPropertyChanged(nameof(HasActiveParts));
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
        var material = ProjectMaterials.FirstOrDefault(x => x.Id == row.MaterialId);
        if (material == null)
            row.SetError(_localization.ErrorMaterialNotAssigned);
        else if (row.Length <= 0)
            row.SetError(_localization.ErrorLengthPositive);
        else if (row.Quantity <= 0)
            row.SetError(_localization.ErrorQuantityPositive);
        else if (row.Length + material.CutWidth > material.StandardLength)
            row.SetError(_localization.ErrorPartDoesNotFit);
        else
            row.ClearError();
    }

    private bool CanCalculate()
    {
        if (IsBusy || ProjectMaterials.Count == 0 || Parts.Count == 0)
            return false;
        return Parts.All(p => !p.HasError);
    }

    public async Task CalculateAsync()
    {
        IsBusy = true;
        ProgressValue = 0;
        StatusText = _localization.StatusCalculating;

        try
        {
            var task = CreateProjectTaskSnapshot();
            var options = AppConstants.DefaultCuttingOptions;
            var progress = new Progress<double>(v => ProgressValue = v * 100);
            var result = await Task.Run(() => _cuttingEngine.Solve(task, options, progress));
            StatusText = result.Status == CalculationStatus.Optimal
                ? StatusOptimal
                : StatusBestFound;
            _resultWindowService?.Show(task, result);
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

    private ProjectCuttingTask CreateProjectTaskSnapshot()
        => new()
        {
            Materials = ProjectMaterials.Select(CloneMaterial).ToList(),
            Parts = Parts.Select(row => row.ToPartRequirement()).ToList()
        };

    private void OpenMaterials()
    {
        var dialog = new Views.MaterialsWindow(LibraryMaterials)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            _materialsLibraryService.SaveAll(dialog.ViewModel.Materials);
            LoadLibrary();
            OnPropertyChanged(nameof(AvailableLibraryMaterials));
        }
    }

    private void ClearAll()
    {
        Parts.Clear();
        ProjectMaterials.Clear();
        ActiveMaterialId = null;
        StatusText = string.Empty;
        ClearUndoHistory();
        OnPropertyChanged(nameof(HasProjectMaterials));
        OnPropertyChanged(nameof(HasActiveParts));
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
        OnPropertyChanged(nameof(ActiveParts));
    }

    private bool CanSaveProject()
    {
        return _projectFileService != null && Parts.Count > 0 && ProjectMaterials.Count > 0;
    }

    private void SaveProject()
    {
        if (_projectFileService == null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = $"N.StLine project (*{AppConstants.ProjectFileExtension})|*{AppConstants.ProjectFileExtension}",
            DefaultExt = AppConstants.ProjectFileExtension
        };

        if (dialog.ShowDialog() != true)
            return;

        var data = new ProjectData
        {
            Materials = ProjectMaterials.Select(CloneMaterial).ToList(),
            Parts = Parts.Select(p => p.ToPartRequirement()).ToList()
        };

        _projectFileService.Save(dialog.FileName, data);
        StatusText = string.Format(_localization.StatusProjectSaved, dialog.FileName);
    }

    private void OpenProject()
    {
        if (_projectFileService == null)
            return;

        var dialog = new OpenFileDialog
        {
            Filter = $"N.StLine project (*{AppConstants.ProjectFileExtension})|*{AppConstants.ProjectFileExtension}",
            DefaultExt = AppConstants.ProjectFileExtension
        };

        if (dialog.ShowDialog() != true)
            return;

        var data = _projectFileService.Load(dialog.FileName);
        LoadProject(data);
        StatusText = string.Format(_localization.StatusProjectLoaded, dialog.FileName);
    }

    private void LoadProject(ProjectData data)
    {
        ProjectMaterials.Clear();
        foreach (var material in data.Materials)
            ProjectMaterials.Add(CloneMaterial(material));

        Parts.Clear();
        for (var index = 0; index < data.Parts.Count; index++)
            Parts.Add(PartRow.FromPartRequirement(data.Parts[index], index + 1));

        ActiveMaterialId = ProjectMaterials.FirstOrDefault()?.Id;
        RenumberRows();
        ValidateParts();
        ClearUndoHistory();
        OnPropertyChanged(nameof(HasProjectMaterials));
        OnPropertyChanged(nameof(HasActiveParts));
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
        OnPropertyChanged(nameof(ActiveParts));
    }

    public void BeginPartEdit()
    {
        _pendingEditSnapshot ??= CaptureSnapshot();
    }

    public void CancelPartEdit()
    {
        _pendingEditSnapshot = null;
    }

    public void CommitPartEdit()
    {
        if (_pendingEditSnapshot == null)
            return;

        if (!RowsEqual(_pendingEditSnapshot.Rows, Parts))
        {
            _undoHistory.Push(_pendingEditSnapshot.Rows, _pendingEditSnapshot.ActiveMaterialId);
            RaiseUndoCanExecuteChanged();
        }

        _pendingEditSnapshot = null;
        ValidateParts();
    }

    public void ClearUndoHistory()
    {
        _undoHistory.Clear();
        _pendingEditSnapshot = null;
        RaiseUndoCanExecuteChanged();
    }

    private void PushUndoSnapshot()
    {
        var snapshot = CaptureSnapshot();
        _undoHistory.Push(snapshot.Rows, snapshot.ActiveMaterialId);
        RaiseUndoCanExecuteChanged();
    }

    private PartRowsSnapshot CaptureSnapshot()
        => new(Parts.Select(CloneRow).ToList(), ActiveMaterialId);

    private void Undo()
    {
        if (!_undoHistory.TryPop(out var snapshot) || snapshot == null)
            return;

        Parts.Clear();
        foreach (var row in snapshot.Rows)
            Parts.Add(CloneRow(row));

        ActiveMaterialId = snapshot.ActiveMaterialId;
        SelectedPart = null;
        RenumberRows();
        ValidateParts();
        OnPropertyChanged(nameof(ActiveParts));
        RaiseUndoCanExecuteChanged();
    }

    private static bool RowsEqual(IReadOnlyList<PartRow> left, IReadOnlyList<PartRow> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            var first = left[index];
            var second = right[index];
            if (first.MaterialId != second.MaterialId ||
                first.Name != second.Name ||
                first.Length != second.Length ||
                first.Quantity != second.Quantity)
                return false;
        }

        return true;
    }

    private static PartRow CloneRow(PartRow row)
        => new()
        {
            RowNumber = row.RowNumber,
            MaterialId = row.MaterialId,
            Name = row.Name,
            Length = row.Length,
            Quantity = row.Quantity
        };

    private void RaiseUndoCanExecuteChanged()
        => ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();

    private void SetLanguage(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return;

        _localization.Culture = culture;
        ValidateParts();
        OnPropertyChanged(string.Empty);
        SaveSettings();
    }

    private void SetTheme(AppTheme theme)
    {
        _currentTheme = theme;
        ThemeService.Apply(theme);
        SaveSettings();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
