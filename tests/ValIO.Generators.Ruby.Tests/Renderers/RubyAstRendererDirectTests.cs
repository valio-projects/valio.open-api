using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Ruby.Ast.Nodes;
using ValIO.Generators.Ruby.Ast.Renderers;

namespace ValIO.Generators.Ruby.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="RubyAstRenderer"/> — the Visit*
/// surface (file/class/method/attribute and the type-rendering helpers
/// used in YARD documentation comments).
/// </summary>
[TestFixture]
public class RubyAstRendererDirectTests
{
    private RubyAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new RubyAstRenderer();

    private static SimpleTypeNode Simple(string n) => new(new IdentifierNode(n));

    [Test]
    public void VisitSimpleType_RendersPlainName()
    {
        _renderer.VisitSimpleType(Simple("Integer")).Should().Be("Integer");
    }

    [Test]
    public void VisitGenericType_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("Array"),
            ImmutableList.Create<TypeNode>(Simple("String")));
        _renderer.VisitGenericType(t).Should().Be("Array<String>");
    }

    [Test]
    public void VisitArrayType_RendersAsArrayGeneric()
    {
        _renderer.VisitArrayType(new ArrayTypeNode(Simple("Pet"))).Should().Be("Array<Pet>");
    }

    [Test]
    public void VisitDictionaryType_RendersAsHashGeneric()
    {
        var t = new DictionaryTypeNode(Simple("String"), Simple("Pet"));
        _renderer.VisitDictionaryType(t).Should().Be("Hash<String, Pet>");
    }

    [Test]
    public void VisitNullableType_PassesThroughInner()
    {
        // Ruby treats nil specially — types are documented without a suffix.
        _renderer.VisitNullableType(new NullableTypeNode(Simple("Pet"))).Should().Be("Pet");
    }

    [Test]
    public void VisitRubyAttribute_RendersAttrAccessor()
    {
        var attr = new RubyAttributeNode(new IdentifierNode("name"), Simple("String"));
        var output = _renderer.VisitRubyAttribute(attr);
        output.Should().Contain("attr_accessor :name");
        output.Should().Contain("@return [String]");
    }

    [Test]
    public void VisitRubyMethod_RendersDefWithBodyAndEnd()
    {
        var method = new RubyMethodNode(
            new IdentifierNode("greet"),
            parameters: ["name"],
            body: ["puts \"Hello, #{name}\""]);
        var output = _renderer.VisitRubyMethod(method);
        output.Should().Contain("def greet(name)");
        output.Should().Contain("puts \"Hello, #{name}\"");
        output.Should().EndWith("end" + System.Environment.NewLine);
    }

    [Test]
    public void VisitRubyClass_RendersClassWithBodyAndEnd()
    {
        var classNode = new RubyClassNode(
            new IdentifierNode("Greeter"),
            parentClass: null,
            members: [
                new RubyAttributeNode(new IdentifierNode("name"), Simple("String")),
            ]);
        var output = _renderer.VisitRubyClass(classNode);
        output.Should().StartWith("class Greeter");
        output.Should().Contain("attr_accessor :name");
        output.Should().EndWith("end" + System.Environment.NewLine);
    }

    [Test]
    public void VisitRubyFile_AlwaysStartsWithFrozenStringLiteralPragma()
    {
        var file = new RubyFileNode(
            classes: [new RubyClassNode(new IdentifierNode("Empty"), null, members: [])]);
        var output = _renderer.Render(file);
        output.Should().StartWith("# frozen_string_literal: true");
        output.Should().Contain("class Empty");
        output.Should().Contain("end");
    }
}
