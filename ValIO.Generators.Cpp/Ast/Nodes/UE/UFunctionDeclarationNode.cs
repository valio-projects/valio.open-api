using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Узел объявления метода с UFUNCTION макросом в Unreal Engine.
/// Пример: UFUNCTION(BlueprintCallable, ...) static UMyAsync* Create(UObject* WorldContextObject, ...);
/// </summary>
public sealed class UFunctionDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Тип возвращаемого значения (null для void)
    /// </summary>
    public TypeNode? ReturnType { get; }

    /// <summary>
    /// Параметры функции
    /// </summary>
    public IReadOnlyList<CppParameterNode> Parameters { get; }

    /// <summary>
    /// Спецификаторы UFUNCTION
    /// </summary>
    public UFunctionSpecifiers Specifiers { get; }

    /// <summary>
    /// Является ли метод static
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>
    /// Является ли метод virtual
    /// </summary>
    public bool IsVirtual { get; }

    /// <summary>
    /// Является ли метод override
    /// </summary>
    public bool IsOverride { get; }

    /// <summary>
    /// Является ли метод const
    /// </summary>
    public bool IsConst { get; }

    /// <summary>
    /// Inline-тело метода (BlockNode с statements).
    /// При заданном значении рендерится { ... } вместо ;
    /// </summary>
    public BlockNode? Body { get; }

    public override AstNodeKind Kind => AstNodeKind.UFunction;

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (ReturnType != null) children.Add(ReturnType);
            children.AddRange(Parameters);
            if (Body != null) children.Add(Body);
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public UFunctionDeclarationNode(
        IdentifierNode name,
        TypeNode? returnType = null,
        IReadOnlyList<CppParameterNode>? parameters = null,
        UFunctionSpecifiers? specifiers = null,
        bool isStatic = false,
        bool isVirtual = false,
        bool isOverride = false,
        bool isConst = false,
        BlockNode? body = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, metadata: metadata)
    {
        ReturnType = returnType;
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<CppParameterNode>.Empty;
        Specifiers = specifiers ?? new UFunctionSpecifiers();
        IsStatic = isStatic;
        IsVirtual = isVirtual;
        IsOverride = isOverride;
        IsConst = isConst;
        Body = body;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitUFunction(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit UFunctionDeclarationNode");
    }

    public override AstNode Clone()
    {
        return new UFunctionDeclarationNode(
            (IdentifierNode)Name.Clone(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            Parameters.Select(p => (CppParameterNode)p.Clone()).ToImmutableList(),
            Specifiers,
            IsStatic, IsVirtual, IsOverride, IsConst,
            Body != null ? (BlockNode)Body.Clone() : null,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var modifiers = new List<string>();
        if (IsStatic) modifiers.Add("static");
        if (IsVirtual) modifiers.Add("virtual");
        var ret = ReturnType?.ToString() ?? "void";
        var suffix = IsOverride ? " override" : "";
        var constStr = IsConst ? " const" : "";
        return $"UFUNCTION {string.Join(' ', modifiers)} {ret} {Name.Name}(...){constStr}{suffix}";
    }
}
