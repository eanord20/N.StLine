using System.Globalization;
using System.Windows.Data;
using NStLine.App.Formatters;
using NStLine.App.Services;
using NStLine.Core.Models;

namespace NStLine.App.Converters;

public sealed class BarPartsSummaryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not BarCutting bar || bar.Parts.Count == 0)
            return string.Empty;

        return CuttingTextFormatter.FormatGroupedParts(bar.Parts, culture, LocalizationService.Instance.UnitMillimeters);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
