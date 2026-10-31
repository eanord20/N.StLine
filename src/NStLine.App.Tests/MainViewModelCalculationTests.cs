using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Contracts;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MainViewModelCalculationTests
{
    [Theory]
    [InlineData(1.4, 0.1, 1.3, true)]
    [InlineData(0.6, 0.2, 0.4, true)]
    [InlineData(0.3, 0.2, 0.1, true)]
    [InlineData(1.4, 0.1, 1.3000000001, false)]
    public void DecimalBoundary_ValidationMatchesEngineContract(double stock, double kerf, double length, bool expected)
    {
        var material = new LinearMaterial { Id = "decimal", Name = "Tube", StandardLength = stock, CutWidth = kerf };
        var viewModel = new MainViewModel(new RecordingProjectCuttingEngine(),
            new MemoryMaterialsLibraryService(material), null, new MemorySettingsService(new AppSettings()));
        viewModel.AddMaterialCommand.Execute(material);
        viewModel.AddRowCommand.Execute(null);
        var row = Assert.Single(viewModel.Parts);

        row.Length = length;
        row.Quantity = 1;

        Assert.Equal(expected, viewModel.CalculateCommand.CanExecute(null));
        Assert.Equal(!expected, row.HasError);
    }

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

    [Fact]
    public void AboutDialogAssemblyVersion_IsCurrentRelease()
    {
        var version = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.StartsWith("1.2.0", version);
    }

    [Fact]
    public void SelectingExactProfile_UpdatesLimitsAndPersistsSettings()
    {
        var (viewModel, _, settings) = CreateViewModel();

        viewModel.SelectedExactProfileKey = "fast";

        Assert.Equal("fast", viewModel.SelectedExactProfileKey);
        Assert.Equal(4.0, viewModel.ExactTimeLimitSeconds);
        Assert.Equal(25_000, viewModel.ExactMaxPatternCount);
        Assert.Equal("fast", settings.Saved?.ExactProfileKey);
    }

    [Fact]
    public void PresetLimits_RejectManualChangesUntilCustomSelected()
    {
        var (viewModel, _, settings) = CreateViewModel();
        viewModel.SelectedExactProfileKey = "fast";

        viewModel.ExactTimeLimitSeconds = 17;
        viewModel.ExactMaxPatternCount = 42_000;

        Assert.Equal("fast", viewModel.SelectedExactProfileKey);
        Assert.Equal(4, viewModel.ExactTimeLimitSeconds);
        Assert.Equal(25_000, viewModel.ExactMaxPatternCount);
        Assert.Single(settings.Saves);

        viewModel.SelectedExactProfileKey = "custom";
        viewModel.ExactTimeLimitSeconds = 17;
        viewModel.ExactMaxPatternCount = 42_000;

        Assert.Equal("custom", settings.Saved?.ExactProfileKey);
        Assert.Equal(17, settings.Saved?.ExactTimeLimitSeconds);
        Assert.Equal(42_000, settings.Saved?.ExactMaxPatternCount);
    }

    [Fact]
    public void LoadingCustomLimits_RestoresProfileAndValues()
    {
        var material = new LinearMaterial { Id = "material", Name = "Tube", StandardLength = 6000, CutWidth = 3 };
        var settings = new MemorySettingsService(new AppSettings
        {
            ExactProfileKey = "custom",
            ExactTimeLimitSeconds = 17,
            ExactMaxPatternCount = 42_000
        });

        var viewModel = new MainViewModel(
            new RecordingProjectCuttingEngine(),
            new MemoryMaterialsLibraryService(material),
            null,
            settings);

        Assert.Equal("custom", viewModel.SelectedExactProfileKey);
        Assert.Equal(17, viewModel.ExactTimeLimitSeconds);
        Assert.Equal(42_000, viewModel.ExactMaxPatternCount);
    }

    [Fact]
    public void LegacyDeepProfile_LoadsAsCustomWithoutLosingLimits()
    {
        var material = new LinearMaterial { Id = "material", Name = "Tube", StandardLength = 6000, CutWidth = 3 };
        var settings = new MemorySettingsService(new AppSettings
        {
            ExactProfileKey = "deep",
            ExactTimeLimitSeconds = 90,
            ExactMaxPatternCount = 1_000_000
        });

        var viewModel = new MainViewModel(new RecordingProjectCuttingEngine(),
            new MemoryMaterialsLibraryService(material), null, settings);

        Assert.Equal("custom", viewModel.SelectedExactProfileKey);
        Assert.Equal(90, viewModel.ExactTimeLimitSeconds);
        Assert.Equal(1_000_000, viewModel.ExactMaxPatternCount);
    }

    [Fact]
    public void DeepProfile_CannotBeSelectedAgain()
    {
        var (viewModel, _, settings) = CreateViewModel();

        viewModel.SelectedExactProfileKey = "deep";

        Assert.Equal("balanced", viewModel.SelectedExactProfileKey);
        Assert.Empty(settings.Saves);
    }

    [Fact]
    public void SelectingCustomProfile_LeavesManualLimitsUnchanged()
    {
        var (viewModel, _, _) = CreateViewModel();
        viewModel.SelectedExactProfileKey = "fast";

        viewModel.SelectedExactProfileKey = "custom";

        Assert.Equal("custom", viewModel.SelectedExactProfileKey);
        Assert.Equal(4, viewModel.ExactTimeLimitSeconds);
        Assert.Equal(25_000, viewModel.ExactMaxPatternCount);
    }

    [Fact]
    public void MissingSelection_DoesNotResetActiveProfile()
    {
        var (viewModel, _, settings) = CreateViewModel();
        viewModel.SelectedExactProfileKey = "fast";

        viewModel.SelectedExactProfileKey = null!;

        Assert.Equal("fast", viewModel.SelectedExactProfileKey);
        Assert.Equal(4, viewModel.ExactTimeLimitSeconds);
        Assert.Single(settings.Saves);
    }

    [Fact]
    public async Task ReportOptions_ArePersistedAndPassedAsSnapshot()
    {
        var outputFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var (viewModel, reports, settings) = CreateViewModel();
        try
        {
            var options = new ReportSettings { FileName = "Order", TimestampPosition = ReportTimestampPosition.None, ShowPartNames = false };
            viewModel.ApplyReportSettings(options);
            options.FileName = "Mutated";
            viewModel.ReportOutputPath = outputFolder;
            await viewModel.CalculateAsync();
            await viewModel.GenerateReportAsync();

            Assert.Equal("Order.docx", Path.GetFileName(reports.OutputPath));
            Assert.False(reports.Options!.Settings.ShowPartNames);
            Assert.Equal("Order", settings.Saved!.Report.FileName);
            viewModel.ReportFileName = "Next";
            Assert.Equal("Order", reports.Options.Settings.FileName);
            Assert.Equal("Next", settings.Saved.Report.FileName);
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingReport_RequiresConfirmation(bool confirm)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Order.docx");
        File.WriteAllText(path, "Original");
        try
        {
            var material = new LinearMaterial { Id = "material", Name = "Tube", StandardLength = 6000, CutWidth = 3 };
            var reports = new RecordingReportService();
            string? confirmedPath = null;
            var viewModel = new MainViewModel(new RecordingProjectCuttingEngine(), new MemoryMaterialsLibraryService(material),
                null, new MemorySettingsService(new AppSettings { ReportOutputPath = folder }), reports,
                confirmReportOverwrite: existing => { confirmedPath = existing; return confirm; });
            viewModel.ApplyReportSettings(new ReportSettings { FileName = "Order", TimestampPosition = ReportTimestampPosition.None });
            viewModel.AddMaterialCommand.Execute(material);
            viewModel.AddRowCommand.Execute(null);
            viewModel.Parts[0].Length = 1000;
            await viewModel.CalculateAsync();
            await viewModel.GenerateReportAsync();

            Assert.Equal(path, confirmedPath);
            Assert.Equal(confirm ? path : null, viewModel.LastReportPath);
            Assert.Equal(confirm ? "" : "Original", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
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
        public ReportGenerationOptions? Options { get; private set; }

        public void GenerateReport(string outputPath, ProjectCuttingTask task, ProjectCuttingResult result,
            ReportGenerationOptions? options = null)
        {
            OutputPath = outputPath;
            Options = options;
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
        public List<AppSettings> Saves { get; } = new();
        public AppSettings Load() => _settings;
        public void Save(AppSettings settings)
        {
            Saved = settings;
            Saves.Add(settings);
        }
    }
}
