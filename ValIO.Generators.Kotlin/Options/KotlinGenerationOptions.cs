namespace ValIO.Generators.Kotlin.Options;

/// <summary>
/// Generation options for the Kotlin plugin.
/// </summary>
public sealed class KotlinGenerationOptions
{
    /// <summary>Root package for generated Kotlin code (dot-separated).</summary>
    public string Package { get; set; } = "com.example.generated";

    /// <summary>Serialization library to target: kotlinx (default), moshi, gson (reserved).</summary>
    public string Serialization { get; set; } = KotlinOptionIds.Serialization.Kotlinx;

    /// <summary>Whether to also emit Gradle/build files. Disabled by default.</summary>
    public bool GenerateBuildFiles { get; set; } = false;
}
