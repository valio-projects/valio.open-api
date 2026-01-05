using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Renderers;

/// <summary>
/// Интерфейс рендерера AST для C#
/// </summary>
public interface ICSharpAstRenderer : IAstRenderer
{
    /// <summary>
    /// Рендерит CompilationUnit
    /// </summary>
    string Render(CompilationUnitNode node);
    
    /// <summary>
    /// Рендерит Namespace
    /// </summary>
    string Render(NamespaceDeclarationNode node);
    
    /// <summary>
    /// Рендерит Class
    /// </summary>
    string Render(ClassDeclarationNode node);
    
    /// <summary>
    /// Рендерит Record
    /// </summary>
    string Render(RecordDeclarationNode node);
    
    /// <summary>
    /// Рендерит Struct
    /// </summary>
    string Render(StructDeclarationNode node);
    
    /// <summary>
    /// Рендерит Interface
    /// </summary>
    string Render(InterfaceDeclarationNode node);
    
    /// <summary>
    /// Рендерит Enum
    /// </summary>
    string Render(EnumDeclarationNode node);
    
    /// <summary>
    /// Рендерит Property
    /// </summary>
    string Render(PropertyDeclarationNode node);
    
    /// <summary>
    /// Рендерит Method
    /// </summary>
    string Render(MethodDeclarationNode node);
    
    /// <summary>
    /// Рендерит Field
    /// </summary>
    string Render(FieldDeclarationNode node);
    
    /// <summary>
    /// Рендерит Constructor
    /// </summary>
    string Render(ConstructorDeclarationNode node);
    
    /// <summary>
    /// Рендерит UsingDirective
    /// </summary>
    string Render(UsingDirectiveNode node);
}
