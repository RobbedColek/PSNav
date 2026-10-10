using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PSNav.Infrastructure.Database;
using PSNav.Models.Patches;

namespace PSNav.Repositories.Patches;

public sealed class PatchesRepository(IDatabaseService databaseService) : IPatchesRepository
{
    public IReadOnlyList<Patch> GetAll()
    {
        List<Patch> patches = [];

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 
                e.EntitlementID, 
                e.Name, 
                e.ReferencePackageUrl, 
                p.IsSent,
                e.Image
            FROM Entitlements e 
            LEFT JOIN PatchEntitlements p ON e.EntitlementID = p.EntitlementID
            WHERE e.ReferencePackageUrl IS NOT NULL
            AND e.Type = 'PSGD'
            AND UPPER(e.Platform) = 'PS5'
            ORDER BY p.IsSent IS NOT NULL, e.Name COLLATE NOCASE;
            """;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            patches.Add(ReadPatch(reader));
        }

        return patches;
    }

    public IReadOnlyList<Patch> GetAllUnsent()
    {
        List<Patch> patches = [];

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 
                e.EntitlementID, 
                e.Name, 
                e.ReferencePackageUrl, 
                p.IsSent,
                e.Image
            FROM Entitlements e 
            LEFT JOIN PatchEntitlements p ON e.EntitlementID = p.EntitlementID
            WHERE e.ReferencePackageUrl IS NOT NULL
            AND e.Type = 'PSGD'
            AND UPPER(e.Platform) = 'PS5'
            AND p.IsSent IS NULL
            ORDER BY e.Name COLLATE NOCASE;
            """;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            patches.Add(ReadPatch(reader));
        }

        return patches;
    }


    public void UpsertPatch(Patch patch)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO PatchEntitlements 
            (
                EntitlementID,
                IsSent
            )
            VALUES
            (
                @entitlementId,
                @isSent
            )
            ON CONFLICT (EntitlementID) DO UPDATE SET 
                IsSent = @isSent;
            """;

        command.Parameters.AddWithValue("@entitlementId", patch.EntitlementId);
        command.Parameters.AddWithValue("@isSent", patch.IsSent);

        command.ExecuteNonQuery();
    }

    private static Patch ReadPatch(SqliteDataReader reader)
    {
        return new Patch
        {
            EntitlementId = reader.GetString(0),
            Name = reader.IsDBNull(1) ? null : reader.GetString(1),
            ReferencePackageUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
            IsSent = !reader.IsDBNull(3) && reader.GetBoolean(3),
            Image = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4)
        };
    }
}