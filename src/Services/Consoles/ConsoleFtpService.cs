using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using FluentFTP.Exceptions;
using PSNav.Models.Common;

namespace PSNav.Services.Consoles;

public sealed class ConsoleFtpService : IConsoleFtpService
{
    private const string DatabaseDirectory = "/system_data/priv/license";

    public async Task<string> DownloadDatabaseAsync(string ipAddress, int port, string destinationPath,
        IProgress<ProgressReport>? progress = null)
    {
        if (!IPAddress.TryParse(ipAddress, out IPAddress? address))
            throw new ArgumentException("Enter a valid console IP address.", nameof(ipAddress));

        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "The FTP port must be between 1 and 65535.");

        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string endpoint = $"{address}:{port}";
        string stage = "Connecting";
        await using SingleAttemptFtpClient client = new(address.ToString(), port);

        try
        {
            progress?.Report(new ProgressReport(stage));
            await client.Connect();

            stage = "Finding database";
            progress?.Report(new ProgressReport(stage));
            await client.SetWorkingDirectory(DatabaseDirectory);
            List<FtpListItem> listing = await client.GetDatabaseListingAsync();
            FtpListItem? database = listing
                .Where(item => item.Type == FtpObjectType.File &&
                    (item.Name is "entitlement4.db" or "entitlement.db") &&
                    string.Equals(item.FullName, $"{DatabaseDirectory}/{item.Name}", StringComparison.Ordinal))
                .OrderBy(item => item.Name == "entitlement4.db" ? 0 : 1)
                .FirstOrDefault();

            if (database is null)
                throw new FileNotFoundException(
                    $"No entitlement4.db or entitlement.db file was found in {DatabaseDirectory}.");

            string remotePath = $"{DatabaseDirectory}/{database.Name}";
            stage = "Downloading";
            progress?.Report(new ProgressReport(stage));
            FtpStatus result = await client.DownloadFile(destinationPath, remotePath,
                FtpLocalExists.Overwrite, FtpVerify.None,
                progress is null ? null : new DownloadProgress(progress, database.Size));

            if (result != FtpStatus.Success)
                throw new IOException($"The FTP download returned {result}.");

            return database.Name;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Console FTP {endpoint} — {stage}: {exception.Message}", exception);
        }
    }

    private sealed class DownloadProgress(IProgress<ProgressReport> progress, long listedSize) : IProgress<FtpProgress>
    {
        public void Report(FtpProgress value)
        {
            double? percent = double.IsFinite(value.Progress) && value.Progress is >= 0 and <= 100
                ? value.Progress
                : null;
            if (percent is null && listedSize > 0)
                percent = Math.Clamp((double)value.TransferredBytes / listedSize * 100, 0, 100);
            progress.Report(new ProgressReport("Downloading", percent));
        }
    }

    private sealed class SingleAttemptFtpClient : AsyncFtpClient
    {
        private bool _downloadStarted;

        public SingleAttemptFtpClient(string host, int port) : base(host, "anonymous", "anonymous", port)
        {
            Config.EncryptionMode = FtpEncryptionMode.None;
            Config.DataConnectionEncryption = false;
            Config.DataConnectionType = FtpDataConnectionType.PASV;
            Config.DownloadDataType = FtpDataType.Binary;
            Config.ListingDataType = FtpDataType.Binary;
            Config.ConnectTimeout = 5000;
            Config.ReadTimeout = 15000;
            Config.DataConnectionConnectTimeout = 15000;
            Config.DataConnectionReadTimeout = 15000;
            Config.RetryAttempts = 1;
            Config.SelfConnectMode = FtpSelfConnectMode.Never;
            Config.DisconnectWithQuit = false;
        }

        public async Task<List<FtpListItem>> GetDatabaseListingAsync()
        {
            const string command = "LIST";
            List<string> rawListing = await GetListingInternal(command,
                FtpListOption.ForceList | FtpListOption.NoPath, false, CancellationToken.None);

            if (LastReply.Type != FtpResponseType.PositiveCompletion)
                throw new FtpCommandException(LastReply);

            string path = DatabaseDirectory;
            FtpListItem item = null!;
            List<FtpListItem> listing = new();
            for (int i = 0; i < rawListing.Count; i++)
                LoadBasicListingInfo(ref path, ref item, listing, rawListing, ref i, command,
                    rawListing[i], false, false, false);

            return listing;
        }

        public override Task<Stream> OpenReadInternal(string path, FtpDataType type, long fileLen,
            long restart, bool ignoreStaleData, CancellationToken token = default)
        {
            if (_downloadStarted || restart != 0)
                throw new IOException("The transfer was interrupted, automatic FTP resume is disabled.");

            _downloadStarted = true;

            return base.OpenReadInternal(path, type, fileLen, restart, ignoreStaleData, token);
        }
    }
}
