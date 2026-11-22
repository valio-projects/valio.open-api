using System.Diagnostics;
using System.Text;
using Microsoft.OpenApi;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Builders;
using ValIO.Core.Services;
using ValIO.Core.Models;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Models;
using ValIO.Generators.CSharp.Options;
using ValIO.Generators.CSharp.Services;
using ValIO.Generators.CSharp.Steps;
using ValIO.Tests.Fixtures;

namespace ValIO.Generators.CSharp.Tests;

/// <summary>Compiles and executes generated C# in an isolated SDK project without repository references.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CSharpGeneratedCompilationTests
{
    /// <summary>
    /// Projects required and repeated multipart parts as borrowed streams and retains byte arrays in the shared JSON model.
    /// </summary>
    /// <returns>The assertions for the wire field name, optional inline part, and generated stream content factory.</returns>
    [Test]
    public async Task MultipartOpenApi_EmitsBorrowedFilesAndKeepsJsonByteArrays()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(OpenApiFixture.Read("CoreIR/multipart-file-parts-3.0.json")));
        var parsed = await OpenApiDocument.LoadAsync(input, "json");
        var ir = new IrBuilder(new SchemaResolver(parsed.Document!), new NamingConverter()).BuildFromOpenApi(parsed.Document!);
        var context = new GenerationContext
        {
            IrDocument = ir,
            Configuration = new GenerationConfiguration
            {
                Namespace = "Generated.Contracts",
                NamingConvention = new NamingConventions { Properties = NamingConvention.PascalCase }
            },
            PluginOptions = new CSharpGenerationOptions()
        };
        var naming = new CSharpNamingConverter();
        var renderer = new CSharpAstRenderer();
        await new CSharpModelsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpClientStep(naming, renderer).ExecuteAsync(context);

        var generated = context.GeneratedFiles.ToDictionary(file => file.RelativePath, file => file.Content);
        generated["Models/UploadFilesFormModel.cs"].Should()
            .Contain("MultipartFile PrimaryFile").And.Contain("MultipartFile[] Attachments")
            .And.Contain("[JsonPropertyName(\"primary-file\")]");
        generated["Models/UploadFields.cs"].Should().Contain("byte[] PrimaryFile").And.Contain("byte[][] Attachments");
        generated["Models/UploadInlineFormModel.cs"].Should().Contain("MultipartFile? File");
        generated["Models/MultipartFile.cs"].Should().Contain("MultipartFile(Stream content, string fileName, string contentType");
        generated["Api/UploadsClient.cs"].Should().Contain("MultipartFormDataContentFactory.Create(request)");
        generated["Api/MultipartFormDataContentFactory.cs"].Should().Contain("new StreamContent(new BorrowedReadStream(file.Content))");
    }

    /// <summary>
    /// Executes the generated response-aware financial client against synthetic success, conflict, validation, and absence responses.
    /// </summary>
    /// <remarks>
    /// A declared JSON receipt keeps the exact decimal and int64, and a documented empty 404 keeps the actual no-store and trace headers.
    /// Wrong media, malformed or oversized JSON, undeclared status, a nonempty 404, and missing required fields expose status without a typed body.
    /// Numeric, wrong-case, and undeclared enum values leave the receipt unconfirmed, while explicit zero and false remain valid.
    /// A separate response limit admits the full 500-row evidence page and every call sends exactly one request through the configured gateway path.
    /// </remarks>
    /// <returns>The isolated C# compilation and the assertions against the generated client.</returns>
    [Test]
    public async Task OptInResponseMetadata_ConfirmsOnlyDeclaredResponses()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(OpenApiFixture.Read("CoreIR/csharp-response-metadata-3.0.json")));
        var parsed = await OpenApiDocument.LoadAsync(input, "json");
        var ir = new IrBuilder(new SchemaResolver(parsed.Document!), new NamingConverter()).BuildFromOpenApi(parsed.Document!);
        var context = new GenerationContext
        {
            IrDocument = ir,
            Configuration = new GenerationConfiguration
            {
                Namespace = "Generated.Contracts",
                NamingConvention = new NamingConventions { Properties = NamingConvention.PascalCase }
            },
            PluginOptions = new CSharpGenerationOptions
            {
                GenerateInterfaces = true,
                InterfaceFileStrategy = InterfaceFileStrategy.SeparateFile,
                UseRequired = true,
                GenerateResponseMetadata = true,
                ResponseMetadataOperations = new Dictionary<string, int> { ["FindReceipt"] = 256, ["ReadWins"] = 524_288 },
                StrictStringEnums = true,
                PascalCaseEnumMembers = true,
                GenerateTraces = true,
                GenerateMetrics = true,
            }
        };
        var naming = new CSharpNamingConverter();
        var renderer = new CSharpAstRenderer();
        await new CSharpModelsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpEnumsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpClientStep(naming, renderer).ExecuteAsync(context);

        var files = context.GeneratedFiles.ToDictionary(file => file.RelativePath, file => file.Content);
        files["Api/IReceipts.cs"].Should().Contain("Task<Receipt> FindReceipt(")
            .And.Contain("Task<ApiResponse<Receipt>> FindReceiptWithResponse(")
            .And.Contain("Task<ApiResponse<WinPage>> ReadWinsWithResponse(")
            .And.Contain("Task ReadOrdinary(").And.NotContain("ReadOrdinaryWithResponse");
        files["Api/ReceiptsClient.cs"].Should().Contain("ApiResponseReader.SendAsync<Receipt>")
            .And.Contain("new ApiResponseContract(400, \"application/problem+json\", typeof(ReceiptProblem))")
            .And.Contain("new ApiResponseContract(404, null, null)");
        files["Api/ApiResponse.cs"].Should().Contain("HttpCompletionOption.ResponseHeadersRead").And.Contain("ReadAtLeastAsync");
        ir.Clients.Single().Operations.Single(operation => operation.OriginalMethodName == "FindReceipt")
            .Responses.Single(response => response.StatusCode == "404")
            .Headers.Single().Name.Should().Be("Cache-Control");

        var directory = Path.Combine(Path.GetTempPath(), "ValIO.CSharp.ResponseMetadata", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Smoke.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
            foreach (var (path, content) in files)
            {
                var destination = Path.Combine(directory, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllTextAsync(destination, content);
            }

            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), """"
                using System.Net;
                using System.Net.Http.Headers;
                using System.Text;
                using System.Text.Json;
                using Generated.Contracts;

                var id = Guid.Parse("43648bac-f7af-4a8a-b26a-a4d272780b93");
                using var handler = new FixtureHandler(id);
                using var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/private-owner/") };
                IReceipts client = new Receipts(http);

                var found = await client.FindReceiptWithResponse(id);
                if (found.StatusCode != 200 || found.ContentType != "application/json" || !found.HasBody || found.IsBodyless ||
                    found.Body is not { Revision: 9007199254740993L, Amount: 123456789123456789.1234567891m, Accepted: true,
                        Status: ReceiptStatus.Applied })
                    throw new InvalidOperationException("The typed success receipt lost its HTTP or numeric contract.");

                var conflict = await client.FindReceiptWithResponse(id);
                if (conflict.StatusCode != 409 || !conflict.TryGetBody<Receipt>(out var rejection) || rejection.Accepted ||
                    rejection.Status != ReceiptStatus.Rejected || rejection.ErrorCode != ReceiptError.InsufficientCoins)
                    throw new InvalidOperationException("A documented conflict did not retain its typed finance receipt.");

                var missing = await client.FindReceiptWithResponse(id);
                if (missing.StatusCode != 404 || !missing.IsBodyless || !missing.CacheControlNoStore || missing.HasBody ||
                    !missing.Headers.TryGetValue("CACHE-Control", out var cache) || !cache.Contains("no-store") ||
                    !missing.Headers.TryGetValue("x-OWNER-trace", out var trace) || !trace.SequenceEqual(["fixture"]))
                    throw new InvalidOperationException("The authoritative empty 404 did not retain received headers.");

                var invalid = await client.FindReceiptWithResponse(id);
                if (invalid.StatusCode != 400 || invalid.HasBody ||
                    !invalid.TryGetBody<ReceiptProblem>(out var problem) || problem.Code != "invalid_request")
                    throw new InvalidOperationException("The explicitly declared alternate schema was not decoded.");

                foreach (var status in new[] { 409, 409, 202, 404, 409, 409, 409, 409, 409, 409 })
                {
                    var unconfirmed = await client.FindReceiptWithResponse(id);
                    if (unconfirmed.StatusCode != status || unconfirmed.HasBody || unconfirmed.IsBodyless)
                        throw new InvalidOperationException("An unexpected response was mistaken for authoritative evidence.");
                }

                var zero = await client.FindReceiptWithResponse(id);
                if (zero.Body is not { Revision: 0L, Amount: 0m, Accepted: false, Status: ReceiptStatus.Rejected })
                    throw new InvalidOperationException("Explicit zero and false fields were not recognized.");

                var page = await client.ReadWinsWithResponse(id);
                var wins = page.Body;
                if (page.StatusCode != 200 || wins is null || wins.Entries.Length != 500 ||
                    wins.Entries[499].Amount != 123456789123456789.1234567891m ||
                    wins.Entries[499].ReversalSequence != long.MaxValue)
                    throw new InvalidOperationException("The accepted maximum-size evidence page could not be read.");

                if (handler.Sends != 16)
                    throw new InvalidOperationException("A metadata method issued more than one HTTP request.");
                Console.WriteLine("CSharp response metadata passed");

                /// <summary>Emits typed and ambiguous replies while asserting the injected HttpClient address is preserved.</summary>
                internal sealed class FixtureHandler : HttpMessageHandler
                {
                    /// <summary>Retains an exact int64 and decimal in a literal JSON payload.</summary>
                    private const string ReceiptJson = """
                        {"revision":9007199254740993,"amount":123456789123456789.1234567891,"accepted":true,"status":"Applied"}
                        """;
                    /// <summary>Includes the exact declared error wire label on a rejected financial receipt.</summary>
                    private const string RejectedJson = """
                        {"revision":9007199254740993,"amount":123456789123456789.1234567891,"accepted":false,"status":"Rejected","errorCode":"insufficient_coins"}
                        """;
                    /// <summary>Checks that path parameters use the selected receipt identity.</summary>
                    private readonly Guid _id;

                    /// <summary>Initializes the stub for the selected receipt.</summary>
                    /// <param name="id">The path identity expected on each request.</param>
                    internal FixtureHandler(Guid id)
                    {
                        _id = id;
                    }

                    /// <summary>Gets the number of requests issued by the generated client.</summary>
                    /// <value>The fixture uses sixteen requests, including the independently bounded wins page.</value>
                    internal int Sends { get; private set; }

                    /// <inheritdoc />
                    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (request.RequestUri?.AbsolutePath == "/private-owner/api/v1/receipts/" + _id + "/wins")
                        {
                            Sends++;
                            var payload = JsonSerializer.Serialize(new
                            {
                                entries = Enumerable.Range(0, 500).Select(index => new
                                {
                                    winId = _id,
                                    sequence = long.MaxValue - 500 + index,
                                    journalEntryId = _id,
                                    amount = 123456789123456789.1234567891m,
                                    restricted = false,
                                    observedAt = "2026-01-01T00:00:00.1234567+00:00",
                                    postedAt = "2026-01-01T00:00:01.1234567+00:00",
                                    reversalId = _id,
                                    reversalSequence = long.MaxValue,
                                    reversalJournalEntryId = _id,
                                    reversalRestricted = true
                                })
                            });
                            if (Encoding.UTF8.GetByteCount(payload) <= 65_536)
                                throw new InvalidOperationException("The fixture must exceed the ordinary receipt budget.");
                            return Task.FromResult(Json(HttpStatusCode.OK, payload));
                        }

                        if (request.RequestUri?.AbsolutePath != "/private-owner/api/v1/receipts/" + _id ||
                            !request.Headers.Accept.Any(type => type.MediaType == "application/problem+json"))
                            throw new InvalidOperationException("The configured gateway prefix, identity, or declared Accept values were lost.");

                        var response = (++Sends) switch
                        {
                            1 => Json(HttpStatusCode.OK, ReceiptJson, "Application/JSON"),
                            2 => Json(HttpStatusCode.Conflict, RejectedJson),
                            3 => new HttpResponseMessage(HttpStatusCode.NotFound),
                            4 => Json(HttpStatusCode.BadRequest, "{\"code\":\"invalid_request\"}", "application/problem+json"),
                            5 => Json(HttpStatusCode.Conflict, ReceiptJson, "text/html"),
                            6 => Json(HttpStatusCode.Conflict, "{broken"),
                            7 => Json(HttpStatusCode.Accepted, ReceiptJson),
                            8 => Json(HttpStatusCode.NotFound, "{\"code\":\"edge_not_found\"}"),
                            9 => Json(HttpStatusCode.Conflict, ReceiptJson + new string(' ', 257)),
                            10 => Json(HttpStatusCode.Conflict, """{"revision":0,"amount":0,"status":"Applied"}"""),
                            11 => Json(HttpStatusCode.Conflict, ReceiptJson.Replace("\"Applied\"", "1")),
                            12 => Json(HttpStatusCode.Conflict, ReceiptJson.Replace("Applied", "applied")),
                            13 => Json(HttpStatusCode.Conflict, RejectedJson.Replace("insufficient_coins", "undeclared_business_error")),
                            14 => Json(HttpStatusCode.Conflict, RejectedJson.Replace("insufficient_coins", "InsufficientCoins")),
                            15 => Json(HttpStatusCode.Conflict, """{"revision":0,"amount":0,"accepted":false,"status":"Rejected"}"""),
                            _ => throw new InvalidOperationException("Unexpected request.")
                        };
                        response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
                        response.Headers.Add("X-Owner-Trace", "fixture");
                        return Task.FromResult(response);
                    }

                    /// <summary>Encodes the stub response without parsing or rounding its JSON literals.</summary>
                    /// <param name="status">The synthetic status code.</param>
                    /// <param name="json">The exact response bytes represented as UTF-8 text.</param>
                    /// <param name="mediaType">The observed response media type.</param>
                    /// <returns>The response dispatched through the generated transport.</returns>
                    private static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
                        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };
                }
                """");

            var result = await RunGeneratedProject(directory);
            result.ExitCode.Should().Be(0, "the generated response-aware client must compile and preserve evidence:{0}{1}",
                Environment.NewLine, result.Output);
            result.Output.Should().Contain("CSharp response metadata passed");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Compiles nullable references from real OpenAPI and exercises their System.Text.Json codecs.</summary>
    /// <returns>The asynchronous source generation, compiler and runtime verification.</returns>
    [Test]
    public async Task NullableReferenceUnionsCompileAndRoundTrip()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(OpenApiFixture.Read("CoreIR/nullable-reference-unions-3.1.json")));
        var parsed = await OpenApiDocument.LoadAsync(input, "json");
        var ir = new IrBuilder(new SchemaResolver(parsed.Document!), new NamingConverter()).BuildFromOpenApi(parsed.Document!);
        var context = new GenerationContext
        {
            IrDocument = ir,
            Configuration = new GenerationConfiguration { Namespace = "Generated.Contracts" },
            PluginOptions = new CSharpGenerationOptions()
        };
        var naming = new CSharpNamingConverter();
        var renderer = new CSharpAstRenderer();
        await new CSharpModelsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpEnumsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpClientStep(naming, renderer).ExecuteAsync(context);
        var model = context.GeneratedFiles.Single(file => file.RelativePath.EndsWith("NullableReferences.cs", StringComparison.Ordinal)).Content;
        model.Should().Contain("NullableCountry?").And.Contain("NullableState?");
        var directory = Path.Combine(Path.GetTempPath(), "ValIO.CSharp.Nullable", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "Smoke.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        foreach (var file in context.GeneratedFiles)
        {
            var destination = Path.Combine(directory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, file.Content);
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), """"
            using System.Text.Json;
            using Generated.Contracts;
            var empty = JsonSerializer.Deserialize<NullableReferences>("""{"country":null,"state":null,"countries":[null],"states":{"one":null}}""")!;
            var emptyJson = JsonSerializer.SerializeToElement(empty);
            if (emptyJson.GetProperty("country").ValueKind != JsonValueKind.Null || emptyJson.GetProperty("state").ValueKind != JsonValueKind.Null) throw new Exception("Null references were lost.");
            var populated = JsonSerializer.Deserialize<NullableReferences>("""{"country":{"id":7,"name":"DE"},"state":"granted","countries":[null,{"id":9,"name":"US"}],"states":{"one":"pending","two":null}}""")!;
            var json = JsonSerializer.SerializeToElement(populated);
            if (json.GetProperty("country").GetProperty("id").GetInt32() != 7 || json.GetProperty("state").GetString() != "granted" || json.GetProperty("countries")[1].GetProperty("id").GetInt32() != 9) throw new Exception("Structured references did not round-trip.");
            try { JsonSerializer.Deserialize<NullableReferences>("""{"country":"DE","state":null,"countries":[],"states":{}}"""); throw new Exception("A model was decoded as string."); } catch (JsonException) { }
            try { JsonSerializer.Deserialize<NullableReferences>("""{"country":null,"state":"unknown","countries":[],"states":{}}"""); throw new Exception("An invalid enum was accepted."); } catch (JsonException) { }
            Console.WriteLine("CSharp nullable references passed");
            """");
        var result = await RunGeneratedProject(directory);
        result.ExitCode.Should().Be(0, "generated nullable references must compile and round-trip. Sources: {0}\n{1}", directory, result.Output);
        result.Output.Should().Contain("CSharp nullable references passed");
    }

    /// <summary>Checks exact wire keys and string enums in compiled PascalCase models and clients.</summary>
    /// <param name="separateInterface">Whether the source contract documentation belongs to a separate interface.</param>
    /// <returns>The source generation, XML documentation compilation, and JSON runtime checks.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PascalCaseProfilePreservesWireContractAndOpenApiDocumentation(bool separateInterface)
    {
        var context = await GenerateDeliveryContract(NamingConvention.PascalCase, separateInterface, xmlContracts: true);
        var model = context.GeneratedFiles.Single(file => file.RelativePath == "Models/Delivery.cs").Content;
        var baseModel = context.GeneratedFiles.Single(file => file.RelativePath == "Models/DeliveryBase.cs").Content;
        var enumModel = context.GeneratedFiles.Single(file => file.RelativePath == "Models/DeliveryState.cs").Content;
        var client = context.GeneratedFiles.Single(file => file.RelativePath.StartsWith("Api/Deliveries", StringComparison.Ordinal)).Content;
        var docs = separateInterface
            ? context.GeneratedFiles.Single(file => file.RelativePath.StartsWith("Api/IDeliveries", StringComparison.Ordinal)).Content
            : client;

        model.Should().Contain("string RouteName").And.Contain("[JsonPropertyName(\"route-name\")]");
        baseModel.Should().Contain("string Id").And.Contain("[JsonPropertyName(\"Id\")]");
        model.Should().Contain("GetHashCodeValue").And.Contain("[JsonPropertyName(\"getHashCode\")]");
        baseModel.Should().Contain("EqualsValue").And.Contain("[JsonPropertyName(\"equals\")]");
        enumModel.Should().Contain("[JsonStringEnumMemberName(\"Ready\")]")
            .And.Contain("[JsonStringEnumMemberName(\"in-progress\")]")
            .And.Contain("/// The delivery is between pickup &amp; arrival.");
        context.IrDocument.Enums.Single(item => item.Name == "DeliveryState").Values.Select(member => member.Description)
            .Should().Equal("The delivery can start with <c>Ready</c>.", "The delivery is between pickup & arrival.", null);
        context.IrDocument.Models.Single(item => item.Name == "Delivery").Properties
            .Should().Contain(property => property.Name == "route_name");
        docs.Should().Contain("/// Creates a delivery.").And.Contain("/// <remarks>")
            .And.Contain("/// Keeps the submitted address &lt;code&gt; &amp; style.")
            .And.Contain("<see cref=\"Delivery\" />")
            .And.Contain("/// <param name=\"id\">The delivery identifier.</param>")
            .And.Contain("/// <param name=\"cancellationToken_\">").And.Contain("/// The upstream token.")
            .And.Contain("/// Its value may include &lt; &amp; &gt;.")
            .And.Contain("The desired route.</param>")
            .And.Contain("/// <param name=\"request\">The submitted delivery request.</param>")
            .And.Contain("/// <param name=\"cancellationToken\">A token to cancel the HTTP request.</param>")
            .And.Contain("/// The created delivery.").And.Contain("/// Includes the resolved route.")
            .And.Contain("<c>route-name</c>")
            .And.Contain("/// <returns>The delivery was deleted.</returns>")
            .And.NotContain("A delivery error.").And.NotContain("No delivery content.");
        if (separateInterface)
            client.Should().Contain("/// <inheritdoc />");

        var directory = Path.Combine(Path.GetTempPath(), "ValIO.CSharp.PascalWire", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "Smoke.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><GenerateDocumentationFile>true</GenerateDocumentationFile><NoWarn>1591</NoWarn></PropertyGroup></Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        foreach (var file in context.GeneratedFiles)
        {
            var destination = Path.Combine(directory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, file.Content);
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), """"
            using Generated.Contracts;
            using System.Runtime.Serialization;
            using System.Text.Json;
            using System.Xml.Linq;
            using System.Xml.Serialization;

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            const string requestJson = """{"Id":"42","route-name":"north","state":"in-progress","equals":"base","getHashCode":3}""";
            var request = JsonSerializer.Deserialize<Delivery>(requestJson, options)!;
            if (request.Id != "42" || request.RouteName != "north" || request.State != DeliveryState.InProgress ||
                request.EqualsValue != "base" || request.GetHashCodeValue != 3)
                throw new Exception("Input property or enum member names changed.");
            var wire = JsonSerializer.SerializeToElement(request, options);
            if (wire.GetProperty("Id").GetString() != "42" || wire.TryGetProperty("id", out _) ||
                wire.GetProperty("route-name").GetString() != "north" || wire.GetProperty("state").GetString() != "in-progress" ||
                wire.GetProperty("equals").GetString() != "base" || wire.GetProperty("getHashCode").GetInt32() != 3)
                throw new Exception("Output property names or enum wire value changed.");

            var changedBase = JsonSerializer.Deserialize<Delivery>(requestJson.Replace("base", "changed"), options)!;
            if (request.Equals(changedBase)) throw new Exception("Generated equality ignored a renamed base property.");
            var documentation = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Smoke.xml"));
            if (!documentation.Descendants("see").Any(element => (string?)element.Attribute("cref") == "T:Generated.Contracts.Delivery") ||
                !documentation.Descendants("c").Any(element => element.Value == "route-name") ||
                !documentation.Descendants("c").Any(element => element.Value == "42") ||
                !documentation.Descendants("c").Any(element => element.Value == "Ready"))
                throw new Exception("The compiled XML documentation lost inline markup.");

            foreach (var valid in new[] { "Ready", "in-progress", "0" })
            {
                var json = JsonSerializer.Serialize(valid);
                var value = JsonSerializer.Deserialize<DeliveryState>(json);
                if (JsonSerializer.Serialize(value) != json) throw new Exception("A declared enum value failed to round-trip.");
                var xmlSerializer = new XmlSerializer(typeof(DeliveryState));
                using var text = new StringWriter();
                xmlSerializer.Serialize(text, value);
                if (XElement.Parse(text.ToString()).Value != valid ||
                    !value.Equals(xmlSerializer.Deserialize(new StringReader(text.ToString()))))
                    throw new Exception("XmlSerializer lost the declared enum literal.");
                var dataContract = new DataContractSerializer(typeof(DeliveryState));
                using var bytes = new MemoryStream();
                dataContract.WriteObject(bytes, value);
                bytes.Position = 0;
                if (XElement.Load(bytes).Value != valid) throw new Exception("DataContractSerializer lost the enum literal.");
                bytes.Position = 0;
                if (!value.Equals(dataContract.ReadObject(bytes))) throw new Exception("DataContractSerializer enum read failed.");
                var dictionary = new Dictionary<DeliveryState, int> { [value] = 1 };
                var encoded = JsonSerializer.SerializeToElement(dictionary);
                if (encoded.GetProperty(valid).GetInt32() != 1 ||
                    JsonSerializer.Deserialize<Dictionary<DeliveryState, int>>(encoded)![value] != 1)
                    throw new Exception("An enum dictionary key failed to round-trip.");
            }
            foreach (var invalid in new[] { """ "ready" """, """ " Ready " """, """ "in_progress" """,
                """ "in-progress, Ready" """, """ "1" """, "0", "null" })
            {
                try { JsonSerializer.Deserialize<DeliveryState>(invalid); throw new Exception("Accepted " + invalid); }
                catch (JsonException) { }
            }
            try { JsonSerializer.Serialize((DeliveryState)123); throw new Exception("Wrote an undefined enum."); }
            catch (JsonException) { }
            Console.WriteLine("PascalCase wire contract passed");
            """");
        var result = await RunGeneratedProject(directory);
        result.ExitCode.Should().Be(0, "generated code and its XML docs must compile and JSON must round-trip: {0}\n{1}", directory, result.Output);
        result.Output.Should().Contain("PascalCase wire contract passed");
    }

    /// <summary>Preserves source naming and the existing enum converter for default C# profiles.</summary>
    /// <returns>The default generation and its assertions.</returns>
    [Test]
    public async Task DefaultProfilePreservesItsPropertyAndEnumNaming()
    {
        var context = await GenerateDeliveryContract(NamingConvention.CamelCase, separateInterface: false);
        var model = context.GeneratedFiles.Single(file => file.RelativePath == "Models/Delivery.cs").Content;
        var baseModel = context.GeneratedFiles.Single(file => file.RelativePath == "Models/DeliveryBase.cs").Content;
        var enumModel = context.GeneratedFiles.Single(file => file.RelativePath == "Models/DeliveryState.cs").Content;

        model.Should().Contain("string route_name").And.Contain("[JsonPropertyName(\"route-name\")]");
        baseModel.Should().Contain("string Id").And.NotContain("JsonPropertyName");
        enumModel.Should().Contain("[JsonConverter(typeof(JsonStringEnumConverter))]")
            .And.NotContain("[XmlEnum(").And.NotContain("[DataContract]");
        context.GeneratedFiles.Should().NotContain(file => file.RelativePath.Contains("ValioStrictStringEnumConverter"));
    }

    /// <summary>Runs the C# steps over a single OpenAPI contract with independently configured naming.</summary>
    /// <param name="namingConvention">The CLR model property convention configured in the profile.</param>
    /// <param name="separateInterface">Whether client interfaces are generated in separate files.</param>
    /// <param name="xmlContracts">Whether both XML serializers must preserve the declared string enum values.</param>
    /// <returns>The generated source together with its original IR.</returns>
    private static async Task<GenerationContext> GenerateDeliveryContract(
        NamingConvention namingConvention, bool separateInterface, bool xmlContracts = false)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(OpenApiFixture.Read("CSharp/pascal-wire-docs-3.0.json")));
        var parsed = await OpenApiDocument.LoadAsync(input, "json");
        var document = parsed.Document!;
        var context = new GenerationContext
        {
            IrDocument = new IrBuilder(new SchemaResolver(document), new NamingConverter()).BuildFromOpenApi(document),
            Configuration = new GenerationConfiguration
            {
                Namespace = "Generated.Contracts",
                NamingConvention = new NamingConventions { Properties = namingConvention }
            },
            PluginOptions = new CSharpGenerationOptions
            {
                StrictStringEnums = namingConvention == NamingConvention.PascalCase,
                PascalCaseEnumMembers = namingConvention == NamingConvention.PascalCase,
                GenerateXmlEnumAttributes = xmlContracts,
                GenerateDataContractAttributes = xmlContracts,
                GenerateInterfaces = separateInterface,
                InterfaceFileStrategy = InterfaceFileStrategy.SeparateFile,
                UseRequired = true
            }
        };
        var naming = new CSharpNamingConverter();
        var renderer = new CSharpAstRenderer();
        await new CSharpModelsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpEnumsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpClientStep(naming, renderer).ExecuteAsync(context);
        return context;
    }

    /// <summary>Exercises the compiled client against the same wire contracts with each tracing and metrics combination.</summary>
    /// <remarks>
    /// The multipart scenario checks repeated fields, metadata, and deferred reads. Success, HTTP failure, cancellation,
    /// and invalid part metadata preserve caller ownership of the upload streams.
    /// </remarks>
    /// <param name="traces">Whether emitted spans are collected while the same requests run.</param>
    /// <param name="metrics">Whether request outcomes and durations are collected while the same requests run.</param>
    /// <returns>The isolated C# compilation and the request, response, and telemetry assertions.</returns>
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task GeneratedProject_CompilesAndPreservesWireContracts(bool traces, bool metrics)
    {
        var context = new GenerationContext
        {
            IrDocument = CreateDocument(),
            Configuration = new GenerationConfiguration { Namespace = "Generated.Contracts" },
            PluginOptions = new CSharpGenerationOptions
            {
                GenerateInterfaces = traces || metrics,
                InterfaceFileStrategy = InterfaceFileStrategy.SeparateFile,
                GenerateTraces = traces,
                GenerateMetrics = metrics,
                GenerateValidationAttributes = true,
                GenerateValidationConstants = true,
                UseSealed = true
            }
        };
        var naming = new CSharpNamingConverter();
        var renderer = new CSharpAstRenderer();
        await new CSharpModelsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpEnumsStep(naming, renderer).ExecuteAsync(context);
        await new CSharpClientStep(naming, renderer).ExecuteAsync(context);

        var directory = Path.Combine(Path.GetTempPath(), "ValIO.CSharp.Generated", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Smoke.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"),
                "<configuration><packageSources><clear /></packageSources></configuration>");
            foreach (var file in context.GeneratedFiles)
            {
                var destination = Path.Combine(directory, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllTextAsync(destination, file.Content);
            }
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CSharpHttpPipeline", "BearerReplayHandler.cs"),
                Path.Combine(directory, "BearerReplayHandler.cs"));
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "HttpPipelineChecks.cs.txt"),
                Path.Combine(directory, "HttpPipelineChecks.cs"));
            var metricsSetup = metrics ? """
                long successfulRequests = 0;
                long failedRequests = 0;
                var recordedDurations = 0;
                using var metricsListener = new System.Diagnostics.Metrics.MeterListener();
                metricsListener.InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == "test") listener.EnableMeasurementEvents(instrument);
                };
                metricsListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
                {
                    if (instrument.Name == "requests.success") successfulRequests += measurement;
                    if (instrument.Name == "requests.error") failedRequests += measurement;
                });
                metricsListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => recordedDurations++);
                metricsListener.Start();
                """ + Environment.NewLine : string.Empty;
            var telemetryChecks = traces || metrics
                ? "using var source = new System.Diagnostics.ActivitySource(\"test\");\n" +
                    metricsSetup +
                    (metrics ? "using var meter = new System.Diagnostics.Metrics.Meter(\"test\");\n" : string.Empty) +
                    "var decorated = new TransportApiTelemetryDecorator(client, source" +
                    (metrics ? ", new HttpClientMetrics(meter)" : string.Empty) + ");\n" + """
                    if (await decorated.Send("x", null, null, true, null, null, null, null, null, null, null, null, null) != 7)
                        throw new InvalidOperationException("Decorated typed response failed.");
                    await decorated.Ping();
                    handler.ReturnError = true;
                    try
                    {
                        await decorated.Ping();
                        throw new InvalidOperationException("Decorated HTTP error was not surfaced.");
                    }
                    catch (ApiException)
                    {
                    }
                    handler.ReturnError = false;
                    """ + Environment.NewLine + (metrics ? """
                    if (successfulRequests != 2 || failedRequests != 1 || recordedDurations != 3)
                        throw new InvalidOperationException("Telemetry did not record typed, bodyless, and failed calls exactly once.");
                    """ : string.Empty)
                : string.Empty;
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), RuntimeProgram.Replace("// TELEMETRY_CHECKS", telemetryChecks, StringComparison.Ordinal));

            var result = await RunGeneratedProject(directory);

            result.ExitCode.Should().Be(0, "generated C# must compile and pass runtime checks:{0}{1}", Environment.NewLine, result.Output);
            result.Output.Should().Contain("CSharp generated runtime passed");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Executes the isolated generated program and captures compiler or runtime diagnostics.</summary>
    /// <param name="directory">The temporary project directory owned by this test.</param>
    /// <returns>The process exit code and combined diagnostic output.</returns>
    private static async Task<(int ExitCode, string Output)> RunGeneratedProject(string directory)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add("Smoke.csproj");
        start.ArgumentList.Add("--verbosity");
        start.ArgumentList.Add("quiet");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw new TimeoutException("Generated C# compilation exceeded two minutes.");
        }
        return (process.ExitCode, await output + await error);
    }

    /// <summary>Creates a cross-feature document with multiple bases, enum defaults, file uploads, and colliding names.</summary>
    /// <returns>The normalized IR used by every generated source step.</returns>
    private static IrDocument CreateDocument()
    {
        var text = Primitive(IrPrimitiveKind.String);
        var nullableText = new IrNullableType { InnerType = text };
        var state = new IrModelReference { ModelName = "State" };
        var models = new IrModel[]
        {
            new() { Name = "Named", Properties = [Property("name", "Name", nullableText)] },
            new() { Name = "Tagged", Properties = [Property("tag", "Tag", nullableText)] },
            new()
            {
                Name = "Combined",
                Composition = new IrComposition
                {
                    Kind = IrCompositionKind.AllOf,
                    Types = [new IrModelReference { ModelName = "Named" }, new IrModelReference { ModelName = "Tagged" }]
                },
                Properties =
                [
                    new IrProperty(Property("message", "Message", text)) { Constraints = new IrPropertyConstraints { DefaultValue = "default!" } },
                    new IrProperty(Property("state", "State", state)) { IsRequired = false, Constraints = new IrPropertyConstraints { DefaultValue = "in_progress" } },
                    Property("date", "Date", Primitive(IrPrimitiveKind.Date)),
                    Property("time", "Time", Primitive(IrPrimitiveKind.Time)),
                    Property("when", "When", Primitive(IrPrimitiveKind.DateTime)),
                    new IrProperty(Property("quantity", "Quantity", Primitive(IrPrimitiveKind.Integer)))
                    {
                        Constraints = new IrPropertyConstraints { Min = 1.5m, Max = 5.5m }
                    },
                    new IrProperty(Property("longValue", "LongValue", Primitive(IrPrimitiveKind.Long)))
                    {
                        Constraints = new IrPropertyConstraints { Min = 9007199254740993m, Max = long.MaxValue }
                    },
                    new IrProperty(Property("exclusive", "Exclusive", Primitive(IrPrimitiveKind.Number)))
                    {
                        Constraints = new IrPropertyConstraints { ExclusiveMin = 1m, ExclusiveMax = 2m }
                    },
                    new IrProperty(Property("negative", "Negative", Primitive(IrPrimitiveKind.Integer)))
                    {
                        Constraints = new IrPropertyConstraints { Max = 5m }
                    },
                    Property("codes", "Codes", new IrNullableType
                    {
                        InnerType = new IrArrayType { ElementType = new IrNullableType { InnerType = Primitive(IrPrimitiveKind.Integer) } }
                    })
                ]
            },
            new()
            {
                Name = "UploadForm",
                Properties =
                [
                    Property("content", "Content", Primitive(IrPrimitiveKind.File)),
                    Property("related-files", "RelatedFiles", new IrArrayType { ElementType = Primitive(IrPrimitiveKind.File) }),
                    Property("state", "State", state),
                    Property("amount", "Amount", Primitive(IrPrimitiveKind.Number)),
                    Property("enabled", "Enabled", Primitive(IrPrimitiveKind.Boolean))
                ]
            }
        };
        var operation = new IrOperation
        {
            MethodName = "Send",
            HttpMethod = "POST",
            Path = "/files/{path-id}",
            PathParameters = [new IrParameter { Name = "path_id2", OriginalName = "path-id", Type = text, IsRequired = true }],
            QueryParameters =
            [
                new() { Name = "ratio", Type = new IrNullableType { InnerType = Primitive(IrPrimitiveKind.Number) }, IsRequired = false },
                new() { Name = "state", Type = state, IsRequired = false },
                new() { Name = "enabled", Type = Primitive(IrPrimitiveKind.Boolean), IsRequired = true },
                new() { Name = "tags", Type = new IrNullableType { InnerType = new IrArrayType { ElementType = nullableText } }, IsRequired = false },
                new() { Name = "url", Type = text, IsRequired = false },
                new() { Name = "url_", Type = text, IsRequired = false },
                new() { Name = "responseContent", Type = text, IsRequired = false },
                new() { Name = "queryParts", Type = text, IsRequired = false },
                new() { Name = "item", Type = text, IsRequired = false },
                new() { Name = "cancellationToken", Type = text, IsRequired = false }
            ],
            HeaderParameters = [new IrParameter { Name = "X_Trace", OriginalName = "X.Trace", Type = text, IsRequired = false }],
            RequestBody = new IrRequestBody { Type = new IrModelReference { ModelName = "UploadForm" }, IsRequired = false, ContentType = "multipart/form-data" },
            Responses = [new IrResponse { StatusCode = "200", Type = Primitive(IrPrimitiveKind.Integer) }]
        };
        var money = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Number, Format = "decimal" };
        const string moneyMediaType = "application/vnd.winzone.money+json";
        return new IrDocument
        {
            Metadata = new IrDocumentMetadata(),
            Models = models,
            Enums =
            [
                new IrEnum { Name = "State", BaseType = IrEnumBaseType.String, Values = [new IrEnumValue { Name = "InProgress", Value = "in_progress" }] },
                new IrEnum { Name = "Priority", BaseType = IrEnumBaseType.Integer, Values = [new IrEnumValue { Name = "High", Value = 2 }] },
                new IrEnum { Name = "LargeCode", BaseType = IrEnumBaseType.Integer, Values = [new IrEnumValue { Name = "Large", Value = 9007199254740993L }] }
            ],
            Clients = [new IrClient { Name = "TransportApi", Operations = [operation, new IrOperation
            {
                MethodName = "Ping", HttpMethod = "GET", Path = "/ping",
                PathParameters = [], QueryParameters = [], HeaderParameters = [],
                Responses = [new IrResponse { StatusCode = "204" }]
            }, new IrOperation
            {
                MethodName = "SendJson", HttpMethod = "POST", Path = "/json",
                PathParameters = [], QueryParameters = [], HeaderParameters = [],
                RequestBody = new IrRequestBody
                {
                    Type = new IrModelReference { ModelName = "Combined" }, IsRequired = true, ContentType = "application/json"
                },
                Responses = [new IrResponse { StatusCode = "200", Type = Primitive(IrPrimitiveKind.Integer) }]
            }, new IrOperation
            {
                MethodName = "Accept", HttpMethod = "POST", Path = "/accept",
                PathParameters = [], QueryParameters = [], HeaderParameters = [],
                Responses =
                [
                    new IrResponse { StatusCode = "202", Type = Primitive(IrPrimitiveKind.Integer) },
                    new IrResponse { StatusCode = "204" }
                ]
            }, new IrOperation
            {
                MethodName = "SendExactMoney", HttpMethod = "POST", Path = "/money",
                PathParameters = [],
                QueryParameters =
                [
                    new IrParameter
                    {
                        Name = "names", OriginalName = "filter.names", Type = new IrArrayType { ElementType = text },
                        IsRequired = true, Style = IrParameterStyle.Form, Explode = false
                    }
                ],
                HeaderParameters =
                [
                    new IrParameter
                    {
                        Name = "kinds", OriginalName = "X-Kinds", Type = new IrArrayType { ElementType = text },
                        IsRequired = true, Style = IrParameterStyle.Simple
                    }
                ],
                RequestBody = new IrRequestBody { Type = money, IsRequired = true, ContentType = moneyMediaType },
                Responses =
                [
                    new IrResponse
                    {
                        StatusCode = "200", Type = money,
                        MediaTypes =
                        [
                            new() { Name = "text/plain", Type = money },
                            new() { Name = moneyMediaType, Type = money }
                        ]
                    }
                ]
            }] }],
            TypeDependencies = new IrTypeDependencyGraph
            {
                Dependencies = new Dictionary<string, IReadOnlyList<string>>(),
                TopologicalOrder = models.Select(model => model.Name).ToArray()
            }
        };
    }

    /// <summary>Creates one primitive type used by the cross-feature fixture.</summary>
    /// <param name="kind">The normalized primitive kind.</param>
    /// <returns>The primitive IR value.</returns>
    private static IrPrimitiveType Primitive(IrPrimitiveKind kind) => new() { PrimitiveKind = kind };

    /// <summary>Creates a model property while preserving the original wire name.</summary>
    /// <param name="wire">The schema property name.</param>
    /// <param name="name">The C# source identifier.</param>
    /// <param name="type">The normalized property type.</param>
    /// <returns>The property metadata.</returns>
    private static IrProperty Property(string wire, string name, IrType type) => new()
    {
        OriginalName = wire, Name = name, Type = type, IsRequired = true
    };

    /// <summary>Exercises generated source under a culture with comma decimal separators.</summary>
    private const string RuntimeProgram = """"
        using System.Globalization;
        using System.ComponentModel.DataAnnotations;
        using System.Net;
        using System.Text.Json;
        using Generated.Contracts;

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var model = JsonSerializer.Deserialize<Combined>("""
        {"name":null,"tag":null,"codes":[1,null],"date":"2026-10-11","time":"12:34:56","when":"2026-10-11T12:34:56Z"}
        """
        )!;
        if (model.Message != "default!" || model.State != State.InProgress || model.Codes![1] != null)
            throw new InvalidOperationException("Model default, enum, or nullable-array roundtrip failed.");
        var clone = JsonSerializer.Deserialize<Combined>(JsonSerializer.Serialize(model))!;
        if (clone.Date != model.Date || clone.Time != model.Time || clone.When != model.When)
            throw new InvalidOperationException("Date/time JSON roundtrip failed.");
        if (!new Combined().Equals(new Combined()))
            throw new InvalidOperationException("Nullable property equality failed.");
        if (new Combined().Equals((object)new object()))
            throw new InvalidOperationException("Unrelated objects must compare as unequal.");
        if (HttpWireValueFormatter.Format(Priority.High) != "2")
            throw new InvalidOperationException("Numeric enum wire value failed.");
        if (HttpWireValueFormatter.Format(LargeCode.Large) != "9007199254740993" ||
            JsonSerializer.Deserialize<LargeCode>("9007199254740993") != LargeCode.Large)
            throw new InvalidOperationException("Large numeric enum JSON and wire value failed.");
        if (Validator.TryValidateProperty(1, new ValidationContext(model) { MemberName = "Quantity" }, null) ||
            !Validator.TryValidateProperty(2, new ValidationContext(model) { MemberName = "Quantity" }, null) ||
            Validator.TryValidateProperty(9007199254740992L, new ValidationContext(model) { MemberName = "LongValue" }, null) ||
            !Validator.TryValidateProperty(9007199254740993L, new ValidationContext(model) { MemberName = "LongValue" }, null) ||
            Validator.TryValidateProperty(1d, new ValidationContext(model) { MemberName = "Exclusive" }, null) ||
            !Validator.TryValidateProperty(1.5d, new ValidationContext(model) { MemberName = "Exclusive" }, null) ||
            !Validator.TryValidateProperty(-1, new ValidationContext(model) { MemberName = "Negative" }, null))
            throw new InvalidOperationException("Integer, long, exclusive, or one-sided range validation failed.");
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid") };
        var client = new TransportApi(http);
        var count = await client.Send("seg/one", 1.5, State.InProgress, true, ["x y", null],
            "url", "url2", "response", "parts", "item", "cancel", "trace", null);
        if (count != 7 || handler.HadContent || handler.Header != "trace")
            throw new InvalidOperationException("Numeric response, optional body, or header transmission failed.");
        var uri = handler.Uri!.OriginalString;
        if (!uri.Contains("/files/seg%2Fone") || !uri.Contains("ratio=1.5") ||
            !uri.Contains("state=in_progress") || !uri.Contains("enabled=true") || !uri.Contains("tags=x%20y"))
            throw new InvalidOperationException("Invariant and escaped URL serialization failed: " + uri);
        using var primary = new UploadProbeStream([1, 2, 3]);
        using var firstRelated = new UploadProbeStream([4, 5]);
        using var secondRelated = new UploadProbeStream([6, 7]);
        var form = new UploadForm
        {
            Content = new MultipartFile(primary, "primary.jpg", "image/jpeg"),
            RelatedFiles = [new(firstRelated, "first.png", "image/png"), new(secondRelated, "second.bin")],
            State = State.InProgress, Amount = 1.5, Enabled = true
        };
        handler.BeforeBodyRead = () =>
        {
            if (primary.CopyCount + firstRelated.CopyCount + secondRelated.CopyCount != 0)
                throw new InvalidOperationException("Multipart files were read before HTTP transmission.");
        };
        await client.Send("x", null, null, true, null, null, null, null, null, null, null, null, form);
        handler.BeforeBodyRead = null;
        if (!handler.Body!.Contains("in_progress") || !handler.Body.Contains("1.5") || !handler.Body.Contains("true"))
            throw new InvalidOperationException("Multipart invariant and enum wire values failed.");
        if (!handler.FileParts.SequenceEqual(new[]
            {
                ("content", "primary.jpg", "image/jpeg"),
                ("related-files", "first.png", "image/png"),
                ("related-files", "second.bin", "application/octet-stream")
            }) || primary.CopyCount != 1 || firstRelated.CopyCount != 1 || secondRelated.CopyCount != 1 ||
            !primary.CanRead || !firstRelated.CanRead || !secondRelated.CanRead)
            throw new InvalidOperationException("Multipart order, metadata, streaming, or stream ownership failed.");

        using var rejectedFile = new UploadProbeStream([1]);
        var rejectedForm = new UploadForm
        {
            Content = new MultipartFile(rejectedFile, "rejected.bin"), RelatedFiles = [],
            State = State.InProgress, Amount = 1.5, Enabled = true
        };
        handler.ReturnError = true;
        try
        {
            await client.Send("x", null, null, true, null, null, null, null, null, null, null, null, rejectedForm);
            throw new InvalidOperationException("Multipart gateway failure was accepted.");
        }
        catch (ApiException) when (rejectedFile.CopyCount == 1 && rejectedFile.CanRead)
        {
        }
        handler.ReturnError = false;

        using var cancelUpload = new CancellationTokenSource();
        using var cancelledFile = new UploadProbeStream([1], cancelUpload.Cancel);
        var cancelledForm = new UploadForm
        {
            Content = new MultipartFile(cancelledFile, "cancelled.bin"), RelatedFiles = [],
            State = State.InProgress, Amount = 1.5, Enabled = true
        };
        try
        {
            await client.Send("x", null, null, true, null, null, null, null, null, null, null, null, cancelledForm, cancelUpload.Token);
            throw new InvalidOperationException("Cancellation did not interrupt multipart transmission.");
        }
        catch (OperationCanceledException) when (cancelledFile.CopyCount == 1 && cancelledFile.CanRead)
        {
        }

        using var validMetadataFile = new UploadProbeStream([1]);
        using var invalidMetadataFile = new UploadProbeStream([2]);
        var invalidForm = new UploadForm
        {
            Content = new MultipartFile(validMetadataFile, "valid.bin"),
            RelatedFiles = [new MultipartFile(invalidMetadataFile, "invalid.bin", "invalid media type")],
            State = State.InProgress, Amount = 1.5, Enabled = true
        };
        try
        {
            await client.Send("x", null, null, true, null, null, null, null, null, null, null, null, invalidForm);
            throw new InvalidOperationException("Invalid multipart media type was accepted.");
        }
        catch (FormatException) when (validMetadataFile.CopyCount == 0 && invalidMetadataFile.CopyCount == 0 &&
            validMetadataFile.CanRead && invalidMetadataFile.CanRead)
        {
        }

        handler.ReturnError = true;
        try
        {
            await client.Send("x", null, null, true, null, null, null, null, null, null, null, null, null);
            throw new InvalidOperationException("The HTTP error was not surfaced.");
        }
        catch (ApiException exception) when (exception.Content == "<html>gateway failure</html>")
        {
        }
        handler.ReturnError = false;
        await client.Ping();
        handler.AcceptedResponseContent = "7";
        if (await client.Accept() != 7)
            throw new InvalidOperationException("The typed 202 receipt was not decoded.");
        handler.AcceptedResponseContent = "";
        try
        {
            await client.Accept();
            throw new InvalidOperationException("An empty typed 202 was treated as a receipt.");
        }
        catch (ApiException error) when (error.StatusCode == HttpStatusCode.Accepted && error.Content == "")
        {
        }
        handler.AcceptedResponseContent = "{";
        try
        {
            await client.Accept();
            throw new InvalidOperationException("A malformed typed 202 was treated as a receipt.");
        }
        catch (JsonException)
        {
        }
        handler.AcceptedResponseContent = null;
        await client.Accept();
        var money = await client.SendExactMoney(["a,b", "x y"], ["one", "two"], 999999999999999999.9999999999m);
        if (money != 999999999999999999.9999999999m || handler.Body != "999999999999999999.9999999999" ||
            handler.Uri?.Query != "?filter.names=a%2Cb,x%20y" || handler.ArrayHeader != "one,two" ||
            handler.RequestMediaType != "application/vnd.winzone.money+json" ||
            handler.AcceptMediaType != "application/vnd.winzone.money+json")
            throw new InvalidOperationException("Exact decimal, query/header list, or media negotiation failed.");
        // TELEMETRY_CHECKS
        await HttpPipelineChecks.Run();
        Console.WriteLine("CSharp generated runtime passed");

        /// <summary>Tracks HTTP serialization of a non-seekable caller-owned file.</summary>
        internal sealed class UploadProbeStream : MemoryStream
        {
            /// <summary>Optional assertion invoked before each asynchronous copy.</summary>
            private readonly Action? _beginCopy;

            /// <summary>Creates a caller-owned stream that observes multipart serialization.</summary>
            /// <param name="bytes">The complete contents for this probe.</param>
            /// <param name="beginCopy">An optional action when serialization attempts a read.</param>
            public UploadProbeStream(byte[] bytes, Action? beginCopy = null) : base(bytes)
            {
                _beginCopy = beginCopy;
            }

            /// <summary>Gets the number of copy attempts by the HTTP transport.</summary>
            /// <value>The number of times the probe received an asynchronous copy request.</value>
            internal int CopyCount { get; private set; }

            /// <inheritdoc/>
            public override bool CanSeek => false;

            /// <inheritdoc/>
            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                CopyCount++;
                _beginCopy?.Invoke();
                return base.CopyToAsync(destination, bufferSize, cancellationToken);
            }
        }

        /// <summary>Captures generated HTTP requests without making a network connection.</summary>
        internal sealed class CaptureHandler : HttpMessageHandler
        {
            /// <summary>Gets the last generated request URI.</summary>
            internal Uri? Uri { get; private set; }
            /// <summary>Gets the last custom header value.</summary>
            internal string? Header { get; private set; }
            /// <summary>Gets the last array header serialized by the client.</summary>
            internal string? ArrayHeader { get; private set; }
            /// <summary>Gets the last outgoing body media type.</summary>
            internal string? RequestMediaType { get; private set; }
            /// <summary>Gets the last advertised response media type.</summary>
            internal string? AcceptMediaType { get; private set; }
            /// <summary>Gets whether the last request contained a body.</summary>
            internal bool HadContent { get; private set; }
            /// <summary>Gets the last serialized request body.</summary>
            internal string? Body { get; private set; }
            /// <summary>Gets the last transmitted filenames, part names, and media types.</summary>
            /// <value>File metadata in the order chosen by the generated request.</value>
            internal IReadOnlyList<(string Name, string FileName, string MediaType)> FileParts { get; private set; } = [];
            /// <summary>Gets or sets an assertion before the handler serializes a body.</summary>
            /// <value>An optional action that observes whether the caller's streams were read early.</value>
            internal Action? BeforeBodyRead { get; set; }
            /// <summary>Gets or sets whether the next response simulates a non-JSON gateway error.</summary>
            internal bool ReturnError { get; set; }
            /// <summary>Gets or sets the 202 receipt content. Null selects the declared bodyless 204.</summary>
            internal string? AcceptedResponseContent { get; set; }
            /// <inheritdoc/>
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Uri = request.RequestUri;
                Header = request.Headers.TryGetValues("X.Trace", out var values) ? values.Single() : null;
                ArrayHeader = request.Headers.TryGetValues("X-Kinds", out var kinds) ? kinds.Single() : null;
                RequestMediaType = request.Content?.Headers.ContentType?.MediaType;
                AcceptMediaType = request.Headers.Accept.SingleOrDefault()?.MediaType;
                HadContent = request.Content != null;
                BeforeBodyRead?.Invoke();
                FileParts = request.Content is MultipartFormDataContent formData
                    ? formData.Where(part => part.Headers.ContentDisposition?.FileNameStar != null).Select(part =>
                    {
                        var disposition = part.Headers.ContentDisposition!;
                        return (disposition.Name!.Trim('"'), disposition.FileNameStar!, part.Headers.ContentType!.MediaType!);
                    }).ToArray()
                    : [];
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                if (Uri?.AbsolutePath == "/money")
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("999999999999999999.9999999999", System.Text.Encoding.UTF8,
                            "application/vnd.winzone.money+json")
                    };
                if (Uri?.AbsolutePath == "/accept")
                    return AcceptedResponseContent is { } content
                        ? new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent(content) }
                        : new HttpResponseMessage(HttpStatusCode.NoContent);
                return new HttpResponseMessage(ReturnError ? HttpStatusCode.BadGateway : HttpStatusCode.OK)
                {
                    Content = new StringContent(ReturnError ? "<html>gateway failure</html>" : "7")
                };
            }
        }
        """";
}
