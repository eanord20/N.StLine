// QA Agent2 harness — factual verification of N.StLine 1.1.0 candidate.
// References release DLLs directly (no ProjectReference). Own bin/obj only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

var agentDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var dataDir = Path.Combine(agentDir, "data");
var evidenceDir = Path.Combine(agentDir, "evidence");
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(evidenceDir);

var records = new List<Dictionary<string, object?>>();
void Rec(string id, string name, string status, string expected, string actual, string details = "")
    => records.Add(new Dictionary<string, object?>
    {
        ["id"] = id, ["name"] = name, ["status"] = status,
        ["expected"] = expected, ["actual"] = actual, ["details"] = details
    });

// ---------- Validators ----------
static List<string> CheckResult(CuttingTask task, CuttingResult res)
{
    var errs = new List<string>();
    var demand = task.Parts.Where(p => p != null).ToDictionary(p => p.Id, p => p.Quantity);
    var placed = new Dictionary<string, int>();
    foreach (var bar in res.Bars)
    {
        double sum = bar.Parts.Sum(p => p.Source.Length);
        int cnt = bar.Parts.Count;
        double loss = cnt * task.CutWidth;
        if (Math.Abs(bar.CutLoss - loss) > 0.01) errs.Add($"bar {bar.Index}: CutLoss {bar.CutLoss} != {loss}");
        if (bar.CutCount != cnt) errs.Add($"bar {bar.Index}: CutCount {bar.CutCount} != parts {cnt}");
        double rem = task.BarLength - sum - loss;
        if (rem < -0.02) errs.Add($"bar {bar.Index}: infeasible remainder {rem}");
        if (Math.Abs(bar.Remainder - Math.Max(0, rem)) > 0.02) errs.Add($"bar {bar.Index}: Remainder {bar.Remainder} != {rem}");
        if (bar.BarLength != task.BarLength) errs.Add($"bar {bar.Index}: BarLength {bar.BarLength} != {task.BarLength}");
        foreach (var p in bar.Parts)
        {
            placed.TryGetValue(p.Source.Id, out var c);
            placed[p.Source.Id] = c + 1;
        }
    }
    foreach (var kv in demand)
    {
        placed.TryGetValue(kv.Key, out var got);
        if (got != kv.Value) errs.Add($"part {kv.Key}: demand {kv.Value} placed {got}");
    }
    double expRem = res.Bars.Sum(b => b.Remainder);
    if (Math.Abs(res.TotalRemainder - expRem) > 0.05) errs.Add($"TotalRemainder {res.TotalRemainder} != sum {expRem}");
    double expLoss = res.Bars.Sum(b => b.CutLoss);
    if (Math.Abs(res.TotalCutLoss - expLoss) > 0.05) errs.Add($"TotalCutLoss mismatch");
    if (Math.Abs(res.TotalPartLength - res.Bars.Sum(b => b.PartLength)) > 0.05) errs.Add("TotalPartLength mismatch");
    return errs;
}

static CuttingTask MkTask(double bar, double k, params (double len, int qty, string name)[] parts)
{
    var mat = new LinearMaterial { Id = "m", Name = "M", StandardLength = bar, CutWidth = k };
    return new CuttingTask
    {
        Material = mat,
        BarLength = bar,
        CutWidth = k,
        Parts = parts.Select(p => new PartRequirement
        { MaterialId = "m", Name = p.name, Length = p.len, Quantity = p.qty }).ToList()
    };
}

static CuttingOptions Opt(double time = 30, int maxPat = 100_000)
    => new() { TimeLimitSeconds = time, MaxPatternCount = maxPat, ToleranceMm = 0.01 };

var engine = new CuttingEngine();

void RunCase(string id, string name, CuttingTask task, CuttingOptions opt,
    bool useOracle, string note = "")
{
    try
    {
        var res = engine.Solve(task, opt);
        var errs = CheckResult(task, res);
        var lens = task.Parts.Select(p => p.Length).ToArray();
        var qtys = task.Parts.Select(p => p.Quantity).ToArray();
        string oracleInfo = "";
        bool oracleOk = true;
        if (useOracle)
        {
            var or = new Oracle(task.BarLength, task.CutWidth, lens, qtys);
            int minBars = or.MinBars();
            oracleInfo = $"oracleBars={minBars} patterns={or.PatternCount}";
            if (res.TotalBarCount != minBars)
            {
                oracleOk = false;
                errs.Add($"OPTIMALITY: engine {res.TotalBarCount} bars vs oracle {minBars}");
            }
            double expTotalRem = res.TotalBarCount * task.BarLength
                - task.Parts.Sum(p => p.Length * p.Quantity)
                - task.Parts.Sum(p => p.Quantity) * task.CutWidth;
            if (Math.Abs(res.TotalRemainder - expTotalRem) > 0.05)
                errs.Add($"TotalRemainder {res.TotalRemainder} != deterministic {expTotalRem}");
            if (res.Status == CalculationStatus.BestFound && oracleOk)
                oracleInfo += " [status=BestFound despite exact small task]";
        }
        var status = errs.Count == 0 ? "PASS" : "FAIL";
        Rec(id, name, status,
            $"valid plan; oracle={(useOracle ? "checked" : "n/a")}; status={res.Status}",
            $"bars={res.TotalBarCount} rem={res.TotalRemainder:0.##} loss={res.TotalCutLoss:0.##} util={res.UtilizationPercent:0.##}% status={res.Status} reason={res.BestFoundReason} method={res.Method} t={res.ElapsedSeconds:0.###}s pats={res.GeneratedPatterns} {oracleInfo} {note}",
            string.Join("; ", errs));
    }
    catch (Exception ex)
    {
        Rec(id, name, "FAIL", "valid plan", $"EXCEPTION {ex.GetType().Name}: {ex.Message}");
    }
}

// ================= ENGINE CASES =================
// Reference spec cases
RunCase("T01", "spec ref1: 1000x3 / 6000 / k3", MkTask(6000, 3, (1000, 3, "A")), Opt(), true);
RunCase("T02", "spec ref2: 1200x5 / 6000 / k3 -> 2 bars", MkTask(6000, 3, (1200, 5, "A")), Opt(), true);
RunCase("T03", "spec ref3: 2400x2+1800x2+1200 / 6000 / k3", MkTask(6000, 3, (2400, 2, "A"), (1800, 2, "B"), (1200, 1, "C")), Opt(), true);
RunCase("T04", "spec ref4: 600x4+1000x3 / 1500 / k0 -> 5", MkTask(1500, 0, (600, 4, "A"), (1000, 3, "B")), Opt(), true);
RunCase("T05", "spec ref5: 3000x2 / 6000 / k10 -> 2", MkTask(6000, 10, (3000, 2, "A")), Opt(), true);
RunCase("T06", "spec ref7: 5997 / 6000 / k3 -> rem 0", MkTask(6000, 3, (5997, 1, "A")), Opt(), true);
RunCase("T07", "decimal mm: 1250.5x2+749x2 / 3000 / k2.5", MkTask(3000, 2.5, (1250.5, 2, "A"), (749, 2, "B")), Opt(), true);
RunCase("T08", "spec ref9: 1250x10+850x20+2400x6 / 6000 / k3", MkTask(6000, 3, (1250, 10, "A"), (850, 20, "B"), (2400, 6, "C")), Opt(), true);
RunCase("T09", "dup types same length: 500x2 + 500x3 / 3000 / k0", MkTask(3000, 0, (500, 2, "A"), (500, 3, "B")), Opt(), true);
RunCase("T10", "hetero mixed: 8 types / 12000 / k4", MkTask(12000, 4,
    (4800, 2, "A"), (3600, 3, "B"), (2500, 4, "C"), (1900, 5, "D"),
    (1200, 6, "E"), (800, 4, "F"), (650, 3, "G"), (450, 5, "H")), Opt(), false);

// Random small vs oracle
var rng = new Random(20261030);
for (int t = 0; t < 20; t++)
{
    int types = 3 + rng.Next(3);
    var parts = new List<(double, int, string)>();
    for (int i = 0; i < types; i++)
    {
        double len = Math.Round((300 + rng.NextDouble() * 2700) * 2) / 2; // 0.5 mm step
        int q = 1 + rng.Next(6);
        parts.Add((len, q, $"P{i}"));
    }
    double k = new[] { 0.0, 2.0, 3.0, 5.0 }[rng.Next(4)];
    RunCase($"R{t + 1:D2}", $"random#{t + 1} bar6000 k{k}", MkTask(6000, k, parts.ToArray()), Opt(), true,
        "input=" + string.Join(";", parts.Select(p => $"{p.Item1}x{p.Item2}")));
}

// Forced limits -> honest status
RunCase("L01", "pattern limit forced (maxPat=5)", MkTask(6000, 3, (1250, 10, "A"), (850, 20, "B"), (2400, 6, "C")),
    Opt(30, 5), false, "expect BestFound/PatternLimitReached");
RunCase("L02", "time limit forced (0.1ms)", MkTask(6000, 3, (1250, 10, "A"), (850, 20, "B"), (2400, 6, "C")),
    Opt(0.0001, 100_000), false, "expect BestFound/TimeLimitReached");
RunCase("L03", "heavy task deep-ish limits", MkTask(12000, 5,
    (3050, 15, "A"), (2500, 20, "B"), (1800, 25, "C"), (1450, 30, "D"),
    (1100, 20, "E"), (700, 30, "F")), Opt(15, 200_000), false);

// Determinism
try
{
    var t = MkTask(6000, 3, (1250, 10, "A"), (850, 20, "B"), (2400, 6, "C"));
    var r1 = engine.Solve(t, Opt());
    var r2 = engine.Solve(t, Opt());
    string Sig(CuttingResult r) => string.Join("|", r.Bars.Select(b =>
        $"{string.Join(",", b.Parts.Select(p => p.Source.Id))}:{b.Remainder:0.####}"));
    var eq = Sig(r1) == Sig(r2) && r1.Status == r2.Status;
    Rec("D01", "determinism: identical input x2", eq ? "PASS" : "FAIL",
        "identical bar layouts", $"eq={eq} bars={r1.TotalBarCount}/{r2.TotalBarCount}");
}
catch (Exception ex) { Rec("D01", "determinism", "FAIL", "no exception", ex.Message); }

// Validation
void ExpectThrow(string id, string name, Func<object> f, string why)
{
    try { f(); Rec(id, name, "FAIL", "ArgumentException: " + why, "no exception"); }
    catch (ArgumentException ex) { Rec(id, name, "PASS", "ArgumentException: " + why, ex.Message); }
    catch (Exception ex) { Rec(id, name, "FAIL", "ArgumentException: " + why, $"{ex.GetType().Name}: {ex.Message}"); }
}
ExpectThrow("V01", "part longer than bar (7000>6000)", () => engine.Solve(MkTask(6000, 3, (7000, 1, "X")), Opt()), "too long");
ExpectThrow("V02", "part==bar with k>0 (6000,k3)", () => engine.Solve(MkTask(6000, 3, (6000, 1, "X")), Opt()), "L+K>L");
ExpectThrow("V03", "qty=0", () => engine.Solve(MkTask(6000, 3, (1000, 0, "X")), Opt()), "qty>0");
ExpectThrow("V04", "negative length", () => engine.Solve(MkTask(6000, 3, (-50, 1, "X")), Opt()), "len>0");
ExpectThrow("V05", "cutWidth>=bar", () => engine.Solve(MkTask(6000, 6000, (100, 1, "X")), Opt()), "K<L");
ExpectThrow("V06", "empty parts", () => engine.Solve(MkTask(6000, 3), Opt()), ">=1 part");
ExpectThrow("V07", "NaN bar length", () => engine.Solve(MkTask(double.NaN, 3, (100, 1, "X")), Opt()), "finite");
RunCase("V08", "part==bar with k0 -> 1 bar rem 0", MkTask(6000, 0, (6000, 1, "X")), Opt(), true);

// Multi-material project
try
{
    var m1 = new LinearMaterial { Id = "m1", Name = "Tube 6000", StandardLength = 6000, CutWidth = 3 };
    var m2 = new LinearMaterial { Id = "m2", Name = "Bar 12000", StandardLength = 12000, CutWidth = 5 };
    var m3 = new LinearMaterial { Id = "m3", Name = "Unused", StandardLength = 3000, CutWidth = 1 };
    var ptask = new ProjectCuttingTask
    {
        Materials = new List<LinearMaterial> { m1, m2, m3 },
        Parts = new List<PartRequirement>
        {
            new() { MaterialId = "m1", Name = "p1", Length = 2400, Quantity = 2 },
            new() { MaterialId = "m1", Name = "p2", Length = 1800, Quantity = 2 },
            new() { MaterialId = "m1", Name = "p3", Length = 1200, Quantity = 1 },
            new() { MaterialId = "m2", Name = "q1", Length = 5500, Quantity = 2 },
            new() { MaterialId = "m2", Name = "q2", Length = 3000, Quantity = 3 },
        }
    };
    var pe = new ProjectCuttingEngine(new CuttingEngine());
    var pres = pe.Solve(ptask, Opt());
    var errs = new List<string>();
    if (pres.Materials.Count != 2) errs.Add($"expected 2 material sections (empty excluded), got {pres.Materials.Count}");
    foreach (var mr in pres.Materials)
        errs.AddRange(CheckResult(mr.Task, mr.Result).Select(e => mr.Material.Id + ": " + e));
    var m1res = pres.Materials.FirstOrDefault(x => x.Material.Id == "m1");
    var m2res = pres.Materials.FirstOrDefault(x => x.Material.Id == "m2");
    if (m1res != null && m1res.Result.Bars.Any(b => b.BarLength != 6000)) errs.Add("m1 wrong bar length");
    if (m2res != null && m2res.Result.Bars.Any(b => b.BarLength != 12000)) errs.Add("m2 wrong bar length");
    // oracle per material
    var o1 = new Oracle(6000, 3, new[] { 2400.0, 1800, 1200 }, new[] { 2, 2, 1 });
    var o2 = new Oracle(12000, 5, new[] { 5500.0, 3000 }, new[] { 2, 3 });
    if (m1res != null && m1res.Result.TotalBarCount != o1.MinBars()) errs.Add($"m1 bars {m1res.Result.TotalBarCount} != oracle {o1.MinBars()}");
    if (m2res != null && m2res.Result.TotalBarCount != o2.MinBars()) errs.Add($"m2 bars {m2res.Result.TotalBarCount} != oracle {o2.MinBars()}");
    Rec("M01", "project: 2 materials diff lengths + empty material", errs.Count == 0 ? "PASS" : "FAIL",
        "m1=2 bars (6000,k3), m2=oracle bars (12000,k5), m3 skipped",
        $"m1 bars={m1res?.Result.TotalBarCount} status={m1res?.Result.Status}; m2 bars={m2res?.Result.TotalBarCount} status={m2res?.Result.Status}; projectStatus={pres.Status} oracle m1={o1.MinBars()} m2={o2.MinBars()}",
        string.Join("; ", errs));
}
catch (Exception ex) { Rec("M01", "multi-material", "FAIL", "valid result", ex.Message); }

ExpectThrow("M02", "project: unknown MaterialId", () =>
    new ProjectCuttingEngine(new CuttingEngine()).Solve(new ProjectCuttingTask
    {
        Materials = new List<LinearMaterial> { new() { Id = "m1", Name = "A", StandardLength = 6000, CutWidth = 3 } },
        Parts = new List<PartRequirement> { new() { MaterialId = "nope", Length = 100, Quantity = 1 } }
    }, Opt()), "unknown material");

ExpectThrow("M03", "project: zero materials", () =>
    new ProjectCuttingEngine(new CuttingEngine()).Solve(new ProjectCuttingTask
    { Materials = new List<LinearMaterial>(), Parts = new List<PartRequirement>() }, Opt()), ">=1 material");

// Materials library service (temp paths)
try
{
    var libPath = Path.Combine(dataDir, "materials.json");
    var svc = new MaterialsLibraryService(libPath);
    svc.SaveAll(new[] { new LinearMaterial { Id = "a", Name = "T", StandardLength = 6000, CutWidth = 3 } });
    var loaded = svc.LoadAll();
    var ok = loaded.Count == 1 && loaded[0].StandardLength == 6000;
    Rec("S01", "materials library save/load roundtrip (temp path)", ok ? "PASS" : "FAIL", "1 material", $"{loaded.Count}");
    var badPath = Path.Combine(dataDir, "bad.json");
    File.WriteAllText(badPath, "[{\"Name\":\"\",\"StandardLength\":-5,\"CutWidth\":-1}]");
    try { MaterialsLibraryService.LoadForImport(badPath); Rec("S02", "import invalid file rejected", "FAIL", "InvalidDataException", "accepted"); }
    catch (InvalidDataException) { Rec("S02", "import invalid file rejected", "PASS", "InvalidDataException", "thrown"); }
    var dup = new MaterialsViewModel(new[] { new LinearMaterial { Id = "a", Name = "T", StandardLength = 6000, CutWidth = 3 } });
    var res = dup.ImportMaterials(new[]
    {
        new LinearMaterial { Id = "a", Name = "T", StandardLength = 6000, CutWidth = 3 },   // dup -> skip
        new LinearMaterial { Id = "a", Name = "U", StandardLength = 3000, CutWidth = 1 },   // id conflict -> new id
        new LinearMaterial { Id = "b", Name = "t ", StandardLength = 6000, CutWidth = 3 },  // norm name dup -> skip
    });
    var added = dup.Materials.FirstOrDefault(m => m.Name == "U");
    var okImp = res.Added == 1 && res.Skipped == 2 && added != null && added.Id != "a" && dup.Materials.Count == 2;
    Rec("S03", "import dedupe/id-conflict", okImp ? "PASS" : "FAIL", "added=1 skipped=2 new id",
        $"added={res.Added} skipped={res.Skipped} count={dup.Materials.Count} newId={added?.Id}");
}
catch (Exception ex) { Rec("S01", "materials services", "FAIL", "works", ex.Message); }

// Paste parser
try
{
    var sb = new StringBuilder();
    bool a = PartPasteParser.TryParse(new[] { "Det1", "1250", "10" }, out var n1, out var l1, out var q1);
    sb.Append($"3col:{a}/{n1}/{l1}/{q1} ");
    bool b = PartPasteParser.TryParse(new[] { "1250", "10" }, out _, out var l2, out var q2);
    sb.Append($"2col-num:{b}/{l2}/{q2} ");
    bool c = PartPasteParser.TryParse(new[] { "Det2", "850.5" }, out var n3, out var l3, out var q3);
    sb.Append($"2col-name:{c}/{n3}/{l3}/{q3} ");
    bool d = PartPasteParser.TryParse(new[] { "1000" }, out _, out var l4, out _);
    sb.Append($"1col:{d}/{l4} ");
    bool e = PartPasteParser.TryParse(new[] { "x", "y", "z" }, out _, out _, out _);
    sb.Append($"garbage:{e} ");
    bool f = PartPasteParser.TryParse(new[] { "D", "100", "0" }, out _, out _, out var q5);
    sb.Append($"qty0:{f}/{q5} ");
    bool g = PartPasteParser.TryParse(new[] { "D", "100", "-3" }, out _, out _, out var q6);
    sb.Append($"qtyNeg:{g}/{q6} ");
    bool pass = a && l1 == 1250 && q1 == 10 && b && l2 == 1250 && q2 == 10 && c && l3 == 850.5 && d && l4 == 1000 && !e;
    Rec("P01", "clipboard paste parser", pass ? "PASS" : "FAIL", "all parse combos correct", sb.ToString(),
        $"NOTE qty0->{q5} qtyNeg->{q6} (silent clamp to 1)");
}
catch (Exception ex) { Rec("P01", "paste parser", "FAIL", "works", ex.Message); }

// ================= VM / WPF (STA) =================
var vmDone = new ManualResetEventSlim();
var vmThread = new Thread(() =>
{
    try
    {
        var settingsPath = Path.Combine(dataDir, "settings.json");
        var libPath = Path.Combine(dataDir, "materials.json");
        var vm = new MainViewModel(new ProjectCuttingEngine(new CuttingEngine()),
            new MaterialsLibraryService(libPath), null,
            new AppSettingsService(settingsPath), new ReportService(),
            _ => true);
        var mat = new LinearMaterial { Id = "m1", Name = "Tube", StandardLength = 6000, CutWidth = 3 };
        vm.AddMaterialCommand.Execute(mat);
        Rec("U01", "VM: add project material sets active", vm.ActiveMaterialId == "m1" ? "PASS" : "FAIL",
            "ActiveMaterialId=m1", $"active={vm.ActiveMaterialId}");
        vm.AddRowCommand.Execute(null);
        var row = vm.Parts[0];
        row.Length = 2400; row.Quantity = 2;
        vm.AddRowCommand.Execute(null);
        var row2 = vm.Parts[1];
        row2.Length = 1800; row2.Quantity = 2;
        vm.AddRowCommand.Execute(null);
        var row3 = vm.Parts[2];
        row3.Length = 1200; row3.Quantity = 1;

        vm.CalculateAsync().GetAwaiter().GetResult();
        var r = vm.CurrentResult;
        Rec("U02", "VM: calculate produces result", r != null && r.TotalBarCount == 2 ? "PASS" : "FAIL",
            "result, 2 bars", $"bars={r?.TotalBarCount} status={r?.Status} stale={vm.IsCurrentResultStale}");

        var canCopy1 = vm.CopyResultsCommand.CanExecute(null);
        row3.Quantity = 5; // edit input -> stale
        var stale = vm.IsCurrentResultStale;
        var canCopy2 = vm.CopyResultsCommand.CanExecute(null);
        Rec("U03", "VM: edit marks stale, blocks copy", stale && !canCopy2 && canCopy1 ? "PASS" : "FAIL",
            "stale=true, copy disabled", $"stale={stale} copyBefore={canCopy1} copyAfter={canCopy2}");

        vm.CalculateAsync().GetAwaiter().GetResult();
        var fresh = !vm.IsCurrentResultStale && vm.CopyResultsCommand.CanExecute(null);
        Rec("U04", "VM: recalc clears stale", fresh ? "PASS" : "FAIL",
            "stale=false, copy enabled", $"stale={vm.IsCurrentResultStale} bars={vm.CurrentResult?.TotalBarCount}");

        // invalid row blocks calculate
        var rowBad = vm.Parts[0];
        rowBad.Length = 7000;
        var canCalc = vm.CalculateCommand.CanExecute(null);
        Rec("U05", "VM: too-long part blocks calculate", !canCalc && rowBad.HasError ? "PASS" : "FAIL",
            "CanCalculate=false, row error", $"canCalc={canCalc} err={rowBad.Error}");
        rowBad.Length = 2400;
        vm.ReportOutputPath = Path.Combine(dataDir, "reports");
        // stale result must block report generation (spec 8.3)
        var canReportStale = vm.GenerateReportCommand.CanExecute(null);
        vm.CalculateAsync().GetAwaiter().GetResult();
        Rec("U05b", "VM: stale blocks report cmd, recalc restores",
            !canReportStale && vm.GenerateReportCommand.CanExecute(null) ? "PASS" : "FAIL",
            "report cmd disabled while stale, enabled after recalc",
            $"canReportStale={canReportStale} canReportNow={vm.GenerateReportCommand.CanExecute(null)} stale={vm.IsCurrentResultStale}");

        // report
        vm.GenerateReportAsync().GetAwaiter().GetResult();
        var lp = vm.LastReportPath;
        var fileOk = lp != null && File.Exists(lp) && new FileInfo(lp).Length > 1000;
        var zipOk = false;
        if (fileOk)
        {
            try
            {
                using var z = ZipFile.OpenRead(lp!);
                var entry = z.GetEntry("word/document.xml");
                zipOk = entry != null;
                if (entry != null)
                {
                    using var sr = new StreamReader(entry.Open());
                    File.WriteAllText(Path.Combine(evidenceDir, "docx_document.xml.txt"), sr.ReadToEnd());
                }
            }
            catch { zipOk = false; }
        }
        Rec("U06", "VM: DOCX report generated in QA folder", fileOk && zipOk ? "PASS" : "FAIL",
            "valid docx file", $"path={lp} exists={fileOk} zip={zipOk}");

        // profile limits: no 'deep' profile
        vm.SelectedExactProfileKey = "deep";
        var deepAccepted = vm.SelectedExactProfileKey == "deep";
        Rec("U07", "VM: 'deep' profile (spec) selectable", deepAccepted ? "PASS" : "FAIL",
            "profile key 'deep' accepted (90s/1M per spec)",
            $"key={vm.SelectedExactProfileKey} t={vm.ExactTimeLimitSeconds} pat={vm.ExactMaxPatternCount}",
            "spec MVP 2.1 lists Fast/Balanced/Deep/Custom");

        // settings persisted to QA path only
        var persisted = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "";
        Rec("U08", "VM: settings saved to injected path", persisted.Contains("ExactProfileKey") ? "PASS" : "FAIL",
            "settings.json with exact limits", persisted.Length > 0 ? "saved" : "missing");
    }
    catch (Exception ex)
    {
        Rec("U00", "VM harness", "FAIL", "VM usable headless", $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
    finally { vmDone.Set(); }
});
vmThread.SetApartmentState(ApartmentState.STA);
vmThread.IsBackground = true;
vmThread.Start();
if (!vmDone.Wait(TimeSpan.FromMinutes(3)))
    Rec("U00", "VM harness", "FAIL", "complete within 3 min", "timeout");

// ---------- write evidence ----------
var jsonPath = Path.Combine(evidenceDir, "results.json");
File.WriteAllText(jsonPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
var txtPath = Path.Combine(evidenceDir, "results.txt");
File.WriteAllText(txtPath, string.Join(Environment.NewLine, records.Select(r =>
    $"[{r["status"]}] {r["id"]} {r["name"]}\n  expected: {r["expected"]}\n  actual:   {r["actual"]}\n  details:  {r["details"]}")));
Console.WriteLine($"done: {records.Count(r => (string)r["status"]! == "PASS")} PASS / {records.Count(r => (string)r["status"]! == "FAIL")} FAIL of {records.Count}");
Console.WriteLine(txtPath);

// ---------- Independent oracle (exact, integer arithmetic) ----------
sealed class Oracle
{
    readonly long L, K; readonly long[] len; readonly int[] qty; readonly int n;
    readonly List<int[]> patterns = new();
    readonly Dictionary<string, long> memo = new();

    public Oracle(double barLength, double cutWidth, double[] lengths, int[] quantities, int scale = 100)
    {
        L = (long)Math.Round(barLength * scale);
        K = (long)Math.Round(cutWidth * scale);
        len = lengths.Select(x => (long)Math.Round(x * scale)).ToArray();
        qty = (int[])quantities.Clone();
        n = len.Length;
        Gen(0, 0, new int[n]);
    }

    void Gen(int i, long used, int[] counts)
    {
        if (i == n)
        {
            if (counts.Sum() > 0) patterns.Add((int[])counts.Clone());
            return;
        }
        int max = (int)Math.Min(qty[i], (L - used) / (len[i] + K));
        for (int c = 0; c <= max; c++)
        {
            counts[i] = c;
            Gen(i + 1, used + c * (len[i] + K), counts);
        }
        counts[i] = 0;
    }

    public int PatternCount => patterns.Count;

    public int MinBars()
    {
        var rem = (int[])qty.Clone();
        long r = Dfs(rem);
        return r >= int.MaxValue ? -1 : (int)r;
    }

    long Dfs(int[] rem)
    {
        if (rem.All(x => x == 0)) return 0;
        var key = string.Join(",", rem);
        if (memo.TryGetValue(key, out var v)) return v;
        long best = long.MaxValue / 4;
        foreach (var p in patterns)
        {
            bool fits = true;
            for (int i = 0; i < n; i++) if (p[i] > rem[i]) { fits = false; break; }
            if (!fits) continue;
            for (int i = 0; i < n; i++) rem[i] -= p[i];
            var sub = Dfs(rem);
            for (int i = 0; i < n; i++) rem[i] += p[i];
            if (sub + 1 < best) best = sub + 1;
        }
        memo[key] = best;
        return best;
    }
}
