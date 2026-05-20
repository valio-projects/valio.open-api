using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Rust.Ast.Nodes;
using ValIO.Generators.Rust.Ast.Renderers;

namespace ValIO.Generators.Rust.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="RustAstRenderer"/> — the type-rendering and
/// declaration surface that downstream builders depend on.
/// </summary>
[TestFixture]
public class RustAstRendererDirectTests
{
    private RustAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new RustAstRenderer();

    private static SimpleTypeNode Simple(string n) =>
        new(new IdentifierNode(n));

    [Test]
    public void SimpleType_RendersBareName()
    {
        _renderer.VisitSimpleType(Simple("i32")).Should().Be("i32");
    }

    [Test]
    public void GenericType_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(
            new IdentifierNode("Vec"),
            ImmutableList.Create<TypeNode>(Simple("u8")));
        _renderer.VisitGenericType(t).Should().Be("Vec<u8>");
    }

    [Test]
    public void NullableType_WrapsInOption()
    {
        var t = new NullableTypeNode(Simple("i32"));
        _renderer.VisitNullableType(t).Should().Be("Option<i32>");
    }

    [Test]
    public void OptionalType_WrapsInOption()
    {
        var t = new OptionalTypeNode(Simple("String"));
        _renderer.VisitOptionalType(t).Should().Be("Option<String>");
    }

    [Test]
    public void ArrayType_RendersAsVec()
    {
        var t = new ArrayTypeNode(Simple("User"));
        _renderer.VisitArrayType(t).Should().Be("Vec<User>");
    }

    [Test]
    public void DictionaryType_RendersAsHashMap()
    {
        var t = new DictionaryTypeNode(Simple("String"), Simple("i32"));
        _renderer.VisitDictionaryType(t).Should().Be("std::collections::HashMap<String, i32>");
    }

    [Test]
    public void UnionType_DegradesToSerdeJsonValue()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitUnionType(t).Should().Be("serde_json::Value");
    }

    [Test]
    public void Struct_RendersWithDerivesAndFields()
    {
        var s = new RustStructNode(
            new IdentifierNode("User"),
            fields: new[]
            {
                new RustFieldNode(new IdentifierNode("id"), Simple("i32")),
                new RustFieldNode(new IdentifierNode("name"), Simple("String")),
            });

        var output = _renderer.VisitRustStruct(s);
        output.Should().Contain("#[derive(Debug, Clone, Serialize, Deserialize)]");
        output.Should().Contain("pub struct User {");
        output.Should().Contain("pub id: i32,");
        output.Should().Contain("pub name: String,");
        output.Should().Contain("}");
    }

    [Test]
    public void Field_WithSerdeRename_EmitsAttribute()
    {
        var f = new RustFieldNode(
            new IdentifierNode("user_id"),
            Simple("i32"),
            serdeRename: "userId");
        var output = _renderer.VisitRustField(f);
        output.Should().Contain("#[serde(rename = \"userId\")]");
        output.Should().Contain("pub user_id: i32,");
    }

    [Test]
    public void Enum_RendersWithSerdeRenameAllAndVariants()
    {
        var e = new RustEnumNode(
            new IdentifierNode("Role"),
            variants: new[]
            {
                new RustEnumVariantNode(new IdentifierNode("Admin")),
                new RustEnumVariantNode(new IdentifierNode("User")),
            });

        var output = _renderer.VisitRustEnum(e);
        output.Should().Contain("#[derive(Debug, Clone, Serialize, Deserialize)]");
        output.Should().Contain("#[serde(rename_all = \"camelCase\")]");
        output.Should().Contain("pub enum Role {");
        output.Should().Contain("Admin,");
        output.Should().Contain("User,");
    }

    [Test]
    public void Function_RendersAsyncFnWithResult()
    {
        var unit = new SimpleTypeNode(new IdentifierNode("()"), isPrimitive: true);
        var ret = new GenericTypeNode(
            new IdentifierNode("Result"),
            ImmutableList.Create<TypeNode>(unit, Simple("reqwest::Error")));

        var fn = new RustFunctionNode(
            new IdentifierNode("ping"),
            parameters: new[]
            {
                new RustParameter("client", new SimpleTypeNode(new IdentifierNode("&Client"))),
                new RustParameter("base_url", new SimpleTypeNode(new IdentifierNode("&str"))),
            },
            returnType: ret,
            body: new[] { "Ok(())" });

        var output = _renderer.VisitRustFunction(fn);
        output.Should().Contain("pub async fn ping(client: &Client, base_url: &str) -> Result<(), reqwest::Error>");
        output.Should().Contain("Ok(())");
    }

    [Test]
    public void File_EmitsUsesSortedAndDeduped()
    {
        var s = new RustStructNode(new IdentifierNode("Empty"), fields: Array.Empty<RustFieldNode>());
        var file = new RustFileNode(
            declarations: new[] { (DeclarationNode)s },
            uses: new[] { "serde::{Deserialize, Serialize}", "chrono", "chrono" });

        var output = _renderer.Render(file);
        output.Should().Contain("use chrono;");
        output.Should().Contain("use serde::{Deserialize, Serialize};");
        // chrono should appear before serde lexicographically.
        var chronoIdx = output.IndexOf("use chrono;", StringComparison.Ordinal);
        var serdeIdx = output.IndexOf("use serde", StringComparison.Ordinal);
        chronoIdx.Should().BeLessThan(serdeIdx);
    }
}
