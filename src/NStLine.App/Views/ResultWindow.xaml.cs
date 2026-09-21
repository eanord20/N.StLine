using System.Windows;
using NStLine.App.ViewModels;

namespace NStLine.App.Views;

public partial class ResultWindow : Window
{
    public ResultWindow(ResultWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
