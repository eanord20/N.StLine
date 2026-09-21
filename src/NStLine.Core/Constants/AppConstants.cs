using NStLine.Core.Models;

namespace NStLine.Core.Constants;

public static class AppConstants
{
    public const string ApplicationName = "N.StLine";
    public const string ProjectFileExtension = ".nstline";
    public const string MaterialLibraryFileName = "materials.json";
    public const string AppSettingsFileName = "settings.json";
    public const string ReportFileNamePrefix = "N.StLine_Report_";
    public const string ReportFileExtension = ".docx";
    public const string ReportTimestampFormat = "yyyy-MM-dd_HHmmss";
    public const string ReportFontFamily = "Calibri";

    public static CuttingOptions DefaultCuttingOptions => new()
    {
        TimeLimitSeconds = 30.0,
        MaxPatternCount = 100_000,
        ToleranceMm = 0.01
    };
}
