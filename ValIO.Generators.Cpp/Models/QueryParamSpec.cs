namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Спецификация query параметра для Native API функции.
/// </summary>
public sealed record QueryParamSpec
{
    public required string Name { get; init; }
    public required string PropertyName { get; init; }
}
