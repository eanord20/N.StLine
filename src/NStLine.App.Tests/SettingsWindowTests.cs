using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.App.Views;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

namespace NStLine.App.Tests;

[Collection("ReportLocalization")]
public sealed class SettingsWindowTests
{
    [Fact]
    public void Windows_LoadAndLayoutInBothThemesAndLanguages()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousCulture = LocalizationService.Instance.Culture;
            App? app = null;
            var output = new StringWriter();
            var listener = new TextWriterTraceListener(output);
            try
            {
                app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.InitializeComponent();
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
                foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
                foreach (var culture in new[] { "en", "ua" })
                {
                    ThemeService.Apply(theme);
                    LocalizationService.Instance.Culture = culture;
                    var report = new ReportSettingsWindow(new ReportSettingsViewModel(new ReportSettings(), null));
                    Layout(report, 620);
                    Assert.NotNull(report.FindResource("SettingsCheckBoxStyle"));
                    Assert.True(report.FontSize > (double)report.FindResource("DefaultFontSize"));
                    var reportControls = Descendants((DependencyObject)report.Content).ToList();
                    Assert.DoesNotContain(reportControls.OfType<TextBox>(), box =>
                        System.Windows.Data.BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path.Path == "FileName");
                    Assert.All(reportControls.OfType<GroupBox>(), group => Assert.NotNull(group.HeaderTemplate));
                    foreach (var check in reportControls.OfType<CheckBox>())
                    {
                        check.IsChecked = true;
                        check.ApplyTemplate();
                        var mark = Assert.IsType<System.Windows.Shapes.Path>(check.Template.FindName("CheckMark", check));
                        var border = Assert.IsType<Border>(check.Template.FindName("CheckBorder", check));
                        Assert.Equal(Visibility.Visible, mark.Visibility);
                        Assert.NotEqual(((System.Windows.Media.SolidColorBrush)mark.Stroke).Color,
                            ((System.Windows.Media.SolidColorBrush)border.Background).Color);
                    }
                    foreach (var pair in reportControls.OfType<WrapPanel>().Where(panel => panel.Children.OfType<CheckBox>().Count() == 2))
                    {
                        var checks = pair.Children.OfType<CheckBox>().ToArray();
                        Assert.Equal(checks[0].Margin.Top, checks[1].Margin.Top);
                    }
                    report.Close();
                    var calculation = new CalculationSettingsWindow(new CalculationSettingsViewModel("custom", 17, 42000));
                    Layout(calculation, 620);
                    Assert.Equal(report.FontSize, calculation.FontSize);
                    calculation.Close();
                    var materials = new MaterialsWindow(new ObservableCollection<LinearMaterial>());
                    Layout(materials, 650);
                    var materialButtons = Descendants((DependencyObject)materials.Content).OfType<Button>().ToList();
                    var import = Assert.Single(materialButtons.Where(button => Equals(button.Content, LocalizationService.Instance.ButtonImport)));
                    var export = Assert.Single(materialButtons.Where(button => Equals(button.Content, LocalizationService.Instance.ButtonExport)));
                    var ok = Assert.Single(materialButtons.Where(button => Equals(button.Content, LocalizationService.Instance.ButtonOk)));
                    var add = Assert.Single(materialButtons.Where(button => Equals(button.Content, LocalizationService.Instance.ButtonAdd)));
                    Assert.NotEqual(ok.Parent, import.Parent);
                    Assert.Equal(import.Parent, export.Parent);
                    Assert.Equal(HorizontalAlignment.Left, ((FrameworkElement)import.Parent).HorizontalAlignment);
                    Assert.Equal(HorizontalAlignment.Right, ((FrameworkElement)ok.Parent).HorizontalAlignment);
                    Assert.Equal(add.Width, import.Width);
                    Assert.Equal(add.Width, export.Width);
                    materials.Close();

                    var absentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                    var viewModel = new MainViewModel(new ProjectCuttingEngine(new CuttingEngine()),
                        new MaterialsLibraryService(absentPath), null, new AppSettingsService(absentPath));
                    var main = new MainWindow { DataContext = viewModel };
                    ThemeService.Apply(theme);
                    LocalizationService.Instance.Culture = culture;
                    Layout(main, 1100);
                    var buttons = Descendants((DependencyObject)main.Content).OfType<Button>().ToList();
                    var copy = Assert.Single(buttons.Where(button => button.Command == viewModel.CopyResultsCommand));
                    var reportButton = Assert.Single(buttons.Where(button => button.Command == viewModel.GenerateReportCommand));
                    var cancel = Assert.Single(buttons.Where(button => button.Command == viewModel.CancelCalculationCommand));
                    var calculate = Assert.Single(buttons.Where(button => button.Command == viewModel.CalculateCommand));
                    Assert.True(ToolTipService.GetShowOnDisabled(calculate));
                    Assert.Null(calculate.ToolTip);
                    viewModel.AddMaterialCommand.Execute(new LinearMaterial { Name = "Tube", StandardLength = 6000, CutWidth = 3 });
                    viewModel.AddRowCommand.Execute(null);
                    var row = Assert.Single(viewModel.Parts);
                    row.Length = 0;
                    row.Quantity = 1;
                    Layout(main, 1100);
                    Assert.True(row.HasError);
                    Assert.False(viewModel.CalculateCommand.CanExecute(null));
                    Assert.False(string.IsNullOrWhiteSpace(calculate.ToolTip as string));
                    row.Length = 1000;
                    Layout(main, 1100);
                    Assert.False(row.HasError);
                    Assert.True(viewModel.CalculateCommand.CanExecute(null));
                    Assert.Null(calculate.ToolTip);
                    row.Length = 0;
                    viewModel.Parts.Remove(row);
                    Layout(main, 1100);
                    Assert.Null(calculate.ToolTip);
                    var paste = Assert.Single(buttons.Where(button => button.Command == viewModel.PasteFromClipboardCommand));
                    var addMaterial = Assert.IsType<Button>(main.FindName("AddMaterialButton"));
                    Assert.Same(paste.Style, addMaterial.Style);
                    var outputFields = Assert.IsType<Grid>(main.FindName("ReportOutputFields"));
                    Assert.Equal(1, Grid.GetColumn(outputFields));
                    Assert.Equal(0, Grid.GetRow(outputFields));
                    Assert.Equal(2, outputFields.Children.OfType<TextBox>().Count());
                    foreach (var input in outputFields.Children.OfType<TextBox>())
                        Assert.Equal(-4, Assert.IsType<System.Windows.Media.TranslateTransform>(input.RenderTransform).Y);
                    Assert.Equal(2, Grid.GetColumn((UIElement)copy.Parent));
                    Assert.Equal(copy.Parent, reportButton.Parent);
                    Assert.Equal(copy.Parent, cancel.Parent);
                    Assert.True(((Panel)copy.Parent).Children.IndexOf(copy) < ((Panel)copy.Parent).Children.IndexOf(reportButton));
                    Assert.True(((Panel)copy.Parent).Children.IndexOf(reportButton) < ((Panel)copy.Parent).Children.IndexOf(cancel));
                    main.Close();
                }
                listener.Flush();
                Assert.DoesNotContain("System.Windows.Data Error", output.ToString());
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                LocalizationService.Instance.Culture = previousCulture;
                app?.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "WPF layout verification timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Layout(Window window, double width)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, 700));
        content.Arrange(new Rect(0, 0, width, 700));
        content.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
