namespace ValIO.Core.Ast.Enums;

/// <summary>
/// Тип параметра (для C#)
/// </summary>
public enum ParameterKind
{
    Normal,
    Ref,
    Out,
    In,
    Params
}
