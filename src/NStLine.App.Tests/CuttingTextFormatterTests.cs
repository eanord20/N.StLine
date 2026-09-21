using System.Globalization;
using NStLine.App.Formatters;
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
}
