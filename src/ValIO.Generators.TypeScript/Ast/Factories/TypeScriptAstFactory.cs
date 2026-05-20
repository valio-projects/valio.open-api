using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Ast.Factories;

/// <summary>
/// Фабрика для создания TypeScript AST Builder'ов и Renderer'ов
/// </summary>
public class TypeScriptAstFactory
{
    private readonly INamingConverter _namingConverter;
    private ITypeScriptAstBuilder? _builder;
    private ITypeScriptAstRenderer? _renderer;

    public TypeScriptAstFactory(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    /// <summary>
    /// Создает или возвращает существующий TypeScriptAstBuilder
    /// </summary>
    public ITypeScriptAstBuilder GetBuilder()
    {
        return _builder ??= new TypeScriptAstBuilder(_namingConverter);
    }

    /// <summary>
    /// Создает или возвращает существующий TypeScriptAstRenderer
    /// </summary>
    public ITypeScriptAstRenderer GetRenderer()
    {
        return _renderer ??= new TypeScriptAstRenderer();
    }
}
