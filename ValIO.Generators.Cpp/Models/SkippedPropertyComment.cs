using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Информация о пропущенном свойстве (например, рекурсивном) для вывода в комментарий.
/// </summary>
public sealed record SkippedPropertyComment(string PropertyName, TypeNode Type, string Reason);
