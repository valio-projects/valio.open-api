using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Identifies response contracts that the generated Next.js transport can hand to a caller as streams.</summary>
internal static class TypeScriptBinaryResponse
{
    /// <summary>Checks whether each generated client shares the Next.js stream result shape.</summary>
    /// <param name="options">The options of the generated project.</param>
    /// <returns>Whether the project selects Next.js as its only transport.</returns>
    internal static bool UsesNextJsStreams(TypeScriptGenerationOptions? options) =>
        options?.Transports is [TypeScriptOptionIds.Transport.Nextjs];

    /// <summary>Checks whether a successful status declares an unread binary body.</summary>
    /// <param name="operation">The operation defining the HTTP method.</param>
    /// <param name="response">The documented status and its representations.</param>
    /// <returns>Whether all declared representations for this body are raw binary.</returns>
    internal static bool IsBinarySuccess(IrOperation operation, IrResponse response)
    {
        if (operation.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(response.StatusCode, out var status) || status is < 200 or >= 300 or 204 or 205)
            return false;

        return response.MediaTypes.Count > 0
            ? response.MediaTypes.All(media => IsBinary(media.Type) && !IsJson(media.Name))
            : IsBinary(response.Type) && response.ContentTypes.Count > 0 && !response.ContentTypes.Any(IsJson);
    }

    /// <summary>Recognizes a bytes-on-the-wire type independently of nullable or optional wrappers.</summary>
    /// <param name="type">The declaration's payload type.</param>
    /// <returns>Whether the type describes a raw binary stream.</returns>
    private static bool IsBinary(IrType? type) => type is not null && IrTypeQueries.UnwrapNullableOptional(type)
        is IrPrimitiveType { PrimitiveKind: IrPrimitiveKind.Binary or IrPrimitiveKind.File };

    /// <summary>Recognizes the concrete JSON media names parsed by the existing transport.</summary>
    /// <param name="mediaType">The media key declared by the schema.</param>
    /// <returns>Whether the media is JSON.</returns>
    private static bool IsJson(string mediaType)
    {
        var name = mediaType.Split(';', 2)[0].Trim();
        return name.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("text/json", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }
}
