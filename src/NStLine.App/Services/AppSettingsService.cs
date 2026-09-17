using System.IO;
using System.Text.Json;
using NStLine.App.Models;
using NStLine.Core.Constants;

namespace NStLine.App.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppSettingsService(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultFilePath();
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new AppSettings();

        var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
        return settings ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static string GetDefaultFilePath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(folder, AppConstants.ApplicationName, AppConstants.AppSettingsFileName);
    }
}
