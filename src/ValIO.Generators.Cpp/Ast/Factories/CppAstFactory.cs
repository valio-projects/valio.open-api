using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.Ast.Factories;

/// <summary>
/// Фабрика для создания C++/UE AST строителей и рендереров.
/// Централизует создание компонентов pipeline.
/// </summary>
public sealed class CppAstFactory
{
    private readonly CppGenerationOptions _options;

    public CppAstFactory(CppGenerationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Создаёт координирующий AST-строитель
    /// </summary>
    public CppAstBuilder CreateBuilder()
    {
        return new CppAstBuilder(_options);
    }

    /// <summary>
    /// Создаёт AST-рендерер
    /// </summary>
    public CppAstRenderer CreateRenderer()
    {
        return new CppAstRenderer();
    }
}
