using System.IO;
using System.Windows;
using System.Windows.Threading;
using NStLine.App.Services;
using NStLine.Core.Constants;
using NStLine.Cutting.Services;
using Application = System.Windows.Application;

namespace NStLine.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeService.Apply(AppTheme.Light);

        var cuttingEngine = new CuttingEngine();
        var reportService = new ReportService();
        var mainWindow = new MainWindow
        {
            DataContext = new ViewModels.MainViewModel(
                new ProjectCuttingEngine(cuttingEngine),
                new MaterialsLibraryService(),
                new ProjectFileService(),
                new AppSettingsService(),
                reportService)
        };
        mainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogException(ex);
    }

    private static void LogException(Exception exception)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppConstants.ApplicationName);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "crash.log");
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n";
            File.AppendAllText(path, text);
        }
        catch
        {
            // ignore logging failures
        }
    }
}
