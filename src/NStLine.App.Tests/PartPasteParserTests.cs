using NStLine.App.Services;

namespace NStLine.App.Tests;

public sealed class PartPasteParserTests
{
    [Fact]
    public void ParseSingleColumn_LengthOnly()
    {
        var ok = PartPasteParser.TryParse(["1250"], out var name, out var length, out var quantity);

        Assert.True(ok);
        Assert.Equal(string.Empty, name);
        Assert.Equal(1250, length);
        Assert.Equal(1, quantity);
    }

    [Fact]
    public void ParseTwoColumns_LengthAndQuantity()
    {
        var ok = PartPasteParser.TryParse(["1250", "4"], out var name, out var length, out var quantity);

        Assert.True(ok);
        Assert.Equal(string.Empty, name);
        Assert.Equal(1250, length);
        Assert.Equal(4, quantity);
    }

    [Fact]
    public void ParseTwoColumns_NameAndLength()
    {
        var ok = PartPasteParser.TryParse(["Stand", "1250"], out var name, out var length, out var quantity);

        Assert.True(ok);
        Assert.Equal("Stand", name);
        Assert.Equal(1250, length);
        Assert.Equal(1, quantity);
    }

    [Fact]
    public void ParseThreeColumns_NameLengthQuantity()
    {
        var ok = PartPasteParser.TryParse(["Stand", "1250", "2"], out var name, out var length, out var quantity);

        Assert.True(ok);
        Assert.Equal("Stand", name);
        Assert.Equal(1250, length);
        Assert.Equal(2, quantity);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("2.4")]
    [InlineData("2147483648")]
    [InlineData("Infinity")]
    [InlineData("")]
    public void InvalidQuantity_IsRejectedWithoutReinterpretingColumns(string input)
    {
        Assert.False(PartPasteParser.TryParse(["P", "1000", input], out _, out _, out _));
        Assert.False(PartPasteParser.TryParse(["1000", input], out _, out _, out _));
    }

    [Theory]
    [InlineData("2.0", 2)]
    [InlineData("2e1", 20)]
    public void IntegralQuantity_RemainsSupported(string input, int expected)
    {
        Assert.True(PartPasteParser.TryParse(["P", "1000", input], out _, out _, out var quantity));
        Assert.Equal(expected, quantity);
    }

    [Fact]
    public void ParseInvalidRow_Fails()
    {
        var ok = PartPasteParser.TryParse(["Stand", "abc"], out _, out _, out _);

        Assert.False(ok);
    }
}
