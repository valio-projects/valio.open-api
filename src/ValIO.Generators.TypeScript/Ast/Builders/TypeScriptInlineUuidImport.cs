using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Validation;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Builds shared UUID constant imports for schema declarations and inline request validation.</summary>
internal static class TypeScriptInlineUuidImport
{
    /// <summary>Creates an import when an inline body schema references the configured UUID constant.</summary>
    /// <param name="operations">The operations generated in the source file.</param>
    /// <param name="validation">The selected client validation strategy.</param>
    /// <param name="uuidFormat">The configured UUID validation mode.</param>
    /// <param name="modelsPath">The configured models directory.</param>
    /// <param name="relativePrefix">The relative path from the generated file to the output root.</param>
    /// <returns>The required runtime import, or null when no inline body needs it.</returns>
    public static ImportNode? Create(
        IEnumerable<IrOperation> operations,
        IClientValidationStrategy validation,
        string? uuidFormat,
        string? modelsPath,
        string relativePrefix)
        => CreateForTypes(
            operations.Where(operation => operation.RequestBody != null)
                .Select(operation => operation.RequestBody!.Type)
                .Where(ValidationTypes.NeedsConstructedSchema),
            validation, uuidFormat, modelsPath, relativePrefix);

    /// <summary>Creates an import when the declared schema types reference a shared UUID constant.</summary>
    /// <param name="types">The inline types whose schemas are declared in the generated file.</param>
    /// <param name="validation">The selected client validation strategy.</param>
    /// <param name="uuidFormat">The configured UUID validation mode.</param>
    /// <param name="modelsPath">The configured models directory.</param>
    /// <param name="relativePrefix">The relative path from the generated file to the output root.</param>
    /// <returns>The required runtime import, or null when the schema types need no shared UUID constant.</returns>
    public static ImportNode? CreateForTypes(
        IEnumerable<IrType> types,
        IClientValidationStrategy validation,
        string? uuidFormat,
        string? modelsPath,
        string relativePrefix)
    {
        if (!validation.IsEnabled || validation.UuidConstName is not { } uuidConst
            || TypeScriptOptionIds.UuidFormat.Resolve(uuidFormat) != TypeScriptOptionIds.UuidFormat.Regex
            || !types.Any(UuidFormatSupport.ContainsUuid))
            return null;

        return new ImportNode(
            items: [new IdentifierNode(uuidConst)],
            from: $"{relativePrefix}/{modelsPath ?? "models"}/{TypeScriptOptionIds.UuidFormat.ConstFileName}",
            kind: ImportKind.Named);
    }
}
