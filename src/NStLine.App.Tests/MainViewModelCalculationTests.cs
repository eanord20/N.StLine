using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Contracts;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MainViewModelCalculationTests
{
    [Fact]
    public async Task CalculateAsync_OpensIndependentResultSnapshot()
    {
        var material = new LinearMaterial
        {
            Id = "material",
            Name = "Tube",
            StandardLength = 6000,
            CutWidth = 3
        };
        var engine = new RecordingProjectCuttingEngine();
        var windows = new RecordingResultWindowService();
        var viewModel = new MainViewModel(
            engine,
            new MemoryMaterialsLibraryService(material),
            null,
            new MemorySettingsService(),
            windows);
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Length = 1000;
        row.Quantity = 2;

        await viewModel.CalculateAsync();

        Assert.NotNull(windows.Task);
        Assert.NotNull(windows.Result);
        Assert.Equal("material", Assert.Single(windows.Task!.Parts).MaterialId);
    }

    private sealed class RecordingProjectCuttingEngine : IProjectCuttingEngine
    {
        public ProjectCuttingResult Solve(ProjectCuttingTask task, CuttingOptions options, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var material = Assert.Single(task.Materials);
            var cuttingTask = new CuttingTask
            {
                Material = material,
                BarLength = material.StandardLength,
                CutWidth = material.CutWidth,
                Parts = task.Parts
            };
            return new ProjectCuttingResult
            {
                Materials =
                [
                    new MaterialCuttingResult
                    {
                        Material = material,
                        Task = cuttingTask,
                        Result = new CuttingResult { Status = CalculationStatus.Optimal }
                    }
                ]
            };
        }
    }

    private sealed class RecordingResultWindowService : IResultWindowService
    {
        public ProjectCuttingTask? Task { get; private set; }
        public ProjectCuttingResult? Result { get; private set; }

        public void Show(ProjectCuttingTask task, ProjectCuttingResult result)
        {
            Task = task;
            Result = result;
        }
    }

    private sealed class MemoryMaterialsLibraryService : IMaterialsLibraryService
    {
        private readonly LinearMaterial _material;
        public MemoryMaterialsLibraryService(LinearMaterial material) => _material = material;
        public IReadOnlyList<LinearMaterial> LoadAll() => [_material];
        public void SaveAll(IEnumerable<LinearMaterial> materials) { }
    }

    private sealed class MemorySettingsService : IAppSettingsService
    {
        public AppSettings Load() => new();
        public void Save(AppSettings settings) { }
    }
}
