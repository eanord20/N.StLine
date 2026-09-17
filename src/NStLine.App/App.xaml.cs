using System.Windows;
using NStLine.App.Services;
using Application = System.Windows.Application;

namespace NStLine.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeService.Apply(AppTheme.Light);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
