using NStLine.Core.Models;

namespace NStLine.Core.Constants;

public static class AppConstants
{
    public const string ApplicationName = "N.StLine";
    public const string ProjectFileExtension = ".nstline";
    public const string MaterialLibraryFileName = "materials.json";
    public const string AppSettingsFileName = "settings.json";

    public static CuttingOptions DefaultCuttingOptions => new()
    {
        TimeLimitSeconds = 30.0,
        MaxPatternCount = 100_000,
        ToleranceMm = 0.01
    };
}
