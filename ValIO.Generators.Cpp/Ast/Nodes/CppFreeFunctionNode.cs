using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел свободной (free) функции C++, не являющейся членом класса.
/// Используется для нативных TFuture API-методов и Parse-функций.
/// Может представлять как объявление (declaration в .h), так и определение (definition в .cpp).
/// </summary>
public sealed class CppFreeFunctionNode : DeclarationNode
{
    /// <summary>
    /// Тип возвращаемого значения
    /// </summary>
    public TypeNode ReturnType { get; }

    /// <summary>
    /// Параметры функции
    /// </summary>
    public IReadOnlyList<CppParameterNode> Parameters { get; }

    /// <summary>
    /// Тело функции (null для forward declaration в .h)
    /// </summary>
    public BlockNode? Body { get; }

    /// <summary>
    /// Является ли это только объявлением (без тела, для .h файла)
    /// </summary>
    public bool IsDeclarationOnly { get; }

    public override AstNodeKind Kind => AstNodeKind.CppFreeFunction;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, ReturnType };
            children.AddRange(Parameters);
            if (Body != null)
                children.Add(Body);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppFreeFunctionNode(
        IdentifierNode name,
        TypeNode returnType,
        IReadOnlyList<CppParameterNode>? parameters = null,
        BlockNode? body = null,
        bool isDeclarationOnly = false,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, metadata: metadata)
    {
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body;
        IsDeclarationOnly = isDeclarationOnly;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppFreeFunction(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppFreeFunctionNode");
    }

    public override AstNode Clone()
    {
        return new CppFreeFunctionNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)ReturnType.Clone(),
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            Body != null ? (BlockNode)Body.Clone() : null,
            IsDeclarationOnly,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var paramsStr = string.Join(", ", Parameters.Select(p => p.ToString()));
        var suffix = IsDeclarationOnly ? ";" : " { ... }";
        return $"{ReturnType} {Name.Name}({paramsStr}){suffix}";
    }
}
