using System.Globalization;
using System.Text;
using NStLine.App.Services;
using NStLine.Core.Models;

namespace NStLine.App.Formatters;

public static class CuttingTextFormatter
{
    public static string FormatPart(
        string? name,
        double length,
        IFormatProvider? formatProvider = null,
        string? lengthUnit = null)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? "Part" : name.Trim();
        var unitSuffix = string.IsNullOrWhiteSpace(lengthUnit) ? string.Empty : $" {lengthUnit}";
        return $"{displayName} - {length.ToString("0.##", formatProvider ?? CultureInfo.CurrentCulture)}{unitSuffix}";
    }

    public static string FormatGroupedParts(
        IEnumerable<PlacedPart> parts,
        IFormatProvider? formatProvider = null,
        string? lengthUnit = null)
    {
        var groups = parts
            .Select((part, index) => new { Part = part, Index = index })
            .GroupBy(x => new { x.Part.Source.Name, x.Part.Source.Length })
            .OrderBy(group => group.Min(x => x.Index))
            .Select(group =>
            {
                var text = FormatPart(group.Key.Name, group.Key.Length, formatProvider, lengthUnit);
                var count = group.Count();
                return count > 1 ? $"{text} × {count}" : text;
            });

        return string.Join(" + ", groups);
    }

    public static string FormatProjectResult(
        ProjectCuttingResult result,
        LocalizationService localization,
        IFormatProvider? formatProvider = null)
    {
        var provider = formatProvider ?? CultureInfo.CurrentCulture;
        var text = new StringBuilder();
        text.AppendLine(result.Status == CalculationStatus.Optimal
            ? localization.StatusOptimal
            : localization.StatusBestFound);
        AppendMetrics(text, result.TotalBarCount, result.TotalPartLength, result.TotalCutLoss,
            result.TotalRemainder, result.UtilizationPercent, localization, provider);

        foreach (var materialResult in result.Materials)
        {
            text.AppendLine();
            text.AppendLine(materialResult.Material.Name);
            var material = materialResult.Result;
            AppendMetrics(text, material.TotalBarCount, material.TotalPartLength, material.TotalCutLoss,
                material.TotalRemainder, material.UtilizationPercent, localization, provider);
            foreach (var bar in material.Bars)
            {
                var parts = FormatGroupedParts(bar.Parts, provider, localization.UnitMillimeters);
                text.AppendLine($"{bar.Index}. {bar.BarLength.ToString("0.##", provider)} ({bar.CutCount}) = {parts} + {bar.Remainder.ToString("0.##", provider)}");
            }
        }

        return text.ToString();
    }

    private static void AppendMetrics(
        StringBuilder text,
        int totalBarCount,
        double totalPartLength,
        double totalCutLoss,
        double totalRemainder,
        double utilizationPercent,
        LocalizationService localization,
        IFormatProvider formatProvider)
    {
        text.AppendLine($"{localization.LabelTotalBars}: {totalBarCount}");
        text.AppendLine($"{localization.LabelTotalPartLength}: {totalPartLength.ToString("0.##", formatProvider)}");
        text.AppendLine($"{localization.LabelTotalCutLoss}: {totalCutLoss.ToString("0.##", formatProvider)}");
        text.AppendLine($"{localization.LabelTotalRemainder}: {totalRemainder.ToString("0.##", formatProvider)}");
        text.AppendLine($"{localization.LabelUtilization}: {utilizationPercent.ToString("0.##", formatProvider)}%");
    }
}
