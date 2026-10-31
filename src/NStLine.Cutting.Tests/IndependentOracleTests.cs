using NStLine.Core.Models;
using NStLine.Cutting.Services;

namespace NStLine.Cutting.Tests;

public sealed class IndependentOracleTests
{
    [Theory]
    [InlineData(10, 0, 6, 2, 4, 2, 0, 0)]
    [InlineData(10, 1, 6, 2, 3, 2, 0, 0)]
    [InlineData(10, 0, 7, 1, 3, 1, 0, 0)]
    [InlineData(10, 0, 6, 1, 5, 2, 4, 1)]
    [InlineData(15, 1, 8, 2, 5, 2, 3, 1)]
    [InlineData(10, 0, 6, 1, 4, 1, 4, 1)]
    [InlineData(10, 0, 2, 3, 3, 2, 4, 1)]
    public void SmallTasks_MatchIndependentMinimumAndConserveDemand(
        int stock, int kerf, int a, int qa, int b, int qb, int c, int qc)
    {
        var task = CreateTask(stock, kerf, (a, qa), (b, qb), (c, qc));
        var expectedBars = MinimumBarCount(task);

        var result = new CuttingEngine().Solve(task, ExactOptions());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(expectedBars, result.TotalBarCount);
        AssertPlan(task, result);
    }

    [Fact]
    public void DeterministicSmallMatrix_MatchesIndependentMinimum()
    {
        var random = new Random(19284);
        for (var caseIndex = 0; caseIndex < 40; caseIndex++)
        {
            var stock = random.Next(9, 18);
            var kerf = random.Next(0, 3);
            var requirements = Enumerable.Range(0, 3)
                .Select(_ => ((double)random.Next(1, stock - kerf), random.Next(1, 3)))
                .ToArray();
            var task = CreateTask(stock, kerf, requirements);
            var expectedBars = MinimumBarCount(task);

            var result = new CuttingEngine().Solve(task, ExactOptions());

            Assert.True(result.Status == CalculationStatus.Optimal, $"Case {caseIndex}: {result.Status}");
            Assert.True(result.TotalBarCount == expectedBars,
                $"Case {caseIndex}: expected {expectedBars}, got {result.TotalBarCount}");
            AssertPlan(task, result);
        }
    }

    [Fact]
    public void DecimalLengthsAndKerf_ConservePhysicalLength()
    {
        var task = CreateTask(10, 0.1, (3.2, 2), (3.1, 1), (0.3, 2));
        var expectedBars = MinimumBarCount(task);

        var result = new CuttingEngine().Solve(task, ExactOptions());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(expectedBars, result.TotalBarCount);
        AssertPlan(task, result);
    }

    [Theory]
    [InlineData(1, 0, 0.6, 1, 0.5, 1, 0.3, 1, 0.2, 3)]
    [InlineData(2.3, 0, 0.7, 1, 0.9, 2, 0.7, 3, 0, 0)]
    [InlineData(3.3, 0, 1.7, 2, 1.1, 3, 0.6, 4, 3.2, 3)]
    [InlineData(4.6, 0.1, 2.1, 3, 2.2, 1, 1.1, 4, 0, 0)]
    [InlineData(3.3, 0.3, 2.6, 1, 1.3, 2, 0.5, 2, 0.6, 2)]
    public void DecimalRegressions_MatchIndependentMinimum(
        double stock, double kerf, double a, int qa, double b, int qb,
        double c, int qc, double d, int qd)
    {
        var task = CreateTask(stock, kerf, (a, qa), (b, qb), (c, qc), (d, qd));
        var expected = MinimumBarCount(task);

        var result = new CuttingEngine().Solve(task, ExactOptions());

        Assert.Equal(expected, result.TotalBarCount);
        Assert.Equal(CalculationStatus.Optimal, result.Status);
        AssertPlan(task, result);
    }

    [Theory]
    [InlineData(1.4, 0.1, 1.3)]
    [InlineData(0.6, 0.2, 0.4)]
    [InlineData(0.3, 0.2, 0.1)]
    [InlineData(3.4, 0.2, 3.2)]
    public void DecimalBoundary_ExactlyFitsWithKerf(double stock, double kerf, double length)
    {
        var task = CreateTask(stock, kerf, (length, 1));

        var result = new CuttingEngine().Solve(task, ExactOptions());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(1, result.TotalBarCount);
        Assert.Equal(0, result.TotalRemainder);
        AssertPlan(task, result);
    }

    [Theory]
    [InlineData(1.4, 0.1, 1.3000000001)]
    [InlineData(6000, 3, 5997.001)]
    public void DecimalBoundary_ActualExcessIsRejected(double stock, double kerf, double length)
    {
        var task = CreateTask(stock, kerf, (length, 1));

        Assert.Throws<ArgumentException>(() => new CuttingEngine().Solve(task, ExactOptions()));
    }

    [Theory]
    [InlineData(0.3, 0.1, 3, 1)]
    [InlineData(1, 0.5000000002, 2, 2)]
    [InlineData(1e-10, 6e-11, 2, 2)]
    [InlineData(6000, 1e-6, 2, 1)]
    public void DecimalCapacity_ConservesLengthsInExactAndFallback(
        double stock, double length, int quantity, int expectedBars)
    {
        var task = CreateTask(stock, 0, (length, quantity));
        foreach (var patternLimit in new[] { 0, 10000 })
        {
            var options = ExactOptions();
            options.MaxPatternCount = patternLimit;

            var result = new CuttingEngine().Solve(task, options);

            Assert.Equal(expectedBars, result.TotalBarCount);
            Assert.All(result.Bars, bar => Assert.True(bar.Remainder >= 0));
            Assert.Equal(patternLimit == 0 ? CalculationStatus.BestFound : CalculationStatus.Optimal, result.Status);
            AssertPlan(task, result);
        }
    }

    [Fact]
    public void DecimalMatrix_MatchesIndependentMinimumIncludingBoundaryCases()
    {
        var random = new Random(601030);
        for (var caseIndex = 0; caseIndex < 300; caseIndex++)
        {
            var stock = random.Next(8, 51);
            var kerf = random.Next(0, 3);
            var requirements = Enumerable.Range(0, 3)
                .Select(_ => (random.Next(1, stock - kerf + 1) / 10.0, random.Next(1, 4)))
                .ToArray();
            var task = CreateTask(stock / 10.0, kerf / 10.0, requirements);
            var expected = MinimumBarCount(task);

            var result = new CuttingEngine().Solve(task, ExactOptions());

            Assert.True(result.TotalBarCount == expected,
                $"Case {caseIndex}: expected {expected}, actual {result.TotalBarCount}");
            Assert.Equal(CalculationStatus.Optimal, result.Status);
            AssertPlan(task, result);
        }
    }

    [Fact]
    public void NullPart_IsRejectedAtValidationBoundary()
    {
        var task = CreateTask(6000, 3, (1000, 1));
        task.Parts = new PartRequirement[] { null! };

        Assert.Throws<ArgumentException>(() => new CuttingEngine().Solve(task, ExactOptions()));
    }

    [Theory]
    [InlineData(1e308, 9e307, 2)]
    [InlineData(1e308, 6e307, 2)]
    public void NonRepresentableTotals_AreRejectedInsteadOfReturningNaN(double stock, double length, int quantity)
    {
        var task = CreateTask(stock, 0, (length, quantity));

        Assert.Throws<ArgumentException>(() => new CuttingEngine().Solve(task, ExactOptions()));
    }

    [Fact]
    public void ProjectNullPart_IsRejectedAtValidationBoundary()
    {
        var task = new ProjectCuttingTask
        {
            Materials = [new LinearMaterial { Id = "a", Name = "A", StandardLength = 6000 }],
            Parts = [null!]
        };

        Assert.Throws<ArgumentException>(() => new ProjectCuttingEngine(new CuttingEngine()).Solve(task, ExactOptions()));
    }

    [Fact]
    public void ProjectAggregateOverflow_IsRejectedEvenWhenIndividualResultsAreFinite()
    {
        var task = new ProjectCuttingTask
        {
            Materials = [
                new LinearMaterial { Id = "a", Name = "A", StandardLength = 1e308 },
                new LinearMaterial { Id = "b", Name = "B", StandardLength = 1e308 }],
            Parts = [
                new PartRequirement { MaterialId = "a", Length = 6e307, Quantity = 1 },
                new PartRequirement { MaterialId = "b", Length = 6e307, Quantity = 1 }]
        };

        Assert.Throws<ArgumentException>(() => new ProjectCuttingEngine(new CuttingEngine()).Solve(task, ExactOptions()));
    }

    [Fact]
    public async Task CancellationDuringHeuristic_StopsBeforeCompletingLargePacking()
    {
        var engine = new CuttingEngine();
        engine.Solve(CreateTask(6000, 0, (4000, 1)), ExactOptions());
        using var cancellation = new CancellationTokenSource();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => System.Threading.Tasks.Task.Run(() =>
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(30));
            engine.Solve(CreateTask(6000, 0, (4000, 30000)), ExactOptions(), cancellationToken: cancellation.Token);
        }));

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), $"Cancellation took {elapsed.Elapsed}");
    }

    [Fact]
    public void ForcedFallback_RemainsFeasibleWithoutClaimingOptimality()
    {
        var task = CreateTask(10, 0, (6, 2), (4, 2));
        var options = ExactOptions();
        options.MaxPatternCount = 0;

        var result = new CuttingEngine().Solve(task, options);

        Assert.Equal(CalculationStatus.BestFound, result.Status);
        Assert.True(result.TotalBarCount >= MinimumBarCount(task));
        AssertPlan(task, result);
    }

    private static CuttingTask CreateTask(double stock, double kerf, params (double Length, int Quantity)[] requirements)
    {
        var parts = requirements
            .Where(item => item.Quantity > 0)
            .Select((item, index) => new PartRequirement
            {
                Id = $"type-{index}",
                Name = $"Part {index + 1}",
                Length = item.Length,
                Quantity = item.Quantity
            }).ToArray();
        return new CuttingTask
        {
            Material = new LinearMaterial { Name = "Test", StandardLength = stock, CutWidth = kerf },
            BarLength = stock,
            CutWidth = kerf,
            Parts = parts
        };
    }

    private static CuttingOptions ExactOptions()
        => new() { TimeLimitSeconds = 10, MaxPatternCount = 10_000, ToleranceMm = 0.01 };

    private static int MinimumBarCount(CuttingTask task)
    {
        var pieces = task.Parts
            .SelectMany(part => Enumerable.Repeat((decimal)part.Length + (decimal)task.CutWidth, part.Quantity))
            .OrderByDescending(length => length)
            .ToArray();
        var capacity = (decimal)task.BarLength;
        var loads = new List<decimal>();
        var best = pieces.Length;

        void Place(int index)
        {
            if (index == pieces.Length)
            {
                best = Math.Min(best, loads.Count);
                return;
            }

            var previouslyTried = new HashSet<decimal>();
            for (var bar = 0; bar < loads.Count; bar++)
            {
                var load = loads[bar];
                if (load + pieces[index] > capacity || !previouslyTried.Add(load))
                    continue;

                loads[bar] += pieces[index];
                Place(index + 1);
                loads[bar] = load;
            }

            if (loads.Count + 1 >= best)
                return;

            loads.Add(pieces[index]);
            Place(index + 1);
            loads.RemoveAt(loads.Count - 1);
        }

        Place(0);
        return best;
    }

    private static void AssertPlan(CuttingTask task, CuttingResult result)
    {
        var expected = task.Parts.ToDictionary(part => part.Id, part => part.Quantity);
        var observed = task.Parts.ToDictionary(part => part.Id, _ => 0);
        foreach (var bar in result.Bars)
        {
            Assert.NotEmpty(bar.Parts);
            var used = (decimal)bar.Parts.Sum(part => (decimal)part.Source.Length)
                + bar.Parts.Count * (decimal)task.CutWidth;
            Assert.True(used <= (decimal)task.BarLength, $"Bar {bar.Index} uses {used} of {task.BarLength} mm");
            Assert.Equal(bar.Parts.Count * task.CutWidth, bar.CutLoss, 8);
            Assert.Equal((double)((decimal)task.BarLength - used), bar.Remainder, 8);
            foreach (var placed in bar.Parts)
            {
                Assert.Contains(placed.Source.Id, expected.Keys);
                observed[placed.Source.Id]++;
            }
        }

        Assert.Equal(expected.OrderBy(pair => pair.Key), observed.OrderBy(pair => pair.Key));
    }
}
