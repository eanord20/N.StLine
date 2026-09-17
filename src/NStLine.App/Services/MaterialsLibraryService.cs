using System.IO;
using System.Text.Json;
using NStLine.Core.Constants;
using NStLine.Core.Models;

namespace NStLine.App.Services;

public sealed class MaterialsLibraryService : IMaterialsLibraryService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public MaterialsLibraryService(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultFilePath();
    }

    public IReadOnlyList<LinearMaterial> LoadAll()
    {
        if (!File.Exists(_filePath))
            return new List<LinearMaterial>();

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<LinearMaterial>();

        var materials = JsonSerializer.Deserialize<List<LinearMaterial>>(json, _jsonOptions);
        return materials ?? new List<LinearMaterial>();
    }

    public void SaveAll(IEnumerable<LinearMaterial> materials)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(materials.ToList(), _jsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static string GetDefaultFilePath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(folder, AppConstants.ApplicationName, AppConstants.MaterialLibraryFileName);
    }
}
