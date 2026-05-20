namespace ValIO.Generators.Ruby.Options;

/// <summary>
/// Generation options for the Ruby plugin.
/// </summary>
/// <remarks>
/// The Ruby plugin currently has no tunable options — Ruby is duck-typed, so
/// the only thing the generator emits is plain classes with <c>attr_accessor</c>
/// declarations. This type exists so callers can pass <see cref="object"/>
/// plugin options through <see cref="ValIO.Core.Plugins.GenerationContext"/>.
/// </remarks>
public sealed class RubyGenerationOptions
{
    /// <summary>
    /// Root directory name (single folder for generated Ruby files).
    /// </summary>
    public string ModuleName { get; set; } = "generated";
}
