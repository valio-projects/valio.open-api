using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ValIO.Application.DependencyInjection;
using ValIO.Application.Services;
using ValIO.Core.Models;
using ValIO.Generators.CSharp.Options;
using ValIO.Plugins.Bootstrap;
using ValIO.Tests.Fixtures;

namespace ValIO.Application.Tests;

/// <summary>Checks SignalR contracts through the normal OpenAPI-to-files pipeline.</summary>
[TestFixture]
public sealed class SignalRGenerationTests
{
    /// <summary>The contract includes typed RPC, multi-argument events, and a signal without a payload.</summary>
    private const string Fixture = "SignalR/support.json";

    /// <summary>Emits an independent hub base for hosts and a typed client for callers according to the profile.</summary>
    /// <param name="generateClients">Whether to emit the C# client.</param>
    /// <param name="generateHubs">Whether to emit the C# server contract.</param>
    /// <returns>The asynchronous generation assertion.</returns>
    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(false, false)]
    public async Task CSharp_RespectsHubAndClientGenerationSwitches(bool generateClients, bool generateHubs)
    {
        var config = new GenerationConfiguration
        {
            LanguageId = "csharp",
            GenerateClients = generateClients,
            PluginOptions = new CSharpGenerationOptions { GenerateSignalRHubs = generateHubs }
        };

        var result = await GenerateAsync(config);
        result.Success.Should().BeTrue(result.ErrorMessage);
        var files = result.Files.ToDictionary(file => file.RelativePath, file => file.Content);
        files.Should().ContainKey("Models/SupportChanged.cs").And.ContainKey("Models/RealtimeResult.cs");
        files.ContainsKey("SignalR/SupportHubBase.cs").Should().Be(generateHubs);
        files.ContainsKey("SignalR/SupportSignalRClient.cs").Should().Be(generateClients);
        if (generateHubs)
        {
            files["SignalR/SupportHubBase.cs"].Should()
                .Contain("Hub<ISupportClientEvents>")
                .And.Contain("Task<RealtimeResult> SubscribeTicket(Guid @ticketId)")
                .And.Contain("SupportChanged(SupportChanged @message, int? @attempt)")
                .And.Contain("WalletChanged()");
        }

        if (generateClients)
        {
            files["SignalR/SupportSignalRClient.cs"].Should()
                .Contain("InvokeCoreAsync<RealtimeResult>(\"SubscribeTicket\", [@ticketId], cancellationToken)")
                .And.Contain("On(\"SupportChanged\", [ParameterType<SupportChanged>(), ParameterType<int?>()]")
                .And.Contain("On(\"WalletChanged\", []");
        }
    }

    /// <summary>Uses the configured model names and paths in typed calls and removes the exact registered handler.</summary>
    /// <returns>The asynchronous generation assertion.</returns>
    [Test]
    public async Task TypeScript_ReusesModelNamesAndFileSettingsForInvocationsAndEvents()
    {
        var config = new GenerationConfiguration
        {
            LanguageId = "typescript",
            FileOrganization = new FileOrganization { ModelsPath = "custom/models", SeparateModels = true },
            NamingConvention = new NamingConventions { ModelPrefix = "Wire" }
        };

        var result = await GenerateAsync(config);
        result.Success.Should().BeTrue(result.ErrorMessage);
        var files = result.Files.ToDictionary(file => file.RelativePath, file => file.Content);
        var support = Regex.Replace(files["signalr/supportSignalR.ts"], @"\s+", " ");
        support.Should()
            .Contain("import type { WireRealtimeResult } from \"../custom/models/realtimeResult/realtimeResult\"")
            .And.Contain("connection.invoke<WireRealtimeResult>( \"SubscribeTicket\", _ticketId )")
            .And.Contain("_message: WireSupportChanged, _attempt: number | null")
            .And.Contain("connection.off( \"SupportChanged\", handler )")
            .And.Contain("WalletChanged: (handler: () => void)");
        files["index.ts"].Should().Contain("./signalr/supportSignalR").And.Contain("./signalr/notificationsSignalR");
        var notifications = Regex.Replace(files["signalr/notificationsSignalR.ts"], @"\s+", " ");
        notifications.Should()
            .Contain("export const notificationsHubPath = \"/hubs/notifications\"")
            .And.Contain("connection.on( \"PaymentsChanged\", handler )")
            .And.NotContain("SubscribeTicket");
    }

    /// <summary>Emits ordinary REST clients and the SignalR client when the same document provides both.</summary>
    /// <returns>The asynchronous generation assertion.</returns>
    [Test]
    public async Task CSharp_CombinesRestAndSignalRInTheSameProfile()
    {
        const string httpPath = """
            "paths": {
              "/health": {
                "get": {
                  "operationId": "ReadHealth", "tags": ["Health"],
                  "responses": { "204": { "description": "Healthy" } }
                }
              }
            }
            """;
        var source = OpenApiFixture.Read(Fixture).Replace("\"paths\": {}", httpPath, StringComparison.Ordinal);
        var result = await GenerateAsync(new GenerationConfiguration { LanguageId = "csharp" }, source);
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Files.Should().Contain(file => file.RelativePath == "SignalR/SupportSignalRClient.cs");
        result.Files.Should().Contain(file => file.Content.Contains("ReadHealth(", StringComparison.Ordinal));
    }

    /// <summary>Stops generation when a JSON number would be lossy in the official JavaScript SignalR client.</summary>
    /// <returns>The asynchronous generation assertion.</returns>
    [Test]
    public async Task TypeScript_RejectsUnquotedExactNumbersInsideReferencedModels()
    {
        var source = OpenApiFixture.Read(Fixture).Replace(
            "\"visibleSequence\": { \"type\": \"string\" }",
            "\"visibleSequence\": { \"type\": \"integer\", \"format\": \"int64\" }", StringComparison.Ordinal);

        var result = await GenerateAsync(new GenerationConfiguration { LanguageId = "typescript" }, source);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unquoted exact JSON numbers");
        result.Files.Should().BeEmpty();
    }

    /// <summary>Rejects references to a missing schema and duplicate hub method names with an actionable diagnostic.</summary>
    /// <param name="sourceText">The source fragment to replace.</param>
    /// <param name="replacement">An invalid declaration in its place.</param>
    /// <param name="expected">The diagnostic describing the contract violation.</param>
    /// <returns>The asynchronous generation assertion.</returns>
    [TestCase("#/components/schemas/RealtimeResult", "#/components/schemas/Missing", "existing local component schema")]
    [TestCase("\"Heartbeat\"", "\"SubscribeTicket\"", "duplicate wire name")]
    [TestCase("\"$ref\": \"#/components/schemas/RealtimeResult\"",
        "\"$ref\": \"#/components/schemas/RealtimeResult\", \"nullable\": true", "additional schema keywords")]
    public async Task InvalidContract_ReportsTheContractViolation(string sourceText, string replacement, string expected)
    {
        var source = OpenApiFixture.Read(Fixture).Replace(sourceText, replacement, StringComparison.Ordinal);

        var result = await GenerateAsync(new GenerationConfiguration { LanguageId = "csharp" }, source);

        result.Success.Should().BeFalse();
        result.Files.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("x-valio-signalr").And.Contain(expected);
    }

    /// <summary>Runs the real pipeline against a temporary input file using the production plugin registration.</summary>
    /// <param name="configuration">The profile to exercise.</param>
    /// <param name="source">An optional replacement for the shared fixture.</param>
    /// <returns>The generated files and diagnostics.</returns>
    private static async Task<OrchestrationResult> GenerateAsync(GenerationConfiguration configuration, string? source = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton<ILogger<GenerationOrchestrator>>(NullLogger<GenerationOrchestrator>.Instance);
        services.AddValIOCore().AddValIOApplication().AddAllGeneratorPlugins();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IGenerationOrchestrator>();
        var directory = Directory.CreateTempSubdirectory("valio-signalr-test-");
        var path = Path.Combine(directory.FullName, "support.json");
        try
        {
            await File.WriteAllTextAsync(path, source ?? OpenApiFixture.Read(Fixture));
            return await generator.GenerateAsync(configuration, path);
        }
        finally
        {
            File.Delete(path);
            directory.Delete();
        }
    }
}
