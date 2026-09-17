using System.Collections.ObjectModel;
using System.Windows;
using NStLine.App.ViewModels;
using NStLine.Core.Models;

namespace NStLine.App.Views;

public partial class MaterialsWindow : Window
{
    public MaterialsViewModel ViewModel { get; }

    public MaterialsWindow(ObservableCollection<LinearMaterial> materials)
    {
        InitializeComponent();
        ViewModel = new MaterialsViewModel(materials);
        DataContext = ViewModel;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MaterialsViewModel.DialogResult) && ViewModel.DialogResult.HasValue)
            {
                DialogResult = ViewModel.DialogResult.Value;
                Close();
            }
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
