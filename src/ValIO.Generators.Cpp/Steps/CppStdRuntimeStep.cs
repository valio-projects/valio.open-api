using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Runtime;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Emits only runtime layers actually required by the selected model, serializer, and client outputs.
/// </summary>
/// <param name="renderer">The C++ syntax renderer.</param>
public sealed class CppStdRuntimeStep(ICppAstRenderer renderer) : CppStdStepBase(renderer)
{
    /// <inheritdoc />
    public override string Name => "cpp-std-runtime";
    /// <inheritdoc />
    public override int Order => 70;

    /// <inheritdoc />
    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && (EffectiveOptions(context).TemporalBackend != CppTemporalBackend.String
               || EffectiveOptions(context).UuidBackend != CppUuidBackend.String
               || (EffectiveOptions(context).EmitSerializers
                && ((context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0)
                    || (context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0)))
               || (context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0));

    /// <inheritdoc />
    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var options = EffectiveOptions(context);
        ct.ThrowIfCancellationRequested();
        if (context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0 && !options.EmitSerializers)
            throw new InvalidOperationException("Standalone C++ clients require emitSerializers=true; disable client generation for pure DTO output.");
        if (options.TemporalBackend != CppTemporalBackend.String)
        {
            var providerTypes = $"ValIOTemporal{options.TemporalBackend}Types.h";
            AddFixedFile(context, $"Runtime/{providerTypes}");
            AddFixedFile(context, "Runtime/ValIOTemporalTypesBase.h");
            AddIncludeFile(context, StdRuntimeTemplates.TemporalTypesHeaderFileName, [new(providerTypes), new("ValIOTemporalTypesBase.h")]);
        }
        if (options.UuidBackend != CppUuidBackend.String)
            AddUuidTypes(context, options.UuidBackend);
        var hasCodecs = (options.EmitSerializers && ((context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0)
                || (context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0)))
            || (context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0);
        if (!hasCodecs) return Task.CompletedTask;
        if (options.TemporalBackend != CppTemporalBackend.String)
            AddTemporalJson(context, options.TemporalBackend);
        if (options.UuidBackend != CppUuidBackend.String)
            AddUuidJson(context, options.UuidBackend);
        AddJsonRuntime(context, options);
        if (context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0)
        {
            AddFixedFile(context, StdRuntimeTemplates.CancellationHeaderFileName);
            AddFixedFile(context, StdRuntimeTemplates.ExpectedHeaderFileName);
            AddFixedFile(context, StdRuntimeTemplates.ErrorHeaderFileName);
            AddFixedFile(context, StdRuntimeTemplates.HttpHeaderFileName);
            AddFixedFile(context, "Runtime/Impl/ValIORequest.inl");
            AddFixedFile(context, StdRuntimeTemplates.HttpSourceFileName);
            AddFixedFile(context, StdRuntimeTemplates.JsonHeaderFileName);
            AddIncludeFile(context, "Runtime/ValIOHttpBackend.h", [new CppIncludeDirectiveNode(options.HttpInclude)]);
        }
        return Task.CompletedTask;
    }

    /// <summary>Declares the native UUID alias with its selected provider's only type dependency.</summary>
    /// <param name="context">The current generation request.</param>
    /// <param name="backend">The native UUID provider.</param>
    private void AddUuidTypes(GenerationContext context, CppUuidBackend backend)
    {
        var (include, typeName) = backend switch
        {
            CppUuidBackend.BoostUuid => ("boost/uuid/uuid.hpp", "boost::uuids::uuid"),
            CppUuidBackend.StdUuid => ("uuid.h", "uuids::uuid"),
            CppUuidBackend.ModernUuid => ("modern-uuid/uuid.h", "muuid::uuid"),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "A native UUID provider is required."),
        };
        var type = new SimpleTypeNode(new IdentifierNode(typeName));
        var alias = new CppUsingAliasNode(new IdentifierNode("Uuid"), type,
            documentation: new CommentNode("Application UUID representation selected by the generation configuration."));
        var scope = new NamespaceDeclarationNode(new IdentifierNode("ValIO"), [alias]);
        var header = new CppHeaderFileNode("ValIOUuidTypes.h", includes: [new(include, isSystemInclude: true)], declarations: [scope]);
        AddRuntimeFile(context, StdRuntimeTemplates.UuidTypesHeaderFileName, Renderer.Render(header));
    }

    /// <summary>Composes the strict UUID codec with the selected native provider adapter.</summary>
    /// <param name="context">The current generation request.</param>
    /// <param name="backend">The native UUID provider.</param>
    private void AddUuidJson(GenerationContext context, CppUuidBackend backend)
    {
        var provider = backend switch
        {
            CppUuidBackend.BoostUuid => "ValIOUuidBoostJson.h",
            CppUuidBackend.StdUuid => "ValIOUuidStdJson.h",
            CppUuidBackend.ModernUuid => "ValIOUuidModernJson.h",
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "A native UUID provider is required."),
        };
        AddFixedFile(context, $"Runtime/{provider}");
        AddFixedFile(context, "Runtime/ValIOUuidJsonCodec.h");
        AddIncludeFile(context, "Runtime/ValIOUuidJson.h", [new(provider), new("ValIOUuidJsonCodec.h")]);
    }

    /// <summary>Composes the temporal codecs with the selected provider's calendar, clock, and interval operations.</summary>
    /// <param name="context">The current generation request.</param>
    /// <param name="backend">The native temporal provider.</param>
    private void AddTemporalJson(GenerationContext context, CppTemporalBackend backend)
    {
        var provider = $"ValIOTemporal{backend}Json.h";
        AddFixedFile(context, "Runtime/ValIOTemporalJsonSupport.h");
        AddFixedFile(context, $"Runtime/{provider}");
        var includes = new List<CppIncludeDirectiveNode> { new(provider) };
        if (backend is CppTemporalBackend.Chrono or CppTemporalBackend.HowardDate)
            AddFixedFile(context, "Runtime/ValIOTemporalChronoJsonSupport.h");
        if (backend != CppTemporalBackend.Valio)
        {
            AddFixedFile(context, "Runtime/ValIOTemporalFixedDurationJson.h");
            includes.Add(new("ValIOTemporalFixedDurationJson.h"));
        }
        AddFixedFile(context, "Runtime/ValIOTemporalJsonCodecs.h");
        includes.Add(new("ValIOTemporalJsonCodecs.h"));
        AddIncludeFile(context, "Runtime/ValIOTemporalJson.h", includes);
    }

    /// <summary>Publishes the fixed JSON code and the profile-specific include declarations it needs.</summary>
    /// <param name="context">The current generation request.</param>
    /// <param name="options">The selected JSON and scalar backends.</param>
    private void AddJsonRuntime(GenerationContext context, CppGenerationOptions options)
    {
        AddFixedFile(context, "Runtime/ValIOJsonValidation.h");
        AddFixedFile(context, "Runtime/ValIOJsonTypesBase.h");
        AddIncludeFile(context, "Runtime/ValIOJsonBackend.h", [new CppIncludeDirectiveNode(options.JsonInclude)]);
        var includes = new List<CppIncludeDirectiveNode> { new("ValIOJsonTypesBase.h") };
        if (options.TemporalBackend != CppTemporalBackend.String)
            includes.Add(new("ValIOTemporalJson.h"));
        if (options.UuidBackend != CppUuidBackend.String)
            includes.Add(new("ValIOUuidJson.h"));
        AddIncludeFile(context, StdRuntimeTemplates.JsonTypesHeaderFileName, includes);
    }

    /// <summary>Copies a checked-in runtime file to its canonical path in the SDK.</summary>
    /// <param name="context">The current generation request.</param>
    /// <param name="path">The relative output path.</param>
    private static void AddFixedFile(GenerationContext context, string path)
    {
        var source = SourceResource.Read(typeof(CppStdRuntimeStep).Assembly, $"ValIO.Runtime.{Path.GetFileName(path)}");
        AddRuntimeFile(context, path, source);
    }

    /// <summary>Prints a header containing the configured include directives.</summary>
    /// <param name="context">The current generation request.</param>
    /// <param name="path">The relative output path.</param>
    /// <param name="includes">The directives in dependency order.</param>
    private void AddIncludeFile(GenerationContext context, string path, IReadOnlyList<CppIncludeDirectiveNode> includes)
    {
        var header = new CppHeaderFileNode(Path.GetFileName(path), includes: includes);
        AddRuntimeFile(context, path, Renderer.Render(header));
    }

    /// <summary>Adds a canonical runtime file once even if a pipeline is resumed or executed repeatedly.</summary>
    /// <param name="context">The current generation context.</param>
    /// <param name="path">The canonical output path.</param>
    /// <param name="content">The generated runtime content.</param>
    private static void AddRuntimeFile(GenerationContext context, string path, string content)
    {
        if (!context.GeneratedFiles.Any(file => file.RelativePath == path))
            context.GeneratedFiles.Add(new GeneratedFile(path, content));
    }
}
