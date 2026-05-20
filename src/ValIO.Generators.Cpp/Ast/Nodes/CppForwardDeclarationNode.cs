using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Тип ключевого слова для forward declaration (class, struct, enum)
/// </summary>
public enum CppForwardDeclarationKind
{
    /// <summary>
    /// Forward declaration класса: class FMyClass;
    /// </summary>
    Class,

    /// <summary>
    /// Forward declaration структуры: struct FMyStruct;
    /// </summary>
    Struct,

    /// <summary>
    /// Forward declaration перечисления: enum class EMyEnum;
    /// </summary>
    Enum
}

/// <summary>
/// Узел forward declaration для C++.
/// Пример: class FValIOClient;
/// </summary>
public sealed class CppForwardDeclarationNode : AstNode
{
    /// <summary>
    /// Тип ключевого слова (class, struct, enum)
    /// </summary>
    public CppForwardDeclarationKind DeclarationKind { get; }

    /// <summary>
    /// Имя объявляемого типа
    /// </summary>
    public IdentifierNode Name { get; }


    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name };

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppForwardDeclarationNode(
        CppForwardDeclarationKind declarationKind,
        IdentifierNode name,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        DeclarationKind = declarationKind;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppForwardDeclaration(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppForwardDeclarationNode");
    }

    public override AstNode Clone()
    {
        return new CppForwardDeclarationNode(
            DeclarationKind,
            (IdentifierNode)Name.Clone(),
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        return $"{DeclarationKind.ToString().ToLowerInvariant()} {Name.Name};";
    }
}
