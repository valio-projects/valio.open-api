using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Python.Ast.Renderers;

namespace ValIO.Generators.Python.Tests.Renderers;

[TestFixture]
public class PythonAstRendererTests
{
    private PythonAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new PythonAstRenderer();

    [Test]
    public void SimpleType_RendersBareName()
    {
        var n = new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true);
        _renderer.VisitSimpleType(n).Should().Be("str");
    }

    [Test]
    public void GenericType_RendersPep585Style()
    {
        var n = new GenericTypeNode(new IdentifierNode("list"),
            ImmutableList.Create<TypeNode>(new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true)));
        _renderer.VisitGenericType(n).Should().Be("list[str]");
    }

    [Test]
    public void UnionType_RendersPipeSeparated()
    {
        var n = new UnionTypeNode(ImmutableList.Create<TypeNode>(
            new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true),
            new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true)));
        _renderer.VisitUnionType(n).Should().Contain("|");
    }

    [Test]
    public void DictionaryType_RendersDictWithBothTypeArgs()
    {
        var n = new DictionaryTypeNode(
            new SimpleTypeNode(new IdentifierNode("str"), isPrimitive: true),
            new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true));
        var rendered = _renderer.VisitDictionaryType(n);
        rendered.Should().Contain("str").And.Contain("int");
    }

    [Test]
    public void Identifier_RendersBareName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("user_id")).Should().Be("user_id");
    }
}
