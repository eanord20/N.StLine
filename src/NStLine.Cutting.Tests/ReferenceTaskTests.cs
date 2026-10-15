using NStLine.Core.Models;
using NStLine.Cutting.Services;

namespace NStLine.Cutting.Tests;

public class ReferenceTaskTests
{
    private static CuttingEngine Engine => new();

    [Fact]
    public void SinglePart_ThreeCopies_FitsInOneBar()
    {
        var task = Task(6000, 3,
            ("P", 1000, 3));

        var result = Engine.Solve(task, Options());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(1, result.TotalBarCount);
        Assert.Equal(2991, result.TotalRemainder, 3);
    }

    [Fact]
    public void IdenticalParts_FiveCopies_RequireTwoBars()
    {
        // 5 * 1200 + 5 * 3 = 6015 > 6000
        var task = Task(6000, 3,
            ("P", 1200, 5));

        var result = Engine.Solve(task, Options());

        Assert.Equal(2, result.TotalBarCount);
    }

    [Fact]
    public void MixedParts_FitInTwoBars()
    {
        var task = Task(6000, 3,
            ("A", 2400, 2),
            ("B", 1800, 2),
            ("C", 1200, 1));

        var result = Engine.Solve(task, Options());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(2, result.TotalBarCount);
    }

    [Fact]
    public void GreedyFailure_BetterThanObvious()
    {
        // Items cannot be mixed (600 + 1000 > 1500), so each L needs its own bar
        // and the four S items need two bars. Optimal is 5 bars.
        var task = Task(1500, 0,
            ("S", 600, 4),
            ("L", 1000, 3));

        var result = Engine.Solve(task, Options());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(5, result.TotalBarCount);
    }

    [Fact]
    public void CutWidth_ChangesResult()
    {
        // 2 parts of 3000 with cut width 10 in 6000 bar.
        // Without cut they would fit in one bar; with cut they need two.
        var task = Task(6000, 10,
            ("P", 3000, 2));

        var result = Engine.Solve(task, Options());

        Assert.Equal(2, result.TotalBarCount);
    }

    [Fact]
    public void PartExactlyFitsWithCut_ZeroRemainder()
    {
        var task = Task(6000, 3,
            ("P", 5997, 1));

        var result = Engine.Solve(task, Options());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(1, result.TotalBarCount);
        Assert.Equal(0, result.TotalRemainder, 3);
    }

    [Fact]
    public void PartLongerThanBar_Throws()
    {
        var task = Task(6000, 3,
            ("P", 7000, 1));

        Assert.Throws<ArgumentException>(() => Engine.Solve(task, Options()));
    }

    [Theory]
    [InlineData(double.NaN, 3, 1000)]
    [InlineData(double.PositiveInfinity, 3, 1000)]
    [InlineData(6000, double.NaN, 1000)]
    [InlineData(6000, double.PositiveInfinity, 1000)]
    [InlineData(6000, 3, double.NaN)]
    public void NonFiniteDimensions_AreRejected(double stock, double kerf, double length)
    {
        var task = Task(stock, kerf, ("P", length, 1));

        Assert.Throws<ArgumentException>(() => Engine.Solve(task, Options()));
    }

    [Fact]
    public void ReferenceExample_FromConcept()
    {
        var task = Task(6000, 3,
            ("Стойка", 1250, 10),
            ("Перемычка", 850, 20),
            ("Основание", 2400, 6));

        var result = Engine.Solve(task, Options());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.Equal(8, result.TotalBarCount);
    }

    [Fact]
    public void UnnamedParts_DoesNotCrash()
    {
        var task = Task(6000, 3,
            ("", 1000, 2),
            ("", 850, 3),
            ("", 1200, 1),
            ("", 1500, 2),
            ("", 500, 4));

        var result = Engine.Solve(task, Options());

        Assert.True(result.TotalBarCount > 0);
    }

    [Fact]
    public void ManySmallParts_UsesFallbackOrCompletes()
    {
        var task = Task(6000, 3,
            ("P", 100, 200));

        var options = Options();
        options.MaxPatternCount = 5000;

        var result = Engine.Solve(task, options);

        Assert.True(result.TotalBarCount >= 4);
        Assert.True(result.UtilizationPercent > 0);
    }

    [Fact]
    public void PatternLimitReached_SetsBestFoundReason()
    {
        var task = Task(6000, 3,
            ("A", 1000, 3),
            ("B", 850, 2));

        var options = Options();
        options.MaxPatternCount = 0;

        var result = Engine.Solve(task, options);

        Assert.Equal(CalculationStatus.BestFound, result.Status);
        Assert.Equal(BestFoundReason.PatternLimitReached, result.BestFoundReason);
    }

    [Fact]
    public void TimeLimitReached_SetsBestFoundReason()
    {
        var task = Task(6000, 3,
            ("P1", 920, 35),
            ("P2", 760, 40),
            ("P3", 510, 28),
            ("P4", 450, 22),
            ("P5", 310, 50));

        var options = Options();
        options.TimeLimitSeconds = 0.001;
        options.MaxPatternCount = 1_000_000;

        var result = Engine.Solve(task, options);

        Assert.Equal(CalculationStatus.BestFound, result.Status);
        Assert.Equal(BestFoundReason.TimeLimitReached, result.BestFoundReason);
    }

    private static CuttingTask Task(double barLength, double cutWidth, params (string Name, double Length, int Quantity)[] parts)
    {
        return new CuttingTask
        {
            Material = new LinearMaterial { Name = "Steel", StandardLength = barLength },
            BarLength = barLength,
            CutWidth = cutWidth,
            Parts = parts.Select(p => new PartRequirement { Name = p.Name, Length = p.Length, Quantity = p.Quantity }).ToList()
        };
    }

    private static CuttingOptions Options()
    {
        return new CuttingOptions
        {
            TimeLimitSeconds = 10.0,
            MaxPatternCount = 100_000,
            ToleranceMm = 0.01
        };
    }
}
