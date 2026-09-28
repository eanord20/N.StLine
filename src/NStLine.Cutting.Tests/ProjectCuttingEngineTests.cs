using NStLine.Core.Models;
using NStLine.Cutting.Services;

namespace NStLine.Cutting.Tests;

public sealed class ProjectCuttingEngineTests
{
    [Fact]
    public void Solve_SeparatesMaterialsAndAggregatesTotals()
    {
        var first = Material("first", "Tube", 6000, 3);
        var second = Material("second", "Angle", 3000, 2);
        var task = new ProjectCuttingTask
        {
            Materials = [first, second],
            Parts =
            [
                Part("A", 1000, 3, first.Id),
                Part("B", 700, 2, second.Id)
            ]
        };

        var result = new ProjectCuttingEngine(new CuttingEngine()).Solve(task, Options());

        Assert.Equal(2, result.Materials.Count);
        Assert.Equal(first.Id, result.Materials[0].Material.Id);
        Assert.All(result.Materials[0].Result.Bars.SelectMany(x => x.Parts), x => Assert.Equal(first.Id, x.Source.MaterialId));
        Assert.All(result.Materials[1].Result.Bars.SelectMany(x => x.Parts), x => Assert.Equal(second.Id, x.Source.MaterialId));
        Assert.Equal(result.Materials.Sum(x => x.Result.TotalBarCount), result.TotalBarCount);
        Assert.Equal(result.Materials.Sum(x => x.Result.TotalRemainder), result.TotalRemainder, 6);
        Assert.Equal(CalculationStatus.Optimal, result.Status);
    }

    [Fact]
    public void Solve_IsBestFoundWhenAnyMaterialIsNotProvenOptimal()
    {
        var material = Material("first", "Tube", 6000, 3);
        var task = new ProjectCuttingTask
        {
            Materials = [material],
            Parts = [Part("A", 1000, 3, material.Id)]
        };
        var options = Options();
        options.MaxPatternCount = 0;

        var result = new ProjectCuttingEngine(new CuttingEngine()).Solve(task, options);

        Assert.Equal(CalculationStatus.BestFound, result.Status);
    }

    [Fact]
    public void Solve_RejectsPartWithUnknownMaterial()
    {
        var material = Material("first", "Tube", 6000, 3);
        var task = new ProjectCuttingTask
        {
            Materials = [material],
            Parts = [Part("A", 1000, 1, "missing")]
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new ProjectCuttingEngine(new CuttingEngine()).Solve(task, Options()));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Solve_HeuristicSeededIncumbentIsMarkedOptimalWhenProven()
    {
        var material = Material("first", "Tube", 6000, 3);
        var task = new ProjectCuttingTask
        {
            Materials = [material],
            Parts =
            [
                Part("A", 1450, 3, material.Id),
                Part("B", 1200, 3, material.Id),
                Part("C", 980, 2, material.Id),
                Part("D", 760, 2, material.Id)
            ]
        };

        var result = new ProjectCuttingEngine(new CuttingEngine()).Solve(task, Options());

        Assert.Equal(CalculationStatus.Optimal, result.Status);
        Assert.True(result.TotalBarCount > 0);
    }

    private static LinearMaterial Material(string id, string name, double length, double cutWidth)
        => new() { Id = id, Name = name, StandardLength = length, CutWidth = cutWidth };

    private static PartRequirement Part(string name, double length, int quantity, string materialId)
        => new() { Name = name, Length = length, Quantity = quantity, MaterialId = materialId };

    private static CuttingOptions Options()
        => new() { TimeLimitSeconds = 5, MaxPatternCount = 100_000, ToleranceMm = 0.01 };
}
