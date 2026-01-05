namespace ValIO.Core.IR;

/// <summary>
/// IR представление HTTP клиента
/// </summary>
public sealed record IrClient
{
    /// <summary>
    /// Имя клиента (sanitized)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Операции клиента
    /// </summary>
    public required IReadOnlyList<IrOperation> Operations { get; init; }
}
