using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using NStLine.App.Services;
using NStLine.Core.Models;
using Xceed.Words.NET;

namespace NStLine.App.Tests;

public sealed class ReportServiceTests
{
    [Fact]
    public void GenerateReport_WritesSeparateMaterialSections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        try
        {
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
            if (File.Exists(path))
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
