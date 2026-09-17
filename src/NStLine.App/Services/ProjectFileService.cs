using System.IO;
using System.Text.Json;
using NStLine.App.Models;

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
        var data = JsonSerializer.Deserialize<ProjectData>(json, _jsonOptions);
        return data ?? throw new InvalidOperationException("Project file is empty or invalid.");
    }
}
