using System.ComponentModel;
using System.Globalization;
using NStLine.App.Formatters;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.Core.Models;

namespace NStLine.App.ViewModels;

public sealed class ReportSettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
{
    private readonly ReportSettings _settings;
    private readonly MaterialCuttingResult? _previewMaterial;
    private readonly DateTime _previewTime = DateTime.Now;
    private readonly LocalizationService _localization = LocalizationService.ForCulture(LocalizationService.Instance.Culture);
    private CultureInfo Culture => CultureInfo.GetCultureInfo(_localization.Culture == "ua" ? "uk-UA" : "en-US");

    public ReportSettingsViewModel(ReportSettings settings, ProjectCuttingResult? result)
    {
        _settings = settings with { };
        _previewMaterial = result?.Materials.FirstOrDefault(material => material.Result.Patterns.Count > 0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string FileName { get => _settings.FileName; set => Update(() => _settings.FileName = value); }
    public ReportTimestampPosition TimestampPosition { get => _settings.TimestampPosition; set => Update(() => _settings.TimestampPosition = value); }
    public bool FileNameIncludeDate { get => _settings.FileNameIncludeDate; set => Update(() => _settings.FileNameIncludeDate = value); }
    public bool FileNameIncludeTime { get => _settings.FileNameIncludeTime; set => Update(() => _settings.FileNameIncludeTime = value); }
    public string Title { get => _settings.Title; set => Update(() => _settings.Title = value); }
    public bool TitleIncludeDate { get => _settings.TitleIncludeDate; set => Update(() => _settings.TitleIncludeDate = value); }
    public bool TitleIncludeTime { get => _settings.TitleIncludeTime; set => Update(() => _settings.TitleIncludeTime = value); }
    public bool ShowSummary { get => _settings.ShowSummary; set => Update(() => _settings.ShowSummary = value); }
    public ReportPartSeparator PartSeparator { get => _settings.PartSeparator; set => Update(() => _settings.PartSeparator = value); }
    public bool IncludeMaterial { get => _settings.IncludeMaterial; set => Update(() => _settings.IncludeMaterial = value); }
    public bool ShowPartNames { get => _settings.ShowPartNames; set => Update(() => _settings.ShowPartNames = value); }
    public bool IsValid => string.IsNullOrEmpty(Error);
    public string Error
    {
        get
        {
            try { ReportFileNameFormatter.Format(_settings, _previewTime); return string.Empty; }
            catch (ArgumentException ex) { return ex.Message; }
        }
    }
    public string this[string columnName] => columnName == nameof(FileName) ? Error : string.Empty;
    public string PreviewFileName => IsValid ? ReportFileNameFormatter.Format(_settings, _previewTime) : Error;
    public string PreviewTitle => ReportTextFormatter.Title(new(_settings, _previewTime, _localization.Culture), _localization);
    public string PreviewMaterial => _previewMaterial == null ? string.Empty : ReportTextFormatter.MaterialPrefix(_previewMaterial, _settings, Culture, _localization);
    public string PreviewQuantity => _previewMaterial == null ? string.Empty : $"×{_previewMaterial.Result.Patterns[0].Multiplicity}";
    public string PreviewDetails => _previewMaterial == null ? _localization.EmptyNoResult :
        ReportTextFormatter.PatternDetails(_previewMaterial.Result.Patterns[0], _settings, Culture, _localization);

    public ReportSettings ToSettings()
    {
        ReportFileNameFormatter.Format(_settings, _previewTime);
        return _settings with { };
    }

    private void Update(Action change)
    {
        change();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
