using NStLine.App.Models;
using NStLine.App.ViewModels;
using NStLine.Core.Constants;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ReportDialog_UsesCopyAndValidatesFileName()
    {
        var initial = new ReportSettings { FileName = "Original" };
        var viewModel = new ReportSettingsViewModel(initial, null);
        viewModel.FileName = "../invalid";
        Assert.Equal("Original", initial.FileName);
        Assert.False(viewModel.IsValid);
        viewModel.FileName = "Order";
        viewModel.ShowPartNames = false;
        Assert.True(viewModel.IsValid);
        Assert.False(viewModel.ToSettings().ShowPartNames);
        Assert.True(initial.ShowPartNames);
    }

    [Fact]
    public void ReportDialog_PreviewUsesActualResultAndSettings()
    {
        var pattern = new CuttingPattern
        {
            Multiplicity = 4, Remainder = 30,
            Parts = [new PlacedPart { Source = new PartRequirement { Name = "Part 3", Length = 600 } }]
        };
        var result = new ProjectCuttingResult
        {
            Materials = [new MaterialCuttingResult
            {
                Material = new LinearMaterial { Name = "Tube" },
                Task = new CuttingTask { BarLength = 6000 },
                Result = new CuttingResult { Patterns = [pattern] }
            }]
        };
        var viewModel = new ReportSettingsViewModel(new ReportSettings(), result);
        viewModel.ShowPartNames = false;
        viewModel.IncludeMaterial = true;
        Assert.Contains("Tube - 6000", viewModel.PreviewMaterial);
        Assert.Equal("×4", viewModel.PreviewQuantity);
        Assert.DoesNotContain("Part 3", viewModel.PreviewDetails);
        Assert.Contains("600", viewModel.PreviewDetails);
    }

    [Fact]
    public void CalculationDialog_PresetsAndValidationPreserveSourceValues()
    {
        var viewModel = new CalculationSettingsViewModel("custom", 17, 42000);
        Assert.True(viewModel.IsValid);
        viewModel.SelectedExactProfileKey = "fast";
        Assert.Equal(AppConstants.FastCuttingOptions.TimeLimitSeconds, viewModel.ExactTimeLimitSeconds);
        Assert.Equal(AppConstants.FastCuttingOptions.MaxPatternCount, viewModel.ExactMaxPatternCount);
        viewModel.SelectedExactProfileKey = "custom";
        viewModel.ExactTimeLimitSeconds = double.NaN;
        Assert.False(viewModel.IsValid);
        viewModel.ExactTimeLimitSeconds = 17;
        viewModel.ExactMaxPatternCount = 0;
        Assert.False(viewModel.IsValid);
    }
}
