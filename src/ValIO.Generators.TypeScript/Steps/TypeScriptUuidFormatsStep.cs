using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Plugins;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>Emits the shared regex used by validators in profiles that validate UUID format explicitly.</summary>
public sealed class TypeScriptUuidFormatsStep : ICodeGenerationStep
{
    /// <inheritdoc />
    public string Name => "typescript-uuid-formats";

    /// <inheritdoc />
    public int Order => 29;

    /// <inheritdoc />
    public bool ShouldExecute(GenerationContext context) =>
        context.PluginOptions is TypeScriptGenerationOptions opts && !string.IsNullOrEmpty(opts.ValidationLibrary) &&
        TypeScriptOptionIds.UuidFormat.Resolve(opts.UuidFormat) == TypeScriptOptionIds.UuidFormat.Regex &&
        UuidFormatSupport.DocumentUsesUuid(context.IrDocument);

    /// <inheritdoc />
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var modelsPath = context.Configuration.FileOrganization.ModelsPath ?? "models";
        var patternName = new IdentifierNode(TypeScriptOptionIds.UuidFormat.PatternConstName);
        var pattern = new VariableDeclarationNode(patternName,
            initializer: new LiteralExpressionNode(TypeScriptOptionIds.UuidFormat.Pattern, LiteralKind.String), isExported: true);
        var regex = new CallExpressionNode(new IdentifierExpressionNode(new IdentifierNode("RegExp")),
            [new IdentifierExpressionNode(patternName)], isNewExpression: true);
        var regexDeclaration = new VariableDeclarationNode(new(TypeScriptOptionIds.UuidFormat.RegexConstName),
            initializer: regex, isExported: true);
        var source = new SourceFileNode(declarations: [pattern, regexDeclaration],
            fileDocumentation: new CommentNode("Shared format patterns for generated validation schemas."));
        var path = $"{modelsPath}/{TypeScriptOptionIds.UuidFormat.ConstFileName}.ts";
        TypeScriptModuleFiles.Emit(context, path, source, new TypeScriptAstRenderer().Render);
        return Task.CompletedTask;
    }
}
