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

    /// <summary>Visits a RustBorrowNode syntax node.</summary>
    /// <param name="node">The Rust syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRustBorrow(RustBorrowNode node) => DefaultVisit(node);

    /// <summary>Visits a RustTryNode syntax node.</summary>
    /// <param name="node">The Rust syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRustTry(RustTryNode node) => DefaultVisit(node);

    /// <summary>Visits a RustTupleNode syntax node.</summary>
    /// <param name="node">The Rust syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRustTuple(RustTupleNode node) => DefaultVisit(node);

    /// <summary>Visits a RustLetNode syntax node.</summary>
    /// <param name="node">The Rust syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRustLet(RustLetNode node) => DefaultVisit(node);

    /// <summary>Visits a RustIfSomeNode syntax node.</summary>
    /// <param name="node">The Rust syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitRustIfSome(RustIfSomeNode node) => DefaultVisit(node);
    TResult VisitRustField(RustFieldNode node);
    TResult VisitRustEnumVariant(RustEnumVariantNode node);
}
