using System.ComponentModel;
using NStLine.App.Services;
using NStLine.Core.Constants;

namespace NStLine.App.ViewModels;

public sealed class CalculationSettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
{
    private string _profileKey;
    private double _timeLimit;
    private int _patternLimit;

    public CalculationSettingsViewModel(string profileKey, double timeLimit, int patternLimit)
    {
        _profileKey = profileKey;
        _timeLimit = timeLimit;
        _patternLimit = patternLimit;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string SelectedExactProfileKey
    {
        get => _profileKey;
        set
        {
            if (value is not ("fast" or "balanced" or "custom"))
                return;
            _profileKey = value;
            var options = value == "fast" ? AppConstants.FastCuttingOptions : AppConstants.DefaultCuttingOptions;
            if (value != "custom")
            {
                _timeLimit = options.TimeLimitSeconds;
                _patternLimit = options.MaxPatternCount;
            }
            Notify();
        }
    }
    public double ExactTimeLimitSeconds { get => _timeLimit; set { _timeLimit = value; Notify(); } }
    public int ExactMaxPatternCount { get => _patternLimit; set { _patternLimit = value; Notify(); } }
    public bool IsCustom => SelectedExactProfileKey == "custom";
    public bool IsValid => double.IsFinite(_timeLimit) && _timeLimit > 0 && _patternLimit > 0;
    public string Error => IsValid ? string.Empty : LocalizationService.Instance.ErrorInvalidInput;
    public string this[string columnName] => columnName switch
    {
        nameof(ExactTimeLimitSeconds) when !double.IsFinite(_timeLimit) || _timeLimit <= 0 => Error,
        nameof(ExactMaxPatternCount) when _patternLimit <= 0 => Error,
        _ => string.Empty
    };

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
