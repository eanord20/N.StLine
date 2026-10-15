using System.Windows.Data;
using System.Windows.Threading;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Cutting.Services;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MainViewModelMaterialTests
{
    [Fact]
    public void NewRowsBelongToActiveMaterialAndRemainInSingleList()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        var viewModel = CreateViewModel(first, second);

        viewModel.AddMaterialCommand.Execute(first);
        viewModel.AddRowCommand.Execute(null);
        viewModel.AddMaterialCommand.Execute(second);
        viewModel.AddRowCommand.Execute(null);

        Assert.Equal("second", viewModel.ActiveMaterialId);
        Assert.Equal(["first", "second"], viewModel.Parts.Select(row => row.MaterialId));
        Assert.Equal([1, 1], viewModel.Parts.Select(row => row.RowNumber));
        Assert.Equal(["first", "second"], viewModel.PartsView.Groups!.Cast<CollectionViewGroup>().Select(group => group.Name));

        viewModel.SetActiveMaterialCommand.Execute("first");

        Assert.Equal(2, viewModel.Parts.Count);
    }

    [Fact]
    public void ChangingMaterialMovesRowToItsSectionAndRenumbersRows()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        var viewModel = CreateViewModel(first, second);
        viewModel.AddMaterialCommand.Execute(first);
        viewModel.AddRowCommand.Execute(null);
        viewModel.AddMaterialCommand.Execute(second);
        viewModel.AddRowCommand.Execute(null);
        var movedRow = viewModel.Parts[1];

        viewModel.BeginPartEdit();
        movedRow.MaterialId = "first";
        viewModel.CommitPartEdit();

        Assert.Single(viewModel.PartsView.Groups!);
        Assert.Equal("first", ((CollectionViewGroup)viewModel.PartsView.Groups![0]).Name);
        Assert.Equal([1, 2], viewModel.Parts.Where(row => row.MaterialId == "first").Select(row => row.RowNumber));
    }

    [Fact]
    public void GroupHeaderCanActivateMaterialByName()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        var viewModel = CreateViewModel(first, second);
        viewModel.AddMaterialCommand.Execute(first);
        viewModel.AddMaterialCommand.Execute(second);

        viewModel.SetActiveMaterialCommand.Execute("Tube");

        Assert.Equal("first", viewModel.ActiveMaterialId);
    }

    [Fact]
    public void ChangingMaterialRevalidatesPartAgainstNewBarLength()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        second.StandardLength = 900;
        var viewModel = CreateViewModel(first, second);
        viewModel.AddMaterialCommand.Execute(first);
        viewModel.AddMaterialCommand.Execute(second);
        viewModel.SetActiveMaterialCommand.Execute("first");
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Length = 1000;

        row.MaterialId = "second";

        Assert.True(row.HasError);
        Assert.False(viewModel.CalculateCommand.CanExecute(null));
    }

    [Fact]
    public void EmptyNewRowIsInvalidUntilLengthIsEntered()
    {
        var material = Material("first", "Tube");
        var viewModel = CreateViewModel(material);
        viewModel.AddMaterialCommand.Execute(material);

        viewModel.AddRowCommand.Execute(null);

        Assert.True(Assert.Single(viewModel.Parts).HasError);
        Assert.False(viewModel.CalculateCommand.CanExecute(null));
    }

    [Fact]
    public void OversizedLengthEditedInTable_BlocksCalculation()
    {
        var material = Material("first", "Tube");
        var viewModel = CreateViewModel(material);
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);

        var row = Assert.Single(viewModel.Parts);
        row.Length = material.StandardLength;

        Assert.True(row.HasError);
        Assert.False(viewModel.CalculateCommand.CanExecute(null));
    }

    [Fact]
    public void QuantityEditedToZero_BlocksCalculationUntilCorrected()
    {
        var material = Material("first", "Tube");
        var viewModel = CreateViewModel(material);
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Length = 1000;

        row.Quantity = 0;

        Assert.True(row.HasError);
        Assert.False(viewModel.CalculateCommand.CanExecute(null));
        row.Quantity = 2;
        Assert.False(row.HasError);
        Assert.True(viewModel.CalculateCommand.CanExecute(null));
    }

    [Fact]
    public void NonFinitePartLength_BlocksCalculation()
    {
        var material = Material("first", "Tube");
        var viewModel = CreateViewModel(material);
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);

        var row = Assert.Single(viewModel.Parts);
        row.Length = double.NaN;

        Assert.True(row.HasError);
        Assert.False(viewModel.CalculateCommand.CanExecute(null));
    }

    [Fact]
    public void AvailableLibraryMaterialsExcludeProjectMaterials()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        var viewModel = CreateViewModel(first, second);

        viewModel.AddMaterialCommand.Execute(first);

        var available = viewModel.AvailableLibraryMaterials.ToList();
        Assert.DoesNotContain(available, x => x.Id == first.Id);
        Assert.Contains(available, x => x.Id == second.Id);
    }

    [Fact]
    public void CommitDuringActiveViewEditDefersRefreshWithoutCrashing()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        var viewModel = CreateViewModel(first, second);
        viewModel.AddMaterialCommand.Execute(first);
        viewModel.AddRowCommand.Execute(null);
        viewModel.AddMaterialCommand.Execute(second);
        viewModel.AddRowCommand.Execute(null);
        var movedRow = viewModel.Parts[1];

        viewModel.BeginPartEdit();
        movedRow.MaterialId = "first";
        viewModel.PartsView.EditItem(movedRow);

        viewModel.CommitPartEdit();

        viewModel.PartsView.CommitEdit();
        PumpDispatcher();

        var groups = viewModel.PartsView.Groups!.Cast<CollectionViewGroup>().ToList();
        Assert.Single(groups);
        Assert.Equal("first", groups[0].Name);
        Assert.Equal([1, 2], viewModel.Parts.Where(row => row.MaterialId == "first").Select(row => row.RowNumber));
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static MainViewModel CreateViewModel(params LinearMaterial[] materials)
        => new(
            new ProjectCuttingEngine(new CuttingEngine()),
            new MemoryMaterialsLibraryService(materials),
            null,
            new MemorySettingsService());

    private static LinearMaterial Material(string id, string name)
        => new() { Id = id, Name = name, StandardLength = 6000, CutWidth = 3 };

    private sealed class MemoryMaterialsLibraryService : IMaterialsLibraryService
    {
        private readonly IReadOnlyList<LinearMaterial> _materials;

        public MemoryMaterialsLibraryService(IReadOnlyList<LinearMaterial> materials)
        {
            _materials = materials;
        }

        public IReadOnlyList<LinearMaterial> LoadAll() => _materials;
        public void SaveAll(IEnumerable<LinearMaterial> materials) { }
    }

    private sealed class MemorySettingsService : IAppSettingsService
    {
        public AppSettings Load() => new();
        public void Save(AppSettings settings) { }
    }
}
