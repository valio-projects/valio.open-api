using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел using alias для C++.
/// Пример: using FValIOClientRef = TSharedRef&lt;FValIOClient&gt;;
/// </summary>
public sealed class CppUsingAliasNode : DeclarationNode
{
    /// <summary>
    /// Имя алиаса
    /// </summary>
    public IdentifierNode Alias => Name;

    /// <summary>
    /// Целевой тип, на который ссылается алиас
    /// </summary>
    public TypeNode TargetType { get; }


    public override IReadOnlyList<AstNode> Children => [..base.Children, TargetType];

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppUsingAliasNode(
        IdentifierNode alias,
        TypeNode targetType,
        IReadOnlyDictionary<string, object>? metadata = null,
        CommentNode? documentation = null)
        : base(alias, documentation, metadata: metadata)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppUsingAlias(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppUsingAliasNode");
    }

    public override AstNode Clone()
    {
        return new CppUsingAliasNode(
            (IdentifierNode)Alias.Clone(),
            (TypeNode)TargetType.Clone(),
            Metadata.ToImmutableDictionary(),
            (CommentNode?)Documentation?.Clone());
    }

    public override string ToString()
    {
        return $"using {Alias.Name} = {TargetType};";
    }
}
