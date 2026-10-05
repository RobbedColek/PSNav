using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PSNav.Infrastructure.Database;
using PSNav.Models.Entitlements;

namespace PSNav.Repositories.Entitlements;

public sealed class EntitlementsRepository(IDatabaseService databaseService) : IEntitlementsRepository
{
    public IReadOnlyList<Entitlement> GetAllEntitlements()
    {
        List<Entitlement> entitlements = [];

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                e.EntitlementID,
                e.TitleID,
                pe.ProductID,
                e.Name,
                COALESCE(UPPER(e.Platform), 'N/A') as Platform,
                pe.Membership,
                e.ReferencePackageUrl,
                CASE
                   WHEN pe.InactiveDate IS NULL THEN 1
                   WHEN pe.InactiveDate <= unixepoch('now') THEN 0
                   ELSE 1
                END as IsActive,
                e.PackageFileSize,
                e.EntitlementType,
                pe.FeatureType,
                COALESCE(eo.PackageType, e.PackageType) as PackageType,
                COALESCE(eo.Type, e.Type, 'N/A') as Type,
                a.OnlineID,
                pe.RetentionPolicy
            FROM Entitlements e
            INNER JOIN ProfileEntitlements pe ON e.EntitlementID = pe.EntitlementID
            INNER JOIN ConsoleProfiles cp ON pe.ProfileID = cp.ProfileID
            INNER JOIN Accounts a ON cp.AccountID = a.AccountID
            LEFT JOIN EntitlementOverrides eo ON e.TitleID = eo.TitleID AND e.Type = eo.MatchType

            UNION ALL

            SELECT
                e.EntitlementID,
                e.TitleID,
                ae.ProductID,
                e.Name,
                COALESCE(UPPER(e.Platform), 'N/A') as Platform,
                ae.Membership,
                e.ReferencePackageUrl,
                ae.IsActive,
                e.PackageFileSize,
                e.EntitlementType,
                null,
                COALESCE(eo.PackageType, e.PackageType),
                COALESCE(eo.Type, e.Type, 'Game') as Type,
                a.OnlineID,
                null
            FROM Entitlements e
            INNER JOIN AccountEntitlements ae ON e.EntitlementID = ae.EntitlementID
            INNER JOIN Accounts a ON ae.AccountID = a.AccountID
            LEFT JOIN EntitlementOverrides eo ON e.TitleID = eo.TitleID AND e.Type = eo.MatchType
            WHERE NOT EXISTS (
                SELECT 1
                FROM ProfileEntitlements pe
                INNER JOIN ConsoleProfiles cp ON pe.ProfileID = cp.ProfileID
                WHERE pe.EntitlementID = e.EntitlementID
                AND cp.AccountID = a.AccountID)
            ORDER BY Name COLLATE NOCASE;
            """;

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                entitlements.Add(ReadEntitlement(reader));
            }
        }

        // Images are loaded separately so the sort above does not have to carry the image blobs
        Dictionary<string, byte[]> images = GetAllImages(connection);

        foreach (Entitlement entitlement in entitlements)
        {
            entitlement.Image = images.GetValueOrDefault(entitlement.EntitlementId);
        }

        return entitlements;
    }

    private static Dictionary<string, byte[]> GetAllImages(SqliteConnection connection)
    {
        Dictionary<string, byte[]> images = [];

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EntitlementID, Image FROM Entitlements WHERE Image IS NOT NULL;";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            images[reader.GetString(0)] = (byte[])reader["Image"];
        }

        return images;
    }

    public void Upsert(string accountId, Entitlement entitlement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(entitlement);

        SqliteConnection connection = databaseService.GetConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO Entitlements
                    (
                        EntitlementID,
                        TitleID,
                        Name,
                        Image,
                        ImageUrl,
                        Platform,
                        ReferencePackageUrl,
                        PackageFileSize,
                        EntitlementType,
                        PackageType,
                        Type
                    )
                    VALUES
                    (
                        $entitlementId,
                        $titleId,
                        $name,
                        $image,
                        $imageUrl,
                        $platform,
                        $referencePackageUrl,
                        $packageFileSize,
                        $entitlementType,
                        $packageType,
                        $type
                    )
                    ON CONFLICT(EntitlementID) DO UPDATE SET
                        TitleID = excluded.TitleID,
                        Name = excluded.Name,
                        Image = excluded.Image,
                        ImageUrl = excluded.ImageUrl,
                        Platform = excluded.Platform,
                        ReferencePackageUrl = excluded.ReferencePackageUrl,
                        PackageFileSize = excluded.PackageFileSize,
                        EntitlementType = excluded.EntitlementType,
                        PackageType = excluded.PackageType,
                        Type = excluded.Type;
                    """;

                AddEntitlementParameters(command, entitlement);
                command.ExecuteNonQuery();
            }

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO AccountEntitlements
                    (
                        AccountID,
                        EntitlementID,
                        ProductID,
                        Membership,
                        IsActive
                    )
                    VALUES
                    (
                        $accountId,
                        $entitlementId,
                        $productId,
                        $membership,
                        $isActive
                    )
                    ON CONFLICT(AccountID, EntitlementID) DO UPDATE SET
                        ProductID = excluded.ProductID,
                        Membership = excluded.Membership,
                        IsActive = excluded.IsActive;
                    """;
                command.Parameters.AddWithValue("$accountId", accountId);
                AddAccountEntitlementParameters(command, entitlement);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public bool Exists(string entitlementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementId);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM Entitlements
                WHERE EntitlementID = $entitlementId
            );
            """;

        command.Parameters.AddWithValue("$entitlementId", entitlementId);

        object? result = command.ExecuteScalar();

        return Convert.ToInt32(result) == 1;
    }

    public bool HasImage(string entitlementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementId);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM Entitlements
                WHERE EntitlementID = $entitlementId
                  AND Image IS NOT NULL
            );
            """;

        command.Parameters.AddWithValue("$entitlementId", entitlementId);

        object? result = command.ExecuteScalar();

        return Convert.ToInt32(result) == 1;
    }

    public void UpsertBatch(string accountId, IEnumerable<Entitlement> entitlements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(entitlements);

        SqliteConnection connection = databaseService.GetConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            foreach (Entitlement entitlement in entitlements)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO Entitlements
                        (
                            EntitlementID,
                            TitleID,
                            Name,
                            Image,
                            ImageUrl,
                            Platform,
                            ReferencePackageUrl,
                            PackageFileSize,
                            EntitlementType,
                            PackageType,
                            Type
                        )
                        VALUES
                        (
                            $entitlementId,
                            $titleId,
                            $name,
                            $image,
                            $imageUrl,
                            $platform,
                            $referencePackageUrl,
                            $packageFileSize,
                            $entitlementType,
                            $packageType,
                            $type
                        )
                        ON CONFLICT(EntitlementID) DO NOTHING;
                        """;

                    AddEntitlementParameters(command, entitlement);
                    command.ExecuteNonQuery();
                }

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO AccountEntitlements
                        (
                            AccountID,
                            EntitlementID,
                            ProductID,
                            Membership,
                            IsActive
                        )
                        VALUES
                        (
                            $accountId,
                            $entitlementId,
                            $productId,
                            $membership,
                            $isActive
                        )
                        ON CONFLICT(AccountID, EntitlementID) DO UPDATE SET
                            ProductID = excluded.ProductID,
                            Membership = excluded.Membership,
                            IsActive = excluded.IsActive;
                        """;
                    command.Parameters.AddWithValue("$accountId", accountId);
                    AddAccountEntitlementParameters(command, entitlement);
                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void LinkAccountEntitlement(string accountId, Entitlement entitlement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(entitlement);

        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO AccountEntitlements
            (
                AccountID,
                EntitlementID,
                ProductID,
                Membership,
                IsActive
            )
            VALUES
            (
                $accountId,
                $entitlementId,
                $productId,
                $membership,
                $isActive
            )
            ON CONFLICT(AccountID, EntitlementID) DO UPDATE SET
                ProductID = excluded.ProductID,
                Membership = excluded.Membership,
                IsActive = excluded.IsActive;
            """;
        command.Parameters.AddWithValue("$accountId", accountId);
        AddAccountEntitlementParameters(command, entitlement);
        command.ExecuteNonQuery();
    }

    public void DeleteAllByAccountId(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AccountEntitlements WHERE AccountID = $accountId;";
        command.Parameters.AddWithValue("$accountId", accountId);

        command.ExecuteNonQuery();
    }

    private static void AddEntitlementParameters(SqliteCommand command, Entitlement entitlement)
    {
        command.Parameters.AddWithValue("$entitlementId", entitlement.EntitlementId);
        command.Parameters.AddWithValue("$titleId", ToDbValue(entitlement.TitleId));
        command.Parameters.AddWithValue("$name", ToDbValue(entitlement.Name));
        command.Parameters.AddWithValue("$image", ToDbValue(entitlement.Image));
        command.Parameters.AddWithValue("$imageUrl", ToDbValue(entitlement.ImageUrl));
        command.Parameters.AddWithValue("$platform", ToDbValue(entitlement.Platform));
        command.Parameters.AddWithValue("$referencePackageUrl", ToDbValue(entitlement.ReferencePackageUrl));
        command.Parameters.AddWithValue("$packageFileSize", ToDbValue(entitlement.PackageFileSize));
        command.Parameters.AddWithValue("$entitlementType", ToDbValue(entitlement.EntitlementType));
        command.Parameters.AddWithValue("$featureType", ToDbValue(entitlement.FeatureType));
        command.Parameters.AddWithValue("$packageType", ToDbValue(entitlement.PackageType));
        command.Parameters.AddWithValue("$type", ToDbValue(entitlement.Type));
    }

    private static void AddAccountEntitlementParameters(SqliteCommand command, Entitlement entitlement)
    {
        command.Parameters.AddWithValue("$entitlementId", entitlement.EntitlementId);
        command.Parameters.AddWithValue("$productId", ToDbValue(entitlement.ProductId));
        command.Parameters.AddWithValue("$membership", ToDbValue(entitlement.Membership));
        command.Parameters.AddWithValue("$isActive", ToDbValue(entitlement.IsActive));
    }

    private static Entitlement ReadEntitlement(SqliteDataReader reader)
    {
        return new Entitlement
        {
            EntitlementId = reader.GetString(0),
            TitleId = reader.IsDBNull(1) ? null : reader.GetString(1),
            ProductId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Name = reader.IsDBNull(3) ? null : reader.GetString(3),
            Platform = reader.IsDBNull(4) ? null : reader.GetString(4),
            Membership = reader.IsDBNull(5) ? null : reader.GetString(5),
            ReferencePackageUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            IsActive = reader.IsDBNull(7) ? null : reader.GetBoolean(7),
            PackageFileSize = reader.IsDBNull(8) ? null : reader.GetInt64(8),
            EntitlementType = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            FeatureType = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            PackageType = reader.IsDBNull(11) ? null : reader.GetString(11),
            Type = reader.IsDBNull(12) ? null : reader.GetString(12),
            OwnerOnlineId = reader.IsDBNull(13) ? null : reader.GetString(13),
            TypeDisplayString = GetTypeDisplayString(
                reader.IsDBNull(12) ? null : reader.GetString(12)),
            RetentionPolicyDisplayString = GetRetentionPolicyDisplayString(
                reader.IsDBNull(14) ? null : reader.GetInt32(14)),
            FeatureTypeDisplayString = GetFeatureTypeDisplayString(
                reader.IsDBNull(10) ? null : reader.GetInt32(10)),
            MembershipDisplayString = GetMembershipDisplayString(
                reader.IsDBNull(5) ? null : reader.GetString(5))
        };
    }

    private static string GetTypeDisplayString(string? input)
    {
        return input switch
        {
            null => "N/A",
            "PS4AC" or "PSAC" => "DLC (Package)",
            "PS4AL" or "PSAL" => "DLC (License)",
            "PS4GD" or "PSGD" => "Game",
            "PS4MISC" => "Miscellaneous",
            "PSTRACK" => "Tracker",
            "PSCONS" => "Consumable",
            "PSSUBS" => "Subscription",
            "PSMEDIA" => "Media",
            _ => input
        };
    }

    private static string GetRetentionPolicyDisplayString(int? retentionPolicy)
    {
        return retentionPolicy switch
        {
            0 => "Permanent", // permanent (ex. purchase)
            4 => "Subscription", // valid for the duration of a subscription (ex. monthly PS+ games)
            5 => "Limited", // valid for the duration of subscription or until expiry (ex. PS+ catalogue games)
            _ => "N/A"
        };
    }

    private static string GetFeatureTypeDisplayString(int? featureType)
    {
        return featureType switch
        {
            0 => "Entitlement", // obtained via another entitlement
            1 => "Free",
            3 => "Purchase",
            5 => "PS4 -> PS5 Upgrade", // PS4 -> PS5 upgrade, requires PS4 disc to run
            7 => "Trial",
            _ => "N/A"
        };
    }

    private static string GetMembershipDisplayString(string? membership)
    {
        return membership switch
        {
            null => "N/A",
            "NONE" => "Owned",
            "PS_PLUS" => "PS Plus",
            "EA_ACCESS" => "EA Access",
            _ => membership
        };
    }

    private static object ToDbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }
}