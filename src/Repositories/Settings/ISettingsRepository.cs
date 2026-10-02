namespace PSNav.Repositories.Settings;

public interface ISettingsRepository
{
    string? GetString(string key);

    void SetString(string key, string value);

    T? Get<T>(string key);

    void Set<T>(string key, T value);
}
