using System.Globalization;
using Avalonia.Data.Converters;
using ValIO.Core.Plugins;
using ValIO.Desktop.ViewModels;

namespace ValIO.Core.Plugins;

/// <summary>
/// Static converters used by the desktop GenerationView to switch the right control
/// for each OptionKind, and to bind RadioButton.IsChecked to DesktopOutputFormat.
/// </summary>
/// <remarks>
/// Lives in <c>ValIO.Core.Plugins</c> namespace only so the XAML can reference it without
/// extra <c>xmlns</c> declarations. Implementation lives in the desktop assembly.
/// </remarks>
public static class OptionKindConverters
{
    public static readonly IValueConverter IsBool       = new KindIs(OptionKind.Bool);
    public static readonly IValueConverter IsString     = new KindIs(OptionKind.String);
    public static readonly IValueConverter IsInt        = new KindIs(OptionKind.Int);
    public static readonly IValueConverter IsEnumSingle = new KindIs(OptionKind.EnumSingle);
    public static readonly IValueConverter IsEnumMulti  = new KindIs(OptionKind.EnumMulti);

    public static readonly IValueConverter IsZero = new FuncConverter(v => v is int i && i == 0);

    public static readonly IValueConverter FormatIsFolder = new FormatEquals(DesktopOutputFormat.Folder);
    public static readonly IValueConverter FormatIsZip    = new FormatEquals(DesktopOutputFormat.Zip);

    private sealed class KindIs(OptionKind expected) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is OptionKind k && k == expected;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class FormatEquals(DesktopOutputFormat expected) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is DesktopOutputFormat f && f == expected;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? expected : Avalonia.Data.BindingOperations.DoNothing;
    }

    private sealed class FuncConverter(Func<object?, bool> predicate) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => predicate(value);
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
