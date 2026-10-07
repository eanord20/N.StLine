namespace NStLine.App.Models;

public sealed class AppSettings
{
    public string Culture { get; set; } = "en";
    public string Theme { get; set; } = "Light";
    public string ReportOutputPath { get; set; } = string.Empty;
    public double ExactTimeLimitSeconds { get; set; } = 30.0;
    public int ExactMaxPatternCount { get; set; } = 100_000;
}
