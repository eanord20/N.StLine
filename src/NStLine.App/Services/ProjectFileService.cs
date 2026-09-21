using System.IO;
using System.Text.Json;
using NStLine.App.Models;
using NStLine.Core.Models;

namespace NStLine.App.Services;

public sealed class ProjectFileService : IProjectFileService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public void Save(string path, ProjectData data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(path, json);
    }

    public ProjectData Load(string path)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<ProjectData>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Project file is empty or invalid.");

        MigrateLegacyProject(data);
        return data;
    }

    private static void MigrateLegacyProject(ProjectData data)
    {
        if (data.Materials.Count == 0 && data.Material != null)
        {
            var material = new LinearMaterial
            {
                Id = data.Material.Id,
                Name = data.Material.Name,
                StandardLength = data.BarLength ?? data.Material.StandardLength,
                CutWidth = data.CutWidth ?? data.Material.CutWidth
            };
            data.Materials.Add(material);
        }

        if (data.Materials.Count == 1)
        {
            foreach (var part in data.Parts.Where(x => string.IsNullOrWhiteSpace(x.MaterialId)))
                part.MaterialId = data.Materials[0].Id;
        }

        data.Material = null;
        data.BarLength = null;
        data.CutWidth = null;
    }
}
