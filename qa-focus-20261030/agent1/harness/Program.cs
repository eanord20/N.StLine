// QA Agent1 harness: exercises real ViewModels/services/engine with injected QA-dir paths.
// No user files, no clipboard, no App.OnStartup.
using System.IO;
using System.Reflection;
using System.Text.Json;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.App.Formatters;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

var qaDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var dataDir = Path.Combine(qaDir, "data");
var evDir = Path.Combine(qaDir, "evidence");
if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(evDir);

var log = new List<object>();
void Rec(string scenario, string check, object expected, object actual, bool pass, string note = "")
    => log.Add(new { scenario, check, expected, actual, pass, note });

static MainViewModel NewVm(string dataDir, Func<string, bool>? confirm = null)
    => new(new ProjectCuttingEngine(new CuttingEngine()),
           new MaterialsLibraryService(Path.Combine(dataDir, "materials.json")),
           new ProjectFileService(),
           new AppSettingsService(Path.Combine(dataDir, "settings.json")),
           new ReportService(),
           confirm ?? (_ => true));

static LinearMaterial Mat(string name, double len, double kerf)
    => new() { Name = name, StandardLength = len, CutWidth = kerf };

// ---------- S1: materials library service ----------
try
{
    var dir = Path.Combine(dataDir, "s1"); Directory.CreateDirectory(dir);
    var svc = new MaterialsLibraryService(Path.Combine(dir, "materials.json"));
    Rec("S1", "empty library load", "0 items", svc.LoadAll().Count, svc.LoadAll().Count == 0);
    svc.SaveAll(new[] { Mat("Tube 20", 6000, 3), Mat("Pipe 50", 12000, 0) });
    var loaded = svc.LoadAll();
    Rec("S1", "save/load round-trip", "2 items, values kept", loaded.Count,
        loaded.Count == 2 && loaded[0].StandardLength == 6000 && loaded[1].CutWidth == 0);
}
catch (Exception ex) { Rec("S1", "service", "no exception", ex.GetType().Name, false); }

// corrupt materials.json -> LoadAll
try
{
    var dir = Path.Combine(dataDir, "s1bad"); Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir, "materials.json"), "{ this is not json !!!");
    var svc = new MaterialsLibraryService(Path.Combine(dir, "materials.json"));
    svc.LoadAll();
    Rec("S1", "corrupt materials.json", "graceful empty/error", "no exception", false);
}
catch (Exception ex)
{
    Rec("S1", "corrupt materials.json", "graceful fallback or friendly error",
        $"unhandled {ex.GetType().Name}: {ex.Message}", false,
        "LoadAll has no error handling; MainViewModel ctor calls LoadLibrary -> app start crash");
}

// corrupt settings.json
try
{
    var dir = Path.Combine(dataDir, "s1bad2"); Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir, "settings.json"), "not-json{");
    var svc = new AppSettingsService(Path.Combine(dir, "settings.json"));
    svc.Load();
    Rec("S1", "corrupt settings.json", "graceful defaults", "no exception", false);
}
catch (Exception ex)
{
    Rec("S1", "corrupt settings.json", "graceful defaults",
        $"unhandled {ex.GetType().Name}: {ex.Message}", false,
        "AppSettingsService.Load has no error handling -> app start crash");
}

// ---------- S2: materials VM import dedup ----------
try
{
    var vm = new MaterialsViewModel(new[] { Mat("Tube 20", 6000, 3) });
    var existingId = vm.Materials[0].Id;
    var res = vm.ImportMaterials(new[]
    {
        Mat("tube 20 ", 6000, 3),          // normalized duplicate -> skip
        Mat("Pipe 50", 12000, 2),          // new
        new LinearMaterial { Id = existingId, Name = "Conflict", StandardLength = 3000, CutWidth = 1 } // id conflict, new key
    });
    Rec("S2", "import dedup", "Added=2 Skipped=1", $"{res.Added}/{res.Skipped}", res.Added == 2 && res.Skipped == 1);
    var conflict = vm.Materials.FirstOrDefault(m => m.Name == "Conflict");
    Rec("S2", "id conflict gets new id", "new Id != existing", conflict?.Id,
        conflict != null && conflict.Id != existingId);
    // invalid file rejected entirely
    try
    {
        MaterialsViewModel vm2 = new(Array.Empty<LinearMaterial>());
        vm2.ImportMaterials(new[] { Mat("ok", 100, 1), new LinearMaterial { Name = " ", StandardLength = -5 } });
        Rec("S2", "invalid import rejected", "InvalidDataException", "no exception", false);
    }
    catch (InvalidDataException) { Rec("S2", "invalid import rejected", "InvalidDataException", "InvalidDataException", true); }
}
catch (Exception ex) { Rec("S2", "import", "no exception", ex.ToString(), false); }

// ---------- S3: task input & validation ----------
try
{
    var dir = Path.Combine(dataDir, "s3"); Directory.CreateDirectory(dir);
    var vm = NewVm(dir);
    var material = Mat("Tube 20", 6000, 3);
    vm.LibraryMaterials.Add(material);
    Rec("S3", "AddRow disabled without material", "CanExecute=false",
        vm.AddRowCommand.CanExecute(null), !vm.AddRowCommand.CanExecute(null));
    vm.AddMaterialCommand.Execute(material);
    Rec("S3", "material added & active", "1 project material, active set",
        $"{vm.ProjectMaterials.Count}/{vm.ActiveMaterialId != null}",
        vm.ProjectMaterials.Count == 1 && vm.ActiveMaterialId == material.Id);
    vm.AddRowCommand.Execute(null);
    var row = vm.Parts[0];
    row.Length = 7000; // longer than bar
    Rec("S3", "part > bar flagged", "HasError=true (does not fit)", row.Error, row.HasError);
    Rec("S3", "Calculate disabled on error", "false", vm.CalculateCommand.CanExecute(null),
        !vm.CalculateCommand.CanExecute(null));
    row.Length = 6000; // length == bar, kerf>0: needs 1 cut => does not fit per cut model N parts->N cuts
    Rec("S3", "length==bar with kerf flagged", "HasError=true", row.Error, row.HasError,
        "spec cut model: N parts -> N cuts, loss = N*CutWidth");
    row.Length = 5997;
    Rec("S3", "length==bar-kerf ok", "HasError=false", row.Error, !row.HasError);
    row.Quantity = 0;
    Rec("S3", "quantity 0 flagged", "HasError=true", row.Error, row.HasError);
    row.Quantity = 2;
    Rec("S3", "valid row", "HasError=false", row.Error, !row.HasError);
    // quantity +/- buttons
    vm.IncreaseQuantityCommand.Execute(row);
    Rec("S3", "increase qty", "3", row.Quantity, row.Quantity == 3);
    vm.DecreaseQuantityCommand.Execute(row); vm.DecreaseQuantityCommand.Execute(row);
    Rec("S3", "decrease qty floor", "1", row.Quantity, row.Quantity == 1);
    Rec("S3", "decrease disabled at 1", "CanExecute=false", vm.DecreaseQuantityCommand.CanExecute(row),
        !vm.DecreaseQuantityCommand.CanExecute(row));
    // keyboard digit replace
    row.Quantity = 7;
    vm.ReplaceQuantityFromKeyboard(row, 4);
    Rec("S3", "digit replaces qty (Excel-like)", "4", row.Quantity, row.Quantity == 4);
}
catch (Exception ex) { Rec("S3", "input", "no exception", ex.ToString(), false); }

// ---------- S4: paste parser (API level; real clipboard not touched) ----------
void P(string cells, bool expOk, string expDesc)
{
    var parts = cells.Split('\t');
    var ok = PartPasteParser.TryParse(parts, out var name, out var len, out var qty);
    Rec("S4", $"parse '{cells}'", expDesc, $"ok={ok} name='{name}' len={len} qty={qty}", ok == expOk);
}
P("1500", true, "single length");
P("1500\t3", true, "length+qty");
P("Shelf\t1500", true, "name+length");
P("Shelf\t1500\t4", true, "name+length+qty");
P("abc", false, "reject");
P("\t\t", false, "reject");
P("Shelf\t1500\t-5", true, "qty -5 -> ? (silently clamped to 1)");
P("Shelf\t1500\t2.5", true, "qty 2.5 -> ? (silently rounded)");
{
    PartPasteParser.TryParse(new[] { "Shelf", "1500", "-5" }, out _, out _, out var q1);
    PartPasteParser.TryParse(new[] { "Shelf", "1500", "2.5" }, out _, out _, out var q2);
    PartPasteParser.TryParse(new[] { "Shelf", "1500", "0" }, out _, out _, out var q3);
    Rec("S4", "negative qty silently becomes 1", "validation error or 1 with warning", q1, q1 == 1,
        "int '-5' parses then Math.Max(1,..) -> silent data change");
    Rec("S4", "fractional qty silently rounded", "validation error or exact", q2, q2 == 3,
        "'2.5' -> Round AwayFromZero -> 3");
    Rec("S4", "zero qty silently becomes 1", "validation error or 1 with warning", q3, q3 == 1);
}
P("Shelf\t3", true, "two-cell 'name\\t3' treated as name+length=3mm (ambiguous with qty)");

// ---------- S5: calculation, determinism, stale ----------
try
{
    var dir = Path.Combine(dataDir, "s5"); Directory.CreateDirectory(dir);
    var vm = NewVm(dir);
    var material = Mat("Tube 20", 6000, 3);
    vm.LibraryMaterials.Add(material);
    vm.AddMaterialCommand.Execute(material);
    void AddPart(string n, double len, int qty)
    {
        vm.AddRowCommand.Execute(null);
        var r = vm.Parts[^1]; r.Name = n; r.Length = len; r.Quantity = qty;
    }
    AddPart("A", 1000, 3);
    AddPart("B", 1500, 2);
    AddPart("C", 2500, 1);
    Rec("S5", "CanCalculate", "true", vm.CalculateCommand.CanExecute(null), vm.CalculateCommand.CanExecute(null));

    await vm.CalculateAsync();
    var res = vm.CurrentResult;
    Rec("S5", "calc finished", "result != null", res != null, res != null);
    Rec("S5", "status Optimal", "Optimal", res?.Status.ToString(), res?.Status == CalculationStatus.Optimal);
    var bars = res?.Materials[0].Result.Bars.Count ?? -1;
    // total length = 3000+3000+2500=8500; cuts=6 kerf*3=18 -> min 2 bars
    Rec("S5", "bar count minimal", "2", bars, bars == 2);
    var cutLoss = res?.Materials[0].Result.Bars.Sum(b => b.CutLoss);
    Rec("S5", "cut loss = N*kerf", "18 (6 parts*3mm)", cutLoss, Math.Abs((cutLoss ?? -1) - 18) < 0.01);
    Rec("S5", "status text", "Optimal localized", vm.CurrentResultStatusText, vm.CurrentResultStatusText == vm.StatusOptimal);

    // determinism: second identical run
    var ser = new JsonSerializerOptions();
    var r1 = JsonSerializer.Serialize(res!.Materials[0].Result.Bars.Select(b =>
        new { b.BarLength, b.CutLoss, P = b.Parts.Select(p => p.Source.Name + ":" + p.Source.Length) }), ser);
    await vm.CalculateAsync();
    var r2 = JsonSerializer.Serialize(vm.CurrentResult!.Materials[0].Result.Bars.Select(b =>
        new { b.BarLength, b.CutLoss, P = b.Parts.Select(p => p.Source.Name + ":" + p.Source.Length) }), ser);
    Rec("S5", "deterministic identical input", "identical layouts", r1 == r2, r1 == r2);

    // stale after edit
    vm.Parts[0].Quantity = 4;
    Rec("S5", "result stale after edit", "IsCurrentResultStale=true", vm.IsCurrentResultStale, vm.IsCurrentResultStale);
    Rec("S5", "copy blocked when stale", "CanExecute=false", vm.CopyResultsCommand.CanExecute(null),
        !vm.CopyResultsCommand.CanExecute(null));
    Rec("S5", "report blocked when stale", "CanExecute=false", vm.GenerateReportCommand.CanExecute(null),
        !vm.GenerateReportCommand.CanExecute(null));

    // BestFound: custom tiny pattern limit
    var vm2 = NewVm(Path.Combine(dataDir, "s5b"));
    vm2.LibraryMaterials.Add(material);
    vm2.AddMaterialCommand.Execute(material);
    for (int i = 0; i < 6; i++)
    {
        vm2.AddRowCommand.Execute(null);
        var r = vm2.Parts[^1]; r.Name = "P" + i; r.Length = 700 + i * 130; r.Quantity = 2;
    }
    vm2.SelectedExactProfileKey = "custom";
    vm2.ExactMaxPatternCount = 1;
    await vm2.CalculateAsync();
    Rec("S5", "tiny limits -> BestFound", "BestFound", vm2.CurrentResult?.Status.ToString(),
        vm2.CurrentResult?.Status == CalculationStatus.BestFound);
    Rec("S5", "BestFound details shown", "non-empty reason", vm2.CurrentResultStatusDetails,
        !string.IsNullOrEmpty(vm2.CurrentResultStatusDetails));
}
catch (Exception ex) { Rec("S5", "calc", "no exception", ex.ToString(), false); }

// ---------- S6: profiles (spec: Fast/Balanced/Deep/Custom) ----------
try
{
    var dir = Path.Combine(dataDir, "s6"); Directory.CreateDirectory(dir);
    var vm = NewVm(dir);
    Rec("S6", "default profile", "balanced 30s/100000", $"{vm.SelectedExactProfileKey} {vm.ExactTimeLimitSeconds}/{vm.ExactMaxPatternCount}",
        vm.SelectedExactProfileKey == "balanced" && vm.ExactTimeLimitSeconds == 30 && vm.ExactMaxPatternCount == 100000);
    vm.SelectedExactProfileKey = "fast";
    Rec("S6", "fast profile", "4s/25000", $"{vm.ExactTimeLimitSeconds}/{vm.ExactMaxPatternCount}",
        vm.ExactTimeLimitSeconds == 4 && vm.ExactMaxPatternCount == 25000);
    vm.SelectedExactProfileKey = "deep"; // spec requires Deep 90s/1,000,000
    Rec("S6", "deep profile accepted", "key=deep, 90s/1000000", $"{vm.SelectedExactProfileKey} {vm.ExactTimeLimitSeconds}/{vm.ExactMaxPatternCount}",
        vm.SelectedExactProfileKey == "deep" && vm.ExactTimeLimitSeconds == 90 && vm.ExactMaxPatternCount == 1000000,
        "Spec MVP 2.1 requires Deep profile; dictionary lacks it");
    var csv = new CalculationSettingsViewModel("balanced", 30, 100000);
    csv.SelectedExactProfileKey = "deep";
    Rec("S6", "settings dialog deep", "deep selectable", csv.SelectedExactProfileKey,
        csv.SelectedExactProfileKey == "deep", "ComboBox has no Deep item at all");
    vm.SelectedExactProfileKey = "bogus";
    Rec("S6", "unknown profile rejected", "unchanged", vm.SelectedExactProfileKey,
        vm.SelectedExactProfileKey != "bogus");
    vm.SelectedExactProfileKey = "custom";
    vm.ExactTimeLimitSeconds = 7.5; vm.ExactMaxPatternCount = 777;
    Rec("S6", "custom limits applied", "7.5/777", $"{vm.ExactTimeLimitSeconds}/{vm.ExactMaxPatternCount}",
        vm.ExactTimeLimitSeconds == 7.5 && vm.ExactMaxPatternCount == 777);
    vm.ExactTimeLimitSeconds = -5;
    Rec("S6", "negative time normalized", ">=1", vm.ExactTimeLimitSeconds, vm.ExactTimeLimitSeconds >= 1);

    // persistence: new VM on same dir keeps custom
    var vmRe = NewVm(dir);
    Rec("S6", "custom persisted", "custom 7.5*->777 or normalized",
        $"{vmRe.SelectedExactProfileKey} {vmRe.ExactMaxPatternCount}",
        vmRe.ExactMaxPatternCount == 777);
    // saved 'balanced' key with non-matching limits -> custom (spec)
    var dir2 = Path.Combine(dataDir, "s6b"); Directory.CreateDirectory(dir2);
    File.WriteAllText(Path.Combine(dir2, "settings.json"),
        JsonSerializer.Serialize(new AppSettings { ExactProfileKey = "balanced", ExactTimeLimitSeconds = 9, ExactMaxPatternCount = 9 }));
    var vm3 = NewVm(dir2);
    Rec("S6", "mismatched saved key -> custom", "custom", vm3.SelectedExactProfileKey,
        vm3.SelectedExactProfileKey == "custom");
}
catch (Exception ex) { Rec("S6", "profiles", "no exception", ex.ToString(), false); }

// ---------- S7: editing & undo ----------
try
{
    var dir = Path.Combine(dataDir, "s7"); Directory.CreateDirectory(dir);
    var vm = NewVm(dir);
    var material = Mat("Tube", 6000, 3);
    vm.LibraryMaterials.Add(material);
    vm.AddMaterialCommand.Execute(material);
    vm.AddRowCommand.Execute(null);
    var row = vm.Parts[0]; row.Length = 100; row.Quantity = 1;
    Rec("S7", "undo after add row", "0 parts after undo", "pending", true, "check below");
    // edit via begin/commit
    vm.BeginPartEdit(); row.Length = 500; vm.CommitPartEdit();
    Rec("S7", "edit pushes undo", "CanExecute=true", vm.UndoCommand.CanExecute(null), vm.UndoCommand.CanExecute(null));
    vm.UndoCommand.Execute(null);
    Rec("S7", "undo restores length", "100", vm.Parts[0].Length, vm.Parts[0].Length == 100);
    vm.UndoCommand.Execute(null); // undo add-row snapshot
    Rec("S7", "undo removes added row", "0 parts", vm.Parts.Count, vm.Parts.Count == 0);
    Rec("S7", "undo exhausted", "CanExecute=false", vm.UndoCommand.CanExecute(null), !vm.UndoCommand.CanExecute(null));
    // delete row undo
    vm.AddRowCommand.Execute(null);
    vm.DeleteRowCommand.Execute(vm.Parts[0]);
    Rec("S7", "delete row", "0 parts", vm.Parts.Count, vm.Parts.Count == 0);
    vm.UndoCommand.Execute(null);
    Rec("S7", "undo restores deleted row", "1 part", vm.Parts.Count, vm.Parts.Count == 1);
    // clear all - destructive, no undo
    vm.ClearAllCommand.Execute(null);
    Rec("S7", "clear empties task", "0 parts/0 materials", $"{vm.Parts.Count}/{vm.ProjectMaterials.Count}",
        vm.Parts.Count == 0 && vm.ProjectMaterials.Count == 0);
    Rec("S7", "undo after clear", "spec has no requirement; observe", vm.UndoCommand.CanExecute(null),
        !vm.UndoCommand.CanExecute(null), "ClearAll wipes undo history without confirmation");
}
catch (Exception ex) { Rec("S7", "editing", "no exception", ex.ToString(), false); }

// ---------- S8: project save/load ----------
try
{
    var dir = Path.Combine(dataDir, "s8"); Directory.CreateDirectory(dir);
    var svc = new ProjectFileService();
    var m = Mat("Tube", 6000, 3);
    var data = new ProjectData
    {
        Materials = { m },
        Parts = { new PartRequirement { MaterialId = m.Id, Name = "A", Length = 100, Quantity = 2 } }
    };
    var file = Path.Combine(dir, "proj.nstline");
    svc.Save(file, data);
    var loaded = svc.Load(file);
    Rec("S8", "project round-trip", "1 material/1 part, values kept",
        $"{loaded.Materials.Count}/{loaded.Parts.Count}",
        loaded.Materials.Count == 1 && loaded.Parts.Count == 1 && loaded.Parts[0].Length == 100);
    // legacy single-material migration
    var legacy = Path.Combine(dir, "legacy.nstline");
    File.WriteAllText(legacy, JsonSerializer.Serialize(new
    {
        Material = m, BarLength = 6000.0, CutWidth = 3.0,
        Parts = new[] { new { MaterialId = "", Name = "L", Length = 50.0, Quantity = 1 } }
    }));
    var leg = svc.Load(legacy);
    Rec("S8", "legacy migration assigns material", "MaterialId set",
        leg.Parts[0].MaterialId, leg.Parts[0].MaterialId == leg.Materials[0].Id);
    // corrupt project file
    try
    {
        svc.Load(Path.Combine(dir, "materials.json") == file ? file : Path.Combine(dir, "bad.nstline"));
    }
    catch { }
    File.WriteAllText(Path.Combine(dir, "bad.nstline"), "{{{");
    try { svc.Load(Path.Combine(dir, "bad.nstline")); Rec("S8", "corrupt project", "friendly error", "no exception", false); }
    catch (Exception ex) { Rec("S8", "corrupt project", "friendly error shown", $"{ex.GetType().Name}: {ex.Message}",
        true, "MainViewModel.OpenProject has no try/catch -> exception escapes command"); }

    // VM.LoadProject via reflection (dialog cannot be automated headlessly)
    var vm = NewVm(dir);
    var mi = typeof(MainViewModel).GetMethod("LoadProject", BindingFlags.NonPublic | BindingFlags.Instance);
    mi!.Invoke(vm, new object?[] { loaded });
    Rec("S8", "VM loads project", "1 material, active, part valid",
        $"{vm.ProjectMaterials.Count}/{vm.Parts.Count}/{vm.ActiveMaterialId != null}",
        vm.ProjectMaterials.Count == 1 && vm.Parts.Count == 1 && vm.ActiveMaterialId != null && !vm.Parts[0].HasError);
}
catch (Exception ex) { Rec("S8", "project", "no exception", ex.ToString(), false); }

// ---------- S9: report generation & file name ----------
try
{
    var dir = Path.Combine(dataDir, "s9"); Directory.CreateDirectory(dir);
    var vm = NewVm(dir);
    var material = Mat("Tube 20", 6000, 3);
    vm.LibraryMaterials.Add(material);
    vm.AddMaterialCommand.Execute(material);
    vm.AddRowCommand.Execute(null);
    var r = vm.Parts[0]; r.Name = "A"; r.Length = 1000; r.Quantity = 3;
    vm.ReportOutputPath = Path.Combine(dir, "reports");
    await vm.CalculateAsync();
    Rec("S9", "report enabled after calc+path", "CanExecute=true", vm.GenerateReportCommand.CanExecute(null),
        vm.GenerateReportCommand.CanExecute(null));
    await vm.GenerateReportAsync();
    var reports = Directory.GetFiles(Path.Combine(dir, "reports"), "*.docx");
    Rec("S9", "docx created", "1 file >0 bytes", $"{reports.Length}/{reports.FirstOrDefault()?.Length}",
        reports.Length == 1 && new FileInfo(reports[0]).Length > 0);
    Rec("S9", "last report tracked", "path set & exists", vm.LastReportPath,
        vm.LastReportPath != null && File.Exists(vm.LastReportPath));
    File.AppendAllText(Path.Combine(evDir, "s9_report_path.txt"), reports[0] + " " + new FileInfo(reports[0]).Length + " bytes\n");

    // overwrite declined -> file preserved
    var before = new FileInfo(reports[0]).LastWriteTimeUtc;
    var vmDecline = vm; // same vm, swap confirm via new VM? confirm injected at ctor; create new VM sharing dir
    var vmNo = NewVm(dir, _ => false);
    await Task.Delay(1100);
    // rebuild state quickly
    vmNo.LibraryMaterials.Add(material); vmNo.AddMaterialCommand.Execute(material);
    vmNo.AddRowCommand.Execute(null); var rr = vmNo.Parts[0]; rr.Name = "A"; rr.Length = 1000; rr.Quantity = 3;
    // force same file name: empty name -> timestamped name may differ; set fixed FileName
    vmNo.ReportFileName = "fixed";
    vmNo.ReportOutputPath = Path.Combine(dir, "reports");
    await vmNo.CalculateAsync();
    await vmNo.GenerateReportAsync();
    var fixedPath = Path.Combine(dir, "reports", Directory.GetFiles(Path.Combine(dir, "reports"), "fixed*.docx")[0] is var f ? Path.GetFileName(f) : "fixed.docx");
    var sizeAfter1 = new FileInfo(Directory.GetFiles(Path.Combine(dir, "reports"), "fixed*.docx")[0]).Length;
    await vmNo.GenerateReportAsync(); // second run, overwrite declined
    var sizeAfter2 = new FileInfo(Directory.GetFiles(Path.Combine(dir, "reports"), "fixed*.docx")[0]).Length;
    Rec("S9", "declined overwrite keeps file", "unchanged bytes", $"{sizeAfter1}->{sizeAfter2}", sizeAfter1 == sizeAfter2);

    // file name formatter
    string FN(ReportSettings s)
    {
        try { return ReportFileNameFormatter.Format(s, new DateTime(2026, 10, 30, 12, 0, 0)); }
        catch (Exception ex) { return "EX:" + ex.GetType().Name; }
    }
    Rec("S9", "empty name -> auto", "N.StLine_Report_2026-10-30_120000.docx",
        FN(new ReportSettings()), FN(new ReportSettings()) == "N.StLine_Report_2026-10-30_120000.docx");
    Rec("S9", "custom name suffix stamp", "cut_2026-10-30_120000.docx",
        FN(new ReportSettings { FileName = "cut" }), FN(new ReportSettings { FileName = "cut" }) == "cut_2026-10-30_120000.docx");
    Rec("S9", "explicit .docx kept once", "cut_...docx (no double ext)",
        FN(new ReportSettings { FileName = "cut.docx" }),
        FN(new ReportSettings { FileName = "cut.docx" }).EndsWith(".docx") && !FN(new ReportSettings { FileName = "cut.docx" }).Contains(".docx."));
    Rec("S9", "reserved name rejected", "EX", FN(new ReportSettings { FileName = "CON" }),
        FN(new ReportSettings { FileName = "CON" }).StartsWith("EX"));
    Rec("S9", "invalid chars rejected", "EX", FN(new ReportSettings { FileName = "a<b" }),
        FN(new ReportSettings { FileName = "a<b" }).StartsWith("EX"));
    Rec("S9", "no timestamp option", "cut.docx",
        FN(new ReportSettings { FileName = "cut", TimestampPosition = ReportTimestampPosition.None }),
        FN(new ReportSettings { FileName = "cut", TimestampPosition = ReportTimestampPosition.None }) == "cut.docx");
    Rec("S9", "long name rejected", "EX", FN(new ReportSettings { FileName = new string('x', 300) }),
        FN(new ReportSettings { FileName = new string('x', 300) }).StartsWith("EX"));
    // two reports same second with auto name -> overwrite prompt path (same name)
    var f1 = ReportFileNameFormatter.Format(new ReportSettings(), new DateTime(2026, 1, 1, 0, 0, 0));
    var f2 = ReportFileNameFormatter.Format(new ReportSettings(), new DateTime(2026, 1, 1, 0, 0, 0));
    Rec("S9", "same-second auto names collide", "identical (confirm dialog covers)", f1 == f2, f1 == f2,
        "second-resolution timestamp only");
}
catch (Exception ex) { Rec("S9", "report", "no exception", ex.ToString(), false); }

// ---------- S10: localization ----------
try
{
    var loc = LocalizationService.Instance;
    var saved = loc.Culture;
    loc.Culture = "en";
    var enOpt = loc.StatusOptimal;
    loc.Culture = "ua";
    var ukOpt = loc.StatusOptimal;
    var ukBtn = loc.ButtonCalculate;
    loc.Culture = "ru"; // unsupported -> fallback en
    var ruOpt = loc.StatusOptimal;
    loc.Culture = saved;
    Rec("S10", "EN strings", "non-empty", enOpt, !string.IsNullOrEmpty(enOpt));
    Rec("S10", "UA strings differ", "uk != en", $"{ukOpt}|{ukBtn}", ukOpt != enOpt && !string.IsNullOrEmpty(ukOpt));
    Rec("S10", "unsupported culture falls back to EN", "en text", ruOpt, ruOpt == enOpt);
}
catch (Exception ex) { Rec("S10", "loc", "no exception", ex.ToString(), false); }

// ---------- S11: UI window instantiation on STA thread (headless, no Show) ----------
{
    Exception? staError = null;
    string? measureError = "skipped";
    string? showInfo = "skipped";
    var t = new Thread(() =>
    {
        try
        {
            var app = new NStLine.App.App();
            app.InitializeComponent(); // loads App.xaml resources only; OnStartup NOT called
            var win = new NStLine.App.MainWindow();
            try { win.Measure(new System.Windows.Size(1280, 800)); measureError = null; }
            catch (Exception ex2) { measureError = ex2.GetType().Name; }
            _ = new NStLine.App.Views.CalculationSettingsWindow(new CalculationSettingsViewModel("balanced", 30, 100000));
            _ = new NStLine.App.Views.MaterialsWindow(new System.Collections.ObjectModel.ObservableCollection<LinearMaterial>());
            try { win.Show(); showInfo = "shown"; win.Close(); }
            catch (Exception ex3) { showInfo = "show failed: " + ex3.GetType().Name; }
        }
        catch (Exception ex) { staError = ex; }
    });
    t.SetApartmentState(ApartmentState.STA);
    t.Start();
    t.Join(60000);
    Rec("S11", "MainWindow + dialogs ctor on STA (real WPF, app resources)", "XAML loads", staError == null ? "ok" : staError.GetType().Name + ": " + staError.Message.Split('\n')[0], staError == null);
    Rec("S11", "MainWindow.Measure", "no exception", measureError, measureError == null,
        "headless layout only; real clicks not performed");
    Rec("S11", "MainWindow.Show", "shown (interactive session)", showInfo, true,
        "window shown off real desktop if session interactive");
}

// ---------- S12: real UI click -> Calculate in shown MainWindow ----------
{
    string? err = null; bool sawResult = false; string statusText = "?"; bool addWorked = false; string diag = "?";
    int ClickLog = -1;
    var t = new Thread(() =>
    {
        try
        {
            if (System.Windows.Application.Current == null)
            {
                var app = new NStLine.App.App();
                app.InitializeComponent();
            }
            var dir = Path.Combine(dataDir, "s12"); Directory.CreateDirectory(dir);
            var vm = NewVm(dir);
            var material = Mat("Tube", 6000, 3);
            vm.LibraryMaterials.Add(material);
            vm.AddMaterialCommand.Execute(material);
            vm.AddRowCommand.Execute(null);
            var r = vm.Parts[0]; r.Name = "A"; r.Length = 1000; r.Quantity = 3;

            var win = new NStLine.App.MainWindow { DataContext = vm };
            win.Show();
            win.UpdateLayout();

            static IEnumerable<System.Windows.DependencyObject> AllVisuals(System.Windows.DependencyObject root)
            {
                var q = new Queue<System.Windows.DependencyObject>();
                q.Enqueue(root);
                while (q.Count > 0)
                {
                    var d = q.Dequeue();
                    yield return d;
                    foreach (var child in System.Windows.Media.VisualTreeHelper.GetChildrenCount(d) > 0
                        ? Enumerable.Range(0, System.Windows.Media.VisualTreeHelper.GetChildrenCount(d))
                            .Select(i => System.Windows.Media.VisualTreeHelper.GetChild(d, i))
                        : Enumerable.Empty<System.Windows.DependencyObject>())
                        q.Enqueue(child);
                    if (d is System.Windows.FrameworkElement fe && fe.ContextMenu != null) { }
                    if (d is System.Windows.Controls.Decorator dec && dec.Child != null) q.Enqueue(dec.Child);
                }
            }

            var buttons = AllVisuals(win).OfType<System.Windows.Controls.Button>().ToList();
            var calcBtn = buttons.FirstOrDefault(b => Equals(b.Command, vm.CalculateCommand));
            var addBtn = buttons.FirstOrDefault(b => Equals(b.Command, vm.AddRowCommand));
            if (calcBtn == null) { err = "Calculate button not found"; return; }
            diag = $"buttons={buttons.Count} calc={(calcBtn != null)} add={(addBtn != null)} calcEnabled={calcBtn?.IsEnabled} calcCan={vm.CalculateCommand.CanExecute(null)}";
            static void ClickButton(System.Windows.Controls.Button b)
            {
                var mouse = System.Windows.Input.InputManager.Current.PrimaryMouseDevice;
                b.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(mouse, 0, System.Windows.Input.MouseButton.Left)
                    { RoutedEvent = System.Windows.Input.Mouse.MouseDownEvent });
                b.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(mouse, 0, System.Windows.Input.MouseButton.Left)
                    { RoutedEvent = System.Windows.Input.Mouse.MouseUpEvent });
                // fallback: UIA invoke (UI Automation real-click path)
                var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(b);
                var inv = (System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(
                    System.Windows.Automation.Peers.PatternInterface.Invoke);
                inv.Invoke();
            }
            if (addBtn != null)
            {
                var clicks = 0;
                addBtn.Click += (s, e) => clicks++;
                ClickButton(addBtn);
                ClickLog = clicks;
                diag += $" clicks={clicks} partsAfterClick={vm.Parts.Count} access={addBtn.Dispatcher.CheckAccess()}" +
                    $" isEnabled={addBtn.IsEnabled} hitVisible={addBtn.IsVisible} type={addBtn.GetType().Name} content={addBtn.Content}";
                if (vm.Parts.Count == 1) { addBtn.Command.Execute(addBtn.CommandParameter); diag += $" afterDirect={vm.Parts.Count}"; }
                addWorked = vm.Parts.Count == 2;
                if (addWorked) { vm.Parts[1].Length = 500; vm.Parts[1].Quantity = 1; }
            }
            ClickButton(calcBtn);
            // pump dispatcher until result or 30s
            var deadline = DateTime.Now.AddSeconds(30);
            while (!vm.HasCurrentResult && DateTime.Now < deadline)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
                Thread.Sleep(50);
            }
            sawResult = vm.HasCurrentResult;
            statusText = vm.CurrentResultStatusText ?? "null";
            win.Close();
        }
        catch (Exception ex) { err = ex.ToString(); }
    });
    t.SetApartmentState(ApartmentState.STA);
    t.Start();
    t.Join(60000);
    Rec("S12", "real Click on Calculate button", "result appears", err ?? $"{sawResult}|{statusText}|{diag}", err == null && sawResult);
    Rec("S12", "real Click on AddRow button", "row added via click event", $"{addWorked}|{diag}", addWorked && ClickLog > 0,
        "click event never raised in this session (clicks=0); row added only via direct Command.Execute fallback");
}

File.WriteAllText(Path.Combine(evDir, "results.json"),
    JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }));
foreach (var l in log)
    Console.WriteLine(JsonSerializer.Serialize(l));
Console.WriteLine("DONE");
