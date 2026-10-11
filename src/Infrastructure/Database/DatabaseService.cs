using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace PSNav.Infrastructure.Database;

public sealed class DatabaseService : IDatabaseService, IDisposable
{
    private const string DatabaseFileName = "PSNav.db";
    private const string AppDataFolderName = "PSNav";
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private readonly Lock _lock = new();

    public DatabaseService()
    {
        string databasePath = ResolveDatabasePath();
        _connectionString = $"Filename={databasePath}";

        Initialize(_connectionString, databasePath);
    }

    private static string ResolveDatabasePath()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);
        Directory.CreateDirectory(directory);

        string databasePath = Path.Combine(directory, DatabaseFileName);
        MigrateLegacyDatabase(databasePath);
        return databasePath;
    }

    private static void MigrateLegacyDatabase(string databasePath)
    {
        string legacyPath = Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
        if (File.Exists(databasePath) || !File.Exists(legacyPath)) return;

        try
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(legacyPath + suffix))
                {
                    File.Move(legacyPath + suffix, databasePath + suffix);
                }
            }
        }
        catch (IOException)
        {
            // Nothing
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public SqliteConnection GetConnection()
    {
        lock (_lock)
        {
            if (_connection != null && _connection.State != System.Data.ConnectionState.Closed) return _connection;
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            using SqliteCommand command = _connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();

            return _connection;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    private static void Initialize(string connectionString, string databasePath)
    {
        using SqliteConnection connection = new(connectionString);
        connection.Open();

        string? backupPath = null;
        if (DatabaseMigrations.RequiresMigration(connection))
            backupPath = BackupDatabase(connection, databasePath);

        try
        {
            DatabaseMigrations.Initialize(connection);
        }
        catch (Exception exception) when (backupPath is not null)
        {
            throw new InvalidOperationException(
                $"{exception.Message} A backup made before the upgrade was saved to {backupPath}.", exception);
        }

        EntitlementOverrides.Synchronize(connection);
    }

    private static string BackupDatabase(SqliteConnection connection, string databasePath)
    {
        long version = DatabaseMigrations.GetVersion(connection);
        string backupPath = $"{databasePath}.v{version}-{DateTime.Now:yyyyMMddHHmmss}.bak";

        SqliteConnectionStringBuilder backupConnectionString = new()
        {
            DataSource = backupPath,
            Pooling = false
        };
        using SqliteConnection backupConnection = new(backupConnectionString.ToString());
        backupConnection.Open();
        connection.BackupDatabase(backupConnection);

        return backupPath;
    }
}