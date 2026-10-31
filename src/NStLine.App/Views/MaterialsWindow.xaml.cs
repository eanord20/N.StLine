using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Constants;
using NStLine.Core.Models;
using MessageBox = System.Windows.MessageBox;

namespace NStLine.App.Views;

public partial class MaterialsWindow : Window
{
    public MaterialsViewModel ViewModel { get; }

    public MaterialsWindow(ObservableCollection<LinearMaterial> materials)
    {
        InitializeComponent();
        ViewModel = new MaterialsViewModel(materials);
        DataContext = ViewModel;
    }

    private bool CommitEdit()
        => MaterialsGrid.CommitEdit(DataGridEditingUnit.Cell, true) &&
           MaterialsGrid.CommitEdit(DataGridEditingUnit.Row, true);

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitEdit())
            return;
        var localization = LocalizationService.Instance;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = localization.MaterialLibraryFilter };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            var counts = ViewModel.ImportMaterials(MaterialsLibraryService.LoadForImport(dialog.FileName));
            TransferStatus.Text = string.Format(localization.StatusMaterialsImported, counts.Added, counts.Skipped);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, localization.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitEdit())
            return;
        var localization = LocalizationService.Instance;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = localization.MaterialLibraryFilter,
            FileName = AppConstants.MaterialLibraryFileName,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            MaterialsLibraryService.Validate(ViewModel.Materials);
            new MaterialsLibraryService(dialog.FileName).SaveAll(ViewModel.Materials);
            TransferStatus.Text = string.Format(localization.StatusMaterialsExported, dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, localization.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CommitEdit())
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
