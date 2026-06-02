using System.Text;
using ValIO.Core.Plugins;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.JsDoc;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: генерирует корневой <c>index.ts</c> с re-export всех сгенерированных
/// TypeScript файлов (моделей, схем, клиентов, params/results/query).
/// </summary>
public sealed class TypeScriptIndexStep : ICodeGenerationStep
{
    public string Name => "typescript-index";
    public int Order => 70;

    public bool ShouldExecute(GenerationContext context)
        => context.GeneratedFiles.Any(f => f.RelativePath.EndsWith(".ts"));

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        var tsFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".ts"))
            .Select(f => f.RelativePath.Replace('\\', '/'))
            .Where(p => p != "index.ts" && !p.EndsWith("/index.ts"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        foreach (var path in tsFiles)
        {
            var importPath = "./" + path;
            if (importPath.EndsWith(".ts", StringComparison.Ordinal))
                importPath = importPath[..^3];
            sb.AppendLine($"export * from \"{importPath}\";");
        }

        if (sb.Length > 0)
        {
            var header = new StringBuilder();
            CommentGenerator.GenerateJSDocComment(header, 0,
                TypeScriptDoc.FileHeader("index.ts", "index", "Barrel file re-exporting all generated TypeScript modules.").Text);
            header.AppendLine();
            context.GeneratedFiles.Add(new GeneratedFile("index.ts", header.ToString() + sb.ToString()));
        }

        return Task.CompletedTask;
    }
}
