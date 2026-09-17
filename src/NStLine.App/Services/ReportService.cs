using NStLine.Core.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace NStLine.App.Services;

public sealed class ReportService : IReportService
{
    public void GenerateReport(string outputPath, CuttingTask task, CuttingResult result)
    {
        using var document = DocX.Create(outputPath);

        document.InsertParagraph("N.StLine — Cutting Report").FontSize(18).Bold();
        document.InsertParagraph();

        document.InsertParagraph("Material").FontSize(14).Bold();
        document.InsertParagraph($"Name: {task.Material.Name}");
        document.InsertParagraph($"Bar length: {task.BarLength:F1} mm");
        document.InsertParagraph($"Cut width: {task.CutWidth:F1} mm");
        document.InsertParagraph();

        document.InsertParagraph("Required parts").FontSize(14).Bold();
        var partsTable = document.AddTable(task.Parts.Count + 1, 3);
        partsTable.Design = TableDesign.LightGridAccent1;
        partsTable.Rows[0].Cells[0].Paragraphs[0].Append("Name").Bold();
        partsTable.Rows[0].Cells[1].Paragraphs[0].Append("Length, mm").Bold();
        partsTable.Rows[0].Cells[2].Paragraphs[0].Append("Quantity").Bold();

        for (int i = 0; i < task.Parts.Count; i++)
        {
            var part = task.Parts[i];
            partsTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(part.Name);
            partsTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(part.Length.ToString("F1"));
            partsTable.Rows[i + 1].Cells[2].Paragraphs[0].Append(part.Quantity.ToString());
        }
        document.InsertTable(partsTable);
        document.InsertParagraph();

        document.InsertParagraph("Result").FontSize(14).Bold();
        document.InsertParagraph($"Status: {(result.Status == CalculationStatus.Optimal ? "Optimal" : "Best found")}");
        document.InsertParagraph($"Bars: {result.TotalBarCount}");
        document.InsertParagraph($"Total part length: {result.TotalPartLength:F1} mm");
        document.InsertParagraph($"Total cut loss: {result.TotalCutLoss:F1} mm");
        document.InsertParagraph($"Total remainder: {result.TotalRemainder:F1} mm");
        document.InsertParagraph($"Utilization: {result.UtilizationPercent:F1}%");
        document.InsertParagraph();

        document.InsertParagraph("Cutting schemes").FontSize(14).Bold();
        foreach (var pattern in result.Patterns)
        {
            var partsText = string.Join(" + ", pattern.Parts.Select(p => $"{p.Source.Name} {p.Source.Length:F1}"));
            document.InsertParagraph($"×{pattern.Multiplicity}: {partsText} | remainder {pattern.Remainder:F1}");
        }

        document.Save();
    }
}
