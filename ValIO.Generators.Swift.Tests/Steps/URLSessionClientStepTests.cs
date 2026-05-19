using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.Swift.Tests.TestHelpers;

namespace ValIO.Generators.Swift.Tests.Steps;

[TestFixture]
public class URLSessionClientStepTests
{
    private SwiftPipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new SwiftPipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task EmitsPublicFinalClassWithURLSessionDependencies_InApiDir()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.Replace('\\', '/').Contains("/Api/", StringComparison.Ordinal))
            .ToList();
        clientFiles.Should().NotBeEmpty();

        var content = clientFiles[0].Content;
        content.Should().Contain("import Foundation");
        content.Should().Contain("public final class");
        content.Should().Contain("private let session: URLSession");
        content.Should().Contain("private let baseURL: URL");
        content.Should().Contain("private let decoder: JSONDecoder");
    }

    [Test]
    public async Task InitializerHasDefaultsForSessionAndDecoder()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/Api/", StringComparison.Ordinal));
        var normalized = clientFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized.Should().Contain("public init(session: URLSession = .shared");
        normalized.Should().Contain("decoder: JSONDecoder = JSONDecoder()");
        normalized.Should().Contain("self.session = session");
    }

    [Test]
    public async Task OperationMethods_AreAsyncThrowsAndLowerCamelCase()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/Api/", StringComparison.Ordinal));
        var normalized = clientFile.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized.Should().Contain("public func getUsers() async throws -> [User]");
        normalized.Should().Contain("public func getUserById(id: Int) async throws -> User");
    }

    [Test]
    public async Task MethodBody_UsesSessionDataAndJsonDecode()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/Api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("try await session.data(for: request)");
        clientFile.Content.Should().Contain("return try decoder.decode(User.self, from: data)");
    }

    [Test]
    public async Task PathParametersAreInterpolatedAsSwiftStringTemplates()
    {
        var doc = await SwiftPipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc);

        var clientFile = ctx.GeneratedFiles.First(f =>
            f.RelativePath.Replace('\\', '/').Contains("/Api/", StringComparison.Ordinal));
        clientFile.Content.Should().Contain("baseURL.appendingPathComponent(\"users/\\(id)\")");
    }
}
