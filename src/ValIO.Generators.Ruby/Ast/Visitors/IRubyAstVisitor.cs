using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Visitors;

/// <summary>
/// Visitor for Ruby-specific AST nodes.
/// </summary>
public interface IRubyAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitRubyFile(RubyFileNode node);
    TResult VisitRubyClass(RubyClassNode node);
    TResult VisitRubyMethod(RubyMethodNode node);

    /// <summary>Visits a RubyCallWithBlockNode node.</summary>
    /// <param name="node">The Ruby syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRubyCallWithBlock(RubyCallWithBlockNode node) => DefaultVisit(node);

    /// <summary>Visits a RubyUnlessStatementNode node.</summary>
    /// <param name="node">The Ruby syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRubyUnlessStatement(RubyUnlessStatementNode node) => DefaultVisit(node);
    TResult VisitRubyAttribute(RubyAttributeNode node);
}
