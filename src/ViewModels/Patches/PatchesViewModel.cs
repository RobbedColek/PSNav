using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PSNav.Models.Patches;
using PSNav.Models.Messages;
using PSNav.Repositories.Patches;
using PSNav.Services.Patches;

namespace PSNav.ViewModels.Patches;

public sealed partial class PatchesViewModel : ViewModelBase
{
    private readonly IPatchesRepository _patchesRepository;
    private readonly IPatchService _patchService;

    public PatchesViewModel(IPatchesRepository patchesRepository, IPatchService patchService)
    {
        _patchesRepository = patchesRepository;
        _patchService = patchService;
        WeakReferenceMessenger.Default.Register<ConsolesChangedMessage>(this, (_, _) => RefreshPatches());
    }

    public ObservableCollection<Patch> Patches { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    public partial bool IsUploading { get; set; }

    [ObservableProperty] public partial string UploadStatus { get; set; } = string.Empty;

    [ObservableProperty] public partial double UploadProgress { get; set; }

    [RelayCommand]
    private void LoadPatches()
    {
        if (Patches.Count > 0) return;
        RefreshPatches();
    }

    private void RefreshPatches()
    {
        Patches.Clear();

        foreach (Patch patch in _patchesRepository.GetAll())
        {
            Patches.Add(patch);
        }

        UploadCommand.NotifyCanExecuteChanged();
    }

    private bool CanUpload() => !IsUploading && Patches.Any(p => p.IsSent == false);

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        var patches = _patchesRepository.GetAllUnsent();

        if (patches.Count == 0)
            return;

        IsUploading = true;
        UploadProgress = 0;

        try
        {
            for (var i = 0; i < patches.Count; i++)
            {
                Patch patch = patches[i];

                UploadStatus = $"[{i + 1}/{patches.Count}] Sending JSON URLs...";
                UploadProgress = (double)i / patches.Count * 100;

                bool success = await _patchService.UploadPatch(patch);

                if (!success) continue;
                patch.IsSent = true;
                _patchesRepository.UpsertPatch(patch);
            }

            UploadProgress = 100;
        }
        finally
        {
            IsUploading = false;
            UploadStatus = string.Empty;
            RefreshPatches();
        }
    }
}