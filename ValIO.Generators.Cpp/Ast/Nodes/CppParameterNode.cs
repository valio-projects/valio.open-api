using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes;

/// <summary>
/// Узел параметра функции/метода в C++.
/// Пример: const FString&amp; Name, int32 Count = 0
/// </summary>
public sealed class CppParameterNode : DeclarationNode
{
    /// <summary>
    /// Тип параметра
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Является ли параметр const
    /// </summary>
    public bool IsConst { get; }

    /// <summary>
    /// Передаётся ли параметр по ссылке (&amp;)
    /// </summary>
    public bool IsReference { get; }

    /// <summary>
    /// Передаётся ли параметр по указателю (*)
    /// </summary>
    public bool IsPointer { get; }

    /// <summary>
    /// Значение по умолчанию (может быть null)
    /// </summary>
    public ExpressionNode? DefaultValue { get; }

    public override AstNodeKind Kind => AstNodeKind.CppParameter;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name, Type };
            if (DefaultValue != null)
                children.Add(DefaultValue);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public CppParameterNode(
        IdentifierNode name,
        TypeNode type,
        bool isConst = false,
        bool isReference = false,
        bool isPointer = false,
        ExpressionNode? defaultValue = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, metadata: metadata)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsConst = isConst;
        IsReference = isReference;
        IsPointer = isPointer;
        DefaultValue = defaultValue;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitCppParameter(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit CppParameterNode");
    }

    public override AstNode Clone()
    {
        return new CppParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            IsConst,
            IsReference,
            IsPointer,
            DefaultValue != null ? (ExpressionNode)DefaultValue.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var constStr = IsConst ? "const " : "";
        var refStr = IsReference ? "&" : IsPointer ? "*" : "";
        return $"{constStr}{Type}{refStr} {Name.Name}";
    }
}
