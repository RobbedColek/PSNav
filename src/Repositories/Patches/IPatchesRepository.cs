using System.Collections.Generic;
using PSNav.Models.Patches;

namespace PSNav.Repositories.Patches;

public interface IPatchesRepository
{
    IReadOnlyList<Patch> GetAll();
    IReadOnlyList<Patch> GetAllUnsent();
    void UpsertPatch(Patch patch);
}