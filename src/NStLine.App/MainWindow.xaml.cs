using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using NStLine.App.ViewModels;

namespace NStLine.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void PartsGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        ViewModel?.BeginPartEdit();
    }

    private void PartsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel)
        {
            ViewModel?.CancelPartEdit();
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => ViewModel?.CommitPartEdit());
    }

    private void PartsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.OriginalSource is TextBoxBase textBox && textBox.CanUndo)
                return;

            if (ViewModel?.UndoCommand.CanExecute(null) == true)
            {
                ViewModel.UndoCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Delete && e.OriginalSource is not TextBoxBase && ViewModel?.DeleteRowCommand.CanExecute(null) == true)
        {
            ViewModel.DeleteRowCommand.Execute(null);
            e.Handled = true;
        }
    }
}
