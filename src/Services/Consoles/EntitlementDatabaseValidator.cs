using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PSNav.Services.Consoles;

public static class EntitlementDatabaseValidator
{
    public static IReadOnlyList<string> Validate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new InvalidDataException("Please select an existing entitlement.db / entitlement4.db file.");

        try
        {
            if (new FileInfo(filePath).Length == 0)
                throw new InvalidDataException("The entitlements database is empty.");

            SqliteConnectionStringBuilder connectionString = new()
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            using SqliteConnection connection = new(connectionString.ToString());
            connection.Open();

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA quick_check";
                using SqliteDataReader reader = command.ExecuteReader();
                var integrityOk = false;
                while (reader.Read())
                {
                    var result = reader.GetString(0);
                    if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"The entitlements database failed its integrity check: {result}");

                    integrityOk = true;
                }

                if (!integrityOk)
                    throw new InvalidDataException("The entitlements database integrity check returned no result.");
            }

            var tableNames = new List<string>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'entitlement_%' ORDER BY name";
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    tableNames.Add(reader.GetString(0));
            }

            var profileIds = new List<string>();
            const string prefix = "entitlement_";
            foreach (var tableName in tableNames)
            {
                var id = tableName.StartsWith(prefix, StringComparison.Ordinal) ? tableName[prefix.Length..] : string.Empty;
                if (int.TryParse(id, out _))
                    profileIds.Add(id);
            }

            if (profileIds.Count == 0)
                throw new InvalidDataException("No supported profiles found in the entitlements database.");

            return profileIds;
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException($"Cannot read the entitlements database as valid SQLite: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidDataException &&
                                   ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new InvalidDataException($"Cannot access the entitlements database: {ex.Message}", ex);
        }
    }
}
