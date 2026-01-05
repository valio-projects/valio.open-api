using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>
/// Узел объявления функции (TypeScript)
/// </summary>
public sealed class FunctionDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Возвращаемый тип
    /// </summary>
    public TypeNode? ReturnType { get; }
    
    /// <summary>
    /// Параметры функции
    /// </summary>
    public IReadOnlyList<ParameterDeclarationNode> Parameters { get; }
    
    /// <summary>
    /// Тело функции (null для type-only)
    /// </summary>
    public BlockNode? Body { get; }
    
    /// <summary>
    /// Arrow function
    /// </summary>
    public bool IsArrowFunction { get; }
    
    /// <summary>
    /// Async функция
    /// </summary>
    public bool IsAsync { get; }
    
    public override AstNodeKind Kind => AstNodeKind.FunctionDeclaration;
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            if (ReturnType != null)
                children.Add(ReturnType);
            children.AddRange(Parameters);
            if (Body != null)
                children.Add(Body);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public FunctionDeclarationNode(
        IdentifierNode name,
        TypeNode? returnType = null,
        IReadOnlyList<ParameterDeclarationNode>? parameters = null,
        BlockNode? body = null,
        bool isArrowFunction = false,
        bool isAsync = false,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        ReturnType = returnType;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        Body = body;
        IsArrowFunction = isArrowFunction;
        IsAsync = isAsync;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ITypeScriptAstVisitor<TResult> typescriptVisitor)
        {
            return typescriptVisitor.VisitFunctionDeclaration(this);
        }
        throw new NotSupportedException($"Visitor must implement ITypeScriptAstVisitor<TResult> to visit FunctionDeclarationNode");
    }
    
    public override AstNode Clone()
    {
        return new FunctionDeclarationNode(
            (IdentifierNode)Name.Clone(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Parameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
            Body != null ? (BlockNode)Body.Clone() : null,
            IsArrowFunction,
            IsAsync,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }
}
