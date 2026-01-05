using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.Zod.Ast.Builders;
using ValIO.Generators.TypeScript.Zod.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Zod.Ast.Factories;

/// <summary>
/// Фабрика для создания Zod AST Builder'ов и Renderer'ов
/// </summary>
public class ZodAstFactory
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    private ZodAstBuilder? _builder;
    private IZodAstRenderer? _renderer;
    
    public ZodAstFactory(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }
    
    /// <summary>
    /// Создает или возвращает существующий ZodAstBuilder
    /// </summary>
    public ZodAstBuilder GetBuilder()
    {
        return _builder ??= new ZodAstBuilder(_namingConverter, _typeMapper);
    }
    
    /// <summary>
    /// Создает или возвращает существующий ZodAstRenderer
    /// </summary>
    public IZodAstRenderer GetRenderer()
    {
        return _renderer ??= new ZodAstRenderer();
    }
}
