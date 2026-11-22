using ValIO.Core.IR.Constraints;

namespace ValIO.Core.IR.Types;

/// <summary>
/// Ссылка на модель
/// </summary>
public sealed class IrModelReference : IrType
{
    /// <summary>Creates a type for initialization from source data.</summary>
    public IrModelReference()
    {
    }

    /// <summary>Copies the stored type data before applying initializer overrides.</summary>
    /// <param name="source">The type to copy.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public IrModelReference(IrModelReference source)
    {
        Constraints = source.Constraints;
        ModelName = source.ModelName;
        _originalModelName = source._originalModelName;
    }

    /// <summary>
    /// Имя модели (с учётом affix-преобразования, если оно применялось).
    /// Используется как идентификатор типа во всех генераторах.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>Preserves the original reference before applying the model-name fallback.</summary>
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

    /// <inheritdoc />
    public override IrType WithConstraints(IrPropertyConstraints? constraints) => new IrModelReference(this) { Constraints = constraints };

    /// <inheritdoc />
    protected override bool EqualsType(IrType other) =>
        EqualityComparer<string>.Default.Equals(ModelName, ((IrModelReference)other).ModelName)
        && EqualityComparer<string?>.Default.Equals(_originalModelName, ((IrModelReference)other)._originalModelName);

    /// <inheritdoc />
    protected override int GetTypeHashCode() => HashCode.Combine(ModelName, _originalModelName);
}
