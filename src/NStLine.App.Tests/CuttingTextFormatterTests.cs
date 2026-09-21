using System.Globalization;
using NStLine.App.Formatters;
using NStLine.App.Services;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class CuttingTextFormatterTests
{
    [Fact]
    public void FormatPart_UsesNameDashLength()
    {
        var text = CuttingTextFormatter.FormatPart("Stand", 1250.5, CultureInfo.InvariantCulture);

        Assert.Equal("Stand - 1250.5", text);
    }

    [Fact]
    public void FormatGroupedParts_GroupsEqualPartsInOriginalOrder()
    {
        var first = new PartRequirement { Name = "Stand", Length = 1250 };
        var second = new PartRequirement { Name = "Brace", Length = 850 };
        var parts = new[]
        {
            new PlacedPart { Source = first },
            new PlacedPart { Source = first },
            new PlacedPart { Source = second }
        };

        var text = CuttingTextFormatter.FormatGroupedParts(parts, CultureInfo.InvariantCulture);

        Assert.Equal("Stand - 1250 × 2, Brace - 850", text);
    }

    [Fact]
    public void FormatProjectResultWritesMetricsBeforeEachMaterialsBars()
    {
        var previousCulture = LocalizationService.Instance.Culture;
        try
        {
            LocalizationService.Instance.Culture = "en";
            var first = MaterialResult("Tube", "Stand", 1250, 4747);
            var second = MaterialResult("Angle", "Brace", 850, 5147);
            var result = new ProjectCuttingResult { Materials = [first, second] };

            var text = CuttingTextFormatter.FormatProjectResult(result, LocalizationService.Instance, CultureInfo.InvariantCulture);

            Assert.Contains("Tube", text);
            Assert.Contains("Bars: 1", text);
            Assert.Contains("Total remainder: 4747", text);
            Assert.Contains("Stand - 1250", text);
            Assert.Contains("Angle", text);
            Assert.Contains("Total remainder: 5147", text);
            Assert.Contains("Brace - 850", text);
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
        }
    }

    private static MaterialCuttingResult MaterialResult(string materialName, string partName, double length, double remainder)
    {
        var material = new LinearMaterial { Name = materialName, StandardLength = 6000, CutWidth = 3 };
        var part = new PartRequirement { Name = partName, Length = length, Quantity = 1 };
        var bar = new BarCutting
        {
            Index = 1,
            BarLength = 6000,
            Parts = [new PlacedPart { Source = part }],
            CutLoss = 3,
            Remainder = remainder
        };
        return new MaterialCuttingResult
        {
            Material = material,
            Task = new CuttingTask { Material = material, BarLength = 6000, CutWidth = 3, Parts = [part] },
            Result = new CuttingResult { Status = CalculationStatus.Optimal, Bars = [bar], UtilizationPercent = 20 }
        };
    }
}
