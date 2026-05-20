using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ValIO.Desktop.ViewModels;

namespace ValIO.Desktop.Views;

public partial class GenerationView : UserControl
{
    public GenerationView() => InitializeComponent();

    /// <summary>
    /// Click handler for the multi-select chip ToggleButton inside the EnumMulti template.
    /// Adds or removes the chip's <c>Tag</c> (option choice id) from the field VM's
    /// <see cref="OptionFieldViewModel.EnumMultiSelected"/>.
    /// </summary>
    private void OnMultiChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn) return;
        if (btn.Tag is not string id) return;
        // Walk up the visual tree to find the OptionFieldViewModel that owns this chip.
        if (FindAncestorWithDataContext<OptionFieldViewModel>(btn) is not { } field) return;

        if (btn.IsChecked == true)
        {
            if (!field.EnumMultiSelected.Contains(id))
                field.EnumMultiSelected.Add(id);
        }
        else
        {
            field.EnumMultiSelected.Remove(id);
        }
    }

    /// <summary>
    /// MenuItem.Click handler for entries in the "Recent" flyout. Looks up the
    /// owning GenerationViewModel and sets its schema path to the clicked entry.
    /// </summary>
    private void OnRecentClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string path) return;
        if (DataContext is GenerationViewModel vm) vm.SetSchemaPath(path);
    }

    private static T? FindAncestorWithDataContext<T>(Control control) where T : class
    {
        Avalonia.StyledElement? current = control;
        while (current is not null)
        {
            if (current.DataContext is T match) return match;
            current = current.Parent;
        }
        return null;
    }
}
