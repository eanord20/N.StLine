using System.Diagnostics;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

var benchmarkCases = new[]
{
    BuildCase(
        "Small-Exact",
        6000,
        3,
        ("Stand", 1250, 10),
        ("Brace", 850, 20),
        ("Base", 2400, 6)),
    BuildCase(
        "Medium-Mixed",
        6000,
        3,
        ("P1", 920, 35),
        ("P2", 760, 40),
        ("P3", 510, 28),
        ("P4", 450, 22),
        ("P5", 310, 50)),
    BuildCase(
        "Pattern-Stress",
        6000,
        3,
        ("A", 990, 24),
        ("B", 970, 24),
        ("C", 950, 24),
        ("D", 930, 24),
        ("E", 910, 24),
        ("F", 890, 24)),
    BuildCase(
        "Many-Small",
        6000,
        3,
        ("S", 100, 200)),
    BuildCase(
        "Adversarial-FFD-1",
        100,
        0,
        ("A", 10, 3),
        ("B", 25, 4),
        ("C", 45, 1),
        ("D", 55, 1),
        ("E", 70, 1)),
    BuildCase(
        "Adversarial-FFD-2",
        100,
        0,
        ("A", 15, 1),
        ("B", 20, 2),
        ("C", 25, 1),
        ("D", 40, 1),
        ("E", 50, 1),
        ("F", 60, 1),
        ("G", 65, 1)),
    BuildCase(
        "Production-100",
        6000,
        3,
        ("P1", 300, 20),
        ("P2", 420, 18),
        ("P3", 560, 16),
        ("P4", 710, 14),
        ("P5", 880, 12),
        ("P6", 930, 10),
        ("P7", 1020, 8),
        ("P8", 1200, 6)),
    BuildCase(
        "Production-300",
        6000,
        3,
        ("Q1", 240, 55),
        ("Q2", 360, 50),
        ("Q3", 480, 45),
        ("Q4", 620, 40),
        ("Q5", 760, 35),
        ("Q6", 900, 30),
        ("Q7", 1040, 25),
        ("Q8", 1280, 20))
};

var profiles = new[]
{
    new
    {
        Name = "Default",
        Options = new CuttingOptions
        {
            TimeLimitSeconds = 8,
            MaxPatternCount = 100_000,
            ToleranceMm = 0.01
        },
        CaseFilter = (Func<string, bool>)(_ => true)
    },
    new
    {
        Name = "Extended",
        Options = new CuttingOptions
        {
            TimeLimitSeconds = 20,
            MaxPatternCount = 200_000,
            ToleranceMm = 0.01
        },
        CaseFilter = (Func<string, bool>)(caseName => caseName == "Production-100")
    }
};
var engine = new CuttingEngine();

Console.WriteLine("Profile\tCase\tPartTypes\tTotalQty\tStatus\tReason\tBars\tElapsedMs\tMeasuredMs\tPatterns\tNodes\tPrunedBar\tPrunedRem\tPrunedDom");
foreach (var profile in profiles)
{
    foreach (var @case in benchmarkCases.Where(c => profile.CaseFilter(c.Material.Name)))
    {
        var caseOptions = profile.Name == "Default"
            ? BuildCaseOptions(profile.Options, @case.Material.Name)
            : profile.Options;
        var sw = Stopwatch.StartNew();
        var result = engine.Solve(@case, caseOptions);
        sw.Stop();
        var totalQuantity = @case.Parts.Sum(part => part.Quantity);
        Console.WriteLine(
            $"{profile.Name}\t{@case.Material.Name}\t{@case.Parts.Count}\t{totalQuantity}\t{result.Status}\t{result.BestFoundReason}\t{result.TotalBarCount}\t{result.ElapsedSeconds * 1000:0}\t{sw.Elapsed.TotalMilliseconds:0}\t{result.GeneratedPatterns}\t{result.ExploredNodes}\t{result.PrunedByBarBound}\t{result.PrunedByRemainderBound}\t{result.PrunedByDominance}");
    }
}

static CuttingOptions BuildCaseOptions(CuttingOptions baseOptions, string caseName)
{
    if (caseName is "Medium-Mixed" or "Pattern-Stress" or "Production-100" or "Production-300")
    {
        return new CuttingOptions
        {
            TimeLimitSeconds = Math.Min(baseOptions.TimeLimitSeconds, 6),
            MaxPatternCount = Math.Min(baseOptions.MaxPatternCount, 25_000),
            ToleranceMm = baseOptions.ToleranceMm
        };
    }

    return new CuttingOptions
    {
        TimeLimitSeconds = baseOptions.TimeLimitSeconds,
        MaxPatternCount = baseOptions.MaxPatternCount,
        ToleranceMm = baseOptions.ToleranceMm
    };
}

static CuttingTask BuildCase(
    string name,
    double barLength,
    double cutWidth,
    params (string Name, double Length, int Quantity)[] parts)
{
    var material = new LinearMaterial
    {
        Id = name,
        Name = name,
        StandardLength = barLength,
        CutWidth = cutWidth
    };

    return new CuttingTask
    {
        Material = material,
        BarLength = barLength,
        CutWidth = cutWidth,
        Parts = parts.Select(p => new PartRequirement
        {
            MaterialId = material.Id,
            Name = p.Name,
            Length = p.Length,
            Quantity = p.Quantity
        }).ToList()
    };
}
