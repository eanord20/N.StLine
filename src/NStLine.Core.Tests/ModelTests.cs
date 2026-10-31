using NStLine.Core.Constants;
using NStLine.Core.Models;

namespace NStLine.Core.Tests;

public class ModelTests
{
    [Fact]
    public void CuttingResult_Totals_AreComputedFromBars()
    {
        var part = new PartRequirement { Name = "P", Length = 1000 };
        var bars = new List<BarCutting>
        {
            new()
            {
                Index = 1,
                BarLength = 6000,
                Parts = new[] { new PlacedPart { Source = part, CopyIndex = 1 } },
                CutLoss = 3,
                Remainder = 4997
            },
            new()
            {
                Index = 2,
                BarLength = 6000,
                Parts = new[] { new PlacedPart { Source = part, CopyIndex = 2 } },
                CutLoss = 3,
                Remainder = 4997
            }
        };

        var result = new CuttingResult { Bars = bars };

        Assert.Equal(2, result.TotalBarCount);
        Assert.Equal(12000, result.TotalBarLength);
        Assert.Equal(2000, result.TotalPartLength);
        Assert.Equal(6, result.TotalCutLoss);
        Assert.Equal(9994, result.TotalRemainder);
    }

    [Theory]
    [InlineData(1.3, 0.1, 1.4, true)]
    [InlineData(1.3000000000000003, 0.1, 1.4, false)]
    [InlineData(1e-300, 1e-300, 2e-300, true)]
    [InlineData(1e308, 1e308, 1e308, false)]
    public void LengthScale_FitsPreservesRoundTripPrecision(double length, double kerf, double stock, bool expected)
    {
        Assert.Equal(expected, LengthScale.Fits(length, kerf, stock));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1e-300)]
    [InlineData(1.3000000000000003)]
    [InlineData(1e308)]
    public void LengthScale_RoundTripsAcrossFiniteDimensionRange(double length)
    {
        var scale = new LengthScale(0, length, 3);

        Assert.Equal(length, scale.ToMillimeters(scale[length]));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("uk-UA")]
    public void LengthScale_DoesNotDependOnProcessCulture(string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            var scale = new LengthScale(1.3, 0.1, 1.4);

            Assert.Equal(scale[1.4], scale[1.3] + scale[0.1]);
            Assert.Equal(1.4, scale.ToMillimeters(scale[1.3] + scale[0.1]));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DefaultCuttingOptions_HasExpectedValues()
    {
        var options = AppConstants.DefaultCuttingOptions;

        Assert.Equal(30.0, options.TimeLimitSeconds);
        Assert.Equal(100_000, options.MaxPatternCount);
        Assert.Equal(0.01, options.ToleranceMm);
    }
}
