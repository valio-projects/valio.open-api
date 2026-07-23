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
/// Regression tests for query-string building in <see cref="CSharpAstBuilder"/>:
/// - optional (nullable) query parameters must not be appended to the URL unconditionally,
///   otherwise a null value throws (ArgumentNullException/NullReferenceException) or literally
///   serializes as "null" in the request;
/// - array query parameters must expand into repeated key=value pairs (form/explode=true style),
///   not <c>.ToString()</c> the collection into a single garbage value.
/// </summary>
[TestFixture]
public class CSharpAstBuilderQueryParametersTests
{
    private CSharpAstBuilder _builder = null!;
    private CSharpAstRenderer _renderer = null!;

    [SetUp]
    public void Setup()
    {
        _builder = new CSharpAstBuilder(new CSharpNamingConverter());
        _renderer = new CSharpAstRenderer();
    }

    private static IrParameter Param(string name, IrType type, bool isRequired) =>
        new() { Name = name, Type = type, IsRequired = isRequired };

    private static IrOperation Operation(params IrParameter[] queryParams) => new()
    {
        MethodName = "GetItems",
        HttpMethod = "GET",
        Path = "/items",
        PathParameters = [],
        QueryParameters = queryParams,
        HeaderParameters = [],
        Responses = [new IrResponse { StatusCode = "200" }],
    };

    private static IrPrimitiveType StringType => new() { PrimitiveKind = IrPrimitiveKind.String };
    private static IrPrimitiveType UuidType => new() { PrimitiveKind = IrPrimitiveKind.Uuid };

    [Test]
    public void OptionalScalarQueryParam_IsGuardedByNullCheck()
    {
        var op = Operation(Param("search", StringType, isRequired: false));
        var code = _renderer.Render(_builder.BuildMethod(op));

        code.Should().Contain("if (search != null)");
        code.Should().Contain("queryParts.Add(\"search=\" + Uri.EscapeDataString(search))");
    }

    [Test]
    public void RequiredScalarQueryParam_IsAddedUnconditionally()
    {
        var op = Operation(Param("id", UuidType, isRequired: true));
        var code = _renderer.Render(_builder.BuildMethod(op));

        code.Should().NotContain("if (id != null)");
        code.Should().Contain("queryParts.Add(\"id=\" + id.ToString())");
    }

    [Test]
    public void ArrayQueryParam_ExpandsToRepeatedKeyValuePairs()
    {
        var arrayType = new IrArrayType { ElementType = UuidType };
        var op = Operation(Param("projectIds", arrayType, isRequired: false));
        var code = _renderer.Render(_builder.BuildMethod(op));

        code.Should().Contain("if (projectIds != null)");
        code.Should().Contain("foreach (var item in projectIds)");
        code.Should().Contain("queryParts.Add(\"projectIds=\" + item.ToString())");
    }

    [Test]
    public void ArrayOfStrings_EscapesEachItem()
    {
        var arrayType = new IrArrayType { ElementType = StringType };
        var op = Operation(Param("tags", arrayType, isRequired: true));
        var code = _renderer.Render(_builder.BuildMethod(op));

        code.Should().Contain("foreach (var item in tags)");
        code.Should().Contain("queryParts.Add(\"tags=\" + Uri.EscapeDataString(item))");
        // Required array — no outer null guard.
        code.Should().NotContain("if (tags != null)");
    }

    [Test]
    public void QueryParts_OnlyAppendedToUrlWhenNonEmpty()
    {
        var op = Operation(Param("search", StringType, isRequired: false));
        var code = _renderer.Render(_builder.BuildMethod(op));

        code.Should().Contain("List<string> queryParts = new List<string>();");
        code.Should().Contain("if (queryParts.Count > 0)");
        code.Should().Contain("urlBuilder.Append(string.Join(\"&\", queryParts))");
    }

    [Test]
    public void NoQueryParameters_DoesNotEmitQueryPartsScaffolding()
    {
        var op = Operation();
        var code = _renderer.Render(_builder.BuildMethod(op));

        code.Should().NotContain("queryParts");
    }
}
