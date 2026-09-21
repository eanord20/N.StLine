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
