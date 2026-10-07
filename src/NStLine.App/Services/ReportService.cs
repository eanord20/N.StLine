using NStLine.App.Formatters;
using NStLine.Core.Constants;
using NStLine.Core.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace NStLine.App.Services;

public sealed class ReportService : IReportService
{
    public void GenerateReport(string outputPath, ProjectCuttingTask task, ProjectCuttingResult result)
    {
        using var document = DocX.Create(outputPath);

        InsertParagraph(document, "N.StLine — Cutting Report").FontSize(18).Bold();
        InsertParagraph(document);
        InsertParagraph(document, "Summary").FontSize(14).Bold();
        InsertParagraph(document, $"REQUIRED BARS: {result.TotalBarCount}").FontSize(16).Bold();
        InsertParagraph(document, $"Status: {(result.Status == CalculationStatus.Optimal ? "Optimal" : "Best found")}");
        InsertParagraph(document, $"Materials: {result.Materials.Count}");
        InsertParagraph(document, $"Total part length: {result.TotalPartLength:F1} mm");
        InsertParagraph(document, $"Total cut loss: {result.TotalCutLoss:F1} mm");
        InsertParagraph(document, $"Total remainder: {result.TotalRemainder:F1} mm");
        InsertParagraph(document, $"Utilization: {result.UtilizationPercent:F1}%");

        foreach (var materialResult in result.Materials)
            InsertMaterialSection(document, materialResult);

        document.Save();
    }

    private static void InsertMaterialSection(DocX document, MaterialCuttingResult materialResult)
    {
        var task = materialResult.Task;
        var result = materialResult.Result;
        InsertParagraph(document);
        InsertParagraph(document, task.Material.Name).FontSize(14).Bold();
        InsertParagraph(document, $"Bar length: {task.BarLength:F1} mm");
        InsertParagraph(document, $"Cut width: {task.CutWidth:F1} mm");

        var partsTable = document.AddTable(task.Parts.Count + 1, 3);
        partsTable.Design = TableDesign.LightGridAccent1;
        partsTable.Rows[0].Cells[0].Paragraphs[0].Append("Name").Font(AppConstants.ReportFontFamily).Bold();
        partsTable.Rows[0].Cells[1].Paragraphs[0].Append("Length, mm").Font(AppConstants.ReportFontFamily).Bold();
        partsTable.Rows[0].Cells[2].Paragraphs[0].Append("Quantity").Font(AppConstants.ReportFontFamily).Bold();
        for (var index = 0; index < task.Parts.Count; index++)
        {
            var part = task.Parts[index];
            partsTable.Rows[index + 1].Cells[0].Paragraphs[0].Append(part.Name).Font(AppConstants.ReportFontFamily);
            partsTable.Rows[index + 1].Cells[1].Paragraphs[0].Append(part.Length.ToString("0.##")).Font(AppConstants.ReportFontFamily);
            partsTable.Rows[index + 1].Cells[2].Paragraphs[0].Append(part.Quantity.ToString()).Font(AppConstants.ReportFontFamily);
        }
        document.InsertTable(partsTable);

        InsertParagraph(document, $"Bars: {result.TotalBarCount}; cut loss: {result.TotalCutLoss:F1} mm; remainder: {result.TotalRemainder:F1} mm; utilization: {result.UtilizationPercent:F1}%");
        foreach (var pattern in result.Patterns)
        {
            var parts = CuttingTextFormatter.FormatGroupedParts(
                pattern.Parts,
                lengthUnit: LocalizationService.Instance.UnitMillimeters);
            InsertParagraph(document, $"×{pattern.Multiplicity}: {parts} | remainder {pattern.Remainder:F1}");
        }
    }

    private static Paragraph InsertParagraph(DocX document, string text = "")
        => document.InsertParagraph(text).Font(AppConstants.ReportFontFamily);
}
