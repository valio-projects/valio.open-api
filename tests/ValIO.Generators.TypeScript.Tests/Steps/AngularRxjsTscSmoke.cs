using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// End-to-end check: generates a full Angular RxJS Petstore output to a temp
/// directory and runs <c>tsc --strict --noEmit</c> against the result.
/// Skipped when Node / <c>tsc</c> isn't available (so CI without Node still passes).
/// </summary>
[TestFixture]
public class AngularRxjsTscSmoke
{
    [Test]
    [Category("Tsc")]
    public async Task GeneratedAngularOutput_CompilesUnderTscStrict()
    {
        // Set ANGULAR_TSC_SMOKE_DIR to a prepared directory that already has
        // node_modules with @angular/core, @angular/common, rxjs and a tsconfig
        // (strict, noEmit, skipLibCheck:true). The test then writes generated
        // sources alongside and shells out to `npx tsc --noEmit -p .`.
        var smokeDir = Environment.GetEnvironmentVariable("ANGULAR_TSC_SMOKE_DIR");
        if (string.IsNullOrWhiteSpace(smokeDir) || !Directory.Exists(smokeDir))
        {
            Assert.Ignore("ANGULAR_TSC_SMOKE_DIR is not set or directory does not exist; skipping tsc smoke.");
            return;
        }

        // Generate the full TypeScript Petstore output (models + params + results
        // + service) into the smoke dir's src/.
        using var harness = new PipelineTestHarness();
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["angular-rxjs"],
        });

        var srcDir = Path.Combine(smokeDir, "src");
        if (Directory.Exists(srcDir))
            Directory.Delete(srcDir, recursive: true);
        Directory.CreateDirectory(srcDir);

        foreach (var file in ctx.GeneratedFiles)
        {
            var targetPath = Path.Combine(srcDir, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);
            await File.WriteAllTextAsync(targetPath, file.Content);
        }

        // Shell out to tsc.
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "sh",
            Arguments = OperatingSystem.IsWindows() ? "/c npx tsc --noEmit -p ." : "-c \"npx tsc --noEmit -p .\"",
            WorkingDirectory = smokeDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        process.ExitCode
            .Should()
            .Be(0, "tsc should succeed.\nSTDOUT:\n{0}\nSTDERR:\n{1}", stdout, stderr);
    }
}
