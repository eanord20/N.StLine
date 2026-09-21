using System.Globalization;
using System.Windows.Data;

namespace NStLine.App.Converters;

public sealed class ActiveMaterialMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2 && values[0] is string materialId && values[1] is string activeId && materialId == activeId;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
