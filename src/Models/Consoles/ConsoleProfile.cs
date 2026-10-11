using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PSNav.Models.Consoles;

public sealed partial class ConsoleProfile : ViewModels.ViewModelBase
{
    [ObservableProperty] public partial string? AccountId { get; set; }
    [ObservableProperty] public partial string? OnlineId { get; set; }
    public required int Id { get; init; }
    public int ConsoleId { get; init; }
    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    // Not saved in the database
    public int EntitlementCount { get; set; }
}