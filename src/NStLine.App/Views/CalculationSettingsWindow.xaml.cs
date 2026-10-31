using System.Windows;
using System.Windows.Controls;
using NStLine.App.ViewModels;

namespace NStLine.App.Views;

public partial class CalculationSettingsWindow : Window
{
    public CalculationSettingsWindow(CalculationSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CalculationSettingsViewModel { IsValid: true } &&
            !Validation.GetHasError(TimeLimitInput) && !Validation.GetHasError(PatternLimitInput))
            DialogResult = true;
    }
}
