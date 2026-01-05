using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Renderers;

/// <summary>
/// Интерфейс рендерера AST для TypeScript
/// </summary>
public interface ITypeScriptAstRenderer : IAstRenderer
{
    /// <summary>
    /// Рендерит SourceFile
    /// </summary>
    string Render(SourceFileNode node);
    
    /// <summary>
    /// Рендерит Interface
    /// </summary>
    string Render(InterfaceDeclarationNode node);
    
    /// <summary>
    /// Рендерит TypeAlias
    /// </summary>
    string Render(TypeAliasDeclarationNode node);
    
    /// <summary>
    /// Рендерит Function
    /// </summary>
    string Render(FunctionDeclarationNode node);
    
    /// <summary>
    /// Рендерит Variable
    /// </summary>
    string Render(VariableDeclarationNode node);
    
    /// <summary>
    /// Рендерит Enum
    /// </summary>
    string Render(EnumDeclarationNode node);
    
    /// <summary>
    /// Рендерит Property
    /// </summary>
    string Render(PropertyDeclarationNode node);
}
