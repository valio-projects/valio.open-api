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
    private readonly Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> _typeBuilder;
    private AxiosAstBuilder? _builder;
    private IAxiosAstRenderer? _renderer;
    
    public AxiosAstFactory(INamingConverter namingConverter, Func<ValIO.Core.IR.Types.IrType, ValIO.Core.Ast.Nodes.TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
    }
    
    /// <summary>
    /// Создает или возвращает существующий AxiosAstBuilder
    /// </summary>
    public AxiosAstBuilder GetBuilder()
    {
        return _builder ??= new AxiosAstBuilder(_namingConverter, _typeBuilder);
    }
    
    /// <summary>
    /// Создает или возвращает существующий AxiosAstRenderer
    /// </summary>
    public IAxiosAstRenderer GetRenderer()
    {
        return _renderer ??= new AxiosAstRenderer();
    }
}
