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

    /// <summary>Tests whether an option uses the configured control kind.</summary>
    private sealed class KindIs : IValueConverter
    {
        /// <summary>The kind represented by the control.</summary>
        private readonly OptionKind _expected;

        /// <summary>Creates a predicate for the specified option kind.</summary>
        /// <param name="expected">The kind represented by the control.</param>
        public KindIs(OptionKind expected)
        {
            _expected = expected;
        }

        /// <inheritdoc />
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is OptionKind k && k == _expected;

        /// <inheritdoc />
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Binds a format choice to its checked state.</summary>
    private sealed class FormatEquals : IValueConverter
    {
        /// <summary>The format selected when the control is checked.</summary>
        private readonly DesktopOutputFormat _expected;

        /// <summary>Creates a converter for the specified output format.</summary>
        /// <param name="expected">The output format represented by the control.</param>
        public FormatEquals(DesktopOutputFormat expected)
        {
            _expected = expected;
        }

        /// <inheritdoc />
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is DesktopOutputFormat f && f == _expected;

        /// <inheritdoc />
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? _expected : Avalonia.Data.BindingOperations.DoNothing;
    }

    /// <summary>Evaluates a display predicate from the bound value.</summary>
    private sealed class FuncConverter : IValueConverter
    {
        /// <summary>The rule applied to the bound value.</summary>
        private readonly Func<object?, bool> _predicate;

        /// <summary>Creates a converter for the specified display condition.</summary>
        /// <param name="predicate">The condition that determines the resulting Boolean value.</param>
        public FuncConverter(Func<object?, bool> predicate)
        {
            _predicate = predicate;
        }

        /// <inheritdoc />
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => _predicate(value);

        /// <inheritdoc />
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
