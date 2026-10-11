using System;
using Microsoft.Data.Sqlite;

namespace PSNav.Infrastructure.Database;

internal static class DatabaseMigrations
{
    internal static long GetVersion(SqliteConnection connection)
    {
        return Number(connection, null, "PRAGMA user_version;");
    }

    internal static bool RequiresMigration(SqliteConnection connection)
    {
        return GetVersion(connection) < DatabaseSchema.CurrentVersion
            && Number(connection, null, "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = 'ConsoleProfiles';") > 0;
    }

    internal static void Initialize(SqliteConnection connection)
    {
        long version = GetVersion(connection);
        if (version > DatabaseSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"The database was created by a newer version of PSNav (schema version {version}, supported version {DatabaseSchema.CurrentVersion}).");
        }

        Execute(connection, null, "PRAGMA journal_mode = WAL;");
        Execute(connection, null, "PRAGMA foreign_keys = OFF;");

        try
        {
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

            if (HasLegacyProfileKey(connection, transaction))
            {
                CheckForeignKeys(connection, transaction, "ConsoleProfiles");
                CheckForeignKeys(connection, transaction, "ProfileEntitlements");
                ConvertLegacyProfiles(connection, transaction);
            }

            DatabaseSchema.Create(connection, transaction);
            AddFtpColumns(connection, transaction);
            CheckForeignKeys(connection, transaction, "ConsoleProfiles");
            CheckForeignKeys(connection, transaction, "ProfileEntitlements");
            Execute(connection, transaction, $"PRAGMA user_version = {DatabaseSchema.CurrentVersion};");

            transaction.Commit();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The database could not be upgraded from schema version {version} to {DatabaseSchema.CurrentVersion}. {exception.Message}",
                exception);
        }
        finally
        {
            Execute(connection, null, "PRAGMA foreign_keys = ON;");
        }
    }

    private static bool HasLegacyProfileKey(SqliteConnection connection, SqliteTransaction transaction)
    {
        return Number(connection, transaction,
            "SELECT COUNT(*) FROM pragma_table_info('ConsoleProfiles') WHERE name = 'ConsoleID' AND pk = 0;") > 0;
    }

    private static void ConvertLegacyProfiles(SqliteConnection connection, SqliteTransaction transaction)
    {
        Execute(connection, transaction,
            $"""
            CREATE TABLE ConsoleProfiles_New ({DatabaseSchema.ConsoleProfilesDefinition});
            CREATE TABLE ProfileEntitlements_New ({DatabaseSchema.ProfileEntitlementsDefinition});

            INSERT INTO ConsoleProfiles_New (ProfileID, ConsoleID, Name, AccountID, CreatedAt, UpdatedAt)
            SELECT ProfileID, ConsoleID, Name, AccountID, CreatedAt, UpdatedAt
            FROM ConsoleProfiles;

            INSERT INTO ProfileEntitlements_New
                (ConsoleID, ProfileID, EntitlementID, ProductID, Membership, InactiveDate, RetentionPolicy, FeatureType, Branding)
            SELECT profile.ConsoleID, link.ProfileID, link.EntitlementID, link.ProductID, link.Membership,
                link.InactiveDate, link.RetentionPolicy, link.FeatureType, link.Branding
            FROM ProfileEntitlements AS link
            INNER JOIN ConsoleProfiles AS profile ON profile.ProfileID = link.ProfileID;

            DROP TABLE ProfileEntitlements;
            DROP TABLE ConsoleProfiles;
            ALTER TABLE ConsoleProfiles_New RENAME TO ConsoleProfiles;
            ALTER TABLE ProfileEntitlements_New RENAME TO ProfileEntitlements;
            """);
    }

    private static void AddFtpColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        if (!HasColumn(connection, transaction, "Consoles", "IpAddress"))
        {
            Execute(connection, transaction, "ALTER TABLE Consoles ADD COLUMN IpAddress TEXT NULL;");
        }

        if (!HasColumn(connection, transaction, "Consoles", "FtpPort"))
        {
            Execute(connection, transaction,
                "ALTER TABLE Consoles ADD COLUMN FtpPort INTEGER NOT NULL DEFAULT 2121 CHECK (FtpPort BETWEEN 1 AND 65535);");
        }
    }

    private static bool HasColumn(SqliteConnection connection, SqliteTransaction transaction, string table, string column)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column;";
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static void CheckForeignKeys(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA foreign_key_check({table});";
        using SqliteDataReader reader = command.ExecuteReader();
        if (reader.Read())
        {
            throw new InvalidOperationException($"Table {table} contains rows that reference missing records.");
        }
    }

    private static long Number(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
