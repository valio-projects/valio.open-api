using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Builders;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Services;
using ValIO.Parsers.Services;
using ValIO.Tests.Fixtures;

namespace ValIO.Application.Tests;

/// <summary>Exercises the production parser and IR builder against each supported OpenAPI dialect and serialization.</summary>
[TestFixture]
public sealed class OpenApiParserMatrixTests
{
    /// <summary>Lists every published Swagger 2.0 and OpenAPI 3.x patch through OpenAPI 3.2.1.</summary>
    /// <remarks>Patch versions share dialect fixtures; only the root version marker changes before parsing.</remarks>
    /// <value>The versions published in the official OpenAPI specification index.</value>
    private static IEnumerable<string> PublishedVersions =>
        ["2.0", "3.0.0", "3.0.1", "3.0.2", "3.0.3", "3.0.4", "3.1.0", "3.1.1", "3.1.2", "3.2.0", "3.2.1"];

    /// <summary>Enumerates dialect and serialization pairs, including native YAML rather than JSON disguised as YAML.</summary>
    /// <value>Every published version in both supported serializations.</value>
    private static IEnumerable<TestCaseData> VersionsAndFormats
    {
        get
        {
            foreach (var version in PublishedVersions)
            foreach (var format in new[] { "json", "yaml" })
                yield return new TestCaseData(version, format);
        }
    }

    /// <summary>Restricts composition cases to keywords actually defined by each specification dialect.</summary>
    /// <value>AllOf for all dialects and OneOf/AnyOf for OpenAPI 3.x.</value>
    private static IEnumerable<TestCaseData> CompositionCases
    {
        get
        {
            foreach (var version in PublishedVersions)
            foreach (var format in new[] { "json", "yaml" })
            foreach (var composition in version == "2.0" ? new[] { "allOf" } : new[] { "allOf", "oneOf", "anyOf" })
                yield return new TestCaseData(version, format, composition);
        }
    }

    /// <summary>Enumerates heterogeneous nested compositions supported by OpenAPI 3.x.</summary>
    /// <value>Each nesting order in every supported 3.x dialect and source format.</value>
    private static IEnumerable<TestCaseData> NestedCompositionCases
    {
        get
        {
            foreach (var version in PublishedVersions.Where(v => v != "2.0"))
            foreach (var format in new[] { "json", "yaml" })
            foreach (var composition in new[] { "anyOf-in-allOf", "allOf-in-oneOf" })
                yield return new TestCaseData(version, format, composition);
        }
    }

    /// <summary>Loads actual temporary files and preserves operation, model, enum, and validation metadata.</summary>
    /// <param name="version">The specification version.</param>
    /// <param name="format">The source serialization format.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCaseSource(nameof(VersionsAndFormats))]
    public async Task FileParser_PreservesContractAcrossVersionsAndFormats(string version, string format)
    {
        var document = await ParseVersionedFileAsync(version, "contract", format);
        var ir = BuildIr(document);
        ir.Metadata.Title.Should().Be("Parser matrix");
        ir.Metadata.ServerUrl.Should().Be("https://api.example.test/v1");
        var model = ir.Models.Single(m => m.Name == "Base");
        model.Properties.Single(p => p.OriginalName == "id").Type.Should().BeOfType<IrPrimitiveType>()
            .Which.PrimitiveKind.Should().Be(IrPrimitiveKind.String);
        var label = model.Properties.Single(p => p.OriginalName == "label");
        label.Constraints!.MinLength.Should().Be(2);
        label.Constraints.MaxLength.Should().Be(24);
        label.Description.Should().Be("A constrained label.");
        ir.Enums.Single(e => e.Name == "State").Values.Should().HaveCount(2);
        var operation = ir.Clients.SelectMany(c => c.Operations).Single();
        operation.Path.Should().Be("/items");
        operation.HttpMethod.Should().Be("POST");
        operation.Tags.Should().BeEquivalentTo("Items");
        operation.RequestBody!.IsRequired.Should().BeTrue();
        operation.RequestBody.ContentType.Should().Be("application/json");
        operation.Responses.Single(r => r.StatusCode == "200").Description.Should().Be("Accepted item.");
    }

    /// <summary>Preserves model composition and request/response types through the real parser and IR conversion.</summary>
    /// <param name="version">The specification version.</param>
    /// <param name="format">The source serialization format.</param>
    /// <param name="composition">A composition keyword supported by the selected dialect.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCaseSource(nameof(CompositionCases))]
    public async Task Composition_PreservesModelAndEndpointSemantics(string version, string format, string composition)
    {
        var document = await ParseVersionedFileAsync(version, composition, format);
        var ir = BuildIr(document);
        var model = ir.Models.Single(m => m.Name == "Choice");
        var expectedKind = composition switch
        {
            "allOf" => IrCompositionKind.AllOf,
            "oneOf" => IrCompositionKind.OneOf,
            _ => IrCompositionKind.AnyOf
        };
        model.Composition!.Kind.Should().Be(expectedKind);
        model.Composition.Types.OfType<IrModelReference>().Select(r => r.ModelName)
            .Should().Contain("Base");
        if (composition == "allOf")
        {
            model.Properties.Select(p => p.OriginalName).Should().Contain("extra").And.NotContain("id");
        }
        else
        {
            model.Composition.Types.OfType<IrModelReference>().Select(r => r.ModelName)
                .Should().BeEquivalentTo("Base", "Alternative");
        }
        if (composition == "oneOf")
        {
            model.Composition.DiscriminatorPropertyName.Should().Be("kind");
            model.Composition.DiscriminatorMapping.Should().Contain("base", "Base");
            model.Composition.DiscriminatorMapping.Should().Contain("alternative", "Alternative");
        }

        var operation = ir.Clients.SelectMany(c => c.Operations).Single();
        AssertEndpointComposition(operation.RequestBody!.Type, composition);
        AssertEndpointComposition(operation.Responses.Single(r => r.StatusCode == "200").Type!, composition);
        ir.Models.Select(m => m.Name).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Normalizes nullable request and response schemas without confusing nullability with requiredness.</summary>
    /// <param name="version">The specification version.</param>
    /// <param name="format">The source serialization format.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCaseSource(nameof(VersionsAndFormats))]
    public async Task NullableBodies_PreserveRequirednessAndScalarType(string version, string format)
    {
        var document = await ParseVersionedFileAsync(version, "nullable", format);
        var operation = BuildIr(document).Clients.SelectMany(c => c.Operations).Single();
        operation.RequestBody!.IsRequired.Should().BeTrue();
        foreach (var type in new[] { operation.RequestBody.Type, operation.Responses.Single(r => r.StatusCode == "200").Type })
            type.Should().BeOfType<IrNullableType>().Which.InnerType.Should().BeOfType<IrPrimitiveType>()
                .Which.PrimitiveKind.Should().Be(IrPrimitiveKind.String);
    }

    /// <summary>Separates omission from nullability for properties, parameters, and request bodies in every published dialect.</summary>
    /// <remarks>Swagger 2.0 nullability cases exercise the x-nullable extension; Swagger itself has no null schema type.</remarks>
    /// <param name="version">The published specification version marker.</param>
    /// <param name="format">The actual JSON or native YAML serialization.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCaseSource(nameof(VersionsAndFormats))]
    public async Task Presence_PreservesFlagsWithoutOptionalTypeWrappers(string version, string format)
    {
        var ir = BuildIr(await ParseVersionedFileAsync(version, "presence", format));
        var properties = ir.Models.Single(m => m.Name == "Presence").Properties;
        foreach (var property in properties)
        {
            property.IsRequired.Should().Be(property.OriginalName is "requiredValue" or "requiredNullable");
            property.Type.Should().NotBeOfType<IrOptionalType>();
        }
        foreach (var name in new[] { "requiredValue", "optionalValue", "defaultValue" })
            AssertStringType(properties.Single(p => p.OriginalName == name).Type, false);
        foreach (var name in new[] { "requiredNullable", "optionalNullable" })
            AssertStringType(properties.Single(p => p.OriginalName == name).Type, true);
        var defaultProperty = properties.Single(p => p.OriginalName == "defaultValue");
        defaultProperty.Constraints!.DefaultValue!.ToString().Should().Be("fallback");
        AssertStringType(properties.Single(p => p.OriginalName == "nullableArray").Type
            .Should().BeOfType<IrArrayType>().Which.ElementType, true);
        AssertStringType(properties.Single(p => p.OriginalName == "nullableMap").Type
            .Should().BeOfType<IrDictionaryType>().Which.ValueType, true);
        AssertStringType(properties.Single(p => p.OriginalName == "nullableCollection").Type
            .Should().BeOfType<IrNullableType>().Which.InnerType
            .Should().BeOfType<IrArrayType>().Which.ElementType, false);

        foreach (var operation in ir.Clients.SelectMany(c => c.Operations))
        {
            var path = operation.PathParameters.Single();
            path.IsRequired.Should().BeTrue();
            AssertStringType(path.Type, false);
            foreach (var parameter in operation.QueryParameters.Concat(operation.HeaderParameters))
            {
                parameter.IsRequired.Should().Be(parameter.OriginalName.StartsWith("required", StringComparison.Ordinal));
                AssertStringType(parameter.Type, parameter.OriginalName.Contains("Nullable", StringComparison.Ordinal));
            }
            operation.QueryParameters.Single(p => p.OriginalName == "optionalQuery")
                .DefaultValue!.ToString().Should().Be("fallback");
            operation.RequestBody!.IsRequired.Should().Be(operation.HttpMethod == "POST");
            AssertStringType(operation.RequestBody.Type, true);
        }
        ir.Clients.SelectMany(c => c.Operations).Should().HaveCount(3);
    }

    /// <summary>Rejects an unpublished future dialect rather than silently interpreting it as the latest supported specification.</summary>
    /// <param name="format">The actual JSON or native YAML serialization.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task UnpublishedVersion_IsRejected(string format)
    {
        Func<Task> parse = async () => await ParseVersionedFileAsync("3.3.0", "contract", format);
        await parse.Should().ThrowAsync<Exception>();
    }

    /// <summary>Retains mixed composition boundaries in model properties, declarations, and endpoint schemas.</summary>
    /// <param name="version">The specification version.</param>
    /// <param name="format">The source serialization format.</param>
    /// <param name="composition">The order of the nested composition keywords.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCaseSource(nameof(NestedCompositionCases))]
    public async Task NestedCompositions_PreserveModelPropertyAndEndpointBoundaries(string version, string format, string composition)
    {
        var ir = BuildIr(await ParseVersionedFileAsync(version, composition, format));
        var operation = ir.Clients.SelectMany(c => c.Operations).Single();
        var property = ir.Models.Single(m => m.Name == "Container").Properties.Single(p => p.OriginalName == "choice");
        foreach (var type in new[] { property.Type, operation.RequestBody!.Type, operation.Responses.Single(r => r.StatusCode == "200").Type! })
            AssertNestedComposition(type, composition);

        var declaration = ir.Models.Single(m => m.Name == "Choice");
        if (composition == "anyOf-in-allOf")
        {
            declaration.Composition!.Kind.Should().Be(IrCompositionKind.AllOf);
            declaration.Composition.Types.OfType<IrUnionType>().Should().Contain(t => t.Kind == IrUnionKind.AnyOf);
        }
        else
        {
            declaration.Composition!.Kind.Should().Be(IrCompositionKind.OneOf);
            declaration.Composition.Types.OfType<IrIntersectionType>().Should().NotBeEmpty();
        }
    }

    /// <summary>Rejects diagnostic errors even when the reader returns a partially populated document.</summary>
    /// <param name="format">The source serialization format.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task InvalidStructure_DoesNotReturnPartialDocument(string format)
    {
        Func<Task> parse = async () => await ParseFileAsync($"invalid-info.{format}");
        await parse.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Failed to parse OpenAPI document*");
    }

    /// <summary>Rejects unresolved local references before a generator can silently weaken their types.</summary>
    /// <param name="format">The source serialization format.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task MissingReference_IsReportedAsError(string format)
    {
        Func<Task> parse = async () => await ParseFileAsync($"missing-reference.{format}");
        await parse.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DoesNotExist*");
    }

    /// <summary>Allows operation tag names without requiring optional root-level metadata declarations.</summary>
    /// <param name="format">The source serialization format.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task UndeclaredOperationTags_DoNotBecomeUnresolvedReferences(string format)
    {
        var document = await ParseFileAsync($"undeclared-operation-tags.{format}");
        document.Tags.Should().BeNullOrEmpty();
        var operation = BuildIr(document).Clients.SelectMany(c => c.Operations).Single();
        operation.Tags.Should().BeEquivalentTo("Orders");
        operation.Responses.Single(r => r.StatusCode == "200").Type.Should()
            .BeOfType<IrModelReference>().Which.ModelName.Should().Be("Order");
    }

    /// <summary>Rejects malformed syntax in both readers.</summary>
    /// <param name="format">The requested reader.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task MalformedInput_IsRejected(string format)
    {
        var source = OpenApiFixture.Read($"Parser/malformed.{format}");
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        Func<Task> parse = async () => await CreateParser().ParseFromStreamAsync(stream, format);
        await parse.Should().ThrowAsync<Exception>();
    }

    /// <summary>Does not treat an AsyncAPI description as an OpenAPI HTTP contract.</summary>
    /// <param name="format">The serialization format.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task OtherProtocolDocument_IsRejected(string format)
    {
        Func<Task> parse = async () => await ParseFileAsync($"asyncapi.{format}");
        await parse.Should().ThrowAsync<Exception>();
    }

    /// <summary>Sniffs a forward-only stream and preserves caller ownership of that stream.</summary>
    /// <param name="format">The actual serialization format, omitted from the parser call.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestCase("json")]
    [TestCase("yaml")]
    public async Task AutoDetection_SupportsForwardOnlyStreams(string format)
    {
        await using var stream = new ForwardOnlyStream(Encoding.UTF8.GetBytes(OpenApiFixture.Read($"Parser/3.2.0-contract.{format}")));
        var document = await CreateParser().ParseFromStreamAsync(stream, null);
        document.Info.Title.Should().Be("Parser matrix");
        stream.CanRead.Should().BeTrue();
    }

    /// <summary>Sniffs unread content at the caller's current position instead of resetting to unrelated prefix bytes.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    public async Task AutoDetection_PreservesStreamOffsetAndOwnership()
    {
        var source = "prefix" + OpenApiFixture.Read("Parser/3.2.0-contract.json");
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        stream.Position = Encoding.UTF8.GetByteCount("prefix");
        var document = await CreateParser().ParseFromStreamAsync(stream, null);
        document.Info.Title.Should().Be("Parser matrix");
        stream.CanRead.Should().BeTrue();
    }

    /// <summary>Rejects serializers for other protocols explicitly.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    public async Task UnsupportedFormat_HasClearFailure()
    {
        await using var stream = new MemoryStream();
        Func<Task> parse = async () => await CreateParser().ParseFromStreamAsync(stream, "protobuf");
        await parse.Should().ThrowAsync<NotSupportedException>().WithMessage("*protobuf*");
    }

    /// <summary>Checks the normalized endpoint type without imposing generator-specific representation.</summary>
    /// <param name="type">The request or response IR type.</param>
    /// <param name="composition">The source composition keyword.</param>
    private static void AssertEndpointComposition(IrType type, string composition)
    {
        if (composition == "allOf")
            type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Base");
        else
        {
            var union = type.Should().BeOfType<IrUnionType>().Subject;
            union.Kind.Should().Be(composition == "oneOf" ? IrUnionKind.OneOf : IrUnionKind.AnyOf);
            union.Types.OfType<IrModelReference>().Select(r => r.ModelName)
                .Should().BeEquivalentTo("Base", "Alternative");
        }
    }

    /// <summary>Checks that normalization does not distribute or erase heterogeneous schema combinations.</summary>
    /// <param name="type">The normalized property, request, or response type.</param>
    /// <param name="composition">The expected nesting order.</param>
    private static void AssertNestedComposition(IrType type, string composition)
    {
        if (composition == "anyOf-in-allOf")
        {
            var intersection = type.Should().BeOfType<IrIntersectionType>().Subject;
            intersection.Types.OfType<IrModelReference>().Select(r => r.ModelName).Should().ContainSingle().Which.Should().Be("Extra");
            var union = intersection.Types.OfType<IrUnionType>().Single();
            union.Kind.Should().Be(IrUnionKind.AnyOf);
            union.Types.OfType<IrModelReference>().Select(r => r.ModelName).Should().BeEquivalentTo("Base", "Alternative");
        }
        else
        {
            var union = type.Should().BeOfType<IrUnionType>().Subject;
            union.Kind.Should().Be(IrUnionKind.OneOf);
            union.Types.OfType<IrModelReference>().Select(r => r.ModelName).Should().ContainSingle().Which.Should().Be("Alternative");
            union.Types.OfType<IrIntersectionType>().Single().Types.OfType<IrModelReference>()
                .Select(r => r.ModelName).Should().BeEquivalentTo("Base", "Extra");
        }
    }

    /// <summary>Builds a language-independent contract from the production parser result.</summary>
    /// <param name="document">The parsed OpenAPI description.</param>
    /// <returns>The complete normalized IR.</returns>
    private static IrDocument BuildIr(OpenApiDocument document) =>
        new IrBuilder(new SchemaResolver(document), new NamingConverter()).BuildFromOpenApi(document);

    /// <summary>Checks value nullability while ensuring omission has not become a type wrapper.</summary>
    /// <param name="type">The normalized scalar value type.</param>
    /// <param name="nullable">Whether the source schema accepts JSON null.</param>
    private static void AssertStringType(IrType type, bool nullable)
    {
        if (nullable)
            type = type.Should().BeOfType<IrNullableType>().Which.InnerType;
        type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.String);
    }

    /// <summary>Creates a production parser with no remote calls for local-source tests.</summary>
    /// <returns>The parser under test.</returns>
    private static OpenApiDocumentParser CreateParser() => new(new UnusedHttpClientFactory(), NullLogger<OpenApiDocumentParser>.Instance);

    /// <summary>Reuses a representative dialect fixture while exercising the production reader's actual patch-version dispatch.</summary>
    /// <param name="version">The exact version marker passed to the reader.</param>
    /// <param name="scenario">The fixture scenario within the selected dialect.</param>
    /// <param name="format">The actual source serialization.</param>
    /// <returns>The parsed document.</returns>
    private static async Task<OpenApiDocument> ParseVersionedFileAsync(string version, string scenario, string format)
    {
        var dialectVersion = version switch
        {
            "2.0" => "2.0",
            var v when v.StartsWith("3.0.", StringComparison.Ordinal) => "3.0.4",
            var v when v.StartsWith("3.1.", StringComparison.Ordinal) || scenario == "presence" => "3.1.2",
            _ => "3.2.0"
        };
        var source = OpenApiFixture.Read($"Parser/{dialectVersion}-{scenario}.{format}");
        // Replace only the root version declaration, leaving all schema and operation syntax unchanged.
        source = Regex.Replace(source, """^(\s*"(?:openapi|swagger)"\s*:\s*")[^"]+(")""",
            match => match.Groups[1].Value + version + match.Groups[2].Value, RegexOptions.Multiline);
        return await ParseSourceFileAsync(source, format);
    }

    /// <summary>Copies an explicit source fixture to a temporary file and exercises the public file entry point.</summary>
    /// <param name="fixtureName">The JSON or YAML fixture name inside the Parser fixture directory.</param>
    /// <returns>The parsed document.</returns>
    private static async Task<OpenApiDocument> ParseFileAsync(string fixtureName)
        => await ParseSourceFileAsync(OpenApiFixture.Read($"Parser/{fixtureName}"), Path.GetExtension(fixtureName));

    /// <summary>Exercises the public file parser with an actual source serialization and cleans up its temporary input.</summary>
    /// <param name="source">The complete source document.</param>
    /// <param name="format">The filename extension used to select the production reader.</param>
    /// <returns>The parsed document.</returns>
    private static async Task<OpenApiDocument> ParseSourceFileAsync(string source, string format)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"valio-parser-{Guid.NewGuid():N}.{format.TrimStart('.')}");
        try
        {
            await File.WriteAllTextAsync(filePath, source);
            return await CreateParser().ParseFromFileAsync(filePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>Fails immediately if a local fixture unexpectedly attempts a remote request.</summary>
    private sealed class UnusedHttpClientFactory : IHttpClientFactory
    {
        /// <inheritdoc/>
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("Local parser fixtures must not use HTTP.");
    }

    /// <summary>Models the forward-only response streams returned by HTTP content.</summary>
    private sealed class ForwardOnlyStream : MemoryStream
    {
        /// <summary>Initializes a readable source whose position cannot be queried or changed.</summary>
        /// <param name="content">The source bytes.</param>
        public ForwardOnlyStream(byte[] content) : base(content) { }

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
