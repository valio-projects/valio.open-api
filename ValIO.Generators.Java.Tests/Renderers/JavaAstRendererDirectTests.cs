using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Java.Ast.Renderers;

namespace ValIO.Generators.Java.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="JavaAstRenderer"/> type-rendering.
/// Targets each Visit* type method on the smallest possible inputs.
/// </summary>
[TestFixture]
public class JavaAstRendererDirectTests
{
    private JavaAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new JavaAstRenderer();

    private static SimpleTypeNode Simple(string n) =>
        new(new IdentifierNode(n));

    [Test]
    public void SimpleType_BareName_RendersAsName()
    {
        _renderer.VisitSimpleType(Simple("String")).Should().Be("String");
    }

    [Test]
    public void GenericType_OneArg_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(Simple("Integer")));
        _renderer.VisitGenericType(t).Should().Be("List<Integer>");
    }

    [Test]
    public void GenericType_TwoArgs_AreCommaJoined()
    {
        var t = new GenericTypeNode(new IdentifierNode("Map"),
            ImmutableList.Create<TypeNode>(Simple("String"), Simple("Pet")));
        _renderer.VisitGenericType(t).Should().Be("Map<String, Pet>");
    }

    [Test]
    public void NullableType_StripsWrapper()
    {
        var t = new NullableTypeNode(Simple("String"));
        // Java has no nullable syntax; renderer unwraps to inner.
        _renderer.VisitNullableType(t).Should().Be("String");
    }

    [Test]
    public void OptionalType_AlsoStripsWrapper()
    {
        var t = new OptionalTypeNode(Simple("Integer"));
        _renderer.VisitOptionalType(t).Should().Be("Integer");
    }

    [Test]
    public void ArrayType_RendersBracketsAfterElement()
    {
        var t = new ArrayTypeNode(Simple("Integer"));
        _renderer.VisitArrayType(t).Should().Be("Integer[]");
    }

    [Test]
    public void DictionaryType_BecomesMapGeneric()
    {
        var t = new DictionaryTypeNode(Simple("String"), Simple("Pet"));
        _renderer.VisitDictionaryType(t).Should().Be("Map<String, Pet>");
    }

    [Test]
    public void UnionType_DegradesToObject()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitUnionType(t).Should().Be("Object");
    }

    [Test]
    public void IntersectionType_DegradesToObject()
    {
        var t = new IntersectionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitIntersectionType(t).Should().Be("Object");
    }

    [Test]
    public void Identifier_RendersAsName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("foo")).Should().Be("foo");
    }

    [Test]
    public void Attribute_PrefixesAt()
    {
        var a = new AttributeNode(new IdentifierNode("Override"));
        _renderer.VisitAttribute(a).Should().Be("@Override");
    }

    [Test]
    public void Import_FromCore_RendersEmpty()
    {
        // Core ImportNode is not used by Java renderer (it uses JavaImportNode);
        // generic ImportNode yields empty so it composes cleanly into output.
        _renderer.VisitImport(new ImportNode(ImmutableList<IdentifierNode>.Empty)).Should().Be("");
    }

    [Test]
    public void Comment_PrefixesDoubleSlash()
    {
        _renderer.VisitComment(new CommentNode("hello")).Should().Be("// hello");
    }
}
