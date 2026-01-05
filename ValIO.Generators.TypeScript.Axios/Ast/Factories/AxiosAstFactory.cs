using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.Axios.Ast.Builders;
using ValIO.Generators.TypeScript.Axios.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Axios.Ast.Factories;

/// <summary>
/// Фабрика для создания Axios AST Builder'ов и Renderer'ов
/// </summary>
public class AxiosAstFactory
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    private AxiosAstBuilder? _builder;
    private IAxiosAstRenderer? _renderer;
    
    public AxiosAstFactory(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }
    
    /// <summary>
    /// Создает или возвращает существующий AxiosAstBuilder
    /// </summary>
    public AxiosAstBuilder GetBuilder()
    {
        return _builder ??= new AxiosAstBuilder(_namingConverter, _typeMapper);
    }
    
    /// <summary>
    /// Создает или возвращает существующий AxiosAstRenderer
    /// </summary>
    public IAxiosAstRenderer GetRenderer()
    {
        return _renderer ??= new AxiosAstRenderer();
    }
}
