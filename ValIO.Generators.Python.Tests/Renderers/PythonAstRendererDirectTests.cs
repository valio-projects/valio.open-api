using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Python.Ast.Nodes;
using ValIO.Generators.Python.Ast.Renderers;

namespace ValIO.Generators.Python.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="PythonAstRenderer"/> — every Visit*
/// method is exercised on a tiny hand-built tree. Catches per-method bugs
/// (e.g. missing colon, wrong indent, wrong annotation syntax) without
/// the full OpenAPI pipeline.
/// </summary>
[TestFixture]
public class PythonAstRendererDirectTests
{
    private PythonAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new PythonAstRenderer();

    private static SimpleTypeNode Simple(string n, bool isPrimitive = false) =>
        new(new IdentifierNode(n), isPrimitive);

    [Test]
    public void Import_BareModule_RendersImportStatement()
    {
        var imp = new PythonImportNode("os");
        _renderer.VisitPythonImport(imp).Should().Be("import os");
    }

    [Test]
    public void Import_WithAlias_UsesAsKeyword()
    {
        var imp = new PythonImportNode("numpy", alias: "np");
        _renderer.VisitPythonImport(imp).Should().Be("import numpy as np");
    }

    [Test]
    public void Import_FromModule_SingleName()
    {
        var imp = new PythonImportNode("typing", new[] { "List" });
        _renderer.VisitPythonImport(imp).Should().Be("from typing import List");
    }

    [Test]
    public void Import_FromModule_MultipleNames_AreCommaJoined()
    {
        var imp = new PythonImportNode("typing", new[] { "List", "Dict", "Optional" });
        _renderer.VisitPythonImport(imp).Should().Be("from typing import List, Dict, Optional");
    }

    [Test]
    public void SimpleType_BareName_RendersAsName()
    {
        _renderer.VisitSimpleType(Simple("int", isPrimitive: true)).Should().Be("int");
    }

    [Test]
    public void GenericType_OneArg_RendersInBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("list"),
            ImmutableList.Create<TypeNode>(Simple("int", isPrimitive: true)));
        _renderer.VisitGenericType(t).Should().Be("list[int]");
    }

    [Test]
    public void GenericType_TwoArgs_AreCommaJoined()
    {
        var t = new GenericTypeNode(new IdentifierNode("dict"),
            ImmutableList.Create<TypeNode>(
                Simple("str", isPrimitive: true),
                Simple("int", isPrimitive: true)));
        _renderer.VisitGenericType(t).Should().Be("dict[str, int]");
    }

    [Test]
    public void NullableType_AppendsPipeNone()
    {
        var t = new NullableTypeNode(Simple("str", isPrimitive: true));
        _renderer.VisitNullableType(t).Should().Be("str | None");
    }

    [Test]
    public void OptionalType_AlsoAppendsPipeNone()
    {
        var t = new OptionalTypeNode(Simple("str", isPrimitive: true));
        _renderer.VisitOptionalType(t).Should().Be("str | None");
    }

    [Test]
    public void ArrayType_BecomesListBrackets()
    {
        var t = new ArrayTypeNode(Simple("Pet"));
        _renderer.VisitArrayType(t).Should().Be("list[Pet]");
    }

    [Test]
    public void UnionType_PipeJoined()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(
            Simple("str", isPrimitive: true),
            Simple("int", isPrimitive: true)));
        _renderer.VisitUnionType(t).Should().Be("str | int");
    }

    [Test]
    public void IntersectionType_FallsBackToFirstType()
    {
        var t = new IntersectionTypeNode(ImmutableList.Create<TypeNode>(
            Simple("A"), Simple("B")));
        _renderer.VisitIntersectionType(t).Should().Be("A");
    }

    // IntersectionTypeNode rejects empty types list at construction time;
    // documented behavior — the "fallback to Any" branch in the renderer
    // is unreachable in practice but kept for safety.

    [Test]
    public void DictionaryType_RendersDictBrackets()
    {
        var t = new DictionaryTypeNode(
            Simple("str", isPrimitive: true),
            Simple("Pet"));
        _renderer.VisitDictionaryType(t).Should().Be("dict[str, Pet]");
    }

    [Test]
    public void RawType_PassesThroughAsIs()
    {
        var t = new RawTypeNode("ClassVar[int]");
        _renderer.VisitRawType(t).Should().Be("ClassVar[int]");
    }

    [Test]
    public void SimpleType_LiteralValue_RendersAsLiteralBracket()
    {
        var t = new SimpleTypeNode(new IdentifierNode("Literal"), literalValue: "open");
        _renderer.VisitSimpleType(t).Should().Be("Literal[\"open\"]");
    }

    [Test]
    public void EmptyClass_GeneratesClassWithPass()
    {
        var cls = new PythonClassDeclarationNode(
            new IdentifierNode("Empty"));
        var rendered = _renderer.VisitPythonClassDeclaration(cls);
        rendered.Should().Contain("class Empty:");
        rendered.Should().Contain("pass");
    }

    [Test]
    public void ClassWithBase_RendersParentheses()
    {
        var cls = new PythonClassDeclarationNode(
            new IdentifierNode("Pet"),
            baseClasses: new[] { (TypeNode)Simple("BaseModel") });
        var rendered = _renderer.VisitPythonClassDeclaration(cls);
        rendered.Should().Contain("class Pet(BaseModel):");
    }

    [Test]
    public void ClassWithDecorator_AddsAtAbove()
    {
        var cls = new PythonClassDeclarationNode(
            new IdentifierNode("X"),
            decorators: new[] { "dataclass" });
        _renderer.VisitPythonClassDeclaration(cls).Should().Contain("@dataclass");
    }

    [Test]
    public void AttributeAssignment_AnnotatedNoDefault_RendersAnnotatedForm()
    {
        var attr = new PythonAttributeAssignmentNode(
            new IdentifierNode("id"),
            annotation: Simple("int", isPrimitive: true));
        _renderer.VisitPythonAttributeAssignment(attr).Trim().Should().Be("id: int");
    }

    [Test]
    public void AttributeAssignment_AnnotatedAndDefault_RendersBoth()
    {
        var attr = new PythonAttributeAssignmentNode(
            new IdentifierNode("count"),
            annotation: Simple("int", isPrimitive: true),
            defaultValueLiteral: "0");
        _renderer.VisitPythonAttributeAssignment(attr).Trim().Should().Be("count: int = 0");
    }

    [Test]
    public void AttributeAssignment_QuotedAnnotation_PutsAnnotationInQuotes()
    {
        var attr = new PythonAttributeAssignmentNode(
            new IdentifierNode("self_ref"),
            annotation: Simple("Self"),
            quoteAnnotation: true);
        var rendered = _renderer.VisitPythonAttributeAssignment(attr).Trim();
        // Forward reference renders the type as a string literal.
        rendered.Should().Contain("\"Self\"");
    }

    [Test]
    public void EnumDeclaration_StrEnum_RendersClassWithMembers()
    {
        var e = new PythonEnumDeclarationNode(
            new IdentifierNode("Status"),
            baseClass: PythonEnumBase.StrEnum,
            members: new[]
            {
                new PythonEnumMemberNode("ON",  "\"on\""),
                new PythonEnumMemberNode("OFF", "\"off\""),
            });
        var rendered = _renderer.VisitPythonEnumDeclaration(e);
        rendered.Should().Contain("class Status");
        rendered.Should().Contain("ON");
        rendered.Should().Contain("OFF");
    }

    [Test]
    public void EnumDeclaration_IntEnum_BaseClassIsIntEnum()
    {
        var e = new PythonEnumDeclarationNode(
            new IdentifierNode("Code"),
            baseClass: PythonEnumBase.IntEnum,
            members: new[]
            {
                new PythonEnumMemberNode("OK",    "200"),
                new PythonEnumMemberNode("ERROR", "500"),
            });
        var rendered = _renderer.VisitPythonEnumDeclaration(e);
        rendered.Should().Contain("IntEnum");
    }
}
