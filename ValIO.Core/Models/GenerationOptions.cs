namespace ValIO.Core.Models;

/// <summary>
/// Опции генерации кода (Strategy pattern)
/// </summary>
public class GenerationOptions
{
    public ValidationLibrary? ValidationLibrary { get; set; }
    public QueryWrapperLibrary? QueryWrapperLibrary { get; set; }
    
    /// <summary>
    /// C# специфичные опции (используется только для C# генератора)
    /// </summary>
    public object? CSharpOptions { get; set; }
    
    /// <summary>
    /// TypeScript специфичные опции (используется только для TypeScript генератора)
    /// </summary>
    public object? TypeScriptOptions { get; set; }
    
    /// <summary>
    /// C++/Unreal Engine специфичные опции (используется только для C++ генератора)
    /// </summary>
    public object? CppOptions { get; set; }
}

