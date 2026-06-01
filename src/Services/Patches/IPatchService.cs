using System.Threading.Tasks;
using PSNav.Models.Patches;

namespace PSNav.Services.Patches;

public interface IPatchService
{
    Task<bool> UploadPatch(Patch patch);
}