using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Объявление Python-функции / метода.
/// </summary>
public sealed class PythonFunctionDeclarationNode : DeclarationNode
{
    /// <summary>Если true, генерируем <c>async def</c>.</summary>
    public bool IsAsync { get; }

    /// <summary>Параметры функции.</summary>
    public IReadOnlyList<PythonParameterNode> Parameters { get; }

    /// <summary>Аннотация возвращаемого типа (опционально).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Gets the executable statements in the function.</summary>
    /// <value>The function body AST.</value>
    public BlockNode Body { get; }

    /// <summary>Декораторы функции.</summary>
    public IReadOnlyList<string> Decorators { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            children.AddRange(Parameters);
            if (ReturnType != null) children.Add(ReturnType);
            children.Add(Body);
            return children;
        }
    }

    /// <summary>Creates a Python function with executable statements and an optional type annotation.</summary>
    /// <param name="name">The function name.</param>
    /// <param name="parameters">The positional and keyword parameters.</param>
    /// <param name="returnType">The return annotation.</param>
    /// <param name="body">The ordered statements.</param>
    /// <param name="isAsync">Whether the function may await.</param>
    /// <param name="decorators">The names of decorators to apply.</param>
    /// <param name="documentation">The function docstring.</param>
    /// <param name="metadata">The optional AST metadata.</param>
    public PythonFunctionDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<PythonParameterNode>? parameters = null,
        TypeNode? returnType = null,
        BlockNode? body = null,
        bool isAsync = false,
        IReadOnlyList<string>? decorators = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        IsAsync = isAsync;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<PythonParameterNode>.Empty;
        ReturnType = returnType;
        Body = body ?? new BlockNode();
        Decorators = decorators?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonFunctionDeclaration(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonFunctionDeclarationNode");
    }

    /// <inheritdoc/>
    public override AstNode Clone() =>
        new PythonFunctionDeclarationNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => (PythonParameterNode)p.Clone()).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            (BlockNode)Body.Clone(),
            IsAsync,
            Decorators.ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
