using System.Text.Json;
using Microsoft.Data.Sqlite;
using PSNav.Infrastructure.Database;

namespace PSNav.Repositories.Settings;

public sealed class SettingsRepository(IDatabaseService databaseService) : ISettingsRepository
{
    public string? GetString(string key)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);

        return command.ExecuteScalar() as string;
    }

    public void SetString(string key, string value)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Settings (Key, Value, UpdatedAt)
            VALUES (@key, @value, unixepoch())
            ON CONFLICT (Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAt = excluded.UpdatedAt
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    public T? Get<T>(string key)
    {
        string? json = GetString(key);
        if (json is null) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void Set<T>(string key, T value) => SetString(key, JsonSerializer.Serialize(value));
}
