using ValIO.Core.Plugins;

namespace ValIO.Web.Endpoints;

/// <summary>
/// GET /api/options/{language} — describe every plugin option that the
/// generator accepts. Used by the Web/Desktop dynamic-form rendering and by
/// 3rd-party tooling that wants to build its own UI on top of ValIO.
/// </summary>
public static class OptionsEndpoint
{
    /// <summary>Describes an accepted value for a plugin option.</summary>
    public sealed class OptionChoiceDto : IEquatable<OptionChoiceDto>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="Id">The value accepted in configuration.</param>
        /// <param name="DisplayName">The display label.</param>
        /// <param name="DescriptionKey">The resource key for a localized description.</param>
        public OptionChoiceDto(string Id, string DisplayName, string? DescriptionKey)
        {
            this.Id = Id;
            this.DisplayName = DisplayName;
            this.DescriptionKey = DescriptionKey;
        }

        /// <summary>Gets the value accepted in configuration.</summary>
        /// <value>The value accepted in configuration.</value>
        public string Id { get; init; }

        /// <summary>Gets the display label.</summary>
        /// <value>The display label.</value>
        public string DisplayName { get; init; }

        /// <summary>Gets the resource key for a localized description.</summary>
        /// <value>The resource key for a localized description.</value>
        public string? DescriptionKey { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(OptionChoiceDto? other) => other is not null
            && EqualityComparer<string>.Default.Equals(Id, other.Id)
            && EqualityComparer<string>.Default.Equals(DisplayName, other.DisplayName)
            && EqualityComparer<string?>.Default.Equals(DescriptionKey, other.DescriptionKey);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is OptionChoiceDto other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Id, DisplayName, DescriptionKey);

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(OptionChoiceDto? left, OptionChoiceDto? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(OptionChoiceDto? left, OptionChoiceDto? right) => !Equals(left, right);
    }

    /// <summary>Describes a plugin option for HTTP consumers.</summary>
    public sealed class OptionDto : IEquatable<OptionDto>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="Name">The option key used in configuration.</param>
        /// <param name="Description">The default description for the option.</param>
        /// <param name="DefaultValue">The initial configuration value, when present.</param>
        /// <param name="IsRequired">Whether the caller must provide a value.</param>
        /// <param name="Kind">The kind of input the client renders.</param>
        /// <param name="Choices">The accepted choices for a constrained option.</param>
        /// <param name="DescriptionKey">The resource key for a localized description.</param>
        /// <param name="Group">The group displayed with related options.</param>
        /// <param name="DependsOn">The controlling option, when applicable.</param>
        public OptionDto(
            string Name,
            string Description,
            string? DefaultValue,
            bool IsRequired,
            string Kind,
            IReadOnlyList<OptionChoiceDto>? Choices,
            string? DescriptionKey,
            string? Group,
            string? DependsOn)
        {
            this.Name = Name;
            this.Description = Description;
            this.DefaultValue = DefaultValue;
            this.IsRequired = IsRequired;
            this.Kind = Kind;
            this.Choices = Choices;
            this.DescriptionKey = DescriptionKey;
            this.Group = Group;
            this.DependsOn = DependsOn;
        }

        /// <summary>Gets the option key used in configuration.</summary>
        /// <value>The option key used in configuration.</value>
        public string Name { get; init; }

        /// <summary>Gets the default description for the option.</summary>
        /// <value>The default description for the option.</value>
        public string Description { get; init; }

        /// <summary>Gets the initial configuration value, when present.</summary>
        /// <value>The initial configuration value, when present.</value>
        public string? DefaultValue { get; init; }

        /// <summary>Gets whether the caller must provide a value.</summary>
        /// <value>Whether the caller must provide a value.</value>
        public bool IsRequired { get; init; }

        /// <summary>Gets the kind of input the client renders.</summary>
        /// <value>The kind of input the client renders.</value>
        public string Kind { get; init; }

        /// <summary>Gets the accepted choices for a constrained option.</summary>
        /// <value>The accepted choices for a constrained option.</value>
        public IReadOnlyList<OptionChoiceDto>? Choices { get; init; }

        /// <summary>Gets the resource key for a localized description.</summary>
        /// <value>The resource key for a localized description.</value>
        public string? DescriptionKey { get; init; }

        /// <summary>Gets the group displayed with related options.</summary>
        /// <value>The group displayed with related options.</value>
        public string? Group { get; init; }

        /// <summary>Gets the controlling option, when applicable.</summary>
        /// <value>The controlling option, when applicable.</value>
        public string? DependsOn { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(OptionDto? other) => other is not null
            && EqualityComparer<string>.Default.Equals(Name, other.Name)
            && EqualityComparer<string>.Default.Equals(Description, other.Description)
            && EqualityComparer<string?>.Default.Equals(DefaultValue, other.DefaultValue)
            && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
            && EqualityComparer<string>.Default.Equals(Kind, other.Kind)
            && EqualityComparer<IReadOnlyList<OptionChoiceDto>?>.Default.Equals(Choices, other.Choices)
            && EqualityComparer<string?>.Default.Equals(DescriptionKey, other.DescriptionKey)
            && EqualityComparer<string?>.Default.Equals(Group, other.Group)
            && EqualityComparer<string?>.Default.Equals(DependsOn, other.DependsOn);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is OptionDto other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(Description);
            hash.Add(DefaultValue);
            hash.Add(IsRequired);
            hash.Add(Kind);
            hash.Add(Choices);
            hash.Add(DescriptionKey);
            hash.Add(Group);
            hash.Add(DependsOn);
            return hash.ToHashCode();
        }

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(OptionDto? left, OptionDto? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(OptionDto? left, OptionDto? right) => !Equals(left, right);
    }

    public static IResult Handle(string language, IGeneratorRegistry registry)
    {
        var plugin = registry.FindPlugin(language);
        if (plugin is null)
            return Results.NotFound(new { error = $"Unknown language: {language}" });

        var options = plugin.OptionsDescriptor.GetOptionDescriptions()
            .Select(d => new OptionDto(
                d.Name,
                d.Description,
                d.DefaultValue,
                d.IsRequired,
                d.Kind.ToString(),
                d.Choices?.Select(c => new OptionChoiceDto(c.Id, c.DisplayName, c.DescriptionKey)).ToArray(),
                d.DescriptionKey,
                d.Group,
                d.DependsOn))
            .ToArray();
        return Results.Ok(options);
    }
}
