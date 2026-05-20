using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Nodes;

namespace ValIO.Generators.Rust.Ast.Visitors;

/// <summary>
/// Visitor for Rust-specific AST nodes.
/// </summary>
public interface IRustAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitRustFile(RustFileNode node);
    TResult VisitRustStruct(RustStructNode node);
    TResult VisitRustEnum(RustEnumNode node);
    TResult VisitRustFunction(RustFunctionNode node);
    TResult VisitRustField(RustFieldNode node);
    TResult VisitRustEnumVariant(RustEnumVariantNode node);
}
