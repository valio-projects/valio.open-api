using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Nodes.UE;

/// <summary>
/// Параметр делегата (тип + имя)
/// </summary>
public sealed class UDelegateParameter : IEquatable<UDelegateParameter>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="Type">The declared C++ type.</param>
    /// <param name="Name">The parameter name emitted with the delegate.</param>
    public UDelegateParameter(TypeNode Type, IdentifierNode Name)
    {
        this.Type = Type;
        this.Name = Name;
    }

    /// <summary>Gets the declared C++ type.</summary>
    /// <value>The declared C++ type.</value>
    public TypeNode Type { get; init; }

    /// <summary>Gets the parameter name emitted with the delegate.</summary>
    /// <value>The parameter name emitted with the delegate.</value>
    public IdentifierNode Name { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(UDelegateParameter? other) => other is not null
        && EqualityComparer<TypeNode>.Default.Equals(Type, other.Type)
        && EqualityComparer<IdentifierNode>.Default.Equals(Name, other.Name);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UDelegateParameter other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Type, Name);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(UDelegateParameter? left, UDelegateParameter? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(UDelegateParameter? left, UDelegateParameter? right) => !Equals(left, right);
}

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
