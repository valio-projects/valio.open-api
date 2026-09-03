using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Generators.Cpp;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.CompileTests;

/// <summary>
/// Compile-фикстура std-профиля: генерируем клиента in-proc → собираем CMake-проектом
/// с точными пинами cpp-httplib v0.18.3 + nlohmann_json v3.12.0 → запускаем smoke
/// (локальный httplib::Server + сгенерированный клиент; wire-имена, optional-семантика,
/// enum-кодек, не-2xx → Error{Status}, void-ответы, async). Матрица стандартов 17/20/23
/// покрывает и polyfill Expected (17), и std::expected (23).
/// Требует cmake в PATH (локально без тулчейна — Assert.Ignore).
/// </summary>
[TestFixture]
[Category("CompileFixture")]
[Parallelizable(ParallelScope.None)]
public class CppCompileFixtureTests
{
    private static IrDocument BuildDocument()
    {
        var order = new IrModel
        {
            Name = "Order",
            Properties =
            [
                new IrProperty
                {
                    OriginalName = "order_id", Name = "OrderId",
                    Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Long },
                    IsRequired = true,
                },
                new IrProperty
                {
                    OriginalName = "note", Name = "Note",
                    Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String },
                    IsRequired = false,
                },
                new IrProperty
                {
                    OriginalName = "status", Name = "Status",
                    Type = new IrModelReference { ModelName = "OrderStatus" },
                    IsRequired = true,
                },
            ],
        };
        var status = new IrEnum
        {
            Name = "OrderStatus",
            BaseType = IrEnumBaseType.String,
            Values =
            [
                new IrEnumValue { Name = "placed", Value = "placed" },
                new IrEnumValue { Name = "approved", Value = "approved" },
            ],
        };

        IrOperation Operation(string method, string path, string name, string? body = null, string successCode = "200", bool successBody = true) => new()
        {
            MethodName = name,
            HttpMethod = method,
            Path = path,
            PathParameters = path.Contains('{')
                ?
                [
                    new IrParameter
                    {
                        Name = "orderId",
                        Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Long },
                        IsRequired = true,
                    },
                ]
                : [],
            QueryParameters =
            [
                new IrParameter
                {
                    Name = "include",
                    Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String },
                    IsRequired = false,
                },
            ],
            HeaderParameters = [],
            RequestBody = body == null ? null : new IrRequestBody
            {
                Type = new IrModelReference { ModelName = body },
                IsRequired = true,
                ContentType = "application/json",
            },
            Responses =
            [
                new IrResponse
                {
                    StatusCode = successCode,
                    Type = successBody ? new IrModelReference { ModelName = "Order" } : null,
                },
                new IrResponse { StatusCode = "404" },
            ],
        };

        return new IrDocument
        {
            Metadata = new IrDocumentMetadata(),
            Models = [order],
            Enums = [status],
            Clients =
            [
                new IrClient
                {
                    Name = "Store",
                    Operations =
                    [
                        Operation("GET", "/store/order/{orderId}", "GetOrderById"),
                        Operation("POST", "/store/order", "PlaceOrder", body: "Order"),
                        Operation("DELETE", "/store/order/{orderId}", "DeleteOrder", successCode: "204", successBody: false),
                    ],
                },
            ],
            TypeDependencies = new IrTypeDependencyGraph
            {
                Dependencies = new Dictionary<string, IReadOnlyList<string>>(),
                TopologicalOrder = ["Order", "OrderStatus"],
            },
        };
    }

    private static async Task<string> GenerateIntoAsync(string outputDirectory, int cxxStandard, string apiStyle, string clientShape)
    {
        var options = new CppGenerationOptions
        {
            Runtime = CppRuntimeProfile.Std,
            CxxStandard = cxxStandard,
            ApiStyle = apiStyle == "both" ? CppApiStyle.Both : CppApiStyle.Sync,
            ClientShape = clientShape == "interface+impl" ? CppClientShape.InterfaceImpl : CppClientShape.Class,
            Namespace = "Petstore",
        };

        var services = new ServiceCollection()
            .AddSingleton<ICppAstRenderer, CppAstRenderer>()
            .BuildServiceProvider();
        var pipeline = new CppGeneratorPlugin().CreatePipeline(services);
        var context = new GenerationContext
        {
            IrDocument = BuildDocument(),
            Configuration = new GenerationConfiguration { LanguageId = "cpp", PluginOptions = options },
            PluginOptions = options,
        };
        await pipeline.ExecuteAsync(context);

        foreach (var file in context.GeneratedFiles)
        {
            var target = Path.Combine(outputDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, file.Content);
        }
        return outputDirectory;
    }

    private static string? FindCmake()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            var candidate = Path.Combine(directory.Trim('"'), "cmake" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static int RunTool(string fileName, string arguments, string workingDirectory, out string output)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.Start();
        output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds);
        return process.ExitCode;
    }

    [TestCase(17, "both", "interface+impl")]
    [TestCase(20, "sync", "class")]
    [TestCase(23, "both", "interface+impl")]
    public async Task GeneratedClient_CompilesAndRoundTrips(int cxxStandard, string apiStyle, string clientShape)
    {
        var cmake = FindCmake();
        if (cmake == null)
            Assert.Ignore("cmake is not on PATH — compile fixture requires a C++ toolchain");

        var workRoot = Path.Combine(Path.GetTempPath(), $"valio-cpp-fixture-{cxxStandard}-{apiStyle}-{clientShape}-{Guid.NewGuid():N}");
        var generatedDir = Path.Combine(workRoot, "gen");
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "fixtures");
        var buildDir = Path.Combine(workRoot, "build");
        // smoke-файл использует async-методы только при apiStyle=both/sync c async.
        var smokeAsync = apiStyle == "both" ? "1" : "0";
        try
        {
            await GenerateIntoAsync(generatedDir, cxxStandard, apiStyle, clientShape);

            // cmake -D с backslash-путями даёт «Invalid character escape '\U'» в VerifyGlobs —
            // генерируем glob-путь в forward-slash форме.
            var configureArguments = $"-S \"{fixturesDir}\" -B \"{buildDir}\" -DGEN_DIR=\"{generatedDir.Replace('\\', '/')}\" -DCMAKE_CXX_STANDARD={cxxStandard} -DVALIO_SMOKE_ASYNC={smokeAsync}";
            var configureExit = RunTool(cmake, configureArguments, workRoot, out var configureOutput);
            configureExit.Should().Be(0, $"cmake configure must succeed:\n{configureOutput}");

            var buildExit = RunTool(cmake, $"--build \"{buildDir}\" --config Debug", workRoot, out var buildOutput);
            buildExit.Should().Be(0, $"generated C++ must compile warning-clean against cpp-httplib 0.18.3 + nlohmann_json 3.12.0:\n{buildOutput}");

            var smokeExecutable = Directory.EnumerateFiles(buildDir, "smoke_main*", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetExtension(f) is ".exe" || f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f) == string.Empty);
            smokeExecutable.Should().NotBeNull($"smoke_main must be built:\n{buildOutput}");

            var smokeExit = RunTool(smokeExecutable!, string.Empty, workRoot, out var smokeOutput);
            smokeExit.Should().Be(0, $"smoke round-trip must pass (wire names, optional semantics, enum codec, non-2xx, void, async):\n{smokeOutput}");
        }
        finally
        {
            // FetchContent клонирует git-репозитории: pack-файлы read-only, Directory.Delete
            // без снятия атрибута падает с UnauthorizedAccessException на Windows.
            // Очистка best-effort — temp-каталог в любом случае почистится системой.
            try
            {
                foreach (var file in Directory.EnumerateFiles(workRoot, "*", SearchOption.AllDirectories))
                {
                    var attributes = File.GetAttributes(file);
                    if (attributes.HasFlag(FileAttributes.ReadOnly))
                        File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
                Directory.Delete(workRoot, recursive: true);
            }
            catch (Exception cleanupFailure) when (cleanupFailure is IOException or UnauthorizedAccessException)
            {
                // занято тулчейном — оставляем temp как есть
            }
        }
    }
}
