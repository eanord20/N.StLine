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

    [Fact]
    public void DefaultCuttingOptions_HasExpectedValues()
    {
        var options = AppConstants.DefaultCuttingOptions;

        Assert.Equal(30.0, options.TimeLimitSeconds);
        Assert.Equal(100_000, options.MaxPatternCount);
        Assert.Equal(0.01, options.ToleranceMm);
    }
}
