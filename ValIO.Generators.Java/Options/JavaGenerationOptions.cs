namespace ValIO.Generators.Java.Options;

/// <summary>
/// Options for Java code generation.
/// </summary>
public sealed class JavaGenerationOptions
{
    /// <summary>
    /// Root package for generated Java classes (default: <c>com.example.generated</c>).
    /// </summary>
    public string Package { get; set; } = "com.example.generated";

    /// <summary>
    /// HTTP client transport: <c>okhttp</c> (default).
    /// </summary>
    public string UseHttpClient { get; set; } = JavaOptionIds.HttpClient.OkHttp;

    /// <summary>
    /// If true, emit <c>build.gradle.kts</c> and <c>gradle.properties</c> with required dependencies.
    /// </summary>
    public bool GenerateBuildFiles { get; set; } = false;
}
