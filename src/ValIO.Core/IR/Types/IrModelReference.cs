namespace ValIO.Core.IR.Types;

/// <summary>
/// Ссылка на модель
/// </summary>
public sealed record IrModelReference : IrType
{
    /// <summary>
    /// Имя модели (с учётом affix-преобразования, если оно применялось).
    /// Используется как идентификатор типа во всех генераторах.
    /// </summary>
    public required string ModelName { get; init; }

    private readonly string? _originalModelName;

    /// <summary>
    /// Исходное (до affix) имя модели. Используется для вычисления имён файлов и путей
    /// импорта, которые не должны меняться при добавлении prefix/suffix. По умолчанию
    /// равно <see cref="ModelName"/>, если affix не применялся.
    /// </summary>
    public string OriginalModelName
    {
        get => _originalModelName ?? ModelName;
        init => _originalModelName = value;
    }
}
