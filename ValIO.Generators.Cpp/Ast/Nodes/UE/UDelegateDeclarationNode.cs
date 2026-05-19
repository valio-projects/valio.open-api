using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Параметр делегата (тип + имя)
/// </summary>
public sealed record UDelegateParameter(TypeNode Type, IdentifierNode Name);

/// <summary>
/// Узел объявления DECLARE_DYNAMIC_MULTICAST_DELEGATE для Unreal Engine.
/// Пример: DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnCompleted, FMyResult, Result);
/// Вариант макроса выбирается автоматически на основе количества параметров.
/// </summary>
public sealed class UDelegateDeclarationNode : AstNode
{
    /// <summary>
    /// Имя типа делегата (например FOnPanoramaGetQueueTasksListCompleted)
    /// </summary>
    public IdentifierNode DelegateName { get; }

    /// <summary>
    /// Параметры делегата (тип + имя для каждого)
    /// </summary>
    public IReadOnlyList<UDelegateParameter> Parameters { get; }


    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { DelegateName };
            foreach (var param in Parameters)
            {
                children.Add(param.Type);
                children.Add(param.Name);
            }
            return children;
        }
    }

    /// <summary>
    /// Конструктор
    /// </summary>
    public UDelegateDeclarationNode(
        IdentifierNode delegateName,
        IReadOnlyList<UDelegateParameter>? parameters = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        DelegateName = delegateName ?? throw new ArgumentNullException(nameof(delegateName));
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<UDelegateParameter>.Empty;
    }

    /// <summary>
    /// Получает суффикс макроса на основе количества параметров.
    /// 0 -> "", 1 -> "_OneParam", 2 -> "_TwoParams" и т.д.
    /// </summary>
    public string GetMacroParamSuffix()
    {
        return Parameters.Count switch
        {
            0 => "",
            1 => "_OneParam",
            2 => "_TwoParams",
            3 => "_ThreeParams",
            4 => "_FourParams",
            5 => "_FiveParams",
            6 => "_SixParams",
            7 => "_SevenParams",
            8 => "_EightParams",
            9 => "_NineParams",
            _ => throw new NotSupportedException(
                $"DECLARE_DYNAMIC_MULTICAST_DELEGATE supports at most 9 parameters, got {Parameters.Count}")
        };
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ICppAstVisitor<TResult> cppVisitor)
            return cppVisitor.VisitUDelegate(this);
        throw new NotSupportedException("Visitor must implement ICppAstVisitor<TResult> to visit UDelegateDeclarationNode");
    }

    public override AstNode Clone()
    {
        return new UDelegateDeclarationNode(
            (IdentifierNode)DelegateName.Clone(),
            Parameters.Select(p => new UDelegateParameter(
                (TypeNode)p.Type.Clone(),
                (IdentifierNode)p.Name.Clone())).ToImmutableList(),
            Metadata.ToImmutableDictionary());
    }

    public override string ToString()
    {
        var paramsStr = string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name.Name}"));
        return $"DECLARE_DYNAMIC_MULTICAST_DELEGATE{GetMacroParamSuffix()}({DelegateName.Name}, {paramsStr})";
    }
}
