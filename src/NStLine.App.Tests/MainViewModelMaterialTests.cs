using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Cutting.Services;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MainViewModelMaterialTests
{
    [Fact]
    public void NewRowsBelongToActiveMaterialAndActivePartsAreFiltered()
    {
        var first = Material("first", "Tube");
        var second = Material("second", "Angle");
        var viewModel = CreateViewModel(first, second);

        viewModel.AddMaterialCommand.Execute(first);
        viewModel.AddRowCommand.Execute(null);
        viewModel.AddMaterialCommand.Execute(second);
        viewModel.AddRowCommand.Execute(null);

        Assert.Equal("second", viewModel.ActiveMaterialId);
        Assert.Equal("second", Assert.Single(viewModel.ActiveParts).MaterialId);

        viewModel.SetActiveMaterialCommand.Execute("first");

        Assert.Equal("first", Assert.Single(viewModel.ActiveParts).MaterialId);
        Assert.Equal(2, viewModel.Parts.Count);
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
