using ValIO.Core.Plugins;

namespace ValIO.Web.Endpoints;

/// <summary>
/// GET /api/languages — list every plugin known to the registry.
/// Drives the language picker in the UI and any 3rd-party client (CLI scripts,
/// "valio" Slack bot, etc.) that needs the catalog without bundling all the
/// generator DLLs themselves.
/// </summary>
public static class LanguagesEndpoint
{
    /// <summary>Describes an installed language generator for the HTTP catalog.</summary>
    public sealed class LanguageDto : IEquatable<LanguageDto>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="LanguageId">The identifier used in generation requests.</param>
        /// <param name="DisplayName">The name displayed in the language picker.</param>
        /// <param name="Aliases">The alternative identifiers accepted by the plugin.</param>
        /// <param name="OptionsCount">The number of supported configuration options.</param>
        public LanguageDto(
            string LanguageId,
            string DisplayName,
            IReadOnlyList<string> Aliases,
            int OptionsCount)
        {
            this.LanguageId = LanguageId;
            this.DisplayName = DisplayName;
            this.Aliases = Aliases;
            this.OptionsCount = OptionsCount;
        }

        /// <summary>Gets the identifier used in generation requests.</summary>
        /// <value>The identifier used in generation requests.</value>
        public string LanguageId { get; init; }

        /// <summary>Gets the name displayed in the language picker.</summary>
        /// <value>The name displayed in the language picker.</value>
        public string DisplayName { get; init; }

        /// <summary>Gets the alternative identifiers accepted by the plugin.</summary>
        /// <value>The alternative identifiers accepted by the plugin.</value>
        public IReadOnlyList<string> Aliases { get; init; }

        /// <summary>Gets the number of supported configuration options.</summary>
        /// <value>The number of supported configuration options.</value>
        public int OptionsCount { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(LanguageDto? other) => other is not null
            && EqualityComparer<string>.Default.Equals(LanguageId, other.LanguageId)
            && EqualityComparer<string>.Default.Equals(DisplayName, other.DisplayName)
            && EqualityComparer<IReadOnlyList<string>>.Default.Equals(Aliases, other.Aliases)
            && EqualityComparer<int>.Default.Equals(OptionsCount, other.OptionsCount);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is LanguageDto other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(LanguageId, DisplayName, Aliases, OptionsCount);

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(LanguageDto? left, LanguageDto? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(LanguageDto? left, LanguageDto? right) => !Equals(left, right);
    }

    public static IResult Handle(IGeneratorRegistry registry)
    {
        var langs = registry.AllPlugins
            .OrderBy(p => p.DisplayName, StringComparer.Ordinal)
            .Select(p => new LanguageDto(
                p.LanguageId,
                p.DisplayName,
                p.LanguageAliases.ToArray(),
                p.OptionsDescriptor.GetOptionDescriptions().Count))
            .ToArray();
        return Results.Ok(langs);
    }
}
