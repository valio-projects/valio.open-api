namespace ValIO.Generators.Php.Options;

/// <summary>
/// Options for PHP code generation.
/// </summary>
public sealed class PhpGenerationOptions
{
    /// <summary>
    /// Root namespace for generated PHP classes (default: <c>App\Generated</c>).
    /// </summary>
    public string Namespace { get; set; } = "App\\Generated";

    /// <summary>
    /// HTTP client transport: <c>guzzle</c> (default) or <c>laravel</c> (reserved).
    /// </summary>
    public string UseHttpClient { get; set; } = PhpOptionIds.HttpClient.Guzzle;

    /// <summary>
    /// If true, emit a <c>composer.json</c> with required dependencies.
    /// </summary>
    public bool GenerateComposerJson { get; set; } = false;
}
