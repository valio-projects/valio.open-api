using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Angular.Ast.Nodes;

/// <summary>
/// TypeScript-специфичный узел объявления класса.
/// Используется для Angular @Injectable сервисов: класс с конструктором (DI), полями
/// и методами. Декораторы передаются как <see cref="AttributeNode"/> в <c>Attributes</c>.
/// </summary>
public sealed class TypeScriptClassDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Конструктор класса (Angular DI: <c>constructor(private readonly http: HttpClient) {}</c>).
    /// </summary>
    public IReadOnlyList<ParameterDeclarationNode> ConstructorParameters { get; }

    /// <summary>
    /// Является ли класс экспортируемым.
    /// </summary>
    public bool IsExported { get; }

    public override AstNodeKind Kind => AstNodeKind.ClassDeclaration;

    public TypeScriptClassDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<DeclarationNode>? members = null,
        IReadOnlyList<ParameterDeclarationNode>? constructorParameters = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        CommentNode? documentation = null,
        bool isExported = true,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, AccessModifier.Public, baseTypes: null, members: members, documentation: documentation, attributes: attributes, metadata: metadata)
    {
        ConstructorParameters = constructorParameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        IsExported = isExported;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Используем общий VisitTypeDeclaration слот: рендерер диспатчит по типу узла.
        return visitor.VisitTypeDeclaration(this);
    }

    public override AstNode Clone()
    {
        return new TypeScriptClassDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            ConstructorParameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            IsExported,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        return $"class {Name.Name} [{Members.Count} members, {ConstructorParameters.Count} ctor params, {Attributes.Count} decorators]";
    }
}
