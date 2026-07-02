using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: emits the shared <c>{op}.query-keys.ts</c> module for cacheable (non-mutation) operations, holding the
/// single source of an operation's cache identity — TanStack array query keys and/or Next.js string cache tags.
/// </summary>
/// <remarks>
/// Runs when a TanStack query wrapper is selected (needs the query-key functions) and/or the Next.js transport is
/// selected (needs the cache-tag helpers). In a hybrid run both consumers import from this one file so client and
/// server invalidation share the same identity. Which exports are emitted per file is driven by the active
/// consumers; cache tags are emitted for <c>GET</c> operations only, query keys for every non-mutation operation.
/// </remarks>
/// <seealso cref="TypeScriptQueryKeysAstBuilder"/>
public sealed class TypeScriptQueryKeysStep : ICodeGenerationStep
{
    /// <summary>The naming converter used for file names and hook base names.</summary>
    private readonly INamingConverter _namingConverter;

    /// <summary>The AST renderer used to serialize the generated source file.</summary>
    private readonly TypeScriptAstRenderer _renderer;

    /// <summary>The step name.</summary>
    public string Name => "typescript-query-keys";

    /// <summary>The execution order — before the transport and framework-adapter steps that reference the file.</summary>
    public int Order => 48;

    /// <summary>Initializes the step with no feature-plugin renderers.</summary>
    /// <param name="namingConverter">The naming converter.</param>
    public TypeScriptQueryKeysStep(INamingConverter namingConverter)
        : this(namingConverter, Array.Empty<ITypeScriptNodeRenderer>())
    {
    }

    /// <summary>Initializes the step.</summary>
    /// <param name="namingConverter">The naming converter.</param>
    /// <param name="nodeRenderers">The feature-plugin node renderers.</param>
    public TypeScriptQueryKeysStep(INamingConverter namingConverter, IEnumerable<ITypeScriptNodeRenderer> nodeRenderers)
    {
        _namingConverter = namingConverter;
        _renderer = new TypeScriptAstRenderer(nodeRenderers);
    }

    /// <inheritdoc/>
    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients)
            return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return IsAnyTanStackWrapper(opts?.QueryWrapper) || IsNextjsSelected(opts);
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";

        var includeQueryKeys = IsAnyTanStackWrapper(opts?.QueryWrapper);
        var includeCacheTags = IsNextjsSelected(opts);
        var builder = new TypeScriptQueryKeysAstBuilder(_namingConverter);

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                // Query keys/tags are a read-caching concern: only non-mutation operations get them.
                var isMutation = operation.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE";
                if (isMutation)
                    continue;

                var isGet = string.Equals(operation.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase);
                var emitCacheTags = includeCacheTags && isGet;
                if (!includeQueryKeys && !emitCacheTags)
                    continue;

                var fileName = _namingConverter.ToCamelCase(operation.OriginalMethodName);
                var sourceFile = builder.BuildQueryKeysSourceFile(operation, fileName, includeQueryKeys, emitCacheTags);
                var content = _renderer.Render(sourceFile);
                var relativePath = $"{clientsPath}/{fileName}/{fileName}.query-keys.ts";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Whether the configured query wrapper is any TanStack Query adapter (React/Vue/Angular/Solid/Svelte).</summary>
    /// <param name="queryWrapper">The configured query wrapper id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a TanStack wrapper is selected.</returns>
    private static bool IsAnyTanStackWrapper(string? queryWrapper) =>
        !string.IsNullOrWhiteSpace(queryWrapper)
        && (queryWrapper.Contains("tanstack", StringComparison.OrdinalIgnoreCase)
            || queryWrapper.Equals("react-query", StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the Next.js fetch transport is among the selected transports.</summary>
    /// <param name="options">The TypeScript generation options, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the nextjs transport is selected.</returns>
    private static bool IsNextjsSelected(TypeScriptGenerationOptions? options) =>
        options?.Transports?.Contains(TypeScriptOptionIds.Transport.Nextjs, StringComparer.OrdinalIgnoreCase) == true;
}
