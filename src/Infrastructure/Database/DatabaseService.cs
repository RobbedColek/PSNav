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

        Initialize(_connectionString);
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

    private static void Initialize(string connectionString)
    {
        using SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Accounts
            (
                AccountID TEXT NOT NULL PRIMARY KEY,
                OnlineID TEXT NULL,
                ProfilePicture BLOB NULL,
                AvatarPicture BLOB NULL,
                NPSSOToken TEXT NULL,
                NPSSOTokenExpiration INTEGER NULL,
                LastSynchronization INTEGER NULL,
                CreatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
                UpdatedAt INTEGER NOT NULL DEFAULT (unixepoch())
            );

            CREATE TRIGGER IF NOT EXISTS Accounts_SetUpdatedAt
            AFTER UPDATE ON Accounts
            FOR EACH ROW
            WHEN NEW.UpdatedAt = OLD.UpdatedAt
            BEGIN
                UPDATE Accounts
                SET UpdatedAt = unixepoch()
                WHERE AccountID = OLD.AccountID;
            END;

            CREATE TABLE IF NOT EXISTS Entitlements
            (
                EntitlementID TEXT NOT NULL PRIMARY KEY,
                TitleID TEXT NULL,
                Name TEXT NULL,
                Image BLOB NULL,
                ImageUrl TEXT NULL,
                Platform TEXT NULL,
                ReferencePackageUrl TEXT NULL,
                PackageFileSize INTEGER NULL,
                EntitlementType INTEGER NULL,
                PackageType TEXT NULL,
                Type TEXT NULL,
                CreatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
                UpdatedAt INTEGER NOT NULL DEFAULT (unixepoch())
            );

            CREATE TABLE IF NOT EXISTS AccountEntitlements
            (
                AccountID TEXT NOT NULL,
                EntitlementID TEXT NOT NULL,
                ProductID TEXT NULL,
                Membership TEXT NULL,
                IsActive INTEGER NULL,
                PRIMARY KEY (AccountID, EntitlementID),
                FOREIGN KEY (AccountID) REFERENCES Accounts (AccountID) ON DELETE CASCADE,
                FOREIGN KEY (EntitlementID) REFERENCES Entitlements (EntitlementID) ON DELETE CASCADE
            );

            CREATE TRIGGER IF NOT EXISTS Entitlements_SetUpdatedAt
            AFTER UPDATE ON Entitlements
            FOR EACH ROW
            WHEN NEW.UpdatedAt = OLD.UpdatedAt
            BEGIN
                UPDATE Entitlements
                SET UpdatedAt = unixepoch()
                WHERE EntitlementID = OLD.EntitlementID;
            END;

            CREATE TABLE IF NOT EXISTS Consoles
            (
                ConsoleID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                CreatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
                UpdatedAt INTEGER NOT NULL DEFAULT (unixepoch())
            );

            CREATE TRIGGER IF NOT EXISTS Consoles_SetUpdatedAt
            AFTER UPDATE ON Consoles
            FOR EACH ROW
            WHEN NEW.UpdatedAt = OLD.UpdatedAt
            BEGIN
                UPDATE Consoles
                SET UpdatedAt = unixepoch()
                WHERE ConsoleID = OLD.ConsoleID;
            END;

            CREATE TABLE IF NOT EXISTS ConsoleProfiles
            (
                ProfileID INTEGER NOT NULL PRIMARY KEY,
                ConsoleID INTEGER NOT NULL,
                Name TEXT NOT NULL,
                AccountID TEXT NULL,
                CreatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
                UpdatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
                FOREIGN KEY (ConsoleID) REFERENCES Consoles (ConsoleID) ON DELETE CASCADE,
                FOREIGN KEY (AccountID) REFERENCES Accounts (AccountID) ON DELETE SET NULL
            );

            CREATE TRIGGER IF NOT EXISTS ConsoleProfiles_SetUpdatedAt
            AFTER UPDATE ON ConsoleProfiles
            FOR EACH ROW
            WHEN NEW.UpdatedAt = OLD.UpdatedAt
            BEGIN
                UPDATE ConsoleProfiles
                SET UpdatedAt = unixepoch()
                WHERE ProfileID = OLD.ProfileID;
            END;

            CREATE TABLE IF NOT EXISTS ProfileEntitlements
            (
                ProfileID INTEGER NOT NULL,
                EntitlementID TEXT NOT NULL,
                ProductID TEXT NULL,
                Membership TEXT NULL,
                InactiveDate INTEGER NULL,
                RetentionPolicy INTEGER NULL,
                FeatureType INTEGER NULL,
                Branding TEXT NULL,
                PRIMARY KEY (ProfileID, EntitlementID),
                FOREIGN KEY (ProfileID) REFERENCES ConsoleProfiles (ProfileID) ON DELETE CASCADE,
                FOREIGN KEY (EntitlementID) REFERENCES Entitlements (EntitlementID) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS PatchEntitlements
            (
                EntitlementID TEXT NOT NULL,
                IsSent INTEGER NULL,
                PRIMARY KEY (EntitlementID),
                FOREIGN KEY (EntitlementID) REFERENCES Entitlements (EntitlementID)
            );

            CREATE TABLE IF NOT EXISTS Settings
            (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt INTEGER NOT NULL DEFAULT (unixepoch())
            );

            CREATE TABLE IF NOT EXISTS EntitlementOverrides
            (
                TitleID TEXT NOT NULL,
                MatchType TEXT NOT NULL,
                PackageType TEXT NULL,
                Type TEXT NULL,
                PRIMARY KEY (TitleID, MatchType)
            );

            CREATE INDEX IF NOT EXISTS IX_Entitlements_Name
                ON Entitlements (Name COLLATE NOCASE);

            CREATE INDEX IF NOT EXISTS IX_Entitlements_TitleID 
                ON Entitlements (TitleID);

            CREATE INDEX IF NOT EXISTS IX_AccountEntitlements_AccountID 
                ON AccountEntitlements (AccountID);

            CREATE INDEX IF NOT EXISTS IX_AccountEntitlements_EntitlementID 
                ON AccountEntitlements (EntitlementID);

            CREATE INDEX IF NOT EXISTS IX_ProfileEntitlements_ProfileID 
                ON ProfileEntitlements (ProfileID);

            CREATE INDEX IF NOT EXISTS IX_ProfileEntitlements_EntitlementID 
                ON ProfileEntitlements (EntitlementID);

            CREATE INDEX IF NOT EXISTS IX_ConsoleProfiles_AccountID 
                ON ConsoleProfiles (AccountID);

            CREATE INDEX IF NOT EXISTS IX_ConsoleProfiles_ConsoleID 
                ON ConsoleProfiles (ConsoleID);

            CREATE INDEX IF NOT EXISTS IX_Entitlements_EntitlementID
                ON Entitlements (EntitlementID);

            CREATE INDEX IF NOT EXISTS IX_AccountEntitlements_EntitlementID_AccountID
                ON AccountEntitlements (EntitlementID, AccountID, ProductID, Membership, IsActive);

            CREATE INDEX IF NOT EXISTS IX_ProfileEntitlements_EntitlementID_ProfileID
                ON ProfileEntitlements (EntitlementID, ProfileID, ProductID, Membership, InactiveDate);

            CREATE INDEX IF NOT EXISTS IX_Accounts_AccountID_OnlineID
                ON Accounts (AccountID, OnlineID);

            CREATE INDEX IF NOT EXISTS IX_ConsoleProfiles_ProfileID_AccountID
                ON ConsoleProfiles (ProfileID, AccountID);
            """;
        command.ExecuteNonQuery();

        EntitlementOverrides.Synchronize(connection);
    }
}