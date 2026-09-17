using System.Windows;
using NStLine.App.Services;
using NStLine.Cutting.Services;
using Application = System.Windows.Application;

namespace NStLine.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeService.Apply(AppTheme.Light);

        var mainWindow = new MainWindow
        {
            DataContext = new ViewModels.MainViewModel(new Cutting.Services.CuttingEngine())
        };
        mainWindow.Show();
    }
}
