using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.FSharp.Ast.Nodes;
using ValIO.Generators.FSharp.Ast.Renderers;

namespace ValIO.Generators.FSharp.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="FSharpAstRenderer"/>.
/// </summary>
[TestFixture]
public class FSharpAstRendererDirectTests
{
    private FSharpAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new FSharpAstRenderer();

    private static SimpleTypeNode Simple(string n) => new(new IdentifierNode(n));

    [Test]
    public void SimpleType_RendersBareName()
    {
        _renderer.VisitSimpleType(Simple("int")).Should().Be("int");
    }

    [Test]
    public void NullableType_AppendsOption()
    {
        var t = new NullableTypeNode(Simple("int"));
        _renderer.VisitNullableType(t).Should().Be("int option");
    }

    [Test]
    public void OptionalType_AppendsOption()
    {
        var t = new OptionalTypeNode(Simple("int"));
        _renderer.VisitOptionalType(t).Should().Be("int option");
    }

    [Test]
    public void GenericListType_RendersPostFixListIdiom()
    {
        var t = new GenericTypeNode(new IdentifierNode("list"),
            ImmutableList.Create<TypeNode>(Simple("string")));
        _renderer.VisitGenericType(t).Should().Be("string list");
    }

    [Test]
    public void GenericMapType_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("Map"),
            ImmutableList.Create<TypeNode>(Simple("string"), Simple("int")));
        _renderer.VisitGenericType(t).Should().Be("Map<string, int>");
    }

    [Test]
    public void ArrayType_RendersPostFixArrayIdiom()
    {
        var t = new ArrayTypeNode(Simple("byte"));
        _renderer.VisitArrayType(t).Should().Be("byte array");
    }

    [Test]
    public void DictionaryType_RendersAsMap()
    {
        var t = new DictionaryTypeNode(Simple("string"), Simple("int"));
        _renderer.VisitDictionaryType(t).Should().Be("Map<string, int>");
    }

    [Test]
    public void VisitFSharpRecord_EmitsTypeDeclarationWithFourSpaceIndent()
    {
        var record = new FSharpRecordNode(
            new IdentifierNode("User"),
            new[]
            {
                new FSharpRecordNode.Field("Id", Simple("int")),
                new FSharpRecordNode.Field("Name", Simple("string")),
            });
        var rendered = _renderer.VisitFSharpRecord(record);

        rendered.Should().Contain("type User = {");
        rendered.Should().Contain("    Id: int");
        rendered.Should().Contain("    Name: string");
        rendered.Should().Contain("}");
    }

    [Test]
    public void VisitFSharpDU_EmitsCasesWithPipe()
    {
        var du = new FSharpDuNode(
            new IdentifierNode("Color"),
            new[]
            {
                new FSharpDuNode.Case("Red", Payload: null),
                new FSharpDuNode.Case("Wrapped", Simple("int")),
            });
        var rendered = _renderer.VisitFSharpDU(du);

        rendered.Should().Contain("type Color =");
        rendered.Should().Contain("    | Red");
        rendered.Should().Contain("    | Wrapped of int");
    }

    [Test]
    public void VisitFSharpFunction_EmitsLetBindingWithBody()
    {
        var fn = new FSharpFunctionNode(
            new IdentifierNode("getUser"),
            parameters: new[]
            {
                new FSharpFunctionNode.Parameter("client", Simple("RestClient")),
                new FSharpFunctionNode.Parameter("id", Simple("int")),
            },
            returnType: Simple("IRestResponse"),
            bodyLines: new[]
            {
                "let req = RestRequest(\"/users/{id}\", Method.Get)",
                "client.Execute(req)",
            });

        var rendered = _renderer.VisitFSharpFunction(fn);
        rendered.Should().Contain("let getUser (client: RestClient) (id: int) : IRestResponse =");
        rendered.Should().Contain("    let req = RestRequest(\"/users/{id}\", Method.Get)");
        rendered.Should().Contain("    client.Execute(req)");
    }

    [Test]
    public void VisitFSharpFile_EmitsModuleAndOpens()
    {
        var record = new FSharpRecordNode(
            new IdentifierNode("Empty"),
            fields: Array.Empty<FSharpRecordNode.Field>());
        var file = new FSharpFileNode(
            moduleName: "Generated.Models.Empty",
            declarations: new DeclarationNode[] { record },
            opens: new[] { "System", "RestSharp" });

        var rendered = _renderer.Render(file);
        rendered.Should().StartWith("module Generated.Models.Empty");
        rendered.Should().Contain("open RestSharp");
        rendered.Should().Contain("open System");
        rendered.Should().Contain("type Empty = { }");
    }
}
