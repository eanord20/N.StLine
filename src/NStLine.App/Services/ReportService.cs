using System.Globalization;
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
        var localization = LocalizationService.Instance;
        var culture = CultureInfo.GetCultureInfo(localization.Culture == "ua" ? "uk-UA" : "en-US");
        using var document = DocX.Create(outputPath);

        InsertParagraph(document, $"{AppConstants.ApplicationName} — {localization.ReportTitle}").FontSize(18).Bold();
        InsertParagraph(document);
        foreach (var materialResult in result.Materials)
            InsertParagraph(document, MaterialHeading(materialResult, localization, culture)).FontSize(16).Bold();

        InsertParagraph(document);
        InsertParagraph(document, localization.LabelSummary).FontSize(14).Bold();
        InsertParagraph(document, $"{localization.ReportStatus}: {(result.Status == CalculationStatus.Optimal ? localization.StatusOptimal : localization.StatusBestFound)}");
        InsertParagraph(document, $"{localization.LabelTotalPartLength}: {result.TotalPartLength.ToString("0.#", culture)} {localization.UnitMillimeters}");
        InsertParagraph(document, $"{localization.LabelTotalCutLoss}: {result.TotalCutLoss.ToString("0.#", culture)} {localization.UnitMillimeters}");
        InsertParagraph(document, $"{localization.LabelTotalRemainder}: {result.TotalRemainder.ToString("0.#", culture)} {localization.UnitMillimeters}");
        InsertParagraph(document, $"{localization.LabelUtilization}: {result.UtilizationPercent.ToString("0.#", culture)}%");

        foreach (var materialResult in result.Materials)
            InsertMaterialSection(document, materialResult, localization, culture);

        document.Save();
    }

    private static void InsertMaterialSection(DocX document, MaterialCuttingResult materialResult,
        LocalizationService localization, CultureInfo culture)
    {
        var task = materialResult.Task;
        var result = materialResult.Result;
        InsertParagraph(document);
        InsertParagraph(document, MaterialHeading(materialResult, localization, culture)).FontSize(14).Bold();
        InsertParagraph(document, $"{localization.LabelCutWidth}: {task.CutWidth.ToString("0.##", culture)} {localization.UnitMillimeters}");

        var partsTable = document.AddTable(task.Parts.Count + 1, 3);
        partsTable.Design = TableDesign.LightGridAccent1;
        partsTable.Rows[0].Cells[0].Paragraphs[0].Append(localization.ColumnName).Font(AppConstants.ReportFontFamily).Bold();
        partsTable.Rows[0].Cells[1].Paragraphs[0].Append(localization.ColumnLength).Font(AppConstants.ReportFontFamily).Bold();
        partsTable.Rows[0].Cells[2].Paragraphs[0].Append(localization.ColumnQuantity).Font(AppConstants.ReportFontFamily).Bold();
        for (var index = 0; index < task.Parts.Count; index++)
        {
            var part = task.Parts[index];
            partsTable.Rows[index + 1].Cells[0].Paragraphs[0].Append(part.Name).Font(AppConstants.ReportFontFamily);
            partsTable.Rows[index + 1].Cells[1].Paragraphs[0].Append(part.Length.ToString("0.##", culture)).Font(AppConstants.ReportFontFamily);
            partsTable.Rows[index + 1].Cells[2].Paragraphs[0].Append(part.Quantity.ToString(culture)).Font(AppConstants.ReportFontFamily);
        }
        document.InsertTable(partsTable);

        InsertParagraph(document, $"{localization.LabelTotalCutLoss}: {result.TotalCutLoss.ToString("0.#", culture)} {localization.UnitMillimeters}; " +
            $"{localization.LabelTotalRemainder}: {result.TotalRemainder.ToString("0.#", culture)} {localization.UnitMillimeters}; " +
            $"{localization.LabelUtilization}: {result.UtilizationPercent.ToString("0.#", culture)}%");
        foreach (var pattern in result.Patterns)
        {
            var parts = CuttingTextFormatter.FormatGroupedParts(pattern.Parts, culture, localization.UnitMillimeters);
            InsertParagraph(document, $"×{pattern.Multiplicity}: {parts} | {localization.ColumnRemainder.ToLower(culture)} {pattern.Remainder.ToString("0.#", culture)}");
        }
    }

    private static string MaterialHeading(MaterialCuttingResult materialResult,
        LocalizationService localization, CultureInfo culture)
    {
        var count = materialResult.Result.TotalBarCount;
        var bars = localization.Culture == "ua"
            ? count % 10 == 1 && count % 100 != 11
                ? localization.ReportBarsOne
                : count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14
                    ? localization.ReportBarsFew
                    : localization.ReportBarsMany
            : count == 1 ? localization.ReportBarsOne : localization.ReportBarsMany;
        return $"{materialResult.Material.Name} - {materialResult.Task.BarLength.ToString("0.##", culture)} {localization.UnitMillimeters} - {count} {bars}";
    }

    private static Paragraph InsertParagraph(DocX document, string text = "")
        => document.InsertParagraph(text).Font(AppConstants.ReportFontFamily);
}
