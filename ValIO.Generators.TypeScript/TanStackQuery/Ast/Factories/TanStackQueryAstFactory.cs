using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Renderers;

namespace ValIO.Generators.TypeScript.TanStackQuery.Ast.Factories;

/// <summary>
/// Фабрика для создания TanStack Query AST Builder'ов и Renderer'ов
/// </summary>
public class TanStackQueryAstFactory
{
    private readonly INamingConverter _namingConverter;
    private TanStackQueryAstBuilder? _builder;
    private ITanStackQueryAstRenderer? _renderer;

    public TanStackQueryAstFactory(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    /// <summary>
    /// Создает или возвращает существующий TanStackQueryAstBuilder
    /// </summary>
    public TanStackQueryAstBuilder GetBuilder()
    {
        return _builder ??= new TanStackQueryAstBuilder(_namingConverter);
    }

    /// <summary>
    /// Создает или возвращает существующий TanStackQueryAstRenderer
    /// </summary>
    public ITanStackQueryAstRenderer GetRenderer()
    {
        return _renderer ??= new TanStackQueryAstRenderer();
    }
}
