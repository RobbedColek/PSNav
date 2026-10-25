using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PSNav.Models.Common;
using PSNav.Models.Consoles;

namespace PSNav.Repositories.Consoles;

public interface IConsolesRepository
{
    IReadOnlyList<ConsoleModel> GetAllConsoles();
    void UpsertConsole(ConsoleModel console);
    void SetPlatformIfUnknown(int consoleId, string platform);
    void DeleteConsole(int consoleId);
    Task UpdateEntitlements(int consoleId, string entitlementsFilePath, IProgress<ProgressReport>? progress = null);
    IReadOnlyList<ConsoleProfile> GetProfilesForConsole(int consoleId);
    void UpsertProfile(ConsoleProfile profile);
}