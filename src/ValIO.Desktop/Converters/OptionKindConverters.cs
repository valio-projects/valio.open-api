using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ValIO.Core.Plugins;
using ValIO.Desktop.Localization;
using ValIO.Desktop.ViewModels;

namespace ValIO.Core.Plugins;

/// <summary>
/// Static converters used by the desktop GenerationView. Lives in the
/// <c>ValIO.Core.Plugins</c> namespace only so XAML can reference it via the
/// already-imported <c>xmlns:plug</c>.
/// </summary>
public static class OptionKindConverters
{
    public static readonly IValueConverter IsBool       = new KindIs(OptionKind.Bool);
    public static readonly IValueConverter IsString     = new KindIs(OptionKind.String);
    public static readonly IValueConverter IsInt        = new KindIs(OptionKind.Int);
    public static readonly IValueConverter IsEnumSingle = new KindIs(OptionKind.EnumSingle);
    public static readonly IValueConverter IsEnumMulti  = new KindIs(OptionKind.EnumMulti);

    public static readonly IValueConverter IsZero            = new FuncConverter(v => v is int i && i == 0);
    public static readonly IValueConverter IsGreaterThanZero = new FuncConverter(v => v is int i && i > 0);
    public static readonly IValueConverter IsNull            = new FuncConverter(v => v is null);
    public static readonly IValueConverter IsNotNull         = new FuncConverter(v => v is not null);

    public static readonly IValueConverter FormatIsFolder = new FormatEquals(DesktopOutputFormat.Folder);
    public static readonly IValueConverter FormatIsZip    = new FormatEquals(DesktopOutputFormat.Zip);

    /// <summary>
    /// Bool → IBrush. True (error) returns the system error brush; false returns the
    /// secondary text brush. Used to color the status message under the action bar.
    /// </summary>
    public static readonly IValueConverter ErrorBrush = new ErrorBrushConverter();

    /// <summary>
    /// Bool (IsBusy) → label string. True → action.generating, false → action.generate.
    /// Uses the live <see cref="Localizer"/> so the caption updates on culture change.
    /// </summary>
    public static readonly IValueConverter GenerateLabel = new GenerateLabelConverter();

    /// <summary>
    /// MultiBinding converter: [int count, string template] → <c>string.Format(template, count)</c>.
    /// Used to render the "N options hidden..." note with a localized format string.
    /// </summary>
    public static readonly IMultiValueConverter FormatHidden = new FormatHiddenConverter();

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

    private sealed class ErrorBrushConverter : IValueConverter
    {
        // Cached so we don't allocate a brush per binding tick.
        private static readonly IImmutableSolidColorBrush Error = Brushes.IndianRed;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Error : Brushes.Gray;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class FormatHiddenConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var count = values.Count > 0 && values[0] is int n ? n : 0;
            var template = values.Count > 1 && values[1] is string t ? t : "{0}";
            try { return string.Format(culture, template, count); }
            catch (FormatException) { return template + " " + count; }
        }
    }

    private sealed class GenerateLabelConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var key = value is true ? "action.generating" : "action.generate";
            return Localizer.Instance[key];
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
