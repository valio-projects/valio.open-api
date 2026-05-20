using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using ValIO.Core.Plugins;

namespace ValIO.Desktop.ViewModels;

/// <summary>
/// View model for a single plugin option. Captures the user's choice in a
/// strongly-typed slot (one per <see cref="OptionKind"/>) and exposes a
/// normalized <see cref="CurrentValue"/> consumed by GenerationViewModel.
///
/// Also exposes <see cref="IsVisible"/> which the host VM toggles based on
/// <see cref="OptionDescription.DependsOn"/> relationships.
/// </summary>
public sealed partial class OptionFieldViewModel : ObservableObject
{
    public OptionDescription Description { get; }

    [ObservableProperty] private bool _boolValue;
    [ObservableProperty] private string? _stringValue;
    [ObservableProperty] private int _intValue;
    [ObservableProperty] private string? _enumSingleValue;
    [ObservableProperty] private bool _isVisible = true;

    public ObservableCollection<string> EnumMultiSelected { get; } = new();
    public IReadOnlyList<OptionChoice> Choices { get; }

    /// <summary>
    /// Localized display: what the ComboBox/ListBox shows. When DescriptionKey is
    /// set we fall back to <see cref="OptionChoice.DisplayName"/>.
    /// </summary>
    public IReadOnlyList<string> ChoiceLabels { get; }
    public IReadOnlyList<string> ChoiceIds { get; }

    public event EventHandler? ValueChanged;

    public OptionFieldViewModel(OptionDescription description)
    {
        Description = description;
        Choices = description.Choices ?? Array.Empty<OptionChoice>();
        ChoiceLabels = Choices.Select(c => c.DisplayName).ToArray();
        ChoiceIds = Choices.Select(c => c.Id).ToArray();

        switch (description.Kind)
        {
            case OptionKind.Bool:
                BoolValue = bool.TryParse(description.DefaultValue, out var b) && b;
                break;
            case OptionKind.Int:
                IntValue = int.TryParse(description.DefaultValue, out var i) ? i : 0;
                break;
            case OptionKind.EnumSingle:
                EnumSingleValue = description.DefaultValue;
                break;
            default:
                StringValue = description.DefaultValue;
                break;
        }

        EnumMultiSelected.CollectionChanged += (_, _) => Raise();
    }

    public object? CurrentValue => Description.Kind switch
    {
        OptionKind.Bool       => BoolValue,
        OptionKind.Int        => IntValue,
        OptionKind.EnumSingle => EnumSingleValue,
        OptionKind.EnumMulti  => EnumMultiSelected.ToArray(),
        _                     => StringValue,
    };

    /// <summary>True when the user has actually picked / typed something non-default.</summary>
    public bool HasValue => Description.Kind switch
    {
        OptionKind.Bool       => true, // bool is always set
        OptionKind.Int        => true,
        OptionKind.EnumSingle => !string.IsNullOrWhiteSpace(EnumSingleValue),
        OptionKind.EnumMulti  => EnumMultiSelected.Count > 0,
        _                     => !string.IsNullOrWhiteSpace(StringValue),
    };

    partial void OnBoolValueChanged(bool value) => Raise();
    partial void OnStringValueChanged(string? value) => Raise();
    partial void OnIntValueChanged(int value) => Raise();
    partial void OnEnumSingleValueChanged(string? value) => Raise();

    private void Raise() => ValueChanged?.Invoke(this, EventArgs.Empty);
}
