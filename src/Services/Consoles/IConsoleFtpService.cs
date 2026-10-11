using System;
using System.Threading.Tasks;
using PSNav.Models.Common;

namespace PSNav.Services.Consoles;

public interface IConsoleFtpService
{
    Task DownloadDatabaseAsync(string ipAddress, int port, string destinationPath,
        IProgress<ProgressReport>? progress = null);
}
