// Round2 audit harness — independent oracle checks for consolidated findings.
// Direct DLL references to candidate Release build; no product changes.
// Run (foreground, coordinator):
//   dotnet build -c Release
//   dotnet bin\Release\net8.0-windows\Round2Harness.dll
// Output: JSONL to stdout AND to NEW file results-confirmed.jsonl in cwd.
// Self-watchdog: process exits with code 2 if total run exceeds 90 s.
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NStLine.App.Services;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

var jsonOpts = new JsonSerializerOptions
{
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
};
var outFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "results-confirmed.jsonl"), append: false);
void Emit(string name, object data)
{
    string line;
    try { line = JsonSerializer.Serialize(new { name, data }, jsonOpts); }
    catch (Exception ex) { line = JsonSerializer.Serialize(new { name, emitError = ex.Message }); }
    Console.WriteLine(line);
    outFile.WriteLine(line);
    outFile.Flush();
}

// global watchdog: 90 s
var wd = new Timer(_ =>
{
    try { outFile.WriteLine("{\"name\":\"WATCHDOG_TIMEOUT\"}"); outFile.Flush(); } catch { }
    Console.Error.WriteLine("WATCHDOG_TIMEOUT");
    Environment.Exit(2);
}, null, TimeSpan.FromSeconds(90), Timeout.InfiniteTimeSpan);

// ---------- Independent oracle: all lengths scaled to integer units ----------
// Capacity rule: sum(len_i) + count*kerf <= bar (scaled integers). No shared code with product.
static int OracleMinBars(int bar, int kerf, int[] lens, int[] qty)
{
    int n = lens.Length;
    var patterns = new List<int[]>();
    var cur = new int[n];
    void Gen(int idx, int usedLen, int usedCnt)
    {
        if (idx == n)
        {
            if (usedCnt > 0 && usedLen + usedCnt * kerf <= bar)
                patterns.Add((int[])cur.Clone());
            return;
        }
        int max = Math.Min(qty[idx], bar / Math.Max(1, lens[idx]));
        for (int c = max; c >= 0; c--)
        {
            cur[idx] = c;
            Gen(idx + 1, usedLen + c * lens[idx], usedCnt + c);
        }
        cur[idx] = 0;
    }
    Gen(0, 0, 0);

    var memo = new Dictionary<string, int>();
    int Dfs(int[] remaining)
    {
        bool allZero = true;
        foreach (var r in remaining) if (r != 0) { allZero = false; break; }
        if (allZero) return 0;
        var key = string.Join(',', remaining);
        if (memo.TryGetValue(key, out var v)) return v;
        int best = int.MaxValue;
        foreach (var p in patterns)
        {
            int mult = int.MaxValue;
            for (int i = 0; i < n; i++)
                if (p[i] > 0) mult = Math.Min(mult, remaining[i] / p[i]);
            if (mult <= 0 || mult == int.MaxValue) continue;
            for (int i = 0; i < n; i++) remaining[i] -= p[i];
            var sub = Dfs(remaining);
            for (int i = 0; i < n; i++) remaining[i] += p[i];
            if (sub != int.MaxValue) best = Math.Min(best, 1 + sub);
        }
        memo[key] = best;
        return best;
    }
    return Dfs((int[])qty.Clone());
}

static CuttingTask MakeTask(double bar, double kerf, double[] lens, int[] qty)
{
    var parts = new List<PartRequirement>();
    for (int i = 0; i < lens.Length; i++)
        parts.Add(new PartRequirement { Id = $"p{i}", Name = $"P{i}", MaterialId = "m", Length = lens[i], Quantity = qty[i] });
    return new CuttingTask
    {
        Material = new LinearMaterial { Id = "m", Name = "M", StandardLength = bar, CutWidth = kerf },
        BarLength = bar,
        CutWidth = kerf,
        Parts = parts
    };
}

var engine = new CuttingEngine();
var opts = new CuttingOptions { TimeLimitSeconds = 30, MaxPatternCount = 100000, ToleranceMm = 0.01 };

// ---------- Section A: integer-mm fuzz — scope check for D1 (QA6-01) ----------
{
    var rng = new Random(7701);
    int cases = 0;
    var mismatchSamples = new List<object>();
    var rejectedSamples = new List<object>();
    while (cases < 2000)
    {
        double bar = new[] { 1000, 1500, 3000, 6000, 12000 }[rng.Next(5)];
        double kerf = new[] { 0, 1, 2, 3, 5 }[rng.Next(5)];
        int types = 1 + rng.Next(4);
        var lens = new double[types]; var qty = new int[types];
        for (int i = 0; i < types; i++)
        {
            lens[i] = 1 + rng.Next((int)(bar - kerf));
            qty[i] = 1 + rng.Next(4);
        }
        int expected = OracleMinBars((int)bar, (int)kerf, lens.Select(x => (int)x).ToArray(), qty);
        try
        {
            var res = engine.Solve(MakeTask(bar, kerf, lens, qty), opts);
            cases++;
            if (res.TotalBarCount != expected && mismatchSamples.Count < 20)
                mismatchSamples.Add(new { bar, kerf, lens, qty, expected, actual = res.TotalBarCount, status = res.Status.ToString() });
        }
        catch (Exception ex)
        {
            cases++;
            if (rejectedSamples.Count < 20)
                rejectedSamples.Add(new { bar, kerf, lens, qty, ex = ex.GetType().Name, ex.Message });
        }
    }
    Emit("integer-mm-fuzz", new { cases, mismatches = mismatchSamples.Count, rejected = rejectedSamples.Count, mismatchSamples, rejectedSamples });
}

// ---------- Section B: decimal fuzz (0.1 step) — recount D1 frequency ----------
{
    var rng = new Random(7702);
    int cases = 0;
    var mismatchSamples = new List<object>();
    var rejectedSamples = new List<object>();
    while (cases < 3000)
    {
        int bar10 = 10 + rng.Next(70);
        int kerf10 = rng.Next(6);
        int types = 1 + rng.Next(4);
        var lens10 = new int[types]; var qty = new int[types];
        for (int i = 0; i < types; i++)
        {
            lens10[i] = 1 + rng.Next(Math.Max(1, bar10 - kerf10));
            qty[i] = 1 + rng.Next(4);
        }
        int expected = OracleMinBars(bar10, kerf10, lens10, qty);
        var lens = lens10.Select(x => x / 10.0).ToArray();
        try
        {
            var res = engine.Solve(MakeTask(bar10 / 10.0, kerf10 / 10.0, lens, qty), opts);
            cases++;
            if (res.TotalBarCount != expected && mismatchSamples.Count < 20)
                mismatchSamples.Add(new { bar = bar10 / 10.0, kerf = kerf10 / 10.0, lens, qty, expected, actual = res.TotalBarCount, status = res.Status.ToString() });
        }
        catch (Exception ex)
        {
            cases++;
            if (rejectedSamples.Count < 20)
                rejectedSamples.Add(new { bar = bar10 / 10.0, kerf = kerf10 / 10.0, lens, qty, ex = ex.GetType().Name, ex.Message });
        }
    }
    Emit("decimal-fuzz", new { cases, mismatches = mismatchSamples.Count, rejected = rejectedSamples.Count, mismatchSamples, rejectedSamples });
}

// ---------- Section B2: real-scale trap forms ----------
// (a) conversion-probe: same shape multiplied in binary64 (value carries FP residue —
//     documents what a computed/converted input looks like, NOT an exact decimal);
// (b) literal-decimal: exact typed values at realistic scale — the claim target.
// Oracle scaled x10 to match the typed decimal input, not the binary residue.
{
    var rows = new List<object>();
    // (a) conversion probes (kept from previous run; labelled honestly)
    foreach (var k in new[] { 1001, 10003 })
    {
        double bar = 2.3 * k, kerf = 0.0;
        double[] lens = { 0.7 * k, 0.9 * k, 0.7 * k };
        int[] qty = { 1, 2, 3 };
        int expected = OracleMinBars(
            (int)Math.Round(bar * 10), (int)Math.Round(kerf * 10),
            lens.Select(x => (int)Math.Round(x * 10)).ToArray(), qty);
        try
        {
            var res = engine.Solve(MakeTask(bar, kerf, lens, qty), opts);
            rows.Add(new { variant = "conversion-probe", k, bar, lens, qty, expected, actual = res.TotalBarCount, status = res.Status.ToString(), res.TotalRemainder });
        }
        catch (Exception ex) { rows.Add(new { variant = "conversion-probe", k, bar, rejectedEx = ex.GetType().Name, ex.Message }); }
    }
    // (b) literal decimals — typed values, no binary multiplication
    (double bar, double[] lens, int[] qty, string label)[] literals =
    {
        (2302.3,  new double[] { 700.7, 900.9, 700.7 },   new[] { 1, 2, 3 }, "x1001-shape1791"),
        (23006.9, new double[] { 7002.1, 9002.7, 7002.1 }, new[] { 1, 2, 3 }, "x10003-shape1791"),
        (5005.0,  new double[] { 2702.7, 1801.8, 900.9 },  new[] { 1, 2, 1 }, "x1001-shape1876-kerf200.2"),
    };
    foreach (var (bar, lens, qty, label) in literals)
    {
        double kerf = label.Contains("kerf") ? 200.2 : 0.0;
        int expected = OracleMinBars(
            (int)Math.Round(bar * 10), (int)Math.Round(kerf * 10),
            lens.Select(x => (int)Math.Round(x * 10)).ToArray(), qty);
        try
        {
            var res = engine.Solve(MakeTask(bar, kerf, lens, qty), opts);
            rows.Add(new { variant = "literal-decimal", label, bar, kerf, lens, qty, expected, actual = res.TotalBarCount, status = res.Status.ToString(), res.TotalRemainder });
        }
        catch (Exception ex) { rows.Add(new { variant = "literal-decimal", label, bar, rejectedEx = ex.GetType().Name, ex.Message }); }
    }
    Emit("real-scale-traps", rows);
}

// ---------- Section C: boundary equality — D2 (QA3-06) reachability ----------
{
    var rows = new List<object>();
    (double part, double kerf, double bar)[] table =
    {
        (1.3, 0.1, 1.4), (0.4, 0.2, 0.6), (2.5, 0.5, 3.0), (0.1, 0.2, 0.3),
        (1.15, 0.05, 1.2), (0.7, 0.0, 0.7), (1250.0, 3.0, 1253.0),
        (5997.0, 3.0, 6000.0), (0.15, 0.05, 0.2), (2.05, 0.15, 2.2),
        (1250.0, 0.5, 1250.5), (1300.0, 0.1, 1300.1), (4000.0, 0.2, 4000.2)
    };
    foreach (var (part, kerf, bar) in table)
    {
        bool decimalFits = (decimal)part + (decimal)kerf <= (decimal)bar;
        string outcome; double? bars = null;
        try
        {
            var res = engine.Solve(MakeTask(bar, kerf, new[] { part }, new[] { 1 }), opts);
            outcome = "accepted"; bars = res.TotalBarCount;
        }
        catch (Exception ex) { outcome = ex.GetType().Name; }
        rows.Add(new { part, kerf, bar, decimalFits, outcome, bars });
    }
    Emit("boundary-equality", rows);
}

// ---------- Section D: utilization vs spec §9.1 example (C2) ----------
{
    var task = MakeTask(6000, 3, new double[] { 2400, 1800, 1200 }, new[] { 1, 1, 1 });
    try
    {
        var res = engine.Solve(task, opts);
        double partsPlusKerf = res.TotalPartLength + res.TotalCutLoss;
        Emit("utilization-spec-example", new
        {
            reported = res.UtilizationPercent,
            productFormula = res.TotalPartLength / res.TotalBarLength * 100.0,
            specFormula = partsPlusKerf / res.TotalBarLength * 100.0,
            res.TotalBarCount, res.TotalPartLength, res.TotalCutLoss, res.TotalRemainder
        });
    }
    catch (Exception ex) { Emit("utilization-spec-example", new { error = ex.GetType().Name, ex.Message }); }
}

// ---------- Section E: micro-scale re-confirm (D3 — Low severity, unrealistic scale) ----------
{
    try
    {
        var res = engine.Solve(MakeTask(1e-10, 0, new[] { 6e-11 }, new[] { 2 }), opts);
        Emit("micro-reconfirm", new { res.Status, res.TotalBarCount, res.TotalRemainder, res.UtilizationPercent });
    }
    catch (Exception ex) { Emit("micro-reconfirm", new { error = ex.GetType().Name, ex.Message }); }
}

// ---------- Section F: floor->int overflow re-confirm (D13) ----------
{
    try
    {
        var res = engine.Solve(MakeTask(6000, 0, new[] { 1e-6 }, new[] { 2 }), opts);
        Emit("floor-int-overflow", new { res.Status, res.TotalBarCount, res.GeneratedPatterns, reason = res.BestFoundReason.ToString() });
    }
    catch (Exception ex) { Emit("floor-int-overflow", new { error = ex.GetType().Name, ex.Message }); }
}

// ---------- Section G: parser ambiguity matrix (D4/D5/C3 judgement data) ----------
{
    var rows = new List<object>();
    foreach (var culture in new[] { "en-US", "uk-UA", "de-DE", "ru-RU" })
    {
        var prev = CultureInfo.CurrentCulture;
        var prevUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            string[][] inputs =
            {
                new[] { "Shelf", "1.250", "2" },
                new[] { "Shelf", "1,250", "2" },
                new[] { "Shelf", "1 250", "2" },
                new[] { "2400", "6000" },
                new[] { "Shelf", "1250", "" },
                new[] { "Shelf", "1250", "2.5" },
                new[] { "Shelf", "1250", "0" },
            };
            foreach (var cells in inputs)
            {
                try
                {
                    var ok = PartPasteParser.TryParse(cells, out var name, out var len, out var q);
                    rows.Add(new { culture, cells = string.Join("|", cells), ok, name, len, qty = q });
                }
                catch (Exception ex) { rows.Add(new { culture, cells = string.Join("|", cells), ex = ex.GetType().Name }); }
            }
        }
        finally { CultureInfo.CurrentCulture = prev; CultureInfo.CurrentUICulture = prevUi; }
    }
    Emit("parser-matrix", rows);
}

wd.Dispose();
Emit("done", new { });
outFile.Dispose();
