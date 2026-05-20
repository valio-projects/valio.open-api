using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Tests.Builders;

/// <summary>
/// Tests for <see cref="TypeScriptSourceFileBuilder"/> — the fluent helper
/// that any TS AST builder can use to incrementally collect imports
/// (dedup-aware) and declarations.
/// </summary>
[TestFixture]
public class TypeScriptSourceFileBuilderTests
{
    [Test]
    public void EnsureImport_Twice_SameSymbol_IsRecordedOnce()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("Foo", "./foo")
            .EnsureImport("Foo", "./foo")
            .Build();

        sf.Imports.Should().ContainSingle();
        sf.Imports[0].Items.Should().ContainSingle().Which.Name.Should().Be("Foo");
    }

    [Test]
    public void EnsureImport_TwoSymbolsSameModule_AreGroupedIntoOneImportStatement()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("Foo", "./shared")
            .EnsureImport("Bar", "./shared")
            .Build();

        sf.Imports.Should().ContainSingle();
        sf.Imports[0].Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "Foo", "Bar" });
    }

    [Test]
    public void EnsureImport_DifferentModules_ProduceSeparateImportStatements()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("Foo", "./foo")
            .EnsureImport("Bar", "./bar")
            .Build();

        sf.Imports.Should().HaveCount(2);
    }

    [Test]
    public void EnsureImport_SameSymbolDifferentModules_BothRecorded()
    {
        // Different modules → different identity, so both are kept.
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("create", "./foo")
            .EnsureImport("create", "./bar")
            .Build();

        sf.Imports.Should().HaveCount(2);
    }

    [Test]
    public void EnsureImport_TypeOnlyAndValueImport_AreSeparate()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("Pet", "./types", isTypeOnly: true)
            .EnsureImport("createPet", "./types", isTypeOnly: false)
            .Build();

        sf.Imports.Should().HaveCount(2);
        sf.Imports.Should().Contain(i => i.IsTypeOnly && i.From == "./types");
        sf.Imports.Should().Contain(i => !i.IsTypeOnly && i.From == "./types");
    }

    [Test]
    public void EnsureImports_Bulk_AddsAll()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImports("zod", new[] { "z", "ZodSchema", "ZodError" })
            .Build();

        sf.Imports.Should().ContainSingle();
        sf.Imports[0].Items.Select(i => i.Name).Should().BeEquivalentTo(
            new[] { "z", "ZodSchema", "ZodError" });
    }

    [Test]
    public void Add_AppendsDeclarationsInOrder()
    {
        var t1 = new TypeAliasDeclarationNode(
            new IdentifierNode("T1"), new SimpleTypeNode(new IdentifierNode("string"), true));
        var t2 = new TypeAliasDeclarationNode(
            new IdentifierNode("T2"), new SimpleTypeNode(new IdentifierNode("number"), true));

        var sf = new TypeScriptSourceFileBuilder().Add(t1).Add(t2).Build();
        sf.Declarations.Should().HaveCount(2);
        ((TypeAliasDeclarationNode)sf.Declarations[0]).Name.Name.Should().Be("T1");
        ((TypeAliasDeclarationNode)sf.Declarations[1]).Name.Name.Should().Be("T2");
    }

    [Test]
    public void WithMetadata_KeepsValue()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .WithMetadata("directive", "use server")
            .Build();
        sf.Metadata["directive"].Should().Be("use server");
    }

    [Test]
    public void Build_EmptyByDefault()
    {
        var sf = new TypeScriptSourceFileBuilder().Build();
        sf.Imports.Should().BeEmpty();
        sf.Declarations.Should().BeEmpty();
        sf.Exports.Should().BeEmpty();
    }

    [Test]
    public void EnsureImport_EmptySymbol_Throws()
    {
        var sut = new TypeScriptSourceFileBuilder();
        sut.Invoking(b => b.EnsureImport("", "./x"))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void EnsureImport_EmptyFrom_Throws()
    {
        var sut = new TypeScriptSourceFileBuilder();
        sut.Invoking(b => b.EnsureImport("X", ""))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_NullDeclaration_Throws()
    {
        var sut = new TypeScriptSourceFileBuilder();
        sut.Invoking(b => b.Add(null!)).Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void EnsureImport_DefaultImport_IsDifferentFromNamed()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("got", "got", kind: ImportKind.Default)
            .EnsureImport("got", "got", kind: ImportKind.Named)
            .Build();
        // Different ImportKind → separate identity, so both imports kept.
        sf.Imports.Should().HaveCount(2);
    }

    [Test]
    public void ImportOrder_PreservesFirstInsertion()
    {
        var sf = new TypeScriptSourceFileBuilder()
            .EnsureImport("a", "./alpha")
            .EnsureImport("b", "./beta")
            .EnsureImport("c", "./alpha") // joins the alpha group
            .Build();

        // alpha first, beta second.
        sf.Imports[0].From.Should().Be("./alpha");
        sf.Imports[1].From.Should().Be("./beta");
        // c appended to alpha's items.
        sf.Imports[0].Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "a", "c" });
    }
}
