using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>Represents C++ const and lvalue-reference qualification on a type.</summary>
/// <param name="valueType">The unqualified type.</param>
/// <param name="isConst">Whether the referred-to value is const.</param>
/// <param name="isReference">Whether the type is an lvalue reference.</param>
/// <param name="isPointer">Whether the type is a pointer.</param>
public sealed class CppQualifiedTypeNode(TypeNode valueType, bool isConst = false, bool isReference = false, bool isPointer = false) : TypeNode
{
    /// <summary>Gets the unqualified type.</summary>
    public TypeNode ValueType { get; } = valueType;

    /// <summary>Gets whether the referred-to value is const.</summary>
    public bool IsConst { get; } = isConst;

    /// <summary>Gets whether the type is an lvalue reference.</summary>
    public bool IsReference { get; } = isReference;

    /// <summary>Gets whether the type is a pointer.</summary>
    public bool IsPointer { get; } = isPointer;

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => [ValueType];

    /// <inheritdoc />
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ICppAstVisitor<TResult> cppVisitor
            ? cppVisitor.VisitCppQualifiedType(this)
            : throw new NotSupportedException("A C++ visitor is required for a qualified type.");

    /// <inheritdoc />
    public override AstNode Clone() => new CppQualifiedTypeNode((TypeNode)ValueType.Clone(), IsConst, IsReference, IsPointer);
}
