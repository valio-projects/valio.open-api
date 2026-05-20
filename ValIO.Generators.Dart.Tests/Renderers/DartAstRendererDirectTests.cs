using System.Collections.Immutable;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Dart.Ast.Nodes;
using ValIO.Generators.Dart.Ast.Renderers;

namespace ValIO.Generators.Dart.Tests.Renderers;

/// <summary>
/// Direct unit tests for <see cref="DartAstRenderer"/> type-rendering plus
/// import / class / enum smoke checks.
/// </summary>
[TestFixture]
public class DartAstRendererDirectTests
{
    private DartAstRenderer _renderer = null!;

    [SetUp]
    public void Setup() => _renderer = new DartAstRenderer();

    private static SimpleTypeNode Simple(string n, bool isPrimitive = false) =>
        new(new IdentifierNode(n), isPrimitive);

    [Test]
    public void SimpleType_RendersAsName()
    {
        _renderer.VisitSimpleType(Simple("String")).Should().Be("String");
    }

    [Test]
    public void Generic_RendersWithAngleBrackets()
    {
        var t = new GenericTypeNode(new IdentifierNode("List"),
            ImmutableList.Create<TypeNode>(Simple("int", true)));
        _renderer.VisitGenericType(t).Should().Be("List<int>");
    }

    [Test]
    public void Nullable_AppendsQuestion()
    {
        var t = new NullableTypeNode(Simple("String"));
        _renderer.VisitNullableType(t).Should().Be("String?");
    }

    [Test]
    public void Optional_StripsWrapper()
    {
        var t = new OptionalTypeNode(Simple("int", true));
        _renderer.VisitOptionalType(t).Should().Be("int");
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
    public void UnionType_DegradesToDynamic()
    {
        var t = new UnionTypeNode(ImmutableList.Create<TypeNode>(Simple("A"), Simple("B")));
        _renderer.VisitUnionType(t).Should().Be("dynamic");
    }

    [Test]
    public void Import_NoAlias_RendersBareImport()
    {
        var imp = new DartImportNode("package:dio/dio.dart");
        _renderer.VisitDartImport(imp).Should().Be("import 'package:dio/dio.dart';");
    }

    [Test]
    public void Import_WithAlias_UsesAsKeyword()
    {
        var imp = new DartImportNode("dart:typed_data", alias: "td");
        _renderer.VisitDartImport(imp).Should().Be("import 'dart:typed_data' as td;");
    }

    [Test]
    public void EmptyClass_RendersClassBraces()
    {
        var cls = new DartClassDeclarationNode(new IdentifierNode("Empty"));
        var rendered = _renderer.VisitDartClass(cls);
        rendered.Should().Contain("class Empty {");
        rendered.Should().Contain("}");
    }

    [Test]
    public void ClassWithFinalField_RendersFinalSyntax()
    {
        var fld = new DartFieldNode(
            new IdentifierNode("id"),
            Simple("int", true),
            isNullable: false,
            isFinal: true);
        var cls = new DartClassDeclarationNode(
            new IdentifierNode("X"),
            fields: new[] { fld });
        _renderer.VisitDartClass(cls).Should().Contain("final int id;");
    }

    [Test]
    public void NullableField_AppendsQuestionMark()
    {
        var fld = new DartFieldNode(
            new IdentifierNode("name"),
            Simple("String"),
            isNullable: true);
        var cls = new DartClassDeclarationNode(
            new IdentifierNode("X"),
            fields: new[] { fld });
        _renderer.VisitDartClass(cls).Should().Contain("final String? name;");
    }

    [Test]
    public void EnumWithMembers_RendersEnumDeclaration()
    {
        var en = new DartEnumDeclarationNode(
            new IdentifierNode("Status"),
            members: new[]
            {
                new DartEnumMemberNode(new IdentifierNode("active")),
                new DartEnumMemberNode(new IdentifierNode("inactive")),
            });
        var rendered = _renderer.VisitDartEnum(en);
        rendered.Should().Contain("enum Status {");
        rendered.Should().Contain("active");
        rendered.Should().Contain("inactive");
    }
}
