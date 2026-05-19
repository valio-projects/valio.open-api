using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Kotlin.Tests.TestHelpers;

namespace ValIO.Generators.Kotlin.Tests.Steps;

[TestFixture]
public class OkHttpClientStepTests
{
    private KotlinPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new KotlinPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EmitsClassWithOkHttpDependencies_InApiPackage()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').Contains("/api/", StringComparison.Ordinal))
            .ToList();
        clientFiles.Should().NotBeEmpty();

        var content = clientFiles[0].Content;
        content.Should().Contain("import okhttp3.OkHttpClient");
        content.Should().Contain("import okhttp3.Request");
        content.Should().Contain("import okhttp3.HttpUrl");
        content.Should().Contain("import kotlinx.coroutines.Dispatchers");
        content.Should().Contain("import kotlinx.coroutines.withContext");
        content.Should().Contain("import kotlinx.serialization.json.Json");
    }

    [Test]
    public async Task PrimaryConstructor_DeclaresPrivateValProperties()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("private val client: OkHttpClient");
        clientFile.Content.Should().Contain("private val baseUrl: HttpUrl");
        clientFile.Content.Should().Contain("private val json: Json");
    }

    [Test]
    public async Task OperationMethods_AreSuspendAndCamelCase()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("suspend fun getUsers(");
        clientFile.Content.Should().Contain("suspend fun getUserById(id: Int)");
        clientFile.Content.Should().NotContain("snake_case");
    }

    [Test]
    public async Task MethodBody_UsesWithContextAndDecodesJson()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("= withContext(Dispatchers.IO) {");
        clientFile.Content.Should().Contain("json.decodeFromString(User.serializer(), raw)");
    }

    [Test]
    public async Task PathParametersAreInterpolatedAsStringTemplates()
    {
        var doc = await KotlinPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("urlBuilder.addPathSegments(\"users/${id}\")");
    }
}
