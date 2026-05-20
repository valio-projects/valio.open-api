using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Snapshots;

/// <summary>
/// Golden-file snapshot tests: capture the rendered output of selected
/// steps against a stable IR (`simple-schema.json`) so accidental
/// rendering changes fail loudly. First run writes the snapshot.
/// Set <c>UPDATE_SNAPSHOTS=1</c> to regenerate after intentional changes.
/// </summary>
[TestFixture]
public class StepSnapshotTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task UserModel_FetchTransport_MatchesSnapshot()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var userModel = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/user/user.ts", StringComparison.Ordinal));
        Snapshot.Match(userModel.Content, "TypeScriptModelsStep/user_fetch");
    }

    [Test]
    public async Task UserSchema_Zod3_MatchesSnapshot()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "zod3",
        });

        var userSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("models/schemas/user/user.schema.ts", StringComparison.Ordinal));
        Snapshot.Match(userSchema.Content, "ZodSchemasStep/user_zod3");
    }

    [Test]
    public async Task GetUserByIdResults_FetchTransport_MatchesSnapshot()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        var results = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUserById.results.ts", StringComparison.Ordinal));
        Snapshot.Match(results.Content, "TypeScriptResultsStep/getUserById");
    }

    [Test]
    public async Task GetUserByIdKy_Zod3_MatchesSnapshot()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["ky"],
            ValidationLibrary = "zod3",
        });

        var kyClient = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("getUserById.ky.ts", StringComparison.Ordinal));
        Snapshot.Match(kyClient.Content, "KyClientStep/getUserById_zod3");
    }

    [Test]
    public async Task UserRoleEnumSchema_Valibot_MatchesSnapshot()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions
        {
            Transports = ["fetch"],
            ValidationLibrary = "valibot",
        });

        var enumSchema = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').EndsWith("schemas/userRole/userRole.schema.ts", StringComparison.OrdinalIgnoreCase));
        Snapshot.Match(enumSchema.Content, "ValibotSchemasStep/userRole");
    }
}
