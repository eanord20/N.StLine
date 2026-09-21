namespace NStLine.App.Models;

public sealed class AppSettings
{
    public string Culture { get; set; } = "en";
    public string Theme { get; set; } = "Light";
    public string ReportOutputPath { get; set; } = string.Empty;
}
