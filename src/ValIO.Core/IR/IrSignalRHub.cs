using ValIO.Core.IR.Types;

namespace ValIO.Core.IR;

/// <summary>Describes the public wire surface of a SignalR hub.</summary>
/// <param name="Name">The generated name stem for the hub and its clients.</param>
/// <param name="Path">The relative HTTP path at which the host exposes the hub.</param>
/// <param name="Methods">Client-to-server invocations in declaration order.</param>
/// <param name="Events">Server-to-client notifications in declaration order.</param>
public sealed record IrSignalRHub(string Name, string Path, IReadOnlyList<IrSignalRCall> Methods, IReadOnlyList<IrSignalRCall> Events);

/// <summary>Describes one SignalR method or notification.</summary>
/// <param name="Name">The exact wire name used by SignalR.</param>
/// <param name="Parameters">The positional wire arguments.</param>
/// <param name="Result">The result type, or null for a resultless call.</param>
public sealed record IrSignalRCall(string Name, IReadOnlyList<IrSignalRParameter> Parameters, IrType? Result);

/// <summary>Describes an argument in its position on the wire.</summary>
/// <param name="Name">The source identifier written into the generated signature.</param>
/// <param name="Type">The argument's normalized JSON schema type.</param>
public sealed record IrSignalRParameter(string Name, IrType Type);
