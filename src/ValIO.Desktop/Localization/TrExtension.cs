using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ValIO.Desktop.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Tr key.name}</c> resolves to a one-way
/// binding against <see cref="Localizer.Instance"/>'s indexer for <c>key.name</c>.
///
/// Because <see cref="Localizer"/> raises <c>PropertyChanged("Item[]")</c>
/// on culture change, every <c>{loc:Tr ...}</c> binding refreshes automatically.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = Key ?? string.Empty;
        return new Binding($"[{key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = key,
        };
    }
}
