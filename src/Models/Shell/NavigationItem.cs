using System;

namespace PSNav.Models.Shell;

public record NavigationItem(
    string ToolTip,
    object IconKind,
    Type IconType,
    Type ViewModelType
);