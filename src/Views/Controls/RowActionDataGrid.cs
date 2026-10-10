using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PSNav.Views.Controls;

public sealed class RowActionDataGrid : DataGrid, ICustomKeyboardNavigation
{
    protected override Type StyleKeyOverride => typeof(DataGrid);

    public (bool handled, IInputElement? next) GetNext(IInputElement element, NavigationDirection direction)
    {
        if (!IsReadOnly || direction is not (NavigationDirection.Next or NavigationDirection.Previous))
            return (false, null);

        bool reverse = direction == NavigationDirection.Previous;
        bool inside = ReferenceEquals(element, this) ||
                      (element is Control control && ReferenceEquals(control.FindAncestorOfType<DataGrid>(), this));

        if (!inside && !reverse)
            return (true, this);

        if (ReferenceEquals(element, this) && reverse)
            return (false, null);

        List<object> items = CollectionView?.Cast<object>().ToList() ?? [];
        DataGridRow? currentRow = (element as Control)?.FindAncestorOfType<DataGridRow>();
        int rowIndex = currentRow is null ? -1 : items.IndexOf(currentRow.DataContext!);

        if (rowIndex >= 0)
        {
            List<Control> controls = GetRowActions(currentRow!);
            int controlIndex = controls.FindIndex(control => ReferenceEquals(control, element));
            int nextIndex = controlIndex < 0
                ? reverse ? controls.Count - 1 : 0
                : controlIndex + (reverse ? -1 : 1);

            if (nextIndex >= 0 && nextIndex < controls.Count)
                return (true, PrepareAction(controls[nextIndex]));

            rowIndex += reverse ? -1 : 1;
        }
        else
        {
            rowIndex = reverse ? items.Count - 1 : 0;
        }

        for (; rowIndex >= 0 && rowIndex < items.Count; rowIndex += reverse ? -1 : 1)
        {
            DataGridRow? row = RealizeRow(items[rowIndex]);
            if (row is null)
                continue;

            List<Control> controls = GetRowActions(row);
            if (controls.Count > 0)
                return (true, PrepareAction(reverse ? controls[^1] : controls[0]));
        }

        return (true, reverse ? this : null);
    }

    private DataGridRow? RealizeRow(object item)
    {
        DataGridColumn? column = Columns.Where(column => column.IsVisible)
            .OrderBy(column => column.DisplayIndex).FirstOrDefault();
        if (column is null)
            return null;

        ScrollIntoView(item, column);
        UpdateLayout();

        return this.GetVisualDescendants().OfType<DataGridRow>()
            .FirstOrDefault(row => row.IsVisible && Equals(row.DataContext, item));
    }

    private List<Control> GetRowActions(DataGridRow row)
    {
        List<Control> result = [];
        foreach (DataGridColumn column in Columns.Where(column => column.IsVisible)
                     .OrderBy(column => column.DisplayIndex))
        {
            Control? content = column.GetCellContent(row);
            if (content is null)
                continue;

            result.AddRange(content.GetVisualDescendants().Prepend(content).OfType<Control>()
                .Where(control => control.Focusable && control.IsTabStop &&
                                  control.IsEffectivelyEnabled && control.IsVisible &&
                                  control.GetVisualAncestors().OfType<Control>()
                                      .TakeWhile(parent => parent is not DataGridCell)
                                      .All(parent => parent.IsVisible))
                .OrderBy(control => control.TabIndex));
        }

        return result;
    }

    private Control PrepareAction(Control control)
    {
        DataGridRow? row = control.FindAncestorOfType<DataGridRow>();
        DataGridColumn? column = DataGridColumn.GetColumnContainingElement(control);
        if (row?.DataContext is { } item && column is not null)
        {
            ScrollIntoView(item, column);
            UpdateLayout();
        }

        return control;
    }
}
