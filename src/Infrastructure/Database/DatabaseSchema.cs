using Microsoft.Data.Sqlite;

namespace PSNav.Infrastructure.Database;

internal static class DatabaseSchema
{
    internal const int CurrentVersion = 3;

    internal const string ConsoleProfilesDefinition =
        """
        ProfileID INTEGER NOT NULL,
        ConsoleID INTEGER NOT NULL,
        Name TEXT NOT NULL,
        AccountID TEXT NULL,
        CreatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
        UpdatedAt INTEGER NOT NULL DEFAULT (unixepoch()),
        PRIMARY KEY (ConsoleID, ProfileID),
        FOREIGN KEY (ConsoleID) REFERENCES Consoles (ConsoleID) ON DELETE CASCADE,
        FOREIGN KEY (AccountID) REFERENCES Accounts (AccountID) ON DELETE SET NULL
        """;

    internal const string ProfileEntitlementsDefinition =
        """
        ConsoleID INTEGER NOT NULL,
        ProfileID INTEGER NOT NULL,
        EntitlementID TEXT NOT NULL,
        ProductID TEXT NULL,
        Membership TEXT NULL,
        InactiveDate INTEGER NULL,
        ActiveDate INTEGER NULL,
        RetentionPolicy INTEGER NULL,
        FeatureType INTEGER NULL,
        Branding TEXT NULL,
        PRIMARY KEY (ConsoleID, ProfileID, EntitlementID),
        FOREIGN KEY (ConsoleID, ProfileID) REFERENCES ConsoleProfiles (ConsoleID, ProfileID) ON DELETE CASCADE,
        FOREIGN KEY (EntitlementID) REFERENCES Entitlements (EntitlementID) ON DELETE CASCADE
        """;

    internal static void Create(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
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
                IpAddress TEXT NULL,
                FtpPort INTEGER NOT NULL DEFAULT 2121 CHECK (FtpPort BETWEEN 1 AND 65535),
                Platform TEXT NULL,
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
                {ConsoleProfilesDefinition}
            );

            CREATE TRIGGER IF NOT EXISTS ConsoleProfiles_SetUpdatedAt
            AFTER UPDATE ON ConsoleProfiles
            FOR EACH ROW
            WHEN NEW.UpdatedAt = OLD.UpdatedAt
            BEGIN
                UPDATE ConsoleProfiles
                SET UpdatedAt = unixepoch()
                WHERE ConsoleID = NEW.ConsoleID AND ProfileID = NEW.ProfileID;
            END;

            CREATE TABLE IF NOT EXISTS ProfileEntitlements
            (
                {ProfileEntitlementsDefinition}
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

            CREATE TABLE IF NOT EXISTS BackloggerySync
            (
                EntitlementID TEXT NOT NULL PRIMARY KEY,
                Status TEXT NOT NULL,
                BackloggeryGameID INTEGER NULL,
                Platform TEXT NULL,
                SyncedAt INTEGER NOT NULL DEFAULT (unixepoch())
            );

            CREATE INDEX IF NOT EXISTS IX_Entitlements_Name
                ON Entitlements (Name COLLATE NOCASE);

            CREATE INDEX IF NOT EXISTS IX_Entitlements_TitleID
                ON Entitlements (TitleID);

            CREATE INDEX IF NOT EXISTS IX_AccountEntitlements_AccountID
                ON AccountEntitlements (AccountID);

            CREATE INDEX IF NOT EXISTS IX_AccountEntitlements_EntitlementID
                ON AccountEntitlements (EntitlementID);

            CREATE INDEX IF NOT EXISTS IX_ProfileEntitlements_EntitlementID
                ON ProfileEntitlements (EntitlementID);

            CREATE INDEX IF NOT EXISTS IX_ConsoleProfiles_AccountID
                ON ConsoleProfiles (AccountID);

            CREATE INDEX IF NOT EXISTS IX_Entitlements_EntitlementID
                ON Entitlements (EntitlementID);

            CREATE INDEX IF NOT EXISTS IX_AccountEntitlements_EntitlementID_AccountID
                ON AccountEntitlements (EntitlementID, AccountID, ProductID, Membership, IsActive);

            CREATE INDEX IF NOT EXISTS IX_ProfileEntitlements_EntitlementID_ConsoleID_ProfileID
                ON ProfileEntitlements (EntitlementID, ConsoleID, ProfileID, ProductID, Membership, InactiveDate);

            CREATE INDEX IF NOT EXISTS IX_Accounts_AccountID_OnlineID
                ON Accounts (AccountID, OnlineID);

            CREATE INDEX IF NOT EXISTS IX_ConsoleProfiles_ConsoleID_ProfileID_AccountID
                ON ConsoleProfiles (ConsoleID, ProfileID, AccountID);
            """;
        command.ExecuteNonQuery();
    }
}
