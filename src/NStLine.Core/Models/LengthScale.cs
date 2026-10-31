using System.Globalization;
using System.Numerics;

namespace NStLine.Core.Models;

public sealed class LengthScale
{
    private readonly int _scale;
    private readonly Dictionary<double, BigInteger> _units;

    public LengthScale(params double[] lengths)
    {
        var values = lengths.Distinct().Select(value =>
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(lengths));
            var text = value.ToString("R", CultureInfo.InvariantCulture);
            var exponentIndex = text.IndexOf('E');
            var exponent = exponentIndex < 0 ? 0 : int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
            var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
            var point = mantissa.IndexOf('.');
            var scale = (point < 0 ? 0 : mantissa.Length - point - 1) - exponent;
            var coefficient = BigInteger.Parse(mantissa.Replace(".", string.Empty), CultureInfo.InvariantCulture);
            return (Value: value, Coefficient: coefficient, Scale: scale);
        }).ToArray();
        _scale = values.Max(value => value.Scale);
        _units = values.ToDictionary(value => value.Value,
            value => value.Coefficient * BigInteger.Pow(10, _scale - value.Scale));
    }

    public BigInteger this[double length] => _units[length];

    public double ToMillimeters(BigInteger units)
        => double.Parse($"{units.ToString(CultureInfo.InvariantCulture)}E{(-_scale).ToString(CultureInfo.InvariantCulture)}",
            CultureInfo.InvariantCulture);

    public static bool Fits(double length, double cutWidth, double stock)
    {
        var scale = new LengthScale(length, cutWidth, stock);
        return scale[length] + scale[cutWidth] <= scale[stock];
    }
}
