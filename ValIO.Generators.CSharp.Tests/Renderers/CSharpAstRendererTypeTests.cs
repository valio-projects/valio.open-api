using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.CSharp.Ast.Renderers;

namespace ValIO.Generators.CSharp.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="CSharpAstRenderer"/> type rendering.
/// Targets each Visit* type method in isolation.
/// </summary>
[TestFixture]
public class CSharpAstRendererTypeTests
{
    private CSharpAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new CSharpAstRenderer();

    private static SimpleTypeNode Simple(string n) => new(new IdentifierNode(n));

    [Test]
    public void SimpleType_RendersAsName()
    {
        _renderer.VisitSimpleType(Simple("string")).Should().Be("string");
    }

    [Test]
    public void GenericType_OneArg_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(Simple("int")));
        _renderer.VisitGenericType(t).Should().Be("List<int>");
    }

    [Test]
    public void GenericType_TwoArgs_AreCommaJoined()
    {
        var t = new GenericTypeNode(new IdentifierNode("Dictionary"),
            ImmutableList.Create<TypeNode>(Simple("string"), Simple("Pet")));
        _renderer.VisitGenericType(t).Should().Be("Dictionary<string, Pet>");
    }

    [Test]
    public void NullableType_AppendsQuestionMark()
    {
        var t = new NullableTypeNode(Simple("int"));
        _renderer.VisitNullableType(t).Should().Be("int?");
    }

    [Test]
    public void OptionalType_AlsoAppendsQuestionMark()
    {
        var t = new OptionalTypeNode(Simple("string"));
        _renderer.VisitOptionalType(t).Should().Be("string?");
    }

    [Test]
    public void ArrayType_RendersBracketsAfterElement()
    {
        var t = new ArrayTypeNode(Simple("byte"));
        _renderer.VisitArrayType(t).Should().Be("byte[]");
    }

    [Test]
    public void DictionaryType_BecomesDictionaryGeneric()
    {
        var t = new DictionaryTypeNode(Simple("string"), Simple("Pet"));
        _renderer.VisitDictionaryType(t).Should().Be("Dictionary<string, Pet>");
    }

    [Test]
    public void UnionType_DegradesToObject()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitUnionType(t).Should().Be("object");
    }

    [Test]
    public void IntersectionType_FallsBackToFirstType()
    {
        var t = new IntersectionTypeNode(
            ImmutableList.Create<TypeNode>(Simple("Animal"), Simple("Furry")));
        _renderer.VisitIntersectionType(t).Should().Be("Animal");
    }

    [Test]
    public void RawType_PassesThroughUnchanged()
    {
        var t = new RawTypeNode("ReadOnlySpan<char>");
        _renderer.VisitRawType(t).Should().Be("ReadOnlySpan<char>");
    }
}
