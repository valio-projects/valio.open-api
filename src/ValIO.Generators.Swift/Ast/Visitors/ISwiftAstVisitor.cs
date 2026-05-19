using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Nodes;

namespace ValIO.Generators.Swift.Ast.Visitors;

/// <summary>
/// Visitor for Swift-specific AST nodes.
/// </summary>
public interface ISwiftAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitSwiftFile(SwiftFileNode node);
    TResult VisitSwiftImport(SwiftImportNode node);
    TResult VisitSwiftAttribute(SwiftAttributeNode node);
    TResult VisitSwiftStruct(SwiftStructDeclarationNode node);
    TResult VisitSwiftClassDeclaration(SwiftClassDeclarationNode node);
    TResult VisitSwiftProperty(SwiftPropertyNode node);
    TResult VisitSwiftEnumDeclaration(SwiftEnumDeclarationNode node);
    TResult VisitSwiftEnumMember(SwiftEnumMemberNode node);
    TResult VisitSwiftFunctionDeclaration(SwiftFunctionDeclarationNode node);
    TResult VisitSwiftParameter(SwiftParameterNode node);
}
