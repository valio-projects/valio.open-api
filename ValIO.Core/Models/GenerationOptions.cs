namespace ValIO.Core.Models;

/// <summary>
/// Опции генерации кода (Strategy pattern)
/// </summary>
public class GenerationOptions
{
    public ValidationLibrary? ValidationLibrary { get; set; }
    public HttpClientLibrary? HttpClientLibrary { get; set; }
    public QueryWrapperLibrary? QueryWrapperLibrary { get; set; }
    
    /// <summary>
    /// C# специфичные опции (используется только для C# генератора)
    /// </summary>
    public object? CSharpOptions { get; set; }
}

