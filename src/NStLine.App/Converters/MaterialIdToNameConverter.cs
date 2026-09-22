using System.Globalization;
using System.Windows.Data;
using NStLine.Core.Models;

namespace NStLine.App.Converters;

public sealed class MaterialIdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var materialId = values.ElementAtOrDefault(0) as string;
        var materials = values.ElementAtOrDefault(1) as IEnumerable<LinearMaterial>;
        return materials?.FirstOrDefault(material => material.Id == materialId)?.Name ?? "—";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
