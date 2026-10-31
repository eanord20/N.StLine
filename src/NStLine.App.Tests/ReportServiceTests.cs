using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using NStLine.App.Services;
using NStLine.Core.Models;
using Xceed.Words.NET;

namespace NStLine.App.Tests;

[CollectionDefinition("ReportLocalization", DisableParallelization = true)]
public sealed class ReportLocalizationCollection { }

[Collection("ReportLocalization")]
public sealed class ReportServiceTests
{
    [Fact]
    public void GenerateReport_WritesSeparateMaterialSections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        var previousCulture = LocalizationService.Instance.Culture;
        try
        {
            LocalizationService.Instance.Culture = "en";
            var first = MaterialResult("first", "Tube", "Stand", 1250);
            var second = MaterialResult("second", "Angle", "Brace", 850);
            var task = new ProjectCuttingTask
            {
                Materials = [first.Material, second.Material],
                Parts = [first.Task.Parts[0], second.Task.Parts[0]]
            };
            var result = new ProjectCuttingResult { Materials = [first, second] };

            new ReportService().GenerateReport(path, task, result);

            Assert.True(new FileInfo(path).Length > 0);
            using var document = DocX.Load(path);
            var text = string.Join("\n", document.Paragraphs.Select(x => x.Text));
            Assert.Contains("Tube", text);
            Assert.Contains("Angle", text);
            Assert.DoesNotContain("REQUIRED BARS:", text);
            Assert.Contains("Tube - 6000 mm - 1 bar", text);
            Assert.Contains("Angle - 6000 mm - 1 bar", text);
            Assert.True(text.IndexOf("Tube - 6000 mm - 1 bar", StringComparison.Ordinal) < text.IndexOf("Total part length", StringComparison.Ordinal));
            Assert.True(text.IndexOf("Angle - 6000 mm - 1 bar", StringComparison.Ordinal) < text.IndexOf("Total part length", StringComparison.Ordinal));
            Assert.Contains("Stand - 1250", text);
            Assert.Contains("Brace - 850", text);

            using var archive = ZipFile.OpenRead(path);
            using var stream = archive.GetEntry("word/document.xml")!.Open();
            var xml = XDocument.Load(stream);
            XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var textRuns = xml.Descendants(word + "r").Where(run => run.Descendants(word + "t").Any()).ToList();
            Assert.NotEmpty(textRuns);
            Assert.All(textRuns, run =>
            {
                var fonts = run.Element(word + "rPr")?.Element(word + "rFonts");
                Assert.Equal("Calibri", fonts?.Attribute(word + "ascii")?.Value);
                Assert.Equal("Calibri", fonts?.Attribute(word + "hAnsi")?.Value);
            });
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void GenerateReport_InUkrainian_LocalizesSummaryAndDetails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        var previousCulture = LocalizationService.Instance.Culture;
        try
        {
            LocalizationService.Instance.Culture = "ua";
            var first = MaterialResult("first", "Труба", "Стійка", 1250);
            var second = MaterialResult("second", "Кутник", "Розпірка", 850);
            var task = new ProjectCuttingTask
            {
                Materials = [first.Material, second.Material],
                Parts = [first.Task.Parts[0], second.Task.Parts[0]]
            };
            var result = new ProjectCuttingResult { Materials = [first, second] };

            new ReportService().GenerateReport(path, task, result);

            using var document = DocX.Load(path);
            var paragraphs = document.Paragraphs.Select(paragraph => paragraph.Text).ToList();
            Assert.StartsWith("N.StLine — Звіт з розкрою", paragraphs[0]);
            Assert.Equal("Труба - 6000 мм - 1 заготовка", paragraphs[2]);
            Assert.Equal("Кутник - 6000 мм - 1 заготовка", paragraphs[3]);
            Assert.DoesNotContain(paragraphs, paragraph => paragraph.Contains("REQUIRED BARS") || paragraph.Contains("Total part length") || paragraph.Contains("Bar length") || paragraph.Contains("remainder"));
            Assert.Contains(paragraphs, paragraph => paragraph.Contains("Загальна довжина деталей"));
            Assert.Contains(paragraphs, paragraph => paragraph.Contains("Ширина різу"));
            Assert.Contains(paragraphs, paragraph => paragraph.Contains("залишок"));
        }
        finally
        {
            LocalizationService.Instance.Culture = previousCulture;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Theory]
    [InlineData(NStLine.App.Models.ReportPartSeparator.Plus, " + ")]
    [InlineData(NStLine.App.Models.ReportPartSeparator.Pipe, " | ")]
    [InlineData(NStLine.App.Models.ReportPartSeparator.Dash, " - ")]
    public void GenerateReport_AppliesOptionsOnlyToSchemeParagraphs(NStLine.App.Models.ReportPartSeparator separator, string delimiter)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        try
        {
            var material = MaterialResult("first", "Швеллер 12П", "Part 3", 600);
            var secondPart = new PlacedPart { Source = new PartRequirement { Name = "Part 2", Length = 500, Quantity = 1 } };
            var pattern = new CuttingPattern
            {
                Multiplicity = 4, Remainder = 30,
                Parts = Enumerable.Repeat(material.Result.Patterns[0].Parts[0], 9).Append(secondPart).ToList()
            };
            var result = new ProjectCuttingResult
            {
                Materials = [new MaterialCuttingResult
                {
                    Material = material.Material, Task = material.Task,
                    Result = new CuttingResult { Status = CalculationStatus.BestFound, Patterns = [pattern] }
                }]
            };
            var settings = new NStLine.App.Models.ReportSettings
            {
                Title = "Order", TitleIncludeDate = true, TitleIncludeTime = true,
                ShowSummary = false, IncludeMaterial = true, ShowPartNames = false, PartSeparator = separator
            };
            new ReportService().GenerateReport(path, new ProjectCuttingTask(), result,
                new NStLine.App.Models.ReportGenerationOptions(settings, new DateTime(2026, 10, 30, 14, 30, 25), "ua"));

            using var document = DocX.Load(path);
            Assert.Equal("Order — 30.10.2026 14:30:25", document.Paragraphs[0].Text);
            Assert.DoesNotContain(document.Paragraphs, p => p.Text == "Підсумок");
            Assert.Contains(document.Paragraphs, p => p.Text.Contains("Найкращий знайдений варіант"));
            var scheme = Assert.Single(document.Paragraphs.Where(p => p.Text.Contains("×4")));
            Assert.Equal($"Швеллер 12П - 6000 мм ×4 | 600 мм × 9{delimiter}500 мм | залишок 30", scheme.Text);
            Assert.Contains("Part 3", document.Tables[0].Rows[1].Cells[0].Paragraphs[0].Text);

            using var archive = ZipFile.OpenRead(path);
            using var stream = archive.GetEntry("word/document.xml")!.Open();
            var xml = XDocument.Load(stream);
            XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var quantity = Assert.Single(xml.Descendants(word + "r").Where(r => string.Concat(r.Descendants(word + "t").Select(t => t.Value)) == "×4"));
            var bold = quantity.Element(word + "rPr")?.Element(word + "b");
            Assert.True(bold == null || bold.Attribute(word + "val")?.Value is "0" or "false" or "off");
            var details = Assert.Single(xml.Descendants(word + "r").Where(r => string.Concat(r.Descendants(word + "t").Select(t => t.Value)).Contains("600 мм × 9")));
            Assert.True(details.Element(word + "rPr")?.Element(word + "b") == null || details.Element(word + "rPr")?.Element(word + "b")?.Attribute(word + "val")?.Value == "0");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MaterialCuttingResult MaterialResult(string id, string materialName, string partName, double length)
    {
        var material = new LinearMaterial { Id = id, Name = materialName, StandardLength = 6000, CutWidth = 3 };
        var part = new PartRequirement { MaterialId = id, Name = partName, Length = length, Quantity = 1 };
        var bar = new BarCutting
        {
            Index = 1,
            BarLength = 6000,
            Parts = [new PlacedPart { Source = part, CopyIndex = 1 }],
            CutLoss = 3,
            Remainder = 6000 - length - 3
        };
        return new MaterialCuttingResult
        {
            Material = material,
            Task = new CuttingTask { Material = material, BarLength = 6000, CutWidth = 3, Parts = [part] },
            Result = new CuttingResult
            {
                Status = CalculationStatus.Optimal,
                Bars = [bar],
                Patterns = [new CuttingPattern { Multiplicity = 1, Parts = bar.Parts, Remainder = bar.Remainder }]
            }
        };
    }
}
