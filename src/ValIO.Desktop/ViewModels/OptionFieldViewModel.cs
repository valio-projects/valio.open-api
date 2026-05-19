using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ValIO.Core.Plugins;

namespace ValIO.Desktop.ViewModels;

/// <summary>
/// View model for a single plugin option, capturing the user's choice.
/// One instance per OptionDescription; surfaces values strongly-typed
/// while storing them as <c>object?</c> in <see cref="GenerationViewModel.PluginOptions"/>.
/// </summary>
public sealed partial class OptionFieldViewModel : ObservableObject
{
    public OptionDescription Description { get; }

    [ObservableProperty] private bool _boolValue;
    [ObservableProperty] private string? _stringValue;
    [ObservableProperty] private int _intValue;
    [ObservableProperty] private string? _enumSingleValue;

    public ObservableCollection<string> EnumMultiSelected { get; } = new();
    public IReadOnlyList<string> EnumChoices { get; }

    public OptionFieldViewModel(OptionDescription description)
    {
        Description = description;
        EnumChoices = description.Choices?.Select(c => c.Id).ToArray() ?? Array.Empty<string>();

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
    }

    public object? CurrentValue => Description.Kind switch
    {
        OptionKind.Bool       => BoolValue,
        OptionKind.Int        => IntValue,
        OptionKind.EnumSingle => EnumSingleValue,
        OptionKind.EnumMulti  => EnumMultiSelected.ToArray(),
        _                     => StringValue,
    };
}
