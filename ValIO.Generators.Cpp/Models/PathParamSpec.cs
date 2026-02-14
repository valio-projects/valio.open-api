namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Спецификация параметра пути для Path builder метода.
/// </summary>
public sealed record PathParamSpec
{
    /// <summary>Имя в шаблоне URL (для замены {tenantId})</summary>
    public required string OriginalName { get; init; }

    /// <summary>Имя параметра в C++ (TenantId)</summary>
    public required string PropertyName { get; init; }

    /// <summary>Спецификатор printf (%s, %lld)</summary>
    public required string PrintfSpecifier { get; init; }

    /// <summary>Выражение для передачи в Printf (прямое или через промежуточную переменную)</summary>
    public required string PrintfArgExpression { get; init; }

    /// <summary>Требуется ли промежуточная переменная (FGuid → ToString DigitsWithHyphens)</summary>
    public bool IsGuid { get; init; }

    /// <summary>Имя промежуточной переменной для FGuid (TenantIdStr)</summary>
    public string? IntermediateVarName { get; init; }
}
