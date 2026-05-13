using System.Text;
using ValIO.Core.Plugins;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: генерирует index.ts с re-export всех моделей и enum-ов.
/// </summary>
public sealed class TypeScriptIndexStep : ICodeGenerationStep
{
    public string Name => "typescript-index";
    public int Order => 70;

    public bool ShouldExecute(GenerationContext context)
        => context.GeneratedFiles.Any(f =>
            f.RelativePath.EndsWith(".ts") && !f.RelativePath.Contains("/api/"));

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // Собираем все .ts файлы, которые не являются клиентскими (api/)
        var modelFiles = context.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".ts")
                        && !f.RelativePath.Contains("/api/")
                        && !f.RelativePath.EndsWith("index.ts"))
            .OrderBy(f => f.RelativePath)
            .ToList();

        foreach (var file in modelFiles)
        {
            // Убираем расширение .ts из пути импорта
            var importPath = "./" + file.RelativePath.Replace('\\', '/');
            if (importPath.EndsWith(".ts"))
                importPath = importPath[..^3];

            sb.AppendLine($"export * from \"{importPath}\";");
        }

        if (sb.Length > 0)
        {
            var indexPath = "index.ts";
            if (modelFiles.Count > 0)
            {
                // Кладём index.ts рядом с первым файлом (в корне models/)
                var firstFile = modelFiles[0].RelativePath.Replace('\\', '/');
                var dir = firstFile.Contains('/') ? firstFile[..firstFile.LastIndexOf('/')] : "";
                // Определяем корень: если SeparateModels — models/, иначе рядом
                if (dir.Contains('/'))
                    dir = dir[..dir.LastIndexOf('/')]; // поднимаемся на уровень выше
                indexPath = dir.Length > 0 ? $"{dir}/index.ts" : "index.ts";
            }

            context.GeneratedFiles.Add(new GeneratedFile(indexPath, sb.ToString()));
        }

        return Task.CompletedTask;
    }
}
