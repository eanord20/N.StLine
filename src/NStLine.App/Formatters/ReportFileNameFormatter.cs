using System.Globalization;
using System.IO;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.Core.Constants;

namespace NStLine.App.Formatters;

public static class ReportFileNameFormatter
{
    public static string Format(ReportSettings settings, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(settings.FileName))
            return $"{AppConstants.ReportFileNamePrefix}{timestamp.ToString(AppConstants.ReportTimestampFormat, CultureInfo.InvariantCulture)}{AppConstants.ReportFileExtension}";

        var name = settings.FileName.Trim();
        Validate(name);
        if (name.EndsWith(AppConstants.ReportFileExtension, StringComparison.OrdinalIgnoreCase))
            name = name[..^AppConstants.ReportFileExtension.Length];
        Validate(name);
        var stamp = FormatTimestamp(timestamp, settings.FileNameIncludeDate, settings.FileNameIncludeTime,
            AppConstants.ReportFileDateFormat, AppConstants.ReportFileTimeFormat, "_");
        if (stamp.Length > 0)
            name = settings.TimestampPosition switch
            {
                ReportTimestampPosition.Prefix => $"{stamp}_{name}",
                ReportTimestampPosition.Suffix => $"{name}_{stamp}",
                _ => name
            };
        name += AppConstants.ReportFileExtension;
        Validate(name);
        return name;
    }

    public static string FormatTimestamp(DateTime timestamp, bool date, bool time,
        string dateFormat, string timeFormat, string separator)
        => string.Join(separator, new[]
        {
            date ? timestamp.ToString(dateFormat, CultureInfo.InvariantCulture) : null,
            time ? timestamp.ToString(timeFormat, CultureInfo.InvariantCulture) : null
        }.Where(value => value != null));

    private static void Validate(string name)
    {
        var stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
        var reserved = stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
             (stem[3] is >= '1' and <= '9' or '¹' or '²' or '³'));
        if (string.IsNullOrWhiteSpace(name) || name.Length > AppConstants.ReportMaxFileNameLength ||
            name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || reserved)
            throw new ArgumentException(LocalizationService.Instance.ErrorInvalidReportFileName);
    }
}
