using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел объявления record (C#)
/// </summary>
public sealed class RecordDeclarationNode : TypeDeclarationNode
{
    /// <summary>
    /// Является ли record struct
    /// </summary>
    public bool IsRecordStruct { get; }
    
    /// <summary>
    /// Параметры primary constructor
    /// </summary>
    public IReadOnlyList<ParameterDeclarationNode> PrimaryConstructorParameters { get; }
    
    public override AstNodeKind Kind => AstNodeKind.RecordDeclaration;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            children.AddRange(BaseTypes);
            children.AddRange(PrimaryConstructorParameters);
            children.AddRange(Members);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public RecordDeclarationNode(
        IdentifierNode name,
        AccessModifier accessModifier = AccessModifier.Public,
        bool isRecordStruct = false,
        IReadOnlyList<ParameterDeclarationNode>? primaryConstructorParameters = null,
        IReadOnlyList<TypeNode>? baseTypes = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, accessModifier, baseTypes, members, documentation, attributes, metadata)
    {
        IsRecordStruct = isRecordStruct;
        PrimaryConstructorParameters = primaryConstructorParameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICSharpAstVisitor<TResult> csharpVisitor)
        {
            return csharpVisitor.VisitRecordDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ICSharpAstVisitor<TResult> to visit RecordDeclarationNode");
    }
    
    public override AstNode Clone()
    {
        return new RecordDeclarationNode(
            (IdentifierNode)Name.Clone(),
            AccessModifier,
            IsRecordStruct,
            PrimaryConstructorParameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
            BaseTypes.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
