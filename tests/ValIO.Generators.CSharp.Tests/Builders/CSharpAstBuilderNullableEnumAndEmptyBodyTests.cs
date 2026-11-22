using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Services;

namespace ValIO.Generators.CSharp.Tests.Builders;

/// <summary>
/// Regression tests for two runtime-crash classes on real PIN responses:
/// - an optional (<c>required</c>-less) property whose type is a <c>$ref</c> to an enum must be
///   nullable (<c>T?</c>): the server may send <c>null</c> for it, and OpenAPI 3.0 cannot put
///   <c>nullable</c> next to a <c>$ref</c>, so without this the generated value-type field throws
///   <c>JsonException</c> ("The JSON value could not be converted to …");
/// - an empty response body must not be fed to <c>JsonSerializer.Deserialize</c> (which throws
///   "The input does not contain any JSON tokens"): empty 2xx returns null, empty non-2xx throws
///   <c>ApiException</c> with the raw body.
/// </summary>
[TestFixture]
public class CSharpAstBuilderNullableEnumAndEmptyBodyTests
{
    private CSharpAstBuilder _builder = null!;
    private CSharpAstRenderer _renderer = null!;

    /// <summary>Creates a renderer and a builder with the explicit fragment document context.</summary>
    [SetUp]
    public void Setup()
    {
        _builder = new CSharpAstBuilder(new CSharpNamingConverter(), CSharpDocumentFixture.Context(
            [new IrModel { Name = "PropertyObjectPaybackModel", Properties = [] }],
            [new IrEnum
            {
                Name = "PropertyObjectAvailabilityStatus", BaseType = IrEnumBaseType.String,
                Values = [new IrEnumValue { Name = "Available", Value = "available" }]
            }]));
        _renderer = new CSharpAstRenderer();
    }

    private static IrProperty EnumProperty(bool isRequired) => new()
    {
        OriginalName = "propertyObjectStatus",
        Name = "propertyObjectStatus",
        Type = new IrModelReference { ModelName = "PropertyObjectAvailabilityStatus" },
        IsRequired = isRequired,
    };

    [Test]
    public void OptionalEnumRefProperty_IsNullable()
    {
        var code = _renderer.Render(_builder.BuildProperty(EnumProperty(isRequired: false)));

        code.Should().Contain("public PropertyObjectAvailabilityStatus? propertyObjectStatus { get; set; }");
        // Value-type nullable — не нужен = default!; и не должно остаться non-nullable объявление.
        code.Should().NotContain("public PropertyObjectAvailabilityStatus propertyObjectStatus");
    }

    [Test]
    public void RequiredEnumRefProperty_StaysNonNullable()
    {
        var code = _renderer.Render(_builder.BuildProperty(EnumProperty(isRequired: true)));

        code.Should().Contain("public PropertyObjectAvailabilityStatus propertyObjectStatus");
        code.Should().NotContain("PropertyObjectAvailabilityStatus? propertyObjectStatus");
    }

    [Test]
    public void OptionalModelRefProperty_StaysNonNullable_NotAnEnum()
    {
        // $ref на МОДЕЛЬ (reference-type) — не enum: null присваивается штатно, поэтому обёртку в
        // nullable не навязываем (это изменило бы контракт множества клиентов без необходимости).
        var property = new IrProperty
        {
            OriginalName = "payback",
            Name = "payback",
            Type = new IrModelReference { ModelName = "PropertyObjectPaybackModel" },
            IsRequired = false,
        };

        var code = _renderer.Render(_builder.BuildProperty(property));

        code.Should().Contain("public PropertyObjectPaybackModel payback { get; set; } = default!;");
        code.Should().NotContain("PropertyObjectPaybackModel? payback");
    }

    private static IrOperation OperationWith(params IrResponse[] responses) => new()
    {
        MethodName = "GetItem",
        HttpMethod = "GET",
        Path = "/item",
        PathParameters = [],
        QueryParameters = [],
        HeaderParameters = [],
        Responses = responses,
    };

    [Test]
    public void SuccessBranch_GuardsEmptyBody_ReturnsNullInsteadOfDeserializing()
    {
        var op = OperationWith(new IrResponse
        {
            StatusCode = "200",
            Type = new IrModelReference { ModelName = "GetItemResponse" },
        });

        var code = _renderer.Render(_builder.BuildMethod(op));

        // Пустое тело на успехе — не десериализуем, а возвращаем null.
        code.Should().Contain("if (string.IsNullOrWhiteSpace(responseContent))");
        code.Should().Contain("return default!;");
    }

    /// <summary>Emits an API exception for an empty acceptance receipt while allowing the documented bodyless 204.</summary>
    /// <param name="status">The typed contract is declared directly as 202 or applies to 202 through the 2XX range.</param>
    [TestCase("202")]
    [TestCase("2XX")]
    public void TypedAcceptedResponse_RejectsEmptyReceipt(string status)
    {
        var operation = OperationWith(
            new IrResponse { StatusCode = status, Type = new IrModelReference { ModelName = "GetItemResponse" } },
            new IrResponse { StatusCode = "204" });

        var code = _renderer.Render(_builder.BuildMethod(operation));

        code.Should().Contain("if (string.IsNullOrWhiteSpace(responseContent))")
            .And.Contain("statusCodeInt == 202")
            .And.Contain("throw new ApiException(requestUri, httpMethod, statusCode, reasonPhrase, version, responseContent, \"Accepted response body is required\", null)")
            .And.Contain("return default!;");
    }

    /// <summary>Returns the default value for a declared bodyless 202 even when the 2XX range has a typed body.</summary>
    [Test]
    public void BodylessAcceptedResponse_OverridesTypedWildcard()
    {
        var operation = OperationWith(
            new IrResponse { StatusCode = "2XX", Type = new IrModelReference { ModelName = "GetItemResponse" } },
            new IrResponse { StatusCode = "202" });

        var code = _renderer.Render(_builder.BuildMethod(operation));

        code.Should().NotContain("Accepted response body is required").And.Contain("return default!;");
    }

    [Test]
    public void ErrorBranch_GuardsEmptyBody_ThrowsApiExceptionInsteadOfDeserializing()
    {
        // Есть success (200) и объявленная ошибка (400): без guard пустое тело не-2xx улетело бы в
        // JsonSerializer.Deserialize<ErrorType>("") → JsonException. С guard — ApiException.
        var op = OperationWith(
            new IrResponse { StatusCode = "200", Type = new IrModelReference { ModelName = "GetItemResponse" } },
            new IrResponse { StatusCode = "400", Type = new IrModelReference { ModelName = "RestApiErrorResponse" } });

        var code = _renderer.Render(_builder.BuildMethod(op));

        // guard встречается в обеих ветках (успех + ошибка).
        System.Text.RegularExpressions.Regex
            .Matches(code, @"if \(string\.IsNullOrWhiteSpace\(responseContent\)\)")
            .Count.Should().BeGreaterThanOrEqualTo(2);
        code.Should().Contain("throw new ApiException");
    }

    /// <summary>
    /// Verifies that multipart operations delegate to the multipart content factory instead of emitting JSON.
    /// </summary>
    [Test]
    public void MultipartBody_UsesMultipartFactoryInsteadOfJsonSerialization()
    {
        var operation = new IrOperation(OperationWith(new IrResponse { StatusCode = "204" }))
        {
            HttpMethod = "POST",
            RequestBody = new IrRequestBody
            {
                Type = new IrModelReference { ModelName = "ReplaceLogoForm" },
                IsRequired = true,
                ContentType = "multipart/form-data",
            },
        };

        var code = _renderer.Render(_builder.BuildMethod(operation));

        code.Should().Contain("MultipartFormDataContentFactory.Create(request)");
        code.Should().NotContain("JsonSerializer.Serialize(request)");
        code.Should().NotContain("new StringContent(requestJsonContent");
    }
}
