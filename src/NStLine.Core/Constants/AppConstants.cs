using NStLine.Core.Models;

namespace NStLine.Core.Constants;

public static class AppConstants
{
    public const string ApplicationName = "N.StLine";
    public const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";
    public const string ProjectFileExtension = ".nstline";
    public const string MaterialLibraryFileName = "materials.json";
    public const string AppSettingsFileName = "settings.json";
    public const string ReportFileNamePrefix = "N.StLine_Report_";
    public const string ReportFileExtension = ".docx";
    public const string ReportTimestampFormat = "yyyy-MM-dd_HHmmss";
    public const string ReportFileDateFormat = "yyyy-MM-dd";
    public const string ReportFileTimeFormat = "HHmmss";
    public const string ReportTitleDateFormat = "dd.MM.yyyy";
    public const string ReportTitleTimeFormat = "HH:mm:ss";
    public const int ReportMaxFileNameLength = 255;
    public const string ReportFontFamily = "Calibri";

    public static CuttingOptions FastCuttingOptions => new()
    {
        TimeLimitSeconds = 4.0,
        MaxPatternCount = 25_000,
        ToleranceMm = DefaultCuttingOptions.ToleranceMm
    };

    public static CuttingOptions DefaultCuttingOptions => new()
    {
        TimeLimitSeconds = 30.0,
        MaxPatternCount = 100_000,
        ToleranceMm = 0.01
    };

}
