using NStLine.Core.Constants;

namespace NStLine.App.Models;

public sealed class AppSettings
{
    public string Culture { get; set; } = "en";
    public string Theme { get; set; } = "Light";
    public string ReportOutputPath { get; set; } = string.Empty;
    public double ExactTimeLimitSeconds { get; set; } = AppConstants.DefaultCuttingOptions.TimeLimitSeconds;
    public int ExactMaxPatternCount { get; set; } = AppConstants.DefaultCuttingOptions.MaxPatternCount;
    public string ExactProfileKey { get; set; } = "balanced";
}
