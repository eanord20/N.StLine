// QA4 independent harness. Oracle is a from-scratch exhaustive bin-packing
// feasibility search; it shares no code with the production solver.
using System.Text.Json;
using System.Text.Json.Serialization;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

const double Eps = 1e-6;
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "out"));
Directory.CreateDirectory(outDir);
var log = new List<string>();
var records = new List<object>();
void Log(string s) { log.Add(s); Console.WriteLine(s); }

// ---------- independent oracle ----------
static int OracleMinBars(double[] items, double L, double K)
{
    var sorted = items.OrderByDescending(x => x).ToArray();
    int n = sorted.Length;
    if (n == 0) return 0;
    foreach (var it in sorted) if (it + K > L + Eps) return -1; // infeasible
    int lb = (int)Math.Ceiling((sorted.Sum() + n * K) / L - 1e-9);
    if (lb < 1) lb = 1;
    for (int b = lb; b <= n; b++)
        if (Feasible(sorted, L, K, b)) return b;
    return -1;
}
static bool Feasible(double[] items, double L, double K, int maxBins)
{
    var bins = new List<(double sum, int cnt)>();
    var seen = new HashSet<string>();
    return Dfs(0);
    bool Dfs(int i)
    {
        if (i == items.Length) return true;
        var key = string.Join(";", bins.OrderBy(b => b.sum).ThenBy(b => b.cnt).Select(b => $"{b.sum:R}:{b.cnt}"));
        if (!seen.Add(key)) return false;
        var tried = new HashSet<string>();
        for (int b = 0; b < bins.Count; b++)
        {
            var (s, c) = bins[b];
            var tk = $"{s:R}:{c}";
            if (tried.Contains(tk)) continue;
            tried.Add(tk);
            if (s + items[i] + (c + 1) * K <= L + 1e-9)
            {
                bins[b] = (s + items[i], c + 1);
                if (Dfs(i + 1)) return true;
                bins[b] = (s, c);
            }
        }
        if (bins.Count < maxBins)
        {
            bins.Add((items[i], 1));
            if (Dfs(i + 1)) return true;
            bins.RemoveAt(bins.Count - 1);
        }
        return false;
    }
}

// ---------- verification ----------
static List<string> Verify(CuttingTask task, CuttingResult r)
{
    var errs = new List<string>();
    // multiset coverage
    var want = task.Parts.Where(p => p != null).ToDictionary(p => p.Id, p => p.Quantity);
    var got = r.Bars.SelectMany(b => b.Parts).GroupBy(p => p.Source.Id)
        .ToDictionary(g => g.Key, g => g.Count());
    foreach (var kv in want)
        if (!got.TryGetValue(kv.Key, out var g) || g != kv.Value)
            errs.Add($"multiset mismatch part {kv.Key}: want {kv.Value} got {(got.ContainsKey(kv.Key) ? got[kv.Key] : 0)}");
    foreach (var kv in got) if (!want.ContainsKey(kv.Key)) errs.Add($"unexpected part id {kv.Key}");
    foreach (var b in r.Bars)
    {
        int n = b.Parts.Count;
        double pl = b.Parts.Sum(p => p.Source.Length);
        if (b.CutCount != n) errs.Add($"bar {b.Index}: CutCount {b.CutCount} != parts {n}");
        if (Math.Abs(b.CutLoss - n * task.CutWidth) > 0.01) errs.Add($"bar {b.Index}: CutLoss {b.CutLoss} != {n}*{task.CutWidth}");
        double expectRem = b.BarLength - pl - n * task.CutWidth;
        if (Math.Abs(b.Remainder - expectRem) > 0.01) errs.Add($"bar {b.Index}: Remainder {b.Remainder} != {expectRem}");
        if (expectRem < -0.01) errs.Add($"bar {b.Index}: infeasible remainder {expectRem}");
        if (Math.Abs(b.BarLength - task.BarLength) > Eps) errs.Add($"bar {b.Index}: BarLength {b.BarLength}");
    }
    if (Math.Abs(r.TotalRemainder - r.Bars.Sum(b => b.Remainder)) > 0.01) errs.Add("TotalRemainder inconsistent");
    if (Math.Abs(r.TotalCutLoss - r.Bars.Sum(b => b.CutLoss)) > 0.01) errs.Add("TotalCutLoss inconsistent");
    if (Math.Abs(r.TotalPartLength - r.Bars.Sum(b => b.Parts.Sum(p => p.Source.Length))) > 0.01) errs.Add("TotalPartLength inconsistent");
    if (r.TotalBarCount != r.Bars.Count) errs.Add("TotalBarCount inconsistent");
    double expUtil = r.TotalBarLength > 0 ? r.TotalPartLength / r.TotalBarLength * 100.0 : 0.0;
    if (Math.Abs(r.UtilizationPercent - expUtil) > 0.01) errs.Add($"Utilization {r.UtilizationPercent} != {expUtil}");
    if (r.Patterns.Sum(p => p.Multiplicity) != r.Bars.Count) errs.Add("Patterns multiplicity != bar count");
    // each pattern must match some bar multiset
    var barSigs = r.Bars.Select(Sig).ToList();
    foreach (var p in r.Patterns)
    {
        var psig = string.Join(",", p.Parts.Select(x => x.Source.Length).OrderBy(x => x)) + "|" + Math.Round(p.Remainder, 4);
        if (!barSigs.Any(s => s.StartsWith(psig.Split('|')[0] + "|"))) errs.Add($"pattern not found in bars: {psig}");
    }
    static string Sig(BarCutting b) =>
        string.Join(",", b.Parts.Select(x => x.Source.Length).OrderBy(x => x)) + "|" + Math.Round(b.Remainder, 4);
    return errs;
}

static object Describe(CuttingResult r) => new
{
    status = r.Status.ToString(),
    reason = r.BestFoundReason.ToString(),
    bars = r.TotalBarCount,
    totalRemainder = Math.Round(r.TotalRemainder, 4),
    totalCutLoss = Math.Round(r.TotalCutLoss, 4),
    util = Math.Round(r.UtilizationPercent, 4),
    patterns = r.Patterns.Count,
    barSigs = r.Bars.Select(b => $"{string.Join("+", b.Parts.Select(p => p.Source.Length).OrderByDescending(x => x))} rem={Math.Round(b.Remainder, 3)}").ToArray()
};

var engine = new CuttingEngine();
var opts = new CuttingOptions { TimeLimitSeconds = 30, MaxPatternCount = 100_000, ToleranceMm = 0.01 };

PartRequirement P(string name, double len, int qty, string mid = "m1")
    => new() { Name = name, Length = len, Quantity = qty, MaterialId = mid };
CuttingTask Task(double L, double K, params PartRequirement[] parts) => new()
{
    Material = new LinearMaterial { Id = "m1", Name = "M1", StandardLength = L, CutWidth = K },
    BarLength = L,
    CutWidth = K,
    Parts = parts
};

void RunCase(string id, string cat, CuttingTask task, int? expectedBars, CuttingOptions? o = null)
{
    try
    {
        var r = engine.Solve(task, o ?? opts);
        var errs = Verify(task, r);
        int oracle = OracleMinBars(task.Parts.SelectMany(p => Enumerable.Repeat(p.Length, p.Quantity)).ToArray(),
            task.BarLength, task.CutWidth);
        if (expectedBars.HasValue && r.TotalBarCount != expectedBars.Value)
            errs.Add($"barCount {r.TotalBarCount} != expected {expectedBars.Value}");
        if (oracle >= 0 && r.TotalBarCount != oracle)
            errs.Add($"barCount {r.TotalBarCount} != oracle {oracle}");
        var verdict = errs.Count == 0 ? "PASS" : "FAIL";
        Log($"{id} [{cat}] {verdict} bars={r.TotalBarCount} oracle={oracle} status={r.Status} {string.Join(" | ", errs)}");
        records.Add(new { id, cat, verdict, oracle, errs, result = Describe(r) });
    }
    catch (Exception ex)
    {
        Log($"{id} [{cat}] EXC {ex.GetType().Name}: {ex.Message}");
        records.Add(new { id, cat, verdict = "EXC", exc = ex.GetType().Name + ": " + ex.Message });
    }
}

void RunExpectThrow(string id, string cat, CuttingTask task)
{
    try
    {
        var r = engine.Solve(task, opts);
        Log($"{id} [{cat}] FAIL no exception, bars={r.TotalBarCount}");
        records.Add(new { id, cat, verdict = "FAIL", note = "no exception" });
    }
    catch (ArgumentException ex)
    {
        Log($"{id} [{cat}] PASS ArgumentException: {ex.Message}");
        records.Add(new { id, cat, verdict = "PASS", exc = ex.Message });
    }
    catch (Exception ex)
    {
        Log($"{id} [{cat}] FAIL wrong exception {ex.GetType().Name}: {ex.Message}");
        records.Add(new { id, cat, verdict = "FAIL", exc = ex.GetType().Name + ": " + ex.Message });
    }
}

// ===== A: obvious =====
RunCase("QA4-A1", "A", Task(6000, 3, P("a", 2000, 2), P("b", 1500, 1)), 1);
RunCase("QA4-A2", "A", Task(6000, 3, P("a", 1000, 6)), 2);
RunExpectThrow("QA4-A3", "A", Task(6000, 3, P("a", 6500, 1)));
RunCase("QA4-A4", "A", Task(6000, 3, P("a", 2800, 2), P("b", 1700, 1), P("c", 900, 1)), 1);
RunExpectThrow("QA4-A5", "A", Task(6000, 3));
RunExpectThrow("QA4-A6", "A", Task(6000, 3, P("neg", -100, 1)));
RunExpectThrow("QA4-A7", "A", Task(6000, 3, P("zero", 0, 1)));
RunExpectThrow("QA4-A8", "A", Task(6000, 3, P("q0", 100, 0)));
RunExpectThrow("QA4-A9", "A", Task(6000, -1, P("a", 100, 1)));
RunExpectThrow("QA4-A10", "A", Task(6000, 6000, P("a", 100, 1)));
RunExpectThrow("QA4-A11", "A", Task(0, 3, P("a", 100, 1)));
RunExpectThrow("QA4-A12", "A", Task(6000, 3, P("nan", double.NaN, 1)));
RunExpectThrow("QA4-A13", "A", Task(6000, 3, P("inf", double.PositiveInfinity, 1)));
// part == bar with kerf>0 cannot fit (needs N=1 cut)
RunExpectThrow("QA4-A14", "A", Task(6000, 3, P("a", 6000, 1)));
// part == bar with kerf==0 fits exactly
RunCase("QA4-A15", "A", Task(6000, 0, P("a", 6000, 1)), 1);

// ===== B: kerf/capacity boundaries =====
// 1497*4 + 4*3 = 6000 exactly -> remainder 0, 1 bar
RunCase("QA4-B1", "B", Task(6000, 3, P("a", 1497, 4)), 1);
// 1500*4 + 4*3 = 6012 > 6000 -> 2 bars (3+1)
RunCase("QA4-B2", "B", Task(6000, 3, P("a", 1500, 4)), 2);
// kerf = 0: 1500*4 fits exactly
RunCase("QA4-B3", "B", Task(6000, 0, P("a", 1500, 4)), 1);
// part = bar - kerf exactly: fits with remainder 0
RunCase("QA4-B4", "B", Task(6000, 3, P("a", 5997, 1)), 1);
// part = bar - kerf + 1: rejected
RunExpectThrow("QA4-B5", "B", Task(6000, 3, P("a", 5998, 1)));
// two parts whose sum+2K == bar exactly
RunCase("QA4-B6", "B", Task(6000, 3, P("a", 3000, 1), P("b", 2994, 1)), 1);
// sum+2K = bar + 1 -> 2 bars
RunCase("QA4-B7", "B", Task(6000, 3, P("a", 3000, 1), P("b", 2995, 1)), 2);

// ===== C: repeats / quantities =====
RunCase("QA4-C1", "C", Task(6000, 3, P("a", 1999, 3)), 1); // 5997+9=6006>6000 -> actually 2? check
RunCase("QA4-C2", "C", Task(6000, 3, P("a", 999, 6)), 2);  // 999*6+18=6012 -> 2 bars
RunCase("QA4-C3", "C", Task(6000, 3, P("a", 1000, 5), P("b", 1000, 5)), 2); // 5+5
RunCase("QA4-C4", "C", Task(6000, 2, P("a", 500, 12)), 3); // per bar floor((6000)/(502))=11 -> wait compute oracle

// ===== D: different lengths =====
RunCase("QA4-D1", "D", Task(6000, 3, P("a", 2500, 1), P("b", 2000, 1), P("c", 1494, 1)), 1); // 5994+9=6003? check oracle
RunCase("QA4-D2", "D", Task(6000, 3, P("a", 3500, 1), P("b", 2400, 1)), 2); // 3500+2400+6=5906 fits 1! oracle decides
RunCase("QA4-D3", "D", Task(6000, 3, P("a", 3500, 2), P("b", 2400, 1)), 2); // 3500*2>6000 -> 2 bars min

// ===== E: greedy-unfavorable =====
RunCase("QA4-E1", "E", Task(6000, 0, P("a", 4000, 1), P("b", 3500, 1), P("c", 2500, 1), P("d", 2000, 1), P("e", 2000, 1)), 2);
RunCase("QA4-E2", "E", Task(100, 0, P("a", 60, 2), P("b", 50, 2), P("c", 40, 2), P("d", 30, 1), P("e", 20, 1)), 3);
RunCase("QA4-E3", "E", Task(6000, 3, P("a", 3100, 2), P("b", 2900, 2), P("c", 1400, 2), P("d", 1490, 1)), null);

// ===== F: scale =====
{
    var rng = new Random(4);
    var parts = new List<PartRequirement>();
    for (int i = 0; i < 12; i++) parts.Add(P($"p{i}", 200 + rng.Next(2300), 1 + rng.Next(3)));
    var t = Task(6000, 3, parts.ToArray());
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var r = engine.Solve(t, new CuttingOptions { TimeLimitSeconds = 8, MaxPatternCount = 100_000 });
        sw.Stop();
        var errs = Verify(t, r);
        Log($"QA4-F1 [F] {(errs.Count == 0 ? "PASS" : "FAIL")} bars={r.TotalBarCount} status={r.Status} reason={r.BestFoundReason} t={sw.Elapsed.TotalSeconds:F1}s {string.Join("|", errs)}");
        records.Add(new { id = "QA4-F1", cat = "F", verdict = errs.Count == 0 ? "PASS" : "FAIL", errs, result = Describe(r) });
    }
    catch (Exception ex) { Log($"QA4-F1 [F] EXC {ex.Message}"); records.Add(new { id = "QA4-F1", verdict = "EXC" }); }
}
// very large single-type demand
RunCase("QA4-F2", "F", Task(6000, 3, P("a", 250, 200)), null);

// ===== G: determinism =====
{
    var t = Task(6000, 3, P("a", 3100, 2), P("b", 2900, 2), P("c", 1400, 2), P("d", 1490, 1), P("e", 700, 3));
    var r1 = engine.Solve(t, opts);
    var r2 = engine.Solve(t, opts);
    string S(CuttingResult r) => string.Join(";", r.Bars.Select(b =>
        string.Join(",", b.Parts.Select(p => p.Source.Length).OrderBy(x => x)) + $":{b.Remainder:F4}")) + $"|{r.Status}";
    bool same = S(r1) == S(r2);
    Log($"QA4-G1 [G] {(same ? "PASS" : "FAIL")} structural determinism; r1={S(r1)} r2={S(r2)}");
    records.Add(new { id = "QA4-G1", cat = "G", verdict = same ? "PASS" : "FAIL", s1 = S(r1), s2 = S(r2) });
}

// ===== Multi-material project engine =====
{
    var pe = new ProjectCuttingEngine(engine);
    var m1 = new LinearMaterial { Id = "m1", Name = "Steel", StandardLength = 6000, CutWidth = 3 };
    var m2 = new LinearMaterial { Id = "m2", Name = "Alu", StandardLength = 4000, CutWidth = 2 };
    var pt = new ProjectCuttingTask
    {
        Materials = new[] { m1, m2 },
        Parts = new[] { P("a", 2000, 2, "m1"), P("b", 1500, 1, "m1"), P("c", 1000, 3, "m2") }
    };
    var pr = pe.Solve(pt, opts);
    var errs = new List<string>();
    if (pr.Materials.Count != 2) errs.Add($"materials count {pr.Materials.Count}");
    foreach (var m in pr.Materials) errs.AddRange(Verify(m.Task, m.Result).Select(e => m.Material.Id + ":" + e));
    var mm1 = pr.Materials.FirstOrDefault(x => x.Material.Id == "m1");
    var mm2 = pr.Materials.FirstOrDefault(x => x.Material.Id == "m2");
    if (mm1 == null || mm2 == null) errs.Add("material grouping lost");
    else
    {
        if (mm1.Result.TotalBarCount != 1) errs.Add($"m1 bars {mm1.Result.TotalBarCount} != 1");
        // m2: 1000*3+3*2=3006<=4000 -> 1 bar
        if (mm2.Result.TotalBarCount != 1) errs.Add($"m2 bars {mm2.Result.TotalBarCount} != 1");
    }
    Log($"QA4-H1 [multi] {(errs.Count == 0 ? "PASS" : "FAIL")} {string.Join("|", errs)} status={pr.Status}");
    records.Add(new { id = "QA4-H1", cat = "multi", verdict = errs.Count == 0 ? "PASS" : "FAIL", errs });
    // unknown material id
    try
    {
        pe.Solve(new ProjectCuttingTask { Materials = new[] { m1 }, Parts = new[] { P("x", 100, 1, "ghost") } }, opts);
        Log("QA4-H2 [multi] FAIL no exception for unknown MaterialId");
        records.Add(new { id = "QA4-H2", verdict = "FAIL" });
    }
    catch (ArgumentException ex) { Log($"QA4-H2 [multi] PASS {ex.Message}"); records.Add(new { id = "QA4-H2", verdict = "PASS" }); }
    // material with no parts is skipped
    var pr2 = pe.Solve(new ProjectCuttingTask
    {
        Materials = new[] { m1, m2 },
        Parts = new[] { P("a", 2000, 1, "m1") }
    }, opts);
    Log($"QA4-H3 [multi] {(pr2.Materials.Count == 1 ? "PASS" : "FAIL")} sections={pr2.Materials.Count}");
    records.Add(new { id = "QA4-H3", verdict = pr2.Materials.Count == 1 ? "PASS" : "FAIL" });
    // zero materials
    try { pe.Solve(new ProjectCuttingTask { Materials = Array.Empty<LinearMaterial>(), Parts = new[] { P("a", 1, 1, "m1") } }, opts); Log("QA4-H4 FAIL"); }
    catch (ArgumentException) { Log("QA4-H4 [multi] PASS ArgumentException"); records.Add(new { id = "QA4-H4", verdict = "PASS" }); }
}

// ===== Fuzz: random small cases vs oracle =====
{
    var rng = new Random(20261030);
    int pass = 0, fail = 0, failOpt = 0;
    var failCases = new List<object>();
    for (int i = 0; i < 120; i++)
    {
        double L = rng.Next(2) == 0 ? 6000 : 3000;
        double K = new[] { 0.0, 3.0, 5.0 }[rng.Next(3)];
        int types = 1 + rng.Next(4);
        var parts = Enumerable.Range(0, types)
            .Select(t => P($"t{t}", 100 + rng.Next(2400), 1 + rng.Next(3))).ToArray();
        var t = Task(L, K, parts);
        int oracle = OracleMinBars(parts.SelectMany(p => Enumerable.Repeat(p.Length, p.Quantity)).ToArray(), L, K);
        if (oracle < 0) continue;
        try
        {
            var r = engine.Solve(t, new CuttingOptions { TimeLimitSeconds = 10, MaxPatternCount = 100_000 });
            var errs = Verify(t, r);
            bool optMismatch = r.TotalBarCount != oracle;
            if (errs.Count == 0 && !optMismatch) pass++;
            else
            {
                fail++;
                if (optMismatch) failOpt++;
                failCases.Add(new { i, L, K, parts = parts.Select(p => $"{p.Length}x{p.Quantity}"), oracle, got = r.TotalBarCount, errs, r.Status });
            }
        }
        catch (Exception ex) { fail++; failCases.Add(new { i, L, K, exc = ex.Message }); }
    }
    Log($"QA4-FZ [fuzz] pass={pass} fail={fail} optimalityMismatch={failOpt}");
    records.Add(new { id = "QA4-FZ", cat = "fuzz", verdict = fail == 0 ? "PASS" : "FAIL", pass, fail, failOpt, failCases });
}

// ===== Edge: null part element in list =====
{
    var t = Task(6000, 3, P("a", 1000, 1));
    t.Parts = new List<PartRequirement> { P("a", 1000, 1), null };
    try
    {
        engine.Solve(t, opts);
        Log("QA4-X1 [edge] FAIL no exception for null part element");
        records.Add(new { id = "QA4-X1", verdict = "FAIL" });
    }
    catch (Exception ex)
    {
        Log($"QA4-X1 [edge] {(ex is ArgumentException ? "PASS" : "FAIL")} {ex.GetType().Name}: {ex.Message}");
        records.Add(new { id = "QA4-X1", verdict = ex is ArgumentException ? "PASS" : "FAIL", exc = ex.GetType().Name + ": " + ex.Message });
    }
}
// ===== Edge: degenerate options =====
{
    var t = Task(6000, 3, P("a", 2000, 2), P("b", 1500, 1));
    var r0 = engine.Solve(t, new CuttingOptions { TimeLimitSeconds = 0, MaxPatternCount = 100_000 });
    var e0 = Verify(t, r0);
    Log($"QA4-X2 [edge] TimeLimit=0 -> status={r0.Status} reason={r0.BestFoundReason} bars={r0.TotalBarCount} errs={e0.Count}");
    records.Add(new { id = "QA4-X2", verdict = e0.Count == 0 && r0.Status == CalculationStatus.BestFound ? "PASS" : "FAIL", status = r0.Status.ToString(), reason = r0.BestFoundReason.ToString() });
    var rn = engine.Solve(t, new CuttingOptions { TimeLimitSeconds = 30, MaxPatternCount = 1 });
    var en = Verify(t, rn);
    Log($"QA4-X3 [edge] MaxPatterns=1 -> status={rn.Status} reason={rn.BestFoundReason} bars={rn.TotalBarCount} errs={en.Count}");
    records.Add(new { id = "QA4-X3", verdict = en.Count == 0 ? "PASS" : "FAIL", status = rn.Status.ToString(), reason = rn.BestFoundReason.ToString() });
}

File.WriteAllLines(Path.Combine(outDir, "log.txt"), log);
File.WriteAllText(Path.Combine(outDir, "results.json"),
    JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
Log($"WROTE {outDir}");
