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

        try
        {
            var materials = JsonSerializer.Deserialize<List<LinearMaterial>>(File.ReadAllText(_filePath), _jsonOptions);
            if (materials == null || materials.Any(material => material == null))
                throw new InvalidDataException(_filePath);
            return materials;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{_filePath}: {ex.Message}", ex);
        }
    }

    public void SaveAll(IEnumerable<LinearMaterial> materials)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(materials.ToList(), _jsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public static IReadOnlyList<LinearMaterial> LoadForImport(string path)
    {
        var materials = JsonSerializer.Deserialize<List<LinearMaterial>>(File.ReadAllText(path))
            ?? throw new InvalidDataException(LocalizationService.Instance.ErrorInvalidMaterialLibrary);
        Validate(materials);
        return materials;
    }

    public static void Validate(IEnumerable<LinearMaterial> materials)
    {
        if (materials.Any(material => material == null || string.IsNullOrWhiteSpace(material.Name) ||
            !double.IsFinite(material.StandardLength) || material.StandardLength <= 0 ||
            !double.IsFinite(material.CutWidth) || material.CutWidth < 0))
            throw new InvalidDataException(LocalizationService.Instance.ErrorInvalidMaterialLibrary);
    }

    private static string GetDefaultFilePath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(folder, AppConstants.ApplicationName, AppConstants.MaterialLibraryFileName);
    }
}
