using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfTextBox = System.Windows.Controls.TextBox;
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

    private void PartsGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (PartsGrid.CurrentColumn != QuantityColumn ||
            ViewModel?.SelectedPart is not { } row ||
            e.OriginalSource is TextBoxBase ||
            e.Text.Length != 1 ||
            e.Text[0] is < '0' or > '9')
            return;

        ViewModel.BeginPartEdit();
        ViewModel.ReplaceQuantityFromKeyboard(row, e.Text[0] - '0');
        PartsGrid.BeginEdit();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var content = QuantityColumn.GetCellContent(row);
            var editor = FindVisualChild<WpfTextBox>(content);
            if (editor == null)
                return;

            editor.Focus();
            editor.CaretIndex = editor.Text.Length;
        });
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null)
            return null;

        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }

        return null;
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
