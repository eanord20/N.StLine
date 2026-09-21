using NStLine.App.ViewModels;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class ResultWindowViewModelTests
{
    [Fact]
    public void Constructor_PreservesMaterialOrderAndBuildsCopyText()
    {
        var first = MaterialResult("first", "Tube", "Stand", 1250);
        var second = MaterialResult("second", "Angle", "Brace", 850);
        var task = new ProjectCuttingTask
        {
            Materials = [first.Material, second.Material],
            Parts = [first.Task.Parts[0], second.Task.Parts[0]]
        };
        var result = new ProjectCuttingResult { Materials = [first, second] };

        var viewModel = new ResultWindowViewModel(task, result, null);
        var text = viewModel.BuildCopyText();

        Assert.Equal(["Tube", "Angle"], viewModel.Materials.Select(x => x.Material.Name));
        Assert.Contains("Stand - 1250", text);
        Assert.Contains("Brace - 850", text);
    }

    private static MaterialCuttingResult MaterialResult(string id, string materialName, string partName, double length)
    {
        var material = new LinearMaterial { Id = id, Name = materialName, StandardLength = 6000, CutWidth = 3 };
        var part = new PartRequirement { MaterialId = id, Name = partName, Length = length, Quantity = 1 };
        var bar = new BarCutting
        {
            Index = 1,
            BarLength = 6000,
            Parts = [new PlacedPart { Source = part, CopyIndex = 1 }],
            CutLoss = 3,
            Remainder = 6000 - length - 3
        };
        return new MaterialCuttingResult
        {
            Material = material,
            Task = new CuttingTask { Material = material, BarLength = 6000, CutWidth = 3, Parts = [part] },
            Result = new CuttingResult { Status = CalculationStatus.Optimal, Bars = [bar] }
        };
    }
}
