using System.Windows;
using NStLine.App.ViewModels;

namespace NStLine.App.Views;

public partial class ReportSettingsWindow : Window
{
    public ReportSettingsWindow(ReportSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportSettingsViewModel { IsValid: true })
            DialogResult = true;
    }
}
