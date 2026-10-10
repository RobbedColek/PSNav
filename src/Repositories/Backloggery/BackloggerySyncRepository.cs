using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PSNav.Infrastructure.Database;
using PSNav.Models.Backloggery;

namespace PSNav.Repositories.Backloggery;

public sealed class BackloggerySyncRepository(IDatabaseService databaseService) : IBackloggerySyncRepository
{
    public IReadOnlyDictionary<string, BackloggerySyncRecord> GetSyncedEntitlements()
    {
        Dictionary<string, BackloggerySyncRecord> records = [];

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EntitlementID, Status, BackloggeryGameID, Platform FROM BackloggerySync;";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            string entitlementId = reader.GetString(0);

            if (!Enum.TryParse(reader.GetString(1), out BackloggerySyncStatus status))
                continue;

            records[entitlementId] = new BackloggerySyncRecord(
                entitlementId,
                status,
                reader.IsDBNull(2) ? null : reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetString(3));
        }

        return records;
    }

    public void Save(IEnumerable<string> entitlementIds, BackloggerySyncStatus status, long? backloggeryGameId,
        string? platform)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO BackloggerySync
            (
                EntitlementID,
                Status,
                BackloggeryGameID,
                Platform,
                SyncedAt
            )
            VALUES
            (
                $entitlementId,
                $status,
                $backloggeryGameId,
                $platform,
                unixepoch()
            )
            ON CONFLICT (EntitlementID) DO UPDATE SET
                Status = excluded.Status,
                BackloggeryGameID = excluded.BackloggeryGameID,
                Platform = excluded.Platform,
                SyncedAt = excluded.SyncedAt;
            """;

        SqliteParameter entitlementIdParameter = command.Parameters.Add("$entitlementId", SqliteType.Text);
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$backloggeryGameId", (object?)backloggeryGameId ?? DBNull.Value);
        command.Parameters.AddWithValue("$platform", (object?)platform ?? DBNull.Value);

        foreach (string entitlementId in entitlementIds)
        {
            entitlementIdParameter.Value = entitlementId;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void ClearAccountData()
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM BackloggerySync;
            DELETE FROM Settings WHERE Key GLOB 'Backloggery:*';
            """;
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public void Remove(IEnumerable<string> entitlementIds)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM BackloggerySync WHERE EntitlementID = $entitlementId;";

        SqliteParameter entitlementIdParameter = command.Parameters.Add("$entitlementId", SqliteType.Text);

        foreach (string entitlementId in entitlementIds)
        {
            entitlementIdParameter.Value = entitlementId;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
