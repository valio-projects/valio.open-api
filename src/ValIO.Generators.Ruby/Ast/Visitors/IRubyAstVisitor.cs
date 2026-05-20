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
    TResult VisitRubyAttribute(RubyAttributeNode node);
}
