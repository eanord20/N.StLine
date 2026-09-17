using NStLine.App.Models;

namespace NStLine.App.Services;

public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
