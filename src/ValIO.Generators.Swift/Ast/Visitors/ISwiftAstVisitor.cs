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

    /// <summary>Visits a SwiftBindingNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftBinding(SwiftBindingNode node) => DefaultVisit(node);

    /// <summary>Visits a SwiftGuardNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftGuard(SwiftGuardNode node) => DefaultVisit(node);

    /// <summary>Visits a SwiftOptionalBindingNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftOptionalBinding(SwiftOptionalBindingNode node) => DefaultVisit(node);

    /// <summary>Visits a SwiftTryExpressionNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftTryExpression(SwiftTryExpressionNode node) => DefaultVisit(node);

    /// <summary>Visits a SwiftMetatypeExpressionNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftMetatypeExpression(SwiftMetatypeExpressionNode node) => DefaultVisit(node);

    /// <summary>Visits a SwiftImplicitMemberNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftImplicitMember(SwiftImplicitMemberNode node) => DefaultVisit(node);

    /// <summary>Visits a SwiftArrayExpressionNode syntax node.</summary>
    /// <param name="node">The Swift syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitSwiftArrayExpression(SwiftArrayExpressionNode node) => DefaultVisit(node);
    TResult VisitSwiftParameter(SwiftParameterNode node);
}
