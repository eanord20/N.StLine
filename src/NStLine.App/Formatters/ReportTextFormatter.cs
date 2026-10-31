using System.Globalization;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.Core.Constants;
using NStLine.Core.Models;

namespace NStLine.App.Formatters;

public static class ReportTextFormatter
{
    public static string Title(ReportGenerationOptions options, LocalizationService localization)
    {
        var title = string.IsNullOrWhiteSpace(options.Settings.Title)
            ? $"{AppConstants.ApplicationName} — {localization.ReportTitle}"
            : options.Settings.Title.Trim();
        var stamp = ReportFileNameFormatter.FormatTimestamp(options.GeneratedAt,
            options.Settings.TitleIncludeDate, options.Settings.TitleIncludeTime,
            AppConstants.ReportTitleDateFormat, AppConstants.ReportTitleTimeFormat, " ");
        return stamp.Length == 0 ? title : $"{title} — {stamp}";
    }

    public static string MaterialPrefix(MaterialCuttingResult material, ReportSettings settings,
        CultureInfo culture, LocalizationService localization)
        => settings.IncludeMaterial
            ? $"{material.Material.Name} - {material.Task.BarLength.ToString("0.##", culture)} {localization.UnitMillimeters} "
            : string.Empty;

    public static string PatternDetails(CuttingPattern pattern, ReportSettings settings,
        CultureInfo culture, LocalizationService localization)
    {
        var parts = CuttingTextFormatter.FormatGroupedParts(pattern.Parts, culture,
            localization.UnitMillimeters, settings.SeparatorText, settings.ShowPartNames);
        return $" | {parts} | {localization.ColumnRemainder.ToLower(culture)} {pattern.Remainder.ToString("0.#", culture)}";
    }
}
