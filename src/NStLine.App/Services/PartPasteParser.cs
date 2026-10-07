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
        {
            if (!TryParseDouble(cells[0], out length))
                return false;
            return true;
        }

        if (cells.Length == 2)
        {
            var firstLooksLikeLength = TryParseDouble(cells[0], out var parsedLength);
            var secondLooksLikeQuantity = TryParseQuantity(cells[1], out var parsedQuantity);
            if (firstLooksLikeLength && secondLooksLikeQuantity)
            {
                length = parsedLength;
                quantity = parsedQuantity;
                return true;
            }

            name = cells[0];
            if (!TryParseDouble(cells[1], out length))
                return false;
            return true;
        }

        name = cells[0];
        if (!TryParseDouble(cells[1], out length))
            return false;
        if (!TryParseQuantity(cells[2], out quantity))
            return false;
        return true;
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
        {
            quantity = Math.Max(1, quantity);
            return true;
        }

        if ((double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var asDouble)
             || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out asDouble))
            && asDouble > 0)
        {
            quantity = Math.Max(1, (int)Math.Round(asDouble, MidpointRounding.AwayFromZero));
            return true;
        }

        quantity = 1;
        return false;
    }
}
