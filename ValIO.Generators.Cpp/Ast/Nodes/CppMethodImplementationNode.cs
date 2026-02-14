using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел реализации метода класса в .cpp файле.
/// Пример: void UMyClass::Activate() { ... }
/// </summary>
public sealed class CppMethodImplementationNode : AstNode
{
    /// <summary>
    /// Имя класса, которому принадлежит метод
    /// </summary>
    public IdentifierNode ClassName { get; }

    /// <summary>
    /// Имя метода
    /// </summary>
    public IdentifierNode MethodName { get; }

    /// <summary>
    /// Тип возвращаемого значения (null для конструктора/деструктора)
    /// </summary>
    public TypeNode? ReturnType { get; }

    /// <summary>
    /// Параметры метода
    /// </summary>
    public IReadOnlyList<CppParameterNode> Parameters { get; }

    /// <summary>
    /// Тело метода
    /// </summary>
    public BlockNode Body { get; }

    /// <summary>
    /// Является ли метод virtual
    /// </summary>
    public bool IsVirtual { get; }

    /// <summary>
    /// Является ли метод const
    /// </summary>
    public bool IsConst { get; }

    /// <summary>
    /// Документация (doxygen-комментарий)
    /// </summary>
    public CommentNode? Documentation { get; }

    public override AstNodeKind Kind => AstNodeKind.CppMethodImplementation;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { ClassName, MethodName };
            if (ReturnType != null) children.Add(ReturnType);
            children.AddRange(Parameters);
            children.Add(Body);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppMethodImplementationNode(
        IdentifierNode className,
        IdentifierNode methodName,
        TypeNode? returnType,
        IReadOnlyList<CppParameterNode>? parameters,
        BlockNode body,
        bool isVirtual = false,
        bool isConst = false,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        ReturnType = returnType;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        IsVirtual = isVirtual;
        IsConst = isConst;
        Documentation = documentation;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppMethodImplementation(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppMethodImplementationNode");
    }

    public override AstNode Clone()
    {
        return new CppMethodImplementationNode(
            (IdentifierNode)ClassName.Clone(),
            (IdentifierNode)MethodName.Clone(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            (BlockNode)Body.Clone(),
            IsVirtual,
            IsConst,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var retStr = ReturnType != null ? $"{ReturnType} " : "";
        var constStr = IsConst ? " const" : "";
        return $"{retStr}{ClassName.Name}::{MethodName.Name}(...){constStr} {{ ... }}";
    }
}
