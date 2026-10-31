using System.Globalization;

namespace NStLine.App.Services;

public static class PartPasteParser
{
    public static bool TryParse(string[] cells, out string name, out double length, out int quantity)
    {
        name = string.Empty;
        length = 0;
        quantity = 1;
        if (cells.Length == 0)
            return false;
        if (cells.Length == 1)
            return TryParseDouble(cells[0], out length);
        if (cells.Length == 2)
        {
            if (TryParseDouble(cells[0], out length))
                return TryParseQuantity(cells[1], out quantity);
            name = cells[0];
            return TryParseDouble(cells[1], out length);
        }
        name = cells[0];
        var validLength = TryParseDouble(cells[1], out length);
        var validQuantity = TryParseQuantity(cells[2], out quantity);
        return validLength && validQuantity;
    }

    private static bool TryParseDouble(string value, out double parsed)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)
               || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseQuantity(string value, out int quantity)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out quantity)
            || int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
            return quantity > 0;
        if (TryParseDouble(trimmed, out var parsed) && double.IsFinite(parsed) &&
            parsed >= 1 && parsed <= int.MaxValue && Math.Truncate(parsed) == parsed)
        {
            quantity = (int)parsed;
            return true;
        }
        quantity = 0;
        return false;
    }
}
