using ValIO.Core.Ast.Nodes;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Maps IR types to language-specific AST type nodes.
/// </summary>
public interface IIrTypeMapper
{
    /// <summary>Maps an IR type to an AST TypeNode for this language.</summary>
    TypeNode MapType(IrType irType);

    /// <summary>Returns true if the property is optional in this language's type system.</summary>
    bool IsOptionalInLanguage(IrProperty property);

    /// <summary>
    /// Returns the import path/module for this IR type, or null if no import is needed.
    /// Used for generating import statements.
    /// </summary>
    string? GetImportTarget(IrType irType);
}
