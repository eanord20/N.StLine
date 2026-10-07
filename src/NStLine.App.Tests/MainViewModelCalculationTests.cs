using System.IO;
using System.Text.RegularExpressions;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Contracts;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MainViewModelCalculationTests
{
    [Fact]
    public async Task CalculateAsyncStoresResultForMainWindowActions()
    {
        var (viewModel, _, _) = CreateViewModel();

        await viewModel.CalculateAsync();

        Assert.NotNull(viewModel.CurrentTask);
        Assert.NotNull(viewModel.CurrentResult);
        Assert.False(viewModel.IsCurrentResultStale);
        Assert.True(viewModel.CopyResultsCommand.CanExecute(null));
    }

    [Fact]
    public async Task EditingPartDisablesResultActionsUntilNextCalculation()
    {
        var (viewModel, _, _) = CreateViewModel();
        var row = Assert.Single(viewModel.Parts);
        await viewModel.CalculateAsync();

        viewModel.BeginPartEdit();
        row.Length = 1200;
        viewModel.CommitPartEdit();

        Assert.True(viewModel.IsCurrentResultStale);
        Assert.False(viewModel.CopyResultsCommand.CanExecute(null));
        Assert.False(viewModel.GenerateReportCommand.CanExecute(null));

        await viewModel.CalculateAsync();

        Assert.False(viewModel.IsCurrentResultStale);
        Assert.True(viewModel.CopyResultsCommand.CanExecute(null));
    }

    [Fact]
    public async Task GenerateReportAsyncUsesPersistedFolderAndTimestampedFileName()
    {
        var outputFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var (viewModel, reports, settings) = CreateViewModel();
        try
        {
            viewModel.ReportOutputPath = outputFolder;
            await viewModel.CalculateAsync();
            await viewModel.GenerateReportAsync();

            Assert.NotNull(reports.OutputPath);
            Assert.Equal(outputFolder, Path.GetDirectoryName(reports.OutputPath));
            Assert.Matches(new Regex(@"^N\.StLine_Report_\d{4}-\d{2}-\d{2}_\d{6}\.docx$"), Path.GetFileName(reports.OutputPath));
            Assert.Equal(outputFolder, settings.Saved?.ReportOutputPath);
            Assert.Equal(reports.OutputPath, viewModel.LastReportPath);
            Assert.True(viewModel.OpenLastReportCommand.CanExecute(null));
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Fact]
    public async Task CalculateAsync_ShowsBestFoundReasonDetails()
    {
        var material = new LinearMaterial
        {
            Id = "material",
            Name = "Tube",
            StandardLength = 6000,
            CutWidth = 3
        };
        var viewModel = new MainViewModel(
            new BestFoundProjectCuttingEngine(),
            new MemoryMaterialsLibraryService(material),
            null,
            new MemorySettingsService(new AppSettings()));
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Length = 1000;

        await viewModel.CalculateAsync();

        Assert.Equal(LocalizationService.Instance.StatusBestFoundTimeLimit, viewModel.CurrentResultStatusDetails);
    }

    private static (MainViewModel ViewModel, RecordingReportService Reports, MemorySettingsService Settings) CreateViewModel(string reportOutputPath = "")
    {
        var material = new LinearMaterial
        {
            Id = "material",
            Name = "Tube",
            StandardLength = 6000,
            CutWidth = 3
        };
        var reports = new RecordingReportService();
        var settings = new MemorySettingsService(new AppSettings { ReportOutputPath = reportOutputPath });
        var viewModel = new MainViewModel(
            new RecordingProjectCuttingEngine(),
            new MemoryMaterialsLibraryService(material),
            null,
            settings,
            reportService: reports);
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);
        row.Length = 1000;
        row.Quantity = 2;
        return (viewModel, reports, settings);
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

    private sealed class BestFoundProjectCuttingEngine : IProjectCuttingEngine
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
                        Result = new CuttingResult
                        {
                            Status = CalculationStatus.BestFound,
                            BestFoundReason = BestFoundReason.TimeLimitReached
                        }
                    }
                ]
            };
        }
    }

    private sealed class RecordingReportService : IReportService
    {
        public string? OutputPath { get; private set; }

        public void GenerateReport(string outputPath, ProjectCuttingTask task, ProjectCuttingResult result)
        {
            OutputPath = outputPath;
            File.WriteAllText(outputPath, string.Empty);
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
        private readonly AppSettings _settings;

        public MemorySettingsService(AppSettings settings) => _settings = settings;

        public AppSettings? Saved { get; private set; }
        public AppSettings Load() => _settings;
        public void Save(AppSettings settings) => Saved = settings;
    }
}
