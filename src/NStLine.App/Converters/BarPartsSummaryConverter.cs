using System.Globalization;
using System.Windows.Data;
using NStLine.Core.Models;

namespace NStLine.App.Converters;

public sealed class BarPartsSummaryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not BarCutting bar || bar.Parts.Count == 0)
            return string.Empty;

        var parts = bar.Parts
            .GroupBy(p => p.Source.Name)
            .Select(g => $"{g.Key} {g.First().Source.Length:F0} × {g.Count()}");

        return string.Join(", ", parts);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
