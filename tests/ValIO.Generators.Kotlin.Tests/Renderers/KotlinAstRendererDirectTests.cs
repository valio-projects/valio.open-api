using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Kotlin.Ast.Renderers;

namespace ValIO.Generators.Kotlin.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="KotlinAstRenderer"/> — type-rendering
/// methods plus a few identifier/raw passes.
/// </summary>
[TestFixture]
public class KotlinAstRendererDirectTests
{
    private KotlinAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new KotlinAstRenderer();

    private static SimpleTypeNode Simple(string n, bool isNullable = false) =>
        new(new IdentifierNode(n), isNullable: isNullable);

    [Test]
    public void SimpleType_NotNullable_NoQuestionMark()
    {
        _renderer.VisitSimpleType(Simple("String")).Should().Be("String");
    }

    [Test]
    public void SimpleType_Nullable_AppendsQuestionMark()
    {
        _renderer.VisitSimpleType(Simple("String", isNullable: true)).Should().Be("String?");
    }

    [Test]
    public void GenericType_OneArg_RendersAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(Simple("Int")));
        _renderer.VisitGenericType(t).Should().Be("List<Int>");
    }

    [Test]
    public void GenericType_TwoArgs_AreCommaJoined()
    {
        var t = new GenericTypeNode(new IdentifierNode("Map"),
            ImmutableList.Create<TypeNode>(Simple("String"), Simple("Pet")));
        _renderer.VisitGenericType(t).Should().Be("Map<String, Pet>");
    }

    [Test]
    public void NullableType_WrapsInnerWithQuestionMark()
    {
        var t = new NullableTypeNode(Simple("String"));
        _renderer.VisitNullableType(t).Should().Be("String?");
    }

    [Test]
    public void NullableType_AroundAlreadyNullableSimple_DoesNotDoubleQuestion()
    {
        var t = new NullableTypeNode(Simple("String", isNullable: true));
        _renderer.VisitNullableType(t).Should().Be("String?");
    }

    [Test]
    public void OptionalType_StripsWrapper()
    {
        var t = new OptionalTypeNode(Simple("Int"));
        _renderer.VisitOptionalType(t).Should().Be("Int");
    }

    [Test]
    public void ArrayType_BecomesListGeneric()
    {
        var t = new ArrayTypeNode(Simple("Pet"));
        _renderer.VisitArrayType(t).Should().Be("List<Pet>");
    }

    [Test]
    public void DictionaryType_BecomesMapGeneric()
    {
        var t = new DictionaryTypeNode(Simple("String"), Simple("Pet"));
        _renderer.VisitDictionaryType(t).Should().Be("Map<String, Pet>");
    }

    [Test]
    public void UnionType_DegradesToAny()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitUnionType(t).Should().Be("Any");
    }

    [Test]
    public void IntersectionType_DegradesToAny()
    {
        var t = new IntersectionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitIntersectionType(t).Should().Be("Any");
    }

    [Test]
    public void RawType_PassesThroughUnchanged()
    {
        var t = new RawTypeNode("List<out String>");
        _renderer.VisitRawType(t).Should().Be("List<out String>");
    }

    [Test]
    public void Identifier_RendersAsName()
    {
        _renderer.VisitIdentifier(new IdentifierNode("foo")).Should().Be("foo");
    }
}
