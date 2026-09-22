using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Cutting.Services;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MainViewModelUndoTests
{
    [Fact]
    public void Undo_RestoresRowsAfterAddAndDelete()
    {
        var viewModel = CreateViewModel();
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);

        viewModel.SelectedPart = row;
        viewModel.DeleteRowCommand.Execute(null);
        Assert.Empty(viewModel.Parts);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal("Part 1", Assert.Single(viewModel.Parts).Name);

        viewModel.UndoCommand.Execute(null);
        Assert.Empty(viewModel.Parts);
    }

    [Fact]
    public void Undo_RestoresValueBeforeCellEdit()
    {
        var viewModel = CreateViewModel();
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Length = 1000;
        viewModel.ClearUndoHistory();

        viewModel.BeginPartEdit();
        row.Length = 1250;
        viewModel.CommitPartEdit();
        viewModel.UndoCommand.Execute(null);

        Assert.Equal(1000, Assert.Single(viewModel.Parts).Length);
    }

    [Fact]
    public void QuantityCommandsAreUndoableAndDoNotDecreaseBelowOne()
    {
        var viewModel = CreateViewModel();
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        viewModel.ClearUndoHistory();

        viewModel.DecreaseQuantityCommand.Execute(row);
        Assert.Equal(1, row.Quantity);
        Assert.False(viewModel.UndoCommand.CanExecute(null));

        viewModel.IncreaseQuantityCommand.Execute(row);
        Assert.Equal(2, row.Quantity);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(1, Assert.Single(viewModel.Parts).Quantity);
    }

    [Fact]
    public void DirectQuantityInputReplacesValueAndIsUndoable()
    {
        var viewModel = CreateViewModel();
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Quantity = 25;
        viewModel.ClearUndoHistory();

        viewModel.BeginPartEdit();
        viewModel.ReplaceQuantityFromKeyboard(row, 7);
        viewModel.CommitPartEdit();

        Assert.Equal(7, row.Quantity);
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(25, Assert.Single(viewModel.Parts).Quantity);
    }

    [Fact]
    public void UndoRestoresMaterialChangedDuringCellEdit()
    {
        var viewModel = CreateViewModel();
        var second = new LinearMaterial { Id = "second", Name = "Angle", StandardLength = 6000, CutWidth = 3 };
        viewModel.LibraryMaterials.Add(second);
        viewModel.AddMaterialCommand.Execute(second);
        viewModel.SetActiveMaterialCommand.Execute("material");
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        viewModel.ClearUndoHistory();

        viewModel.BeginPartEdit();
        row.MaterialId = "second";
        viewModel.CommitPartEdit();
        viewModel.UndoCommand.Execute(null);

        Assert.Equal("material", Assert.Single(viewModel.Parts).MaterialId);
    }

    private static MainViewModel CreateViewModel()
    {
        var material = new LinearMaterial
        {
            Id = "material",
            Name = "Tube",
            StandardLength = 6000,
            CutWidth = 3
        };
        var viewModel = new MainViewModel(
            new ProjectCuttingEngine(new CuttingEngine()),
            new MemoryMaterialsLibraryService(material),
            null,
            new MemorySettingsService());
        viewModel.AddMaterialCommand.Execute(material);
        return viewModel;
    }

    private sealed class MemoryMaterialsLibraryService : IMaterialsLibraryService
    {
        private readonly LinearMaterial _material;

        public MemoryMaterialsLibraryService(LinearMaterial material)
        {
            _material = material;
        }

        public IReadOnlyList<LinearMaterial> LoadAll() => [_material];
        public void SaveAll(IEnumerable<LinearMaterial> materials) { }
    }

    private sealed class MemorySettingsService : IAppSettingsService
    {
        public AppSettings Load() => new();
        public void Save(AppSettings settings) { }
    }
}
