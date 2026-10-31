using System.Globalization;
using System.IO;
using System.Text.Json;
using NStLine.App.Formatters;
using NStLine.App.Models;
using NStLine.App.Services;

namespace NStLine.App.Tests;

public sealed class ReportSettingsTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 30, 14, 30, 25);

    [Fact]
    public void EmptyFileName_PreservesAutomaticNaming()
    {
        var settings = new ReportSettings { FileName = " ", TimestampPosition = ReportTimestampPosition.None };
        Assert.Equal("N.StLine_Report_2026-10-30_143025.docx", ReportFileNameFormatter.Format(settings, Timestamp));
    }

    [Theory]
    [InlineData(ReportTimestampPosition.None, true, true, "Order.docx")]
    [InlineData(ReportTimestampPosition.Prefix, true, true, "2026-10-30_143025_Order.docx")]
    [InlineData(ReportTimestampPosition.Suffix, true, true, "Order_2026-10-30_143025.docx")]
    [InlineData(ReportTimestampPosition.Suffix, true, false, "Order_2026-10-30.docx")]
    [InlineData(ReportTimestampPosition.Prefix, false, true, "143025_Order.docx")]
    [InlineData(ReportTimestampPosition.Suffix, false, false, "Order.docx")]
    public void CustomFileName_UsesSelectedTimestamp(ReportTimestampPosition position, bool date, bool time, string expected)
    {
        var settings = new ReportSettings
        {
            FileName = "Order.docx", TimestampPosition = position, FileNameIncludeDate = date, FileNameIncludeTime = time
        };
        Assert.Equal(expected, ReportFileNameFormatter.Format(settings, Timestamp));
    }

    [Theory]
    [InlineData("../Order")]
    [InlineData("C:\\Order")]
    [InlineData("CON")]
    [InlineData("con.docx")]
    [InlineData("NUL.any")]
    [InlineData("LPT1")]
    [InlineData("bad.")]
    [InlineData("a|b")]
    public void InvalidFileName_IsRejected(string name)
        => Assert.Throws<ArgumentException>(() => ReportFileNameFormatter.Format(new ReportSettings { FileName = name }, Timestamp));

    [Theory]
    [InlineData(3, " * ")]
    [InlineData(4, " ")]
    [InlineData(5, ", ")]
    [InlineData(6, ". ")]
    public void AdditionalSeparators_AreDefinedAndPreserved(int value, string expected)
    {
        var separator = (ReportPartSeparator)value;
        Assert.True(Enum.IsDefined(separator));
        var settings = new ReportSettings { PartSeparator = separator };
        Assert.Equal(expected, settings.SeparatorText);
        Assert.Equal(settings, JsonSerializer.Deserialize<ReportSettings>(JsonSerializer.Serialize(settings)));
    }

    [Fact]
    public void OldSettings_LoadCompatibleDefaults()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"ReportOutputPath\":\"reports\"}")!;
        Assert.True(settings.Report.ShowSummary);
        Assert.True(settings.Report.ShowPartNames);
        Assert.False(settings.Report.IncludeMaterial);
        Assert.Equal(ReportPartSeparator.Plus, settings.Report.PartSeparator);
        Assert.Equal("", settings.Report.FileName);
    }

    [Fact]
    public void Settings_RoundTripAllReportOptions()
    {
        var path = Path.GetTempFileName();
        try
        {
            var expected = new ReportSettings
            {
                FileName = "Order", TimestampPosition = ReportTimestampPosition.Prefix,
                FileNameIncludeDate = false, FileNameIncludeTime = true, Title = "Custom title",
                TitleIncludeDate = true, TitleIncludeTime = true, ShowSummary = false,
                IncludeMaterial = true, ShowPartNames = false, PartSeparator = ReportPartSeparator.Pipe
            };
            var service = new AppSettingsService(path);
            service.Save(new AppSettings { Report = expected });
            Assert.Equal(expected, service.Load().Report);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FormattingWithoutNames_DoesNotMergeDifferentNamedPartsOrChangeDefaultFormatter()
    {
        var parts = new[]
        {
            new NStLine.Core.Models.PlacedPart { Source = new() { Name = "A", Length = 600 } },
            new NStLine.Core.Models.PlacedPart { Source = new() { Name = "B", Length = 600 } }
        };
        Assert.Equal("600 mm | 600 mm", CuttingTextFormatter.FormatGroupedParts(parts, CultureInfo.InvariantCulture, "mm", " | ", false));
        Assert.Equal("A - 600 mm + B - 600 mm", CuttingTextFormatter.FormatGroupedParts(parts, CultureInfo.InvariantCulture, "mm"));
    }
}
