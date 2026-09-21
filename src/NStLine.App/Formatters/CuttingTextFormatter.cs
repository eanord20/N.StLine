using System.Globalization;
using NStLine.Core.Models;

namespace NStLine.App.Formatters;

public static class CuttingTextFormatter
{
    public static string FormatPart(string? name, double length, IFormatProvider? formatProvider = null)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? "Part" : name.Trim();
        return $"{displayName} - {length.ToString("0.##", formatProvider ?? CultureInfo.CurrentCulture)}";
    }

    public static string FormatGroupedParts(
        IEnumerable<PlacedPart> parts,
        IFormatProvider? formatProvider = null)
    {
        var groups = parts
            .Select((part, index) => new { Part = part, Index = index })
            .GroupBy(x => new { x.Part.Source.Name, x.Part.Source.Length })
            .OrderBy(group => group.Min(x => x.Index))
            .Select(group =>
            {
                var text = FormatPart(group.Key.Name, group.Key.Length, formatProvider);
                var count = group.Count();
                return count > 1 ? $"{text} × {count}" : text;
            });

        return string.Join(", ", groups);
    }
}
