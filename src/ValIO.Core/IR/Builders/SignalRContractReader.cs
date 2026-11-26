using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Builders;

/// <summary>Reads the opt-in SignalR extension alongside the document's ordinary wire schemas.</summary>
internal static partial class SignalRContractReader
{
    /// <summary>Identifies the extension containing the hub contracts.</summary>
    private const string ExtensionName = "x-valio-signalr";

    /// <summary>Reads hub declarations and resolves their wire types through the existing schema builder.</summary>
    /// <param name="document">The parsed OpenAPI document and its component schemas.</param>
    /// <param name="buildType">The schema-to-IR translation used for the rest of this generation request.</param>
    /// <returns>The declared hubs, or an empty collection when the extension is absent.</returns>
    /// <exception cref="InvalidOperationException">The extension contains an invalid or ambiguous declaration.</exception>
    internal static IReadOnlyList<IrSignalRHub> Read(OpenApiDocument document, Func<IOpenApiSchema, IrType> buildType)
    {
        if (document.Extensions?.TryGetValue(ExtensionName, out var extension) != true)
            return [];

        if (extension is not JsonNodeExtension { Node: JsonObject root } || root["hubs"] is not JsonArray { Count: > 0 } hubs)
            throw Invalid("expected an object with a non-empty hubs array");

        var result = new List<IrSignalRHub>(hubs.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in hubs)
        {
            var hub = ReadObject(item, "hub");
            var name = ReadName(hub, "hub");
            var path = GetString(hub["path"]);
            if (path is null || !HubPath().IsMatch(path))
                throw Invalid($"hub '{name}' requires a literal rooted path such as '/hubs/support'");
            if (!names.Add(name) || !paths.Add(path))
                throw Invalid($"hub '{name}' repeats a name or path");

            var methods = ReadCalls(hub, "methods", document, buildType);
            var events = ReadCalls(hub, "events", document, buildType);
            if (methods.Count == 0 && events.Count == 0)
                throw Invalid($"hub '{name}' must declare a method or an event");

            result.Add(new IrSignalRHub(name, path, methods, events));
        }

        return result;
    }

    /// <summary>Rejects unresolved hub types before any language starts emitting output.</summary>
    /// <param name="hubs">The hubs with finalized names.</param>
    /// <param name="models">The document's generated models.</param>
    /// <param name="enums">The document's generated enums.</param>
    /// <param name="dependencies">The shared structural type dependency walker.</param>
    /// <exception cref="InvalidOperationException">A signature refers to a type absent from this document.</exception>
    internal static void ValidateReferences(IReadOnlyList<IrSignalRHub> hubs, IReadOnlyList<IrModel> models,
        IReadOnlyList<IrEnum> enums, IrTypeDependencyGraphBuilder dependencies)
    {
        if (hubs.Count == 0)
            return;

        var declared = new HashSet<string>(models.Select(model => model.Name), StringComparer.Ordinal);
        declared.UnionWith(enums.Select(item => item.Name));
        foreach (var hub in hubs)
        {
            foreach (var call in hub.Methods.Concat(hub.Events))
            {
                var types = call.Parameters.Select(parameter => parameter.Type);
                if (call.Result is { } result)
                    types = types.Append(result);

                var unknown = types.SelectMany(dependencies.ExtractDependenciesFromType).FirstOrDefault(name => !declared.Contains(name));
                if (unknown is not null)
                    throw Invalid($"'{hub.Name}.{call.Name}' references undeclared schema '{unknown}'");
            }
        }
    }

    /// <summary>Reads one direction without changing the declaration order of calls or their arguments.</summary>
    /// <param name="hub">The source hub object.</param>
    /// <param name="key">The collection name, methods or events.</param>
    /// <param name="document">The document supplying referenced schemas.</param>
    /// <param name="buildType">The shared type translation.</param>
    /// <returns>The declarations for the selected direction.</returns>
    private static IReadOnlyList<IrSignalRCall> ReadCalls(JsonObject hub, string key, OpenApiDocument document,
        Func<IOpenApiSchema, IrType> buildType)
    {
        var result = new List<IrSignalRCall>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ReadArray(hub, key))
        {
            var declaration = ReadObject(item, key);
            var name = ReadName(declaration, key);
            if (!names.Add(name))
                throw Invalid($"{key} contains duplicate wire name '{name}'");
            if (key == "methods" && name is "OnConnectedAsync" or "OnDisconnectedAsync" or "OnAuthenticationRefreshedAsync" or
                "Dispose" or "GetType" or "ToString" or "GetHashCode" or "Equals")
                throw Invalid($"'{name}' is reserved by the SignalR hub base class");
            if (key == "events" && declaration.ContainsKey("result"))
                throw Invalid($"event '{name}' cannot declare a result");

            var parameters = new List<IrSignalRParameter>();
            var parameterNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in ReadArray(declaration, "parameters"))
            {
                var parameter = ReadObject(argument, $"argument of '{name}'");
                var parameterName = GetString(parameter["name"]);
                if (parameterName is null || !ParameterName().IsMatch(parameterName) || !parameterNames.Add(parameterName))
                    throw Invalid($"'{name}' has an invalid or duplicate argument name");

                var type = ReadType(parameter["schema"], $"{name}.{parameterName}", document, buildType);
                parameters.Add(new IrSignalRParameter(parameterName, type));
            }

            var reply = declaration.ContainsKey("result") ? ReadType(declaration["result"], $"{name}.result", document, buildType) : null;
            result.Add(new IrSignalRCall(name, parameters, reply));
        }

        return result;
    }

    /// <summary>Reads a local JSON schema fragment and reports the containing hub member on failure.</summary>
    /// <param name="node">The schema node from the extension.</param>
    /// <param name="member">The declaration being resolved.</param>
    /// <param name="document">The document supplying local references.</param>
    /// <param name="buildType">The shared type translation.</param>
    /// <returns>The normalized type for the argument or result.</returns>
    private static IrType ReadType(JsonNode? node, string member, OpenApiDocument document, Func<IOpenApiSchema, IrType> buildType)
    {
        if (node is not JsonObject schemaObject)
            throw Invalid($"'{member}' requires a JSON schema object");

        if (schemaObject.TryGetPropertyValue("$ref", out var reference))
        {
            if (schemaObject.Count != 1)
                throw Invalid($"'{member}' must put additional schema keywords in the referenced component or a composition");

            const string prefix = "#/components/schemas/";
            var location = GetString(reference);
            if (location?.StartsWith(prefix, StringComparison.Ordinal) != true ||
                document.Components?.Schemas?.ContainsKey(location[prefix.Length..]) != true)
                throw Invalid($"'{member}' requires a reference to an existing local component schema");

            var named = new OpenApiSchemaReference(location[prefix.Length..], document);
            return buildType(named);
        }

        var schema = new OpenApiJsonReader().ReadFragment<OpenApiSchema>(node, OpenApiSpecVersion.OpenApi3_1,
            document, out var diagnostics, new OpenApiReaderSettings());
        if (schema is null || diagnostics.Errors.Count > 0)
            throw Invalid($"'{member}' has an invalid schema: {string.Join("; ", diagnostics.Errors.Select(error => error.Message))}");

        return buildType(schema);
    }

    /// <summary>Returns the object containing one extension declaration.</summary>
    /// <param name="node">The untyped JSON value.</param>
    /// <param name="position">The declaration's location for error reporting.</param>
    /// <returns>The source object.</returns>
    private static JsonObject ReadObject(JsonNode? node, string position) =>
        node as JsonObject ?? throw Invalid($"expected an object for {position}");

    /// <summary>Returns an optional array from a hub or call declaration.</summary>
    /// <param name="objectNode">The containing object.</param>
    /// <param name="property">The array property to read.</param>
    /// <returns>The entries in declaration order, or an empty sequence when omitted.</returns>
    private static IEnumerable<JsonNode?> ReadArray(JsonObject objectNode, string property)
    {
        if (!objectNode.TryGetPropertyValue(property, out var value))
            return [];

        return value as JsonArray ?? throw Invalid($"'{property}' must be an array");
    }

    /// <summary>Reads an identifier which can be emitted unchanged in both supported languages.</summary>
    /// <param name="objectNode">The declaration containing the name.</param>
    /// <param name="position">The declaration kind for error reporting.</param>
    /// <returns>The exact contract name.</returns>
    private static string ReadName(JsonObject objectNode, string position)
    {
        var name = GetString(objectNode["name"]);
        return name is not null && ContractName().IsMatch(name)
            ? name
            : throw Invalid($"{position} requires a PascalCase alphanumeric name");
    }

    /// <summary>Extracts a JSON string without coercing numbers or booleans.</summary>
    /// <param name="node">The source node.</param>
    /// <returns>The string or null when the node has another kind.</returns>
    private static string? GetString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>Creates a diagnostic identifying the extension that supplied the invalid declaration.</summary>
    /// <param name="message">The precise contract error.</param>
    /// <returns>The exception reported by the generation pipeline.</returns>
    private static InvalidOperationException Invalid(string message) => new($"Invalid {ExtensionName}: {message}.");

    /// <summary>Matches an ASCII identifier usable as both a wire and a type name.</summary>
    /// <returns>The generated contract-name expression.</returns>
    [GeneratedRegex(@"\A[A-Z][a-zA-Z0-9]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ContractName();

    /// <summary>Matches an ASCII argument identifier.</summary>
    /// <returns>The generated argument-name expression.</returns>
    [GeneratedRegex(@"\A[a-zA-Z_][a-zA-Z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterName();

    /// <summary>Matches a literal rooted path without an origin, query or fragment.</summary>
    /// <returns>The generated hub-path expression.</returns>
    [GeneratedRegex(@"\A/(?:[a-zA-Z0-9_-]+/)*[a-zA-Z0-9_-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex HubPath();
}
