using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Java.Ast.Builders;
using ValIO.Generators.Java.Ast.Renderers;
using ValIO.Generators.Java.Options;

namespace ValIO.Generators.Java.Steps;

/// <summary>
/// Step: генерирует OkHttp-клиент (один файл на IR-tag) и опционально build-файлы.
/// </summary>
public sealed class OkHttpClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IJavaAstRenderer _renderer;
    private readonly JavaIrTypeMapper _typeMapper;

    public string Name => "java-okhttp-client";
    public int Order => 40;

    public OkHttpClientStep(INamingConverter namingConverter, IJavaAstRenderer renderer, JavaIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        if (context.IrDocument.Clients.Count == 0) return false;
        var opts = context.PluginOptions as JavaGenerationOptions;
        return opts == null || string.Equals(opts.UseHttpClient, JavaOptionIds.HttpClient.OkHttp, StringComparison.OrdinalIgnoreCase);
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as JavaGenerationOptions ?? new JavaGenerationOptions();
        var builder = new OkHttpClientAstBuilder(_namingConverter, _typeMapper);

        foreach (var client in context.IrDocument.Clients)
        {
            var sourceFile = builder.BuildSourceFile(client, opts.Package);
            var content = _renderer.Render(sourceFile);
            var className = string.IsNullOrWhiteSpace(client.Name) ? "ApiClient" : client.Name;
            var fileName = className.EndsWith("Api", StringComparison.OrdinalIgnoreCase) ? className : className + "Api";
            var relativePath = $"api/{fileName}.java";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        if (opts.GenerateBuildFiles)
        {
            const string buildGradle = """
plugins {
    id("java")
    id("application")
}

java {
    toolchain {
        languageVersion.set(JavaLanguageVersion.of(21))
    }
}

repositories {
    mavenCentral()
}

dependencies {
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("com.fasterxml.jackson.core:jackson-databind:2.17.0")
    implementation("com.fasterxml.jackson.datatype:jackson-datatype-jsr310:2.17.0")
}
""";
            const string gradleProps = """
org.gradle.jvmargs=-Xmx2g
""";
            context.GeneratedFiles.Add(new GeneratedFile("build.gradle.kts", buildGradle));
            context.GeneratedFiles.Add(new GeneratedFile("gradle.properties", gradleProps));
        }

        return Task.CompletedTask;
    }
}
