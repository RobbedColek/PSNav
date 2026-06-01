using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PSNav.Infrastructure.Database;
using PSNav.Models.Accounts;

namespace PSNav.Repositories.Accounts;

public sealed class AccountsRepository(IDatabaseService databaseService) : IAccountsRepository
{
    public IReadOnlyList<Account> GetAll()
    {
        List<Account> accounts = [];

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.AccountID,
                a.OnlineID,
                a.ProfilePicture,
                a.AvatarPicture,
                a.NPSSOToken,
                a.NPSSOTokenExpiration,
                a.LastSynchronization,
                a.CreatedAt,
                a.UpdatedAt,
                (SELECT COUNT(*) FROM AccountEntitlements ae WHERE ae.AccountID = a.AccountID) AS EntitlementCount
            FROM Accounts a
            ORDER BY a.OnlineID COLLATE NOCASE;
            """;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public Account? GetById(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                a.AccountID,
                a.OnlineID,
                a.ProfilePicture,
                a.AvatarPicture,
                a.NPSSOToken,
                a.NPSSOTokenExpiration,
                a.LastSynchronization,
                a.CreatedAt,
                a.UpdatedAt,
                (SELECT COUNT(*) FROM AccountEntitlements ae WHERE ae.AccountID = a.AccountID) AS EntitlementCount
            FROM Accounts a
            WHERE a.AccountID = $accountId;
            """;

        command.Parameters.AddWithValue("$accountId", accountId);

        using SqliteDataReader reader = command.ExecuteReader();

        return reader.Read()
            ? ReadAccount(reader)
            : null;
    }

    public void Add(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Accounts
            (
                AccountID,
                OnlineID,
                ProfilePicture,
                AvatarPicture,
                NPSSOToken,
                NPSSOTokenExpiration
            )
            VALUES
            (
                $accountId,
                $onlineId,
                $profilePicture,
                $avatarPicture,
                $npssoToken,
                $npssoTokenExpiration
            );
            """;

        AddAccountParameters(command, account);

        command.ExecuteNonQuery();
    }

    public void Upsert(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Accounts
            (
                AccountID,
                OnlineID,
                ProfilePicture,
                AvatarPicture,
                NPSSOToken,
                NPSSOTokenExpiration,
                LastSynchronization
            )
            VALUES
            (
                $accountId,
                $onlineId,
                $profilePicture,
                $avatarPicture,
                $npssoToken,
                $npssoTokenExpiration,
                $lastSynchronization
            )
            ON CONFLICT(AccountID) DO UPDATE SET
                OnlineID = excluded.OnlineID,
                ProfilePicture = excluded.ProfilePicture,
                AvatarPicture = excluded.AvatarPicture,
                NPSSOToken = excluded.NPSSOToken,
                NPSSOTokenExpiration = excluded.NPSSOTokenExpiration,
                LastSynchronization = excluded.LastSynchronization;
            """;

        AddAccountParameters(command, account);

        command.ExecuteNonQuery();
    }

    public void Update(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Accounts
            SET
                OnlineID = $onlineId,
                ProfilePicture = $profilePicture,
                AvatarPicture = $avatarPicture,
                NPSSOToken = $npssoToken,
                NPSSOTokenExpiration = $npssoTokenExpiration,
                LastSynchronization = $lastSynchronization,
                UpdatedAt = $updatedAt
            WHERE AccountID = $accountId;
            """;

        AddAccountParameters(command, account);

        command.ExecuteNonQuery();
    }

    public void Delete(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM Accounts
            WHERE AccountID = $accountId;
            """;

        command.Parameters.AddWithValue("$accountId", accountId);
    }

    public bool Exists(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        SqliteConnection connection = databaseService.GetConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM Accounts
                WHERE AccountID = $accountId
            );
            """;

        command.Parameters.AddWithValue("$accountId", accountId);

        object? result = command.ExecuteScalar();

        return Convert.ToInt32(result) == 1;
    }

    private static void AddAccountParameters(SqliteCommand command, Account account)
    {
        command.Parameters.AddWithValue("$accountId", account.AccountId);
        command.Parameters.AddWithValue("$onlineId", ToDbValue(account.OnlineId));
        command.Parameters.AddWithValue("$profilePicture", ToDbValue(account.ProfilePicture));
        command.Parameters.AddWithValue("$avatarPicture", ToDbValue(account.AvatarPicture));
        command.Parameters.AddWithValue("$npssoToken", ToDbValue(account.NpssoToken));
        command.Parameters.AddWithValue(
            "$npssoTokenExpiration",
            ToDbValue(account.NpssoTokenExpiration?.ToUnixTimeSeconds()));
        command.Parameters.AddWithValue(
            "$lastSynchronization",
            ToDbValue(account.LastSynchronization?.ToUnixTimeSeconds()));
        command.Parameters.AddWithValue("$updatedAt", account.UpdatedAt.ToUnixTimeSeconds());
    }

    private static Account ReadAccount(SqliteDataReader reader)
    {
        return new Account
        {
            AccountId = reader.GetString(0),
            OnlineId = reader.IsDBNull(1) ? null : reader.GetString(1),
            ProfilePicture = reader.IsDBNull(2) ? null : (byte[])reader["ProfilePicture"],
            AvatarPicture = reader.IsDBNull(3) ? null : (byte[])reader["AvatarPicture"],
            NpssoToken = reader.IsDBNull(4) ? null : reader.GetString(4),
            NpssoTokenExpiration = reader.IsDBNull(5)
                ? null
                : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
            LastSynchronization = reader.IsDBNull(6)
                ? null
                : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(7)),
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(8)),
            EntitlementCount = reader.GetInt32(9)
        };
    }

    private static object ToDbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }
}