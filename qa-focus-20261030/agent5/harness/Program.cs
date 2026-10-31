// QA agent5 harness. Executes the REAL PartPasteParser from NStLine.App.dll
// and replicates the paste loop of MainViewModel.PasteFromClipboard
// (src/NStLine.App/ViewModels/MainViewModel.cs lines 638-688) because the real
// method requires the system clipboard, which must not be modified.
// Real MainViewModel is exercised for manual-entry scenarios via public API.

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Contracts;
using NStLine.Core.Models;

Console.OutputEncoding = Encoding.UTF8;
var results = new List<Dictionary<string, object>>();

// ---------- Phase 1: PartPasteParser under multiple process cultures ----------
// Exact replication of the row-split in PasteFromClipboard (lines 651-670):
//   lines = text.Split(\r\n,\n,\r, RemoveEmptyEntries)
//   cells = line.Split('\t').Select(Trim)
//   PartPasteParser.TryParse -> add row {Name or auto, Length, Math.Max(1,quantity)} or SKIP silently.
List<(string id, string clipText, string note)> cases = new()
{
    ("P01-single-length",        "1250",                                  "1 col: length only"),
    ("P02-len-qty",              "1250\t4",                               "2 cols numeric: length+qty"),
    ("P03-name-len",             "Стойка\t1250",                          "2 cols: name+length, qty default"),
    ("P04-name-len-qty",         "Стойка\t1250\t10",                      "3 cols canonical Excel"),
    ("P05-qty-fraction",         "1250\t4.5",                             "fractional qty -> silent rounding?"),
    ("P05b-qty-fraction-2_4",    "1250\t2.4",                             "fractional qty 2.4"),
    ("P05c-qty-frac-name",       "Стойка\t1250\t4.5",                     "3 cols fractional qty"),
    ("P06-qty-zero",             "1200\t0",                               "qty 0 -> spec says error"),
    ("P07-qty-negative",         "1200\t-3",                              "qty -3 -> spec says error"),
    ("P07b-qty-neg-name",        "Стойка\t1200\t-3",                      "3 cols negative qty"),
    ("P08-two-length-cols",      "2400\t6000",                            "2 numeric cols, 2nd huge: length+barlen?"),
    ("P09-extra-4th-col",        "Стойка\t1250\t10\tEXTRA",               "4 cols: extra silently ignored?"),
    ("P10-trailing-tab",         "Стойка\t1250\t",                        "3rd cell empty -> qty missing"),
    ("P11-empty-name-cell",      "\t1250\t5",                             "empty name, len, qty"),
    ("P12-empty-lines",          "1250\r\n\r\n\t\r\n800\t2\n\n",          "blank + whitespace-only lines"),
    ("P13-text-in-number",       "Стойка\tabc\t3",                        "non-numeric length"),
    ("P14-decimal-comma",        "Стойка\t1250,5\t3",                     "comma decimal: culture dependent"),
    ("P15-decimal-dot",          "Стойка\t1250.5\t3",                     "dot decimal"),
    ("P16-dot-thousands",        "Стойка\t1.250\t2",                      "de-FR thousands sep -> 1.25?"),
    ("P17-space-thousands",      "Стойка\t1 250\t2",                      "Excel format '# ##0'"),
    ("P18-nbsp-thousands",       "Стойка\t1 250\t2",                      "NBSP group sep (fr-FR Excel)"),
    ("P19-exponent",             "1e3\t2",                                "exponent notation"),
    ("P20-negative-length",      "-500\t2",                               "negative length"),
    ("P21-zero-length",          "0\t2",                                  "zero length"),
    ("P22-huge-qty",             "500\t99999999999",                      "qty > int.MaxValue"),
    ("P23-nan-length",           "NaN\t2",                                "NaN literal"),
    ("P24-inf-length",           "Infinity\t2",                           "Infinity literal"),
    ("P25-bom-first-line",       "﻿1250\t2\r\n800\t1",             "BOM U+FEFF on first cell"),
    ("P26-space-separated",      "Стойка 1250 5",                         "spaces not tabs"),
    ("P27-numeric-name",         "1234\t1250",                            "name looks numeric: ambiguity"),
    ("P28-len-text-qty",         "1250\tСтойка",                          "len + text col2: falls to name+len?"),
    ("P29-units-in-text",        "Стойка\t1250 мм\t3",                    "units inside length cell"),
    ("P30-partial-name-only",    "Стойка",                                "1 col text only"),
    ("P31-mixed-valid-invalid",  "A\t100\t1\r\nB\tbad\t2\r\nC\t200\t2\r\nD\t-50\t1\r\nE\t300", "mixed rows"),
    ("P32-dup-rows",             "A\t100\t1\r\nA\t100\t1",                "duplicate rows"),
    ("P33-crlf-and-cr",          "A\t100\t1\rB\t200\t2\nC\t300\t3\r\n",   "mixed line endings"),
    ("P34-qty-with-plus",        "500\t+3",                               "qty '+3'"),
    ("P35-len-hex-ish",          "0x100\t2",                              "hex-like text"),
    ("P36-qty-text-words",       "500\tthree",                            "qty word"),
    ("P37-name-with-digits",     "Деталь 12а\t400\t2",                    "mixed alphanumeric name"),
    ("P38-len-eq-bar",           "6000\t1",                               "len == bar length (cut 3)"),
    ("P39-two-cells-second-num-ambig", "A100\t2",                         "name like A100 + qty? treated name+len fail"),
    ("P40-qty-decimal-comma",    "500\t3,5",                              "comma fraction qty"),
};

var cultures = new[] { "en-US", "ru-RU", "uk-UA", "de-DE" };
foreach (var cultureName in cultures)
{
    var culture = CultureInfo.GetCultureInfo(cultureName);
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;
    CultureInfo.DefaultThreadCurrentCulture = culture;

    foreach (var (id, clipText, note) in cases)
    {
        var lines = clipText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        var rows = new List<Dictionary<string, object>>();
        var skipped = new List<int>();
        var lineNo = 0;
        foreach (var line in lines)
        {
            lineNo++;
            var cells = line.Split('\t').Select(c => c.Trim()).ToArray();
            var ok = PartPasteParser.TryParse(cells, out var name, out var length, out var quantity);
            if (!ok) { skipped.Add(lineNo); continue; }
            rows.Add(new()
            {
                ["line"] = lineNo,
                ["name"] = string.IsNullOrWhiteSpace(name) ? $"Part {rows.Count + 1}" : name,
                ["length"] = length.ToString("R", CultureInfo.InvariantCulture),
                ["quantity"] = Math.Max(1, quantity)
            });
        }
        results.Add(new()
        {
            ["id"] = id, ["culture"] = cultureName, ["note"] = note,
            ["input"] = clipText, ["addedRows"] = rows, ["skippedLines"] = skipped,
            ["wouldShowPasteFailed"] = rows.Count == 0
        });
    }
}

File.WriteAllText(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "parser-results.json"),
    JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

// ---------- Phase 2: real MainViewModel, manual-entry scenarios ----------
var vmLog = new List<string>();
try
{
    var vm = new MainViewModel(
        new FakeEngine(),
        new FakeMaterials(),
        projectFileService: null,
        new FakeSettings(),
        reportService: null,
        confirmReportOverwrite: _ => true);
    var mat = new LinearMaterial { Id = "m1", Name = "Bar 6000/3", StandardLength = 6000, CutWidth = 3 };
    vm.LibraryMaterials.Add(mat);
    vm.AddMaterialCommand.Execute(mat);
    vmLog.Add($"ActiveMaterialId={vm.ActiveMaterialId}");

    // Manual entry: AddRow then edit Length/Quantity like DataGrid bindings do.
    vm.AddRowCommand.Execute(null);
    var row = vm.Parts[0];
    vmLog.Add($"after AddRow: len={row.Length} qty={row.Quantity} err={row.Error ?? "<null>"}");
    row.Length = 1000;
    vmLog.Add($"set Length=1000: err={row.Error ?? "<null>"}");
    row.Length = 0;   vmLog.Add($"set Length=0: err={row.Error}");
    row.Length = -5;  vmLog.Add($"set Length=-5: err={row.Error}");
    row.Length = 5998; vmLog.Add($"set Length=5998 (5998+3>6000): err={row.Error}");
    row.Length = 5997; vmLog.Add($"set Length=5997 (5997+3==6000): err={row.Error ?? "<null>"}");
    row.Quantity = 0; vmLog.Add($"set Quantity=0: err={row.Error}");
    row.Quantity = -3; vmLog.Add($"set Quantity=-3: err={row.Error}");
    row.Quantity = 5; vmLog.Add($"set Quantity=5: err={row.Error ?? "<null>"}");
    vm.ReplaceQuantityFromKeyboard(row, 0);
    vmLog.Add($"ReplaceQuantityFromKeyboard(0): qty={row.Quantity} err={row.Error}");
    vm.ReplaceQuantityFromKeyboard(row, 7);
    vmLog.Add($"ReplaceQuantityFromKeyboard(7): qty={row.Quantity} err={row.Error ?? "<null>"}");

    // Can user enter length with comma? binding is double with CurrentCulture.
    // Simulate what WPF double binding does: Convert.ChangeType via TypeConverter under culture.
    foreach (var cult in new[] { "en-US", "ru-RU" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cult);
        var conv = System.ComponentModel.TypeDescriptor.GetConverter(typeof(double));
        foreach (var txt in new[] { "1250,5", "1250.5", "1 250", "abc", "1e3" })
        {
            try { var v = conv.ConvertFromString(null, CultureInfo.CurrentCulture, txt); vmLog.Add($"grid-double '{txt}' @{cult} -> {v}"); }
            catch (Exception ex) { vmLog.Add($"grid-double '{txt}' @{cult} -> FAIL {ex.GetType().Name}"); }
        }
    }
}
catch (Exception ex)
{
    vmLog.Add($"VM-HARNESS-EXCEPTION: {ex}");
}

File.WriteAllLines(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "vm-results.txt"), vmLog);
foreach (var l in vmLog) Console.WriteLine(l);
Console.WriteLine($"parser cases written: {results.Count}");

sealed class FakeEngine : IProjectCuttingEngine
{
    public ProjectCuttingResult Solve(ProjectCuttingTask task, CuttingOptions options,
        IProgress<double> progress = null, CancellationToken cancellationToken = default)
        => new();
}

sealed class FakeMaterials : IMaterialsLibraryService
{
    public IReadOnlyList<LinearMaterial> LoadAll() => Array.Empty<LinearMaterial>();
    public void SaveAll(IEnumerable<LinearMaterial> materials) { }
}

sealed class FakeSettings : IAppSettingsService
{
    public AppSettings Load() => new();
    public void Save(AppSettings settings) { }
}
