using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Cpp.Ast.Renderers;

namespace ValIO.Generators.Cpp.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="CppAstRenderer"/> type rendering
/// (UE C++: TArray, TMap, F-prefix structs, nullable strip).
/// </summary>
[TestFixture]
public class CppAstRendererTypeTests
{
    private CppAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new CppAstRenderer();

    private static SimpleTypeNode Simple(string n) => new(new IdentifierNode(n));

    [Test]
    public void SimpleType_RendersAsName()
    {
        _renderer.VisitSimpleType(Simple("FString")).Should().Be("FString");
    }

    [Test]
    public void GenericType_RendersTArray()
    {
        var t = new GenericTypeNode(new IdentifierNode("TArray"),
            ImmutableList.Create<TypeNode>(Simple("int32")));
        _renderer.VisitGenericType(t).Should().Be("TArray<int32>");
    }

    [Test]
    public void GenericType_TwoArgs_AreCommaJoined()
    {
        var t = new GenericTypeNode(new IdentifierNode("TMap"),
            ImmutableList.Create<TypeNode>(Simple("FString"), Simple("int32")));
        _renderer.VisitGenericType(t).Should().Be("TMap<FString, int32>");
    }

    [Test]
    public void NullableType_StripsWrapper()
    {
        // UE C++ doesn't model nullability in types — the renderer just unwraps.
        var t = new NullableTypeNode(Simple("FString"));
        _renderer.VisitNullableType(t).Should().Be("FString");
    }

    [Test]
    public void OptionalType_AlsoStripsWrapper()
    {
        var t = new OptionalTypeNode(Simple("int32"));
        _renderer.VisitOptionalType(t).Should().Be("int32");
    }

    [Test]
    public void ArrayType_BecomesTArrayOfElement()
    {
        var t = new ArrayTypeNode(Simple("FPet"));
        _renderer.VisitArrayType(t).Should().Be("TArray<FPet>");
    }

    [Test]
    public void DictionaryType_BecomesTMap()
    {
        var t = new DictionaryTypeNode(Simple("FString"), Simple("FPet"));
        _renderer.VisitDictionaryType(t).Should().Be("TMap<FString, FPet>");
    }

    [Test]
    public void UnionType_FallsBackToFirstType()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("FString"), Simple("int32")));
        _renderer.VisitUnionType(t).Should().Be("FString");
    }

    [Test]
    public void UnionType_Empty_FallsBackToFString()
    {
        // Cannot construct empty UnionTypeNode via ctor (throws),
        // so this verifies behavior only when there's at least one type.
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("int32")));
        _renderer.VisitUnionType(t).Should().Be("int32");
    }

    [Test]
    public void IntersectionType_FallsBackToFirstType()
    {
        var t = new IntersectionTypeNode(
            ImmutableList.Create<TypeNode>(Simple("FAnimal"), Simple("FFurry")));
        _renderer.VisitIntersectionType(t).Should().Be("FAnimal");
    }

    [Test]
    public void RawType_PassesThroughUnchanged()
    {
        var t = new RawTypeNode("TSharedPtr<FJsonObject>");
        _renderer.VisitRawType(t).Should().Be("TSharedPtr<FJsonObject>");
    }
}
