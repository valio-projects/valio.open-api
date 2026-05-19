using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Ast.Builders;

namespace ValIO.Generators.TypeScript.Tests.Builders;

[TestFixture]
public class TypeScriptResultsAstBuilderTests
{
    private readonly NamingConverter _namingConverter = new();
    private TypeScriptResultsAstBuilder _sut = null!;

    [SetUp]
    public void Setup()
    {
        var mapper = new TypeScriptIrTypeMapper();
        _sut = new TypeScriptResultsAstBuilder(_namingConverter, mapper.MapType);
    }

    private static IrOperation Op(string methodName, IReadOnlyList<IrResponse> responses) => new()
    {
        MethodName = methodName,
        Path = "/x",
        HttpMethod = "GET",
        PathParameters = [], QueryParameters = [], HeaderParameters = [],
        RequestBody = null,
        Responses = responses,
    };

    [Test]
    public void HasResponsesWithBody_FalseWhenNoTypedResponses()
    {
        var op = Op("ping", responses: [new IrResponse { StatusCode = "204", Type = null }]);
        _sut.HasResponsesWithBody(op).Should().BeFalse();
    }

    [Test]
    public void SingleResponse_GeneratesResultUnionWithOtherVariant()
    {
        var op = Op("getOne", responses:
        [
            new IrResponse { StatusCode = "200", Type = new IrModelReference { ModelName = "User" } },
        ]);

        var source = _sut.BuildResultsSourceFile(op, importPath: null, fileOrganization: null, currentOperationName: "getOne");
        var typeAliases = source.Declarations.OfType<InterfaceDeclarationNode>().Select(d => d.Name.Name).ToList();
        typeAliases.Should().Contain(new[] { "GetOneResult200", "GetOneResultOther" });
    }

    [Test]
    public void NestedModelReferenceInArray_ImportedAtTopLevel()
    {
        var op = Op("list", responses:
        [
            new IrResponse
            {
                StatusCode = "200",
                Type = new IrArrayType { ElementType = new IrModelReference { ModelName = "Pet" } },
            },
        ]);

        var source = _sut.BuildResultsSourceFile(op, importPath: null,
            fileOrganization: new FileOrganization { ModelsPath = "models" },
            currentOperationName: "list");

        var imported = source.Imports.SelectMany(i => i.Items.Select(it => it.Name)).ToList();
        imported.Should().Contain("Pet");
    }
}
