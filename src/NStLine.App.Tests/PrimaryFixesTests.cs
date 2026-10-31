using System.IO;
using System.Reflection;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Contracts;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

namespace NStLine.App.Tests;

public sealed class PrimaryFixesTests
{
    [Fact]
    public void Paste_PreservesInvalidRowsAndOriginalInputUntilCorrection()
    {
        var vm = Create();
        Invoke(vm, "PasteText", "A\t100\t2\nB\tabc\t3\nC\t200\t2.4\nD\t300\t0");

        Assert.Equal(4, vm.Parts.Count);
        Assert.False(vm.Parts[0].HasError);
        Assert.Contains("abc", vm.Parts[1].Error);
        Assert.Contains("2.4", vm.Parts[2].Error);
        Assert.Equal(0, vm.Parts[3].Quantity);
        Assert.False(vm.CalculateCommand.CanExecute(null));
        Assert.False(vm.SaveProjectCommand.CanExecute(null));
        vm.Parts[1].Name = "Renamed";
        Assert.True(vm.Parts[1].HasError);
        vm.Parts[1].Length = 150;
        vm.Parts[2].Quantity = 4;
        vm.Parts[3].Quantity = 1;
        Assert.True(vm.CalculateCommand.CanExecute(null));
        Assert.All(vm.Parts, row => Assert.False(row.HasError));
    }

    [Fact]
    public void IncreasingMaximumQuantity_DoesNotOverflowOrAddUndoSnapshot()
    {
        var vm = Create();
        vm.PasteText($"A\t1000\t{int.MaxValue}");
        var row = Assert.Single(vm.Parts);

        vm.IncreaseQuantityCommand.Execute(row);

        Assert.Equal(int.MaxValue, row.Quantity);
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.Parts);
    }

    [Fact]
    public void Paste_UndoRestoresOriginalInvalidInput()
    {
        var vm = Create();
        Invoke(vm, "PasteText", "A\tabc\t2");
        Invoke(vm, "PasteText", "B\t100\t3");
        vm.UndoCommand.Execute(null);

        var row = Assert.Single(vm.Parts);
        Assert.Contains("abc", row.Error);
        Assert.False(vm.CalculateCommand.CanExecute(null));
        vm.UndoCommand.Execute(null);
        Assert.Empty(vm.Parts);
    }

    [Fact]
    public async Task EditingDuringCalculation_DoesNotPublishSnapshotAsCurrent()
    {
        var engine = new GatedEngine();
        var vm = Create(engine);
        Invoke(vm, "PasteText", "A\t1000\t1");
        var work = vm.CalculateAsync();
        try
        {
            Assert.True(engine.Started.Wait(TimeSpan.FromSeconds(3)));
            vm.Parts[0].Length = 2000;
        }
        finally { engine.Release.Set(); }
        await work;

        Assert.Equal(1000, Assert.Single(vm.CurrentTask!.Parts).Length);
        Assert.True(vm.IsCurrentResultStale);
        Assert.False(vm.CopyResultsCommand.CanExecute(null));
        Assert.False(vm.GenerateReportCommand.CanExecute(null));
        await vm.CalculateAsync();
        Assert.False(vm.IsCurrentResultStale);
        Assert.Equal(2000, Assert.Single(vm.CurrentTask!.Parts).Length);
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("materials.json")]
    public void CorruptStartupFiles_AllowOpeningWithoutOverwritingOriginal(string damaged)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, damaged);
            File.WriteAllText(path, "{broken");
            var vm = new MainViewModel(new ProjectCuttingEngine(new CuttingEngine()),
                new MaterialsLibraryService(Path.Combine(folder, "materials.json")), new ProjectFileService(),
                new AppSettingsService(Path.Combine(folder, "settings.json")));

            Assert.Contains(path, vm.StatusText);
            vm.ReportOutputPath = folder;
            Assert.Equal("{broken", File.ReadAllText(path));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"Materials\":null,\"Parts\":[]}")]
    [InlineData("{\"Materials\":[],\"Parts\":[null]}")]
    public async Task FailedProjectOpen_PreservesTaskResultAndSourceFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nstline");
        try
        {
            var vm = Create();
            Invoke(vm, "PasteText", "A\t1000\t1");
            await vm.CalculateAsync();
            var result = vm.CurrentResult;
            File.WriteAllText(path, contents);

            Assert.Equal(false, Invoke(vm, "TryOpenProject", path));
            Assert.Same(result, vm.CurrentResult);
            Assert.False(vm.IsCurrentResultStale);
            Assert.Equal(1000, Assert.Single(vm.Parts).Length);
            Assert.Contains(path, vm.StatusText);
            Assert.Equal(contents, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    private static object? Invoke(MainViewModel vm, string name, string argument)
    {
        var method = typeof(MainViewModel).GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        return method.Invoke(vm, [argument]);
    }

    private static MainViewModel Create(IProjectCuttingEngine? engine = null)
    {
        var material = new LinearMaterial { Id = "m", Name = "M", StandardLength = 6000, CutWidth = 3 };
        var vm = new MainViewModel(engine ?? new ProjectCuttingEngine(new CuttingEngine()),
            new MemoryLibrary(material), new ProjectFileService(), new MemorySettings());
        vm.AddMaterialCommand.Execute(material);
        return vm;
    }

    private sealed class MemoryLibrary(LinearMaterial material) : IMaterialsLibraryService
    {
        public IReadOnlyList<LinearMaterial> LoadAll() => [material];
        public void SaveAll(IEnumerable<LinearMaterial> materials) { }
    }

    private sealed class MemorySettings : IAppSettingsService
    {
        public AppSettings Load() => new() { Culture = "en" };
        public void Save(AppSettings settings) { }
    }

    private sealed class GatedEngine : IProjectCuttingEngine
    {
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public ProjectCuttingResult Solve(ProjectCuttingTask task, CuttingOptions options,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Started.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            return new ProjectCuttingEngine(new CuttingEngine()).Solve(task, options, progress, cancellationToken);
        }
    }
}
