using System.Diagnostics;
using System.Reflection;
using System.Windows;
using NStLine.App.Services;
using NStLine.Core.Constants;

namespace NStLine.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var localization = LocalizationService.Instance;
        VersionText.Text = $"{localization.AboutVersion} {version?.ToString(3)}";
    }

    private void License_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppConstants.LicenseUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            var localization = LocalizationService.Instance;
            System.Windows.MessageBox.Show(
                this,
                string.Format(localization.ErrorOpenLicense, ex.Message),
                localization.ErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
        => Close();
}
