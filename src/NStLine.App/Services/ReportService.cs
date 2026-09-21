using NStLine.App.Formatters;
using NStLine.Core.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace NStLine.App.Services;

public sealed class ReportService : IReportService
{
    public void GenerateReport(string outputPath, ProjectCuttingTask task, ProjectCuttingResult result)
    {
        using var document = DocX.Create(outputPath);

        document.InsertParagraph("N.StLine — Cutting Report").FontSize(18).Bold();
        document.InsertParagraph();
        document.InsertParagraph("Summary").FontSize(14).Bold();
        document.InsertParagraph($"Status: {(result.Status == CalculationStatus.Optimal ? "Optimal" : "Best found")}");
        document.InsertParagraph($"Materials: {result.Materials.Count}");
        document.InsertParagraph($"Bars: {result.TotalBarCount}");
        document.InsertParagraph($"Total part length: {result.TotalPartLength:F1} mm");
        document.InsertParagraph($"Total cut loss: {result.TotalCutLoss:F1} mm");
        document.InsertParagraph($"Total remainder: {result.TotalRemainder:F1} mm");
        document.InsertParagraph($"Utilization: {result.UtilizationPercent:F1}%");

        foreach (var materialResult in result.Materials)
            InsertMaterialSection(document, materialResult);

        document.Save();
    }

    private static void InsertMaterialSection(DocX document, MaterialCuttingResult materialResult)
    {
        var task = materialResult.Task;
        var result = materialResult.Result;
        document.InsertParagraph();
        document.InsertParagraph(task.Material.Name).FontSize(14).Bold();
        document.InsertParagraph($"Bar length: {task.BarLength:F1} mm");
        document.InsertParagraph($"Cut width: {task.CutWidth:F1} mm");

        var partsTable = document.AddTable(task.Parts.Count + 1, 3);
        partsTable.Design = TableDesign.LightGridAccent1;
        partsTable.Rows[0].Cells[0].Paragraphs[0].Append("Name").Bold();
        partsTable.Rows[0].Cells[1].Paragraphs[0].Append("Length, mm").Bold();
        partsTable.Rows[0].Cells[2].Paragraphs[0].Append("Quantity").Bold();
        for (var index = 0; index < task.Parts.Count; index++)
        {
            var part = task.Parts[index];
            partsTable.Rows[index + 1].Cells[0].Paragraphs[0].Append(part.Name);
            partsTable.Rows[index + 1].Cells[1].Paragraphs[0].Append(part.Length.ToString("0.##"));
            partsTable.Rows[index + 1].Cells[2].Paragraphs[0].Append(part.Quantity.ToString());
        }
        document.InsertTable(partsTable);

        document.InsertParagraph($"Bars: {result.TotalBarCount}; cut loss: {result.TotalCutLoss:F1} mm; remainder: {result.TotalRemainder:F1} mm; utilization: {result.UtilizationPercent:F1}%");
        foreach (var pattern in result.Patterns)
        {
            var parts = CuttingTextFormatter.FormatGroupedParts(pattern.Parts);
            document.InsertParagraph($"×{pattern.Multiplicity}: {parts} | remainder {pattern.Remainder:F1}");
        }
    }
}
