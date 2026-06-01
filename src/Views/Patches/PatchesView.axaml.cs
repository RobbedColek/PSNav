using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PSNav.ViewModels.Patches;

namespace PSNav.Views.Patches;

public partial class PatchesView : UserControl
{
    public PatchesView()
    {
        InitializeComponent();

        PatchesViewModel viewModel = App.GetServices().GetRequiredService<PatchesViewModel>();
        DataContext = viewModel;

        AttachedToVisualTree += (_, _) => viewModel.LoadPatchesCommand.Execute(null);
    }
}