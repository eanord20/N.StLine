using System.Reflection;
using System.Windows;
using NStLine.App.Services;

namespace NStLine.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var localization = LocalizationService.Instance;
        VersionText.Text = $"{localization.AboutVersion} {version}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
        => Close();
}
