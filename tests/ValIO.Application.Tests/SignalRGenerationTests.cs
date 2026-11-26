using System.Text.Json.Nodes;
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

    /// <summary>Verifies that both input formats generate the same shared HTTP and inline SignalR model references.</summary>
    /// <returns>The asynchronous equivalence assertion.</returns>
    [Test]
    public async Task JsonAndYaml_GenerateTheSameMixedContract()
    {
        var yaml = OpenApiFixture.Read("SignalR/mixed-contract.yaml");
        var json = OpenApiFixture.Read("SignalR/mixed-contract.json");
        var profile = new GenerationConfiguration
        {
            LanguageId = "csharp",
            NamingConvention = new NamingConventions { ModelPrefix = "Wire", ModelSuffix = "Dto" },
            PluginOptions = new CSharpGenerationOptions { GenerateSignalRHubs = true },
        };

        var fromJson = await GenerateAsync(profile, json);
        var fromYaml = await GenerateAsync(profile, yaml, "yaml");

        fromJson.Success.Should().BeTrue(fromJson.ErrorMessage);
        fromYaml.Success.Should().BeTrue(fromYaml.ErrorMessage);
        fromYaml.Files.Should().BeEquivalentTo(fromJson.Files);
        fromYaml.Files.Should().Contain(file => file.RelativePath == "SignalR/SupportSignalRClient.cs");
        var hub = fromYaml.Files.Single(file => file.RelativePath == "SignalR/SupportHubBase.cs");
        hub.Content.Should().Contain("Task<WireSharedResultDto> Submit(WireInlineModel0Dto @request)");
        fromYaml.Files.Should().Contain(file => file.RelativePath == "Models/InlineModel0.cs" && file.Content.Contains("class WireInlineModel0Dto"));
        fromYaml.Files.Should().Contain(file => file.Content.Contains("ReadResult(", StringComparison.Ordinal));
    }

    /// <summary>Stops generation when a JSON number would be lossy in the official JavaScript SignalR client.</summary>
    /// <returns>The asynchronous generation assertion.</returns>
    [Test]
    public async Task TypeScript_RejectsUnquotedExactNumbersInsideReferencedModels()
    {
        var document = JsonNode.Parse(OpenApiFixture.Read(Fixture))!;
        var sequence = document["components"]!["schemas"]!["SupportChanged"]!["properties"]!["visibleSequence"]!;
        sequence["type"] = "integer";
        sequence["format"] = "int64";

        var result = await GenerateAsync(new GenerationConfiguration { LanguageId = "typescript" }, document.ToJsonString());
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unquoted exact JSON numbers");
        result.Files.Should().BeEmpty();
    }

    /// <summary>Rejects invalid hub declarations with a diagnostic that identifies the contract error.</summary>
    /// <param name="violation">The change that makes the shared hub contract invalid.</param>
    /// <param name="expected">The diagnostic describing the contract violation.</param>
    /// <returns>The asynchronous generation assertion.</returns>
    [TestCase("missing-schema", "existing local component schema")]
    [TestCase("duplicate-method", "duplicate wire name")]
    [TestCase("reference-sibling", "additional schema keywords")]
    public async Task InvalidContract_ReportsTheContractViolation(string violation, string expected)
    {
        var document = JsonNode.Parse(OpenApiFixture.Read(Fixture))!;
        var methods = document["x-valio-signalr"]!["hubs"]![0]!["methods"]!.AsArray();
        var returnType = methods[0]!["result"]!;
        switch (violation)
        {
            case "missing-schema":
                returnType["$ref"] = "#/components/schemas/Missing";
                break;
            case "duplicate-method":
                methods[1]!["name"] = "SubscribeTicket";
                break;
            case "reference-sibling":
                returnType["nullable"] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(violation));
        }

        var result = await GenerateAsync(new GenerationConfiguration { LanguageId = "csharp" }, document.ToJsonString());
        result.Success.Should().BeFalse();
        result.Files.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("x-valio-signalr").And.Contain(expected);
    }

    /// <summary>Runs the real pipeline against a temporary input file using the production plugin registration.</summary>
    /// <param name="configuration">The profile to exercise.</param>
    /// <param name="source">An optional replacement for the shared fixture.</param>
    /// <param name="extension">The extension of the input document.</param>
    /// <returns>The generated files and diagnostics.</returns>
    private static async Task<OrchestrationResult> GenerateAsync(GenerationConfiguration configuration, string? source = null, string extension = "json")
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton<ILogger<GenerationOrchestrator>>(NullLogger<GenerationOrchestrator>.Instance);
        services.AddValIOCore().AddValIOApplication().AddAllGeneratorPlugins();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IGenerationOrchestrator>();
        var directory = Directory.CreateTempSubdirectory("valio-signalr-test-");
        var path = Path.Combine(directory.FullName, $"support.{extension}");
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
