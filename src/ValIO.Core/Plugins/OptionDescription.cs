namespace ValIO.Core.Plugins;

public enum OptionKind
{
    Bool,
    String,
    Int,
    EnumSingle,
    EnumMulti
}

/// <summary>Describes one accepted value for a configurable generator option.</summary>
public sealed class OptionChoice : IEquatable<OptionChoice>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="Id">The value used in generator configuration.</param>
    /// <param name="DisplayName">The default label for the choice.</param>
    /// <param name="DescriptionKey">The resource key for a localized description.</param>
    public OptionChoice(string Id, string DisplayName, string? DescriptionKey = null)
    {
        this.Id = Id;
        this.DisplayName = DisplayName;
        this.DescriptionKey = DescriptionKey;
    }

    /// <summary>Gets the value used in generator configuration.</summary>
    /// <value>The value used in generator configuration.</value>
    public string Id { get; init; }

    /// <summary>Gets the default label for the choice.</summary>
    /// <value>The default label for the choice.</value>
    public string DisplayName { get; init; }

    /// <summary>Gets the resource key for a localized description.</summary>
    /// <value>The resource key for a localized description.</value>
    public string? DescriptionKey { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(OptionChoice? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Id, other.Id)
        && EqualityComparer<string>.Default.Equals(DisplayName, other.DisplayName)
        && EqualityComparer<string?>.Default.Equals(DescriptionKey, other.DescriptionKey);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OptionChoice other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, DisplayName, DescriptionKey);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(OptionChoice? left, OptionChoice? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(OptionChoice? left, OptionChoice? right) => !Equals(left, right);
}

/// <summary>
/// Description of a single plugin option, used by UI frontends to render an input control
/// and by validators to check raw values.
/// </summary>
public sealed class OptionDescription : IEquatable<OptionDescription>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="Name">The option key as it appears in configuration or on the command line.</param>
    /// <param name="Description">The default description for the option.</param>
    /// <param name="DefaultValue">The default configuration value, when present.</param>
    /// <param name="IsRequired">Whether the value must be supplied explicitly.</param>
    /// <param name="Kind">The input kind used to select a control.</param>
    /// <param name="Choices">The accepted choices for a constrained option.</param>
    /// <param name="DescriptionKey">The resource key for a localized description.</param>
    /// <param name="Group">The group displayed with related options.</param>
    /// <param name="DependsOn">The controlling option, when applicable.</param>
    public OptionDescription(
        string Name,
        string Description,
        string? DefaultValue,
        bool IsRequired,
        OptionKind Kind = OptionKind.String,
        IReadOnlyList<OptionChoice>? Choices = null,
        string? DescriptionKey = null,
        string? Group = null,
        string? DependsOn = null)
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

    /// <summary>Gets the option key as it appears in configuration or on the command line.</summary>
    /// <value>The option key as it appears in configuration or on the command line.</value>
    public string Name { get; init; }

    /// <summary>Gets the default description for the option.</summary>
    /// <value>The default description for the option.</value>
    public string Description { get; init; }

    /// <summary>Gets the default configuration value, when present.</summary>
    /// <value>The default configuration value, when present.</value>
    public string? DefaultValue { get; init; }

    /// <summary>Gets whether the value must be supplied explicitly.</summary>
    /// <value>Whether the value must be supplied explicitly.</value>
    public bool IsRequired { get; init; }

    /// <summary>Gets the input kind used to select a control.</summary>
    /// <value>The input kind used to select a control.</value>
    public OptionKind Kind { get; init; }

    /// <summary>Gets the accepted choices for a constrained option.</summary>
    /// <value>The accepted choices for a constrained option.</value>
    public IReadOnlyList<OptionChoice>? Choices { get; init; }

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
    public bool Equals(OptionDescription? other) => other is not null
        && EqualityComparer<string>.Default.Equals(Name, other.Name)
        && EqualityComparer<string>.Default.Equals(Description, other.Description)
        && EqualityComparer<string?>.Default.Equals(DefaultValue, other.DefaultValue)
        && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
        && EqualityComparer<OptionKind>.Default.Equals(Kind, other.Kind)
        && EqualityComparer<IReadOnlyList<OptionChoice>?>.Default.Equals(Choices, other.Choices)
        && EqualityComparer<string?>.Default.Equals(DescriptionKey, other.DescriptionKey)
        && EqualityComparer<string?>.Default.Equals(Group, other.Group)
        && EqualityComparer<string?>.Default.Equals(DependsOn, other.DependsOn);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OptionDescription other && Equals(other);

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
    public static bool operator ==(OptionDescription? left, OptionDescription? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(OptionDescription? left, OptionDescription? right) => !Equals(left, right);
}
