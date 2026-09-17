using System.Windows;
using Application = System.Windows.Application;

namespace NStLine.App.Services;

public enum AppTheme
{
    Light,
    Dark
}

public static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();

        var materialTheme = theme == AppTheme.Dark ? "Dark" : "Light";
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.{materialTheme}.xaml")
        });
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml")
        });

        var appTheme = new ResourceDictionary
        {
            Source = new Uri(theme == AppTheme.Dark
                ? "/AppThemeDark.xaml"
                : "/AppTheme.xaml", UriKind.Relative)
        };
        dictionaries.Add(appTheme);
    }

    public static bool TryParse(string? value, out AppTheme theme)
    {
        if (Enum.TryParse<AppTheme>(value, true, out var parsed))
        {
            theme = parsed;
            return true;
        }

        theme = AppTheme.Light;
        return false;
    }
}
