using System.IO;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using System.Windows.Markup;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NStLine.App;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (Environment.GetCommandLineArgs().Contains("--inspect-popup"))
        {
            Console.WriteLine(string.Join(Environment.NewLine, typeof(MaterialDesignThemes.Wpf.PopupBox).GetProperties()
                .Where(property => property.Name.Contains("Toggle") || property.Name.Contains("Style"))
                .Select(property => $"{property.Name}: {property.PropertyType}")));
            return;
        }
        var app = new SmokeApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppResources.xaml"));
        var dictionary = new XElement(document.Descendants().First(element => element.Name.LocalName == "ResourceDictionary"));
        foreach (var attribute in document.Root!.Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
            dictionary.Add(new XAttribute(attribute));
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
        Environment.ExitCode = app.Run();
    }
}

internal sealed class SmokeApplication : Application
{
    private readonly string _folder = Path.Combine(Environment.CurrentDirectory, "run-" + Guid.NewGuid().ToString("N"));
    private readonly List<object> _results = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        Directory.CreateDirectory(_folder);
        NStLine.App.MainWindow? window = null;
        var exit = 1;
        try
        {
            if (Environment.GetCommandLineArgs().Contains("--review-ui"))
            {
                await ReviewUi();
                exit = 0;
                return;
            }
            var material = new LinearMaterial { Name = "Desktop test", StandardLength = 6000, CutWidth = 3 };
            var library = new MaterialsLibraryService(Path.Combine(_folder, "materials.json"));
            library.SaveAll([material]);
            var vm = new MainViewModel(new ProjectCuttingEngine(new CuttingEngine()), library,
                new ProjectFileService(), new AppSettingsService(Path.Combine(_folder, "settings.json")));
            vm.AddMaterialCommand.Execute(material);
            window = new NStLine.App.MainWindow { DataContext = vm };
            MainWindow = window;
            window.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            Record("real-window", window.IsVisible && PresentationSource.FromVisual(window) != null);
            await PasteFromExcel(window, vm);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var calculate = FindButton(window, vm.CalculateCommand);
            Record("invalid-input-disables-rendered-calculate-button", !calculate.IsEnabled && vm.Parts.Count == 3);
            _results.Add(new { name = "excel-paste-observed", rows = vm.Parts.Select(row => new { row.Name, row.Length, row.Quantity, row.Error, row.PasteSource }).ToArray() });
            Record("original-paste-text-in-diagnostics", vm.Parts[1].Error!.Contains("abc") &&
                vm.Parts[2].PasteSource != null && vm.Parts[2].Error!.Contains(vm.Parts[2].PasteSource!));
            SaveImage(window, "invalid-input.png");
            vm.Parts[1].Length = 150;
            vm.Parts[2].Quantity = 4;
            await Dispatcher.InvokeAsync(() => CommandManager.InvalidateRequerySuggested(), DispatcherPriority.ApplicationIdle);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Record("corrected-input-enables-rendered-calculate-button", calculate.IsEnabled);
            var peer = new ButtonAutomationPeer(calculate);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (vm.CurrentResult == null && DateTime.UtcNow < deadline)
                await Task.Delay(30);
            Record("rendered-button-invoke-calculates", vm.CurrentResult != null && !vm.IsBusy && !vm.IsCurrentResultStale);
            Record("desktop-result-quantities", vm.CurrentResult!.Materials.Single().Result.Bars.Sum(bar => bar.Parts.Count) == 9);
            await Dispatcher.InvokeAsync(() => CommandManager.InvalidateRequerySuggested(), DispatcherPriority.ApplicationIdle);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Record("rendered-copy-button-enabled-after-calculation", FindButton(window, vm.CopyResultsCommand).IsEnabled);
            var table = Descendants(window).OfType<DataGrid>().First(grid => grid.IsReadOnly && grid.Items.Count > 0);
            _results.Add(new { name = "result-table-widths", width = table.ActualWidth, columns = table.Columns.Select(column => column.ActualWidth).ToArray() });
            Record("cutting-pattern-column-readable", table.Columns[3].ActualWidth >= table.Columns[4].ActualWidth);
            SaveImage(window, "calculated-result.png");
            vm.Parts[0].Length = 1100;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Record("edit-disables-result-export-actions", vm.IsCurrentResultStale && !vm.CopyResultsCommand.CanExecute(null) && !vm.GenerateReportCommand.CanExecute(null));
            _results.Add(new { name = "clipboard-and-excel", status = "PASS: real Excel Range.Copy, system clipboard and rendered WPF Paste Excel button; temporary unsaved workbook closed" });
            exit = 0;
        }
        catch (Exception exception)
        {
            _results.Add(new { name = "failure", error = exception.ToString() });
        }
        finally
        {
            File.WriteAllText(Path.Combine(_folder, "results.json"), JsonSerializer.Serialize(_results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(_folder);
            window?.Close();
            Shutdown(exit);
        }
    }

    private async Task ReviewUi()
    {
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        foreach (var culture in new[] { "en", "ua" })
        {
            var prefix = $"{theme}-{culture}";
            var material = new LinearMaterial { Name = "Desktop test", StandardLength = 6000, CutWidth = 3 };
            var library = new MaterialsLibraryService(Path.Combine(_folder, prefix + "-materials.json"));
            library.SaveAll([material]);
            var settings = new AppSettingsService(Path.Combine(_folder, prefix + "-settings.json"));
            settings.Save(new NStLine.App.Models.AppSettings { Culture = culture, Theme = theme.ToString() });
            var vm = new MainViewModel(new ProjectCuttingEngine(new CuttingEngine()), library, new ProjectFileService(), settings);
            var main = new NStLine.App.MainWindow { DataContext = vm };
            main.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var add = (Button)main.FindName("AddMaterialButton");
            ((IInvokeProvider)new ButtonAutomationPeer(add).GetPattern(PatternInterface.Invoke)!).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var menu = add.ContextMenu;
            Record(prefix + "-material-menu-opens", menu.IsOpen && menu.Items.Count == 1);
            menu.UpdateLayout();
            var item = (MenuItem)menu.ItemContainerGenerator.ContainerFromIndex(0);
            Record(prefix + "-material-command-bound", item.Command != null && item.Command.CanExecute(item.CommandParameter));
            item.Command!.Execute(item.CommandParameter);
            menu.IsOpen = false;
            Record(prefix + "-material-added", vm.ProjectMaterials.Count == 1 && !add.IsEnabled);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveImage(main, prefix + "-main.png");
            var report = new NStLine.App.Views.ReportSettingsWindow(new ReportSettingsViewModel(new NStLine.App.Models.ReportSettings(), null)) { Owner = main };
            report.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveImage(report, prefix + "-report.png");
            report.Close();
            var calculation = new NStLine.App.Views.CalculationSettingsWindow(new CalculationSettingsViewModel("custom", 17, 42000)) { Owner = main };
            calculation.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveImage(calculation, prefix + "-calculation.png");
            calculation.Close();
            var materials = new NStLine.App.Views.MaterialsWindow(new System.Collections.ObjectModel.ObservableCollection<LinearMaterial>([material])) { Owner = main };
            materials.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            SaveImage(materials, prefix + "-materials.png");
            materials.Close();
            main.Close();
        }
    }

    private async Task PasteFromExcel(Window window, MainViewModel vm)
    {
        object? excel = null, books = null, book = null, sheets = null, sheet = null, range = null;
        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application")
                ?? throw new InvalidOperationException("Excel is not installed");
            excel = Activator.CreateInstance(excelType)!;
            ((dynamic)excel).Visible = false;
            ((dynamic)excel).DisplayAlerts = false;
            books = ((dynamic)excel).Workbooks;
            book = ((dynamic)books).Add();
            sheets = ((dynamic)book).Worksheets;
            sheet = ((dynamic)sheets).Item[1];
            range = ((dynamic)sheet).Range["A1:C3"];
            ((dynamic)range).Value2 = new object[,]
            {
                { "Valid", 1000, 2 },
                { "Invalid", "abc", 3 },
                { "Quantity", 200, "2.4" }
            };
            ((dynamic)range).Copy();
            var clipboard = Clipboard.GetText();
            _results.Add(new { name = "excel-test-range-text", text = clipboard });
            Record("real-excel-copied-test-range-to-clipboard", Clipboard.ContainsText() && clipboard.Contains("abc"));
            var paste = FindButton(window, vm.PasteFromClipboardCommand);
            Record("rendered-paste-excel-button-enabled", paste.IsEnabled);
            ((IInvokeProvider)new ButtonAutomationPeer(paste).GetPattern(PatternInterface.Invoke)!).Invoke();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (vm.Parts.Count == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(30);
            Record("real-excel-to-rendered-paste-button", vm.Parts.Count == 3);
            var copiedRows = clipboard.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            Record("excel-invalid-cells-preserved-without-quantity-coercion", vm.Parts[1].PasteSource == copiedRows[1] &&
                vm.Parts[2].PasteSource == copiedRows[2] && vm.Parts[2].Quantity == 0);
        }
        finally
        {
            try
            {
                if (book != null)
                    ((dynamic)book).Close(false);
            }
            finally
            {
                try
                {
                    if (excel != null)
                        ((dynamic)excel).Quit();
                }
                finally
                {
                    foreach (var value in new[] { range, sheet, sheets, book, books, excel })
                        if (value != null && Marshal.IsComObject(value))
                            Marshal.FinalReleaseComObject(value);
                }
            }
        }
    }

    private void Record(string name, bool passed)
    {
        _results.Add(new { name, passed });
        if (!passed)
            throw new InvalidOperationException(name);
    }

    private static Button FindButton(DependencyObject parent, ICommand command)
    {
        return Descendants(parent).OfType<Button>().First(button => ReferenceEquals(button.Command, command));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private void SaveImage(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(_folder, name));
        encoder.Save(output);
    }
}
