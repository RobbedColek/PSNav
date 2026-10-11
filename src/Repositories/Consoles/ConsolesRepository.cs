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
        command.CommandText = "SELECT ConsoleID, Name, CreatedAt, UpdatedAt, IpAddress, FtpPort FROM Consoles ORDER BY Name COLLATE NOCASE";

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
        command.CommandText = "SELECT ConsoleID, Name, CreatedAt, UpdatedAt, IpAddress, FtpPort FROM Consoles WHERE ConsoleID = @id";
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
                SET Name = @name, IpAddress = @ipAddress, FtpPort = @ftpPort, UpdatedAt = unixepoch()
                WHERE ConsoleID = @id
                """;
            command.Parameters.AddWithValue("@id", console.Id.Value);
        }
        else
        {
            command.CommandText =
                """
                INSERT INTO Consoles (Name, IpAddress, FtpPort)
                VALUES (@name, @ipAddress, @ftpPort)
                RETURNING ConsoleID;
                """;
        }

        command.Parameters.AddWithValue("@name", console.Name);
        command.Parameters.AddWithValue("@ipAddress", ToDbValue(console.IpAddress));
        command.Parameters.AddWithValue("@ftpPort", console.FtpPort);

        if (console.Id.HasValue)
            command.ExecuteNonQuery();
        else
            console.Id = Convert.ToInt32(command.ExecuteScalar());
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
                    tablesToDrop.Add(reader.GetString(0));
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
                    var tableName = reader.GetString(0);
                    if (int.TryParse(tableName.Replace("entitlement_", ""), out _))
                        sourceTables.Add(tableName);
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
        var profileIds = new List<int>();
        var entitlementsWithImages = new HashSet<string>();
        var preparedProfiles = new List<(int ProfileId, List<dynamic> Entitlements)>();
        using (SqliteCommand attachCommand = connection.CreateCommand())
        {
            attachCommand.CommandText = "ATTACH DATABASE @filePath AS sourceDb";
            attachCommand.Parameters.AddWithValue("@filePath", entitlementsFilePath);
            attachCommand.ExecuteNonQuery();
        }

        try
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name FROM sourceDb.sqlite_master WHERE type='table' AND name LIKE 'entitlement_%'";
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var tableName = reader.GetString(0);
                    var idStr = tableName.Replace("entitlement_", "");
                    if (int.TryParse(idStr, out int id))
                        profileIds.Add(id);
                }
            }

            // Get IDs of entitlements that already have an image
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT EntitlementID FROM Entitlements WHERE Image IS NOT NULL";
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    entitlementsWithImages.Add(reader.GetString(0));
            }

            for (int i = 0; i < profileIds.Count; i++)
            {
                var profileId = profileIds[i];
                var targetTable = $"console_{consoleId}_entitlement_{profileId:D10}";
                var entitlementsToUpdate = new List<(string EntitlementId, string Json)>();
                using (SqliteCommand commandSelect = connection.CreateCommand())
                {
                    commandSelect.CommandText = $"SELECT Entitlement_id, JSON FROM {targetTable}";
                    using SqliteDataReader reader = commandSelect.ExecuteReader();
                    while (reader.Read())
                    {
                        entitlementsToUpdate.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

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
                    DateTimeOffset? activeDate =
                        TryGetAny(root, out JsonElement activeDateElement, "active_date", "activeDate") &&
                        activeDateElement.ValueKind != JsonValueKind.Null
                            ? DateTimeOffset.Parse(activeDateElement.GetString()!)
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
                        ProductId = productId,
                        Membership = membership,
                        InactiveDate = inactiveDate,
                        ActiveDate = activeDate,
                        RetentionPolicy = retentionPolicy,
                        Branding = branding
                    });
                }

                preparedProfiles.Add((profileId, processedEntitlements));
            }
        }
        finally
        {
            using SqliteCommand detachCommand = connection.CreateCommand();
            detachCommand.CommandText = "DETACH DATABASE sourceDb";
            detachCommand.ExecuteNonQuery();
        }

        var processedProfiles = new List<(int ProfileId, List<(dynamic Entitlement, byte[]? Image)> Entitlements)>();
        var imageDownloads = new Dictionary<string, Task<byte[]?>>();
        using SemaphoreSlim semaphore = new(10);
        try
        {
            foreach (var (profileId, entitlements) in preparedProfiles)
            {
                var downloads = new List<Task<byte[]?>>();
                foreach (var ent in entitlements)
                {
                    string? imageUrl = ent.ImageUrl;
                    string entitlementId = ent.EntitlementId;
                    if (string.IsNullOrWhiteSpace(imageUrl) || entitlementsWithImages.Contains(entitlementId))
                    {
                        downloads.Add(Task.FromResult<byte[]?>(null));
                        continue;
                    }

                    if (!imageDownloads.TryGetValue(entitlementId, out Task<byte[]?>? download))
                    {
                        download = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                return await apiClient.DownloadImageOrNullAsync(imageUrl);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });
                        imageDownloads.Add(entitlementId, download);
                    }

                    downloads.Add(download);
                }

                var processedEntitlements = new List<(dynamic Entitlement, byte[]? Image)>();
                int i = processedProfiles.Count;
                var profilePrefix = profileIds.Count > 1 ? $"[{i + 1}/{profileIds.Count}] " : string.Empty;
                progress?.Report(new ProgressReport($"{profilePrefix}Updating Profile {profileId}...",
                    (double)i / profileIds.Count * 100));
                for (int j = 0; j < entitlements.Count; j++)
                {
                    if (j % 10 == 0 || j == entitlements.Count - 1)
                    {
                        progress?.Report(new ProgressReport(
                            $"{profilePrefix}Processing entitlement {j + 1}/{entitlements.Count}...",
                            (i + (double)j / entitlements.Count) / profileIds.Count * 100));
                    }

                    processedEntitlements.Add((entitlements[j], await downloads[j]));
                }

                processedProfiles.Add((profileId, processedEntitlements));
            }
        }
        finally
        {
            await Task.WhenAll(imageDownloads.Values);
        }

        using (SqliteTransaction transaction = connection.BeginTransaction())
        {
            foreach (var (profileId, processedEntitlements) in processedProfiles)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO ConsoleProfiles (ProfileID, ConsoleID, Name)
                        SELECT @id, @consoleId, @name
                        WHERE NOT EXISTS (SELECT 1 FROM ConsoleProfiles WHERE ConsoleID = @consoleId AND ProfileID = @id)
                        """;
                    command.Parameters.AddWithValue("@id", profileId);
                    command.Parameters.AddWithValue("@consoleId", consoleId);
                    command.Parameters.AddWithValue("@name", $"Profile {profileId}");
                    command.ExecuteNonQuery();
                }

                foreach (var (ent, image) in processedEntitlements)
                {
                    using (SqliteCommand commandUpsert = connection.CreateCommand())
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
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            """
                            INSERT INTO ProfileEntitlements (ConsoleID, ProfileID, EntitlementID, ProductID, Membership, InactiveDate, ActiveDate, RetentionPolicy, FeatureType, Branding)
                            VALUES(@consoleId, @profileId, @entitlementId, @productId, @membership, @inactiveDate, @activeDate, @retentionPolicy, @featureType, @branding)
                            ON CONFLICT(ConsoleID, ProfileID, EntitlementID) DO UPDATE SET
                                ProductID = @productId,
                                Membership = @membership,
                                InactiveDate = @inactiveDate,
                                ActiveDate = @activeDate,
                                RetentionPolicy = @retentionPolicy,
                                FeatureType = @featureType,
                                Branding = @branding;
                            """;
                        command.Parameters.AddWithValue("@consoleId", consoleId);
                        command.Parameters.AddWithValue("@profileId", profileId);
                        command.Parameters.AddWithValue("@entitlementId", ent.EntitlementId);
                        command.Parameters.AddWithValue("@productId",
                            ent.ProductId is null ? DBNull.Value : ent.ProductId);
                        command.Parameters.AddWithValue("@membership", ent.Membership ?? "NONE");
                        command.Parameters.AddWithValue("@inactiveDate",
                            ent.InactiveDate is null
                                ? DBNull.Value
                                : ((DateTimeOffset)ent.InactiveDate).ToUnixTimeSeconds());
                        command.Parameters.AddWithValue("@activeDate",
                            ent.ActiveDate is null
                                ? DBNull.Value
                                : ((DateTimeOffset)ent.ActiveDate).ToUnixTimeSeconds());
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
        }

        progress?.Report(new ProgressReport("Done.", 100));
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
                (SELECT COUNT(*) FROM ProfileEntitlements pe WHERE pe.ConsoleID = cp.ConsoleID AND pe.ProfileID = cp.ProfileID) AS EntitlementCount
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
        command.CommandText =
            """
            INSERT INTO ConsoleProfiles (ProfileID, ConsoleID, Name, AccountID)
            VALUES (@id, @consoleId, @name, @accountId)
            ON CONFLICT(ConsoleID, ProfileID) DO UPDATE SET
                Name = @name,
                AccountID = @accountId,
                UpdatedAt = unixepoch();
            """;
        command.Parameters.AddWithValue("@id", profile.Id);
        command.Parameters.AddWithValue("@consoleId", profile.ConsoleId);
        command.Parameters.AddWithValue("@name", profile.Name);
        command.Parameters.AddWithValue("@accountId", ToDbValue(profile.AccountId));
        command.ExecuteNonQuery();
    }

    public void DeleteProfile(int consoleId, int profileId)
    {
        SqliteConnection connection = databaseService.GetConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ConsoleProfiles WHERE ConsoleID = @consoleId AND ProfileID = @id";
        command.Parameters.AddWithValue("@consoleId", consoleId);
        command.Parameters.AddWithValue("@id", profileId);
        command.ExecuteNonQuery();
    }

    // Entitlement JSON uses snake_case keys on PS4 exports and camelCase keys on PS5 exports
    private static bool TryGetAny(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (string name in names)
            if (element.TryGetProperty(name, out value))
                return true;

        value = default;
        return false;
    }

    private static JsonElement GetRequired(JsonElement element, string entitlementId, params string[] names)
    {
        if (TryGetAny(element, out JsonElement value, names))
            return value;

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
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
            IpAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
            FtpPort = reader.GetInt32(5)
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