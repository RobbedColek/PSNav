using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PSNav.Infrastructure.Database;
using PSNav.Infrastructure.PlayStationApi;
using PSNav.Models.Common;
using PSNav.Models.Consoles;

namespace PSNav.Repositories.Consoles;

public sealed class ConsolesRepository(
    IPlayStationApiClient apiClient,
    IDatabaseService databaseService)
    : IConsolesRepository
{
    public IReadOnlyList<ConsoleModel> GetAllConsoles()
    {
        var consoles = new List<ConsoleModel>();
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT ConsoleID, Name, CreatedAt, UpdatedAt FROM Consoles ORDER BY Name";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            consoles.Add(ReadConsole(reader));
        }

        return consoles;
    }

    public ConsoleModel? GetConsoleById(int consoleId)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT ConsoleID, Name, CreatedAt, UpdatedAt FROM Consoles WHERE ConsoleID = @id";
        command.Parameters.AddWithValue("@id", consoleId);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadConsole(reader) : null;
    }

    public void UpsertConsole(ConsoleModel console)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();

        if (console.Id.HasValue)
        {
            command.CommandText =
                """
                UPDATE Consoles 
                SET Name = @name, UpdatedAt = unixepoch()
                WHERE ConsoleID = @id
                """;
            command.Parameters.AddWithValue("@id", console.Id.Value);
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO Consoles (Name)
                VALUES (@name)
                RETURNING ConsoleID;
                """;
        }

        command.Parameters.AddWithValue("@name", console.Name);

        if (console.Id.HasValue)
        {
            command.ExecuteNonQuery();
        }
        else
        {
            console.Id = Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void DeleteConsole(int consoleId)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Consoles WHERE ConsoleID = @id";
            command.Parameters.AddWithValue("@id", consoleId);
            command.ExecuteNonQuery();
        }

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE @prefix";
            command.Parameters.AddWithValue("@prefix", $"Console_{consoleId}_entitlement_%");

            var tablesToDrop = new List<string>();
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    tablesToDrop.Add(reader.GetString(0));
                }
            }

            foreach (var tableName in tablesToDrop)
            {
                using SqliteCommand dropCommand = connection.CreateCommand();
                dropCommand.CommandText = $"DROP TABLE IF EXISTS {tableName}";
                dropCommand.ExecuteNonQuery();
            }
        }
    }

    public void ImportEntitlements(int consoleId, string entitlementsFilePath)
    {
        SqliteConnection targetConnection = databaseService.GetConnection();

        using (SqliteCommand attachCommand = targetConnection.CreateCommand())
        {
            attachCommand.CommandText = "ATTACH DATABASE @filePath AS sourceDb";
            attachCommand.Parameters.AddWithValue("@filePath", entitlementsFilePath);
            attachCommand.ExecuteNonQuery();
        }

        try
        {
            var sourceTables = new List<string>();
            using (SqliteCommand command = targetConnection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name FROM sourceDb.sqlite_master WHERE type='table' AND name LIKE 'entitlement_%'";
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sourceTables.Add(reader.GetString(0));
                }
            }

            foreach (var sourceTable in sourceTables)
            {
                var targetTable = $"Console_{consoleId}_{sourceTable}";

                using SqliteCommand command = targetConnection.CreateCommand();
                command.CommandText =
                    $"CREATE TABLE IF NOT EXISTS {targetTable} AS SELECT * FROM sourceDb.{sourceTable} WHERE 1=0";
                command.ExecuteNonQuery();

                command.CommandText =
                    $"CREATE UNIQUE INDEX IF NOT EXISTS IX_{targetTable} ON {targetTable} (Entitlement_id)";
                command.ExecuteNonQuery();

                command.CommandText = $"INSERT OR REPLACE INTO {targetTable} SELECT * FROM sourceDb.{sourceTable}";
                command.ExecuteNonQuery();
            }
        }
        finally
        {
            using SqliteCommand detachCommand = targetConnection.CreateCommand();
            detachCommand.CommandText = "DETACH DATABASE sourceDb";
            detachCommand.ExecuteNonQuery();
        }
    }

    public async Task UpdateEntitlements(int consoleId, string entitlementsFilePath,
        IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(new ProgressReport("Importing database tables..."));
        ImportEntitlements(consoleId, entitlementsFilePath);

        SqliteConnection connection = databaseService.GetConnection();
        await using (SqliteCommand attachCommand = connection.CreateCommand())
        {
            attachCommand.CommandText = "ATTACH DATABASE @filePath AS sourceDb";
            attachCommand.Parameters.AddWithValue("@filePath", entitlementsFilePath);
            attachCommand.ExecuteNonQuery();
        }

        try
        {
            var profileIds = new List<int>();
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name FROM sourceDb.sqlite_master WHERE type='table' AND name LIKE 'entitlement_%'";
                await using SqliteDataReader reader = await command.ExecuteReaderAsync();
                while (reader.Read())
                {
                    var tableName = reader.GetString(0);
                    var idStr = tableName.Replace("entitlement_", "");
                    if (int.TryParse(idStr, out int id))
                    {
                        profileIds.Add(id);
                    }
                }
            }

            // Get IDs of entitlements that already have an image
            var entitlementsWithImages = new HashSet<string>();
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT EntitlementID FROM Entitlements WHERE Image IS NOT NULL";
                await using SqliteDataReader reader = await command.ExecuteReaderAsync();
                while (reader.Read())
                {
                    entitlementsWithImages.Add(reader.GetString(0));
                }
            }

            await using SqliteTransaction transaction = connection.BeginTransaction();

            for (int i = 0; i < profileIds.Count; i++)
            {
                var profileId = profileIds[i];
                var profilePrefix = profileIds.Count > 1 ? $"[{i + 1}/{profileIds.Count}] " : string.Empty;
                progress?.Report(new ProgressReport($"{profilePrefix}Updating Profile {profileId}...",
                    (double)i / profileIds.Count * 100));

                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO ConsoleProfiles (ProfileID, ConsoleID, Name)
                        SELECT @id, @consoleId, @name
                        WHERE NOT EXISTS (SELECT 1 FROM ConsoleProfiles WHERE ProfileID = @id)
                        """;
                    command.Parameters.AddWithValue("@id", profileId);
                    command.Parameters.AddWithValue("@consoleId", consoleId);
                    command.Parameters.AddWithValue("@name", $"Profile {profileId}");
                    command.ExecuteNonQuery();
                }

                var targetTable = $"console_{consoleId}_entitlement_{profileId:D10}";
                var entitlementsToUpdate = new List<(string EntitlementId, string Json)>();
                await using (SqliteCommand commandSelect = connection.CreateCommand())
                {
                    commandSelect.Transaction = transaction;
                    commandSelect.CommandText = $"SELECT Entitlement_id, JSON FROM {targetTable}";
                    await using SqliteDataReader reader = await commandSelect.ExecuteReaderAsync();
                    while (reader.Read())
                    {
                        entitlementsToUpdate.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                SemaphoreSlim semaphore = new(10); // Limit concurrent downloads

                var processedEntitlements = new List<dynamic>();

                foreach (var (entitlementId, json) in entitlementsToUpdate)
                {
                    using JsonDocument jsonDocument = JsonDocument.Parse(json);
                    JsonElement root = jsonDocument.RootElement;

                    string titleId = root.GetProperty("id").GetString()!.Split('-')[1];
                    string? branding = TryGetAny(root, out JsonElement brandingElem, "branding")
                        ? brandingElem.GetString()
                        : null;
                    int entitlementType = GetRequired(root, entitlementId, "entitlement_type", "entitlementType")
                        .GetInt32();
                    int featureType = GetRequired(root, entitlementId, "feature_type", "featureType").GetInt32();

                    string? platform = null;
                    string? referencePackageUrl = null;
                    long? packageFileSize = null;

                    if (TryGetAny(root, out JsonElement entitlementAttributesElement, "entitlement_attributes",
                            "entitlementAttributes") &&
                        entitlementAttributesElement.GetArrayLength() > 0)
                    {
                        JsonElement firstAttr = entitlementAttributesElement[0];
                        platform = TryGetAny(firstAttr, out JsonElement platformIdElement, "platform_id", "platformId")
                            ? platformIdElement.GetString()
                            : null;
                        referencePackageUrl =
                            TryGetAny(firstAttr, out JsonElement refPkgUrlElement, "reference_package_url",
                                "referencePackageUrl")
                                ? refPkgUrlElement.GetString()
                                : null;
                        packageFileSize =
                            TryGetAny(firstAttr, out JsonElement pkgSizeElement, "package_file_size",
                                "packageFileSize")
                                ? pkgSizeElement.GetInt64()
                                : null;
                    }

                    string? name = null;
                    string? packageType = null;
                    string? type = null;
                    string? imageUrl = null;

                    if (TryGetAny(root, out JsonElement gameMetaElement, "game_meta", "gameMeta"))
                    {
                        name = gameMetaElement.GetProperty("name").GetString();
                        packageType = TryGetAny(gameMetaElement, out JsonElement pkgTypeElem, "package_type",
                            "packageType")
                            ? pkgTypeElem.GetString()
                            : null;
                        type = TryGetAny(gameMetaElement, out JsonElement typeElem, "type")
                            ? typeElem.GetString()
                            : null;
                        imageUrl = TryGetAny(gameMetaElement, out JsonElement iconUrlElem, "icon_url", "iconUrl")
                            ? iconUrlElem.GetString()
                            : null;
                    }

                    if (imageUrl == null &&
                        TryGetAny(root, out JsonElement productMetaElement, "product_meta", "productMeta"))
                    {
                        imageUrl = TryGetAny(productMetaElement, out JsonElement iconUrlElem, "image_url", "imageUrl")
                            ? iconUrlElem.GetString()
                            : null;
                    }

                    Task<byte[]?> downloadTask = Task.FromResult<byte[]?>(null);
                    if (!string.IsNullOrWhiteSpace(imageUrl) && !entitlementsWithImages.Contains(entitlementId))
                    {
                        var capturedUrl = imageUrl;
                        downloadTask = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                return await apiClient.DownloadImageOrNullAsync(capturedUrl);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });
                    }

                    string? productId = TryGetAny(root, out JsonElement prodIdElem, "product_id", "productId")
                        ? prodIdElem.GetString()
                        : null;
                    string? membership = null;
                    int? retentionPolicy = null;
                    if (TryGetAny(root, out JsonElement rewardMetaElement, "reward_meta", "rewardMeta"))
                    {
                        membership =
                            TryGetAny(rewardMetaElement, out JsonElement rewardMemTypeElem,
                                "reward_membership_type", "rewardMembershipType")
                                ? rewardMemTypeElem.GetString()
                                : null;
                        retentionPolicy =
                            TryGetAny(rewardMetaElement, out JsonElement retentionPolicyElem, "retention_policy",
                                "retentionPolicy")
                                ? retentionPolicyElem.GetInt32()
                                : null;
                    }

                    DateTimeOffset? inactiveDate =
                        TryGetAny(root, out JsonElement inactiveDateElement, "inactive_date", "inactiveDate")
                            ? DateTimeOffset.Parse(inactiveDateElement.GetString()!)
                            : null;

                    processedEntitlements.Add(new
                    {
                        EntitlementId = entitlementId,
                        TitleId = titleId,
                        Name = name,
                        ImageUrl = imageUrl,
                        Platform = platform,
                        ReferencePackageUrl = referencePackageUrl,
                        PackageFileSize = packageFileSize,
                        EntitlementType = entitlementType,
                        FeatureType = featureType,
                        PackageType = packageType,
                        Type = type,
                        DownloadTask = downloadTask,
                        ProductId = productId,
                        Membership = membership,
                        InactiveDate = inactiveDate,
                        RetentionPolicy = retentionPolicy,
                        Branding = branding
                    });
                }

                // Wait for all downloads and update DB in batches
                for (int j = 0; j < processedEntitlements.Count; j++)
                {
                    var ent = processedEntitlements[j];
                    if (j % 10 == 0 || j == processedEntitlements.Count - 1)
                    {
                        progress?.Report(new ProgressReport(
                            $"{profilePrefix}Processing entitlement {j + 1}/{processedEntitlements.Count}...",
                            (i + (double)j / processedEntitlements.Count) / profileIds.Count * 100));
                    }

                    byte[]? image = await ent.DownloadTask;

                    await using (SqliteCommand commandUpsert = connection.CreateCommand())
                    {
                        commandUpsert.Transaction = transaction;
                        commandUpsert.CommandText =
                            """
                            INSERT INTO Entitlements (EntitlementID, TitleID, Name, Image, ImageUrl, Platform, ReferencePackageUrl, PackageFileSize, EntitlementType, PackageType, Type)
                            VALUES(@entitlementId, @titleId, @name, @image, @imageUrl, @platform, @referencePackageUrl, @packageFileSize, @entitlementType, @packageType, @type)
                            ON CONFLICT(EntitlementID) DO UPDATE SET
                                ReferencePackageUrl = @referencePackageUrl,
                                PackageFileSize = @packageFileSize,
                                EntitlementType = @entitlementType,
                                PackageType = @packageType,
                                Type = @type,
                                ImageUrl = COALESCE(Entitlements.ImageUrl, @imageUrl),
                                Image = COALESCE(Entitlements.Image, @image);
                            """;
                        commandUpsert.Parameters.AddWithValue("@entitlementId", ent.EntitlementId);
                        commandUpsert.Parameters.AddWithValue("@titleId", ent.TitleId);
                        commandUpsert.Parameters.AddWithValue("@name", ent.Name is null ? DBNull.Value : ent.Name);
                        commandUpsert.Parameters.AddWithValue("@image", image is null ? DBNull.Value : image);
                        commandUpsert.Parameters.AddWithValue("@imageUrl",
                            ent.ImageUrl is null ? DBNull.Value : ent.ImageUrl);
                        commandUpsert.Parameters.AddWithValue("@platform",
                            ent.Platform is null ? DBNull.Value : ent.Platform);
                        commandUpsert.Parameters.AddWithValue("@referencePackageUrl",
                            ent.ReferencePackageUrl is null ? DBNull.Value : ent.ReferencePackageUrl);
                        commandUpsert.Parameters.AddWithValue("@packageFileSize",
                            ent.PackageFileSize is null ? DBNull.Value : ent.PackageFileSize);
                        commandUpsert.Parameters.AddWithValue("@entitlementType", ent.EntitlementType);
                        commandUpsert.Parameters.AddWithValue("@packageType",
                            ent.PackageType is null ? DBNull.Value : ent.PackageType);
                        commandUpsert.Parameters.AddWithValue("@type", ent.Type is null ? DBNull.Value : ent.Type);
                        commandUpsert.ExecuteNonQuery();
                    }

                    // Connect entitlements with profiles
                    await using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            """
                            INSERT INTO ProfileEntitlements (ProfileID, EntitlementID, ProductID, Membership, InactiveDate, RetentionPolicy, FeatureType, Branding)
                            VALUES(@profileId, @entitlementId, @productId, @membership, @inactiveDate, @retentionPolicy, @featureType, @branding)
                            ON CONFLICT(ProfileId, EntitlementId) DO UPDATE SET
                                ProductID = @productId,
                                Membership = @membership,
                                InactiveDate = @inactiveDate,
                                RetentionPolicy = @retentionPolicy,
                                FeatureType = @featureType,
                                Branding = @branding;
                            """;
                        command.Parameters.AddWithValue("@profileId", profileId);
                        command.Parameters.AddWithValue("@entitlementId", ent.EntitlementId);
                        command.Parameters.AddWithValue("@productId",
                            ent.ProductId is null ? DBNull.Value : ent.ProductId);
                        command.Parameters.AddWithValue("@membership", ent.Membership ?? "NONE");
                        command.Parameters.AddWithValue("@inactiveDate",
                            ent.InactiveDate is null
                                ? DBNull.Value
                                : ((DateTimeOffset)ent.InactiveDate).ToUnixTimeSeconds());
                        command.Parameters.AddWithValue("@retentionPolicy",
                            ent.RetentionPolicy is null ? DBNull.Value : ent.RetentionPolicy);
                        command.Parameters.AddWithValue("@featureType",
                            ent.FeatureType is null ? DBNull.Value : ent.FeatureType);
                        command.Parameters.AddWithValue("@branding",
                            ent.Branding is null ? DBNull.Value : ent.Branding);
                        command.ExecuteNonQuery();
                    }
                }
            }

            transaction.Commit();
            progress?.Report(new ProgressReport("Done.", 100));
        }
        finally
        {
            await using SqliteCommand detachCommand = connection.CreateCommand();
            detachCommand.CommandText = "DETACH DATABASE sourceDb";
            detachCommand.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<ConsoleProfile> GetProfilesForConsole(int consoleId)
    {
        var profiles = new List<ConsoleProfile>();
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 
                cp.ProfileID, 
                cp.ConsoleID, 
                cp.Name, 
                cp.AccountID, 
                cp.CreatedAt, 
                cp.UpdatedAt,
                a.OnlineID,
                (SELECT COUNT(*) FROM ProfileEntitlements pe WHERE pe.ProfileID = cp.ProfileID) AS EntitlementCount
            FROM ConsoleProfiles cp
            LEFT JOIN Accounts a ON cp.AccountID = a.AccountID
            WHERE cp.ConsoleID = @id 
            ORDER BY cp.Name
            """;
        command.Parameters.AddWithValue("@id", consoleId);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            profiles.Add(ReadProfile(reader));
        }

        return profiles;
    }

    public void UpsertProfile(ConsoleProfile profile)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();

        bool exists = false;
        if (profile.Id.HasValue)
        {
            using SqliteCommand checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM ConsoleProfiles WHERE ProfileID = @id";
            checkCommand.Parameters.AddWithValue("@id", profile.Id.Value);
            exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;
        }

        if (exists)
        {
            command.CommandText =
                """
                UPDATE ConsoleProfiles 
                SET ConsoleID = @consoleId, Name = @name, AccountID = @accountId, UpdatedAt = unixepoch()
                WHERE ProfileID = @id
                """;
            command.Parameters.AddWithValue("@id", profile.Id!.Value);
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO ConsoleProfiles (ProfileID, ConsoleID, Name, AccountID)
                VALUES (@id, @consoleId, @name, @accountId)
                """;

            if (profile.Id.HasValue)
            {
                command.Parameters.AddWithValue("@id", profile.Id.Value);
            }
            else
            {
                command.CommandText =
                    """
                    INSERT INTO ConsoleProfiles (ConsoleID, Name, AccountID)
                    VALUES (@consoleId, @name, @accountId)
                    RETURNING ProfileID;
                    """;
            }
        }

        command.Parameters.AddWithValue("@consoleId", profile.ConsoleId);
        command.Parameters.AddWithValue("@name", profile.Name);
        command.Parameters.AddWithValue("@accountId", ToDbValue(profile.AccountId));

        if (exists || profile.Id.HasValue)
        {
            command.ExecuteNonQuery();
        }
        else
        {
            profile.Id = Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void DeleteProfile(int profileId)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ConsoleProfiles WHERE ProfileID = @id";
        command.Parameters.AddWithValue("@id", profileId);
        command.ExecuteNonQuery();
    }

    // Entitlement JSON uses snake_case keys on PS4 exports and camelCase keys on PS5 exports
    private static bool TryGetAny(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static JsonElement GetRequired(JsonElement element, string entitlementId, params string[] names)
    {
        if (TryGetAny(element, out JsonElement value, names))
        {
            return value;
        }

        throw new InvalidDataException(
            $"Entitlement '{entitlementId}' is missing required property '{string.Join("' / '", names)}'.");
    }

    private static ConsoleModel ReadConsole(SqliteDataReader reader)
    {
        return new ConsoleModel
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)),
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3))
        };
    }

    private static ConsoleProfile ReadProfile(SqliteDataReader reader)
    {
        return new ConsoleProfile
        {
            Id = reader.GetInt32(0),
            ConsoleId = reader.GetInt32(1),
            Name = reader.GetString(2),
            AccountId = reader.IsDBNull(3) ? null : reader.GetString(3),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)),
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
            OnlineId = reader.FieldCount > 6 && !reader.IsDBNull(6) ? reader.GetString(6) : null,
            EntitlementCount = reader.GetInt32(7)
        };
    }

    private static object ToDbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }
}