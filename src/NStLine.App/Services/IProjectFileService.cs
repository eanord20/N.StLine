using NStLine.App.Models;

namespace NStLine.App.Services;

public interface IProjectFileService
{
    void Save(string path, ProjectData data);
    ProjectData Load(string path);
}
