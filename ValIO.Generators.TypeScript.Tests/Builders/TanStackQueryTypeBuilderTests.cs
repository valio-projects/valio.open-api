using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

namespace ValIO.Generators.TypeScript.Tests.Builders;

/// <summary>
/// Unit-tests for <see cref="TanStackQueryTypeBuilder"/> — the small,
/// purely-functional TypeNode factory backing every TanStack hook builder.
/// </summary>
[TestFixture]
public class TanStackQueryTypeBuilderTests
{
    private TanStackQueryTypeBuilder _axios = null!;
    private TanStackQueryTypeBuilder _fetch = null!;

    [SetUp]
    public void Setup()
    {
        _axios = new TanStackQueryTypeBuilder(new NamingConverter(), hookUsesAxios: true);
        _fetch = new TanStackQueryTypeBuilder(new NamingConverter(), hookUsesAxios: false);
    }

    [Test]
    public void Simple_BuildsSimpleTypeNode()
    {
        var t = TanStackQueryTypeBuilder.SimpleType("Pet");
        var simple = t.Should().BeOfType<SimpleTypeNode>().Subject;
        simple.Name.Name.Should().Be("Pet");
        simple.IsPrimitive.Should().BeFalse();
    }

    [Test]
    public void SimpleType_Primitive_FlagsIsPrimitive()
    {
        var t = TanStackQueryTypeBuilder.SimpleType("string", isPrimitive: true);
        ((SimpleTypeNode)t).IsPrimitive.Should().BeTrue();
    }

    [Test]
    public void GenericType_KeepsAllTypeArgsInOrder()
    {
        var t = TanStackQueryTypeBuilder.GenericType("Map",
            TanStackQueryTypeBuilder.SimpleType("string", isPrimitive: true),
            TanStackQueryTypeBuilder.SimpleType("Pet"));
        var g = t.Should().BeOfType<GenericTypeNode>().Subject;
        g.Name.Name.Should().Be("Map");
        g.TypeArguments.Should().HaveCount(2);
        ((SimpleTypeNode)g.TypeArguments[1]).Name.Name.Should().Be("Pet");
    }

    [Test]
    public void ReadonlyArray_WrapsElementWithIsReadOnly()
    {
        var arr = TanStackQueryTypeBuilder.ReadonlyArray(
            TanStackQueryTypeBuilder.SimpleType("unknown", isPrimitive: true));
        var a = arr.Should().BeOfType<ArrayTypeNode>().Subject;
        a.IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void Optional_WrapsInOptionalTypeNode()
    {
        var o = TanStackQueryTypeBuilder.Optional(TanStackQueryTypeBuilder.SimpleType("Pet"));
        o.Should().BeOfType<OptionalTypeNode>();
    }

    [Test]
    public void Union_KeepsAllMembers()
    {
        var u = TanStackQueryTypeBuilder.Union(
            TanStackQueryTypeBuilder.SimpleType("A"),
            TanStackQueryTypeBuilder.SimpleType("B"),
            TanStackQueryTypeBuilder.SimpleType("C"));
        u.Should().BeOfType<UnionTypeNode>().Which.Types.Should().HaveCount(3);
    }

    [Test]
    public void Partial_GenericIsPartial()
    {
        var p = TanStackQueryTypeBuilder.Partial(TanStackQueryTypeBuilder.SimpleType("X"));
        ((GenericTypeNode)p).Name.Name.Should().Be("Partial");
    }

    [Test]
    public void InfiniteData_GenericIsInfiniteData()
    {
        var i = TanStackQueryTypeBuilder.InfiniteData(TanStackQueryTypeBuilder.SimpleType("Page"));
        ((GenericTypeNode)i).Name.Name.Should().Be("InfiniteData");
    }

    [Test]
    public void BuildDataType_NoResultName_FallsBackToVoid()
    {
        var t = (SimpleTypeNode)_axios.BuildDataType(null);
        t.Name.Name.Should().Be("void");
    }

    [Test]
    public void BuildDataType_WithName_ReturnsSimpleWithName()
    {
        var t = (SimpleTypeNode)_axios.BuildDataType("PetList");
        t.Name.Name.Should().Be("PetList");
    }

    [Test]
    public void BuildVariablesType_NoParamsName_VoidFallback()
    {
        var t = (SimpleTypeNode)_axios.BuildVariablesType(null);
        t.Name.Name.Should().Be("void");
    }

    [Test]
    public void BuildVariablesType_WithName_ReturnsName()
    {
        var t = (SimpleTypeNode)_axios.BuildVariablesType("AddPetParams");
        t.Name.Name.Should().Be("AddPetParams");
    }

    [Test]
    public void BuildQueryKeyReturnType_ReadonlyArrayOfUnknown()
    {
        var arr = _axios.BuildQueryKeyReturnType().Should().BeOfType<ArrayTypeNode>().Subject;
        arr.IsReadOnly.Should().BeTrue();
        ((SimpleTypeNode)arr.ElementType).Name.Name.Should().Be("unknown");
    }

    [Test]
    public void BuildQueryFunctionContextType_OneTypeArg()
    {
        var ctx = (GenericTypeNode)_axios.BuildQueryFunctionContextType();
        ctx.Name.Name.Should().Be("QueryFunctionContext");
        ctx.TypeArguments.Should().HaveCount(1);
    }

    [Test]
    public void BuildQueryFunctionContextForInfiniteType_TwoTypeArgs()
    {
        var ctx = (GenericTypeNode)_axios.BuildQueryFunctionContextForInfiniteType();
        ctx.Name.Name.Should().Be("QueryFunctionContext");
        ctx.TypeArguments.Should().HaveCount(2);
        ((SimpleTypeNode)ctx.TypeArguments[1]).Name.Name.Should().Be("number");
    }

    [Test]
    public void BuildRequestOptionsType_AxiosVariant_UsesAxiosRequestConfig()
    {
        var u = (UnionTypeNode)_axios.BuildRequestOptionsType();
        var names = u.Types.OfType<SimpleTypeNode>().Select(s => s.Name.Name).ToList();
        names.Should().Contain("AxiosRequestConfig");
    }

    [Test]
    public void BuildRequestOptionsType_FetchVariant_UsesRequestInit()
    {
        var u = (UnionTypeNode)_fetch.BuildRequestOptionsType();
        var names = u.Types.OfType<SimpleTypeNode>().Select(s => s.Name.Name).ToList();
        names.Should().Contain("RequestInit");
    }

    [Test]
    public void BuildUseQueryOptionsType_4TypeArgs()
    {
        var t = (GenericTypeNode)_axios.BuildUseQueryOptionsType(
            TanStackQueryTypeBuilder.SimpleType("Pet"));
        t.Name.Name.Should().Be("UseQueryOptions");
        t.TypeArguments.Should().HaveCount(4);
    }

    [Test]
    public void BuildUseInfiniteQueryOptionsType_5TypeArgs()
    {
        var t = (GenericTypeNode)_axios.BuildUseInfiniteQueryOptionsType(
            TanStackQueryTypeBuilder.SimpleType("Pet"));
        t.Name.Name.Should().Be("UseInfiniteQueryOptions");
        t.TypeArguments.Should().HaveCount(5);
    }

    [Test]
    public void BuildUseQueryResultType_DataAndError()
    {
        var t = (GenericTypeNode)_axios.BuildUseQueryResultType(
            TanStackQueryTypeBuilder.SimpleType("Pet"));
        t.Name.Name.Should().Be("UseQueryResult");
        ((SimpleTypeNode)t.TypeArguments[1]).Name.Name.Should().Be("Error");
    }

    [Test]
    public void BuildUseInfiniteQueryResultType_DataWrappedInInfiniteData()
    {
        var t = (GenericTypeNode)_axios.BuildUseInfiniteQueryResultType(
            TanStackQueryTypeBuilder.SimpleType("Page"));
        var firstArg = (GenericTypeNode)t.TypeArguments[0];
        firstArg.Name.Name.Should().Be("InfiniteData");
    }

    [Test]
    public void BuildUseMutationResultType_DataErrorVariables()
    {
        var t = (GenericTypeNode)_axios.BuildUseMutationResultType(
            TanStackQueryTypeBuilder.SimpleType("Pet"),
            TanStackQueryTypeBuilder.SimpleType("AddPetParams"));
        t.Name.Name.Should().Be("UseMutationResult");
        t.TypeArguments.Should().HaveCount(3);
    }

    [Test]
    public void BuildPromiseQueryClientType_PromiseOfQueryClient()
    {
        var t = (GenericTypeNode)_axios.BuildPromiseQueryClientType();
        t.Name.Name.Should().Be("Promise");
        ((SimpleTypeNode)t.TypeArguments[0]).Name.Name.Should().Be("QueryClient");
    }

    [Test]
    public void BuildMutationOptionsParamsInterface_HasBothMembers()
    {
        var iface = _axios.BuildMutationOptionsParamsInterface(
            "AddPet",
            TanStackQueryTypeBuilder.SimpleType("Pet"),
            TanStackQueryTypeBuilder.SimpleType("AddPetParams"));
        iface.Name.Name.Should().Be("GetAddPetMutationOptionsParams");
        iface.Members.Should().HaveCount(2);
    }

    [Test]
    public void BuildQueryOptionsParamsInterface_InfiniteVariant_NamesInterfaceWithInfinite()
    {
        var iface = _axios.BuildQueryOptionsParamsInterface(
            "GetPets",
            TanStackQueryTypeBuilder.SimpleType("Pet"),
            isInfinite: true);
        iface.Name.Name.Should().Contain("Infinite");
    }
}
