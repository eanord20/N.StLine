using NStLine.Core.Models;

namespace NStLine.App.Services;

public interface IMaterialsLibraryService
{
    IReadOnlyList<LinearMaterial> LoadAll();
    void SaveAll(IEnumerable<LinearMaterial> materials);
}
