using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Tests.TestHelpers;
using ValIO.Tests.Fixtures;

namespace ValIO.Core.Tests.IR;

/// <summary>Separates request-body presence from required fields within the body schema.</summary>
[TestFixture]
public sealed class IrBuilderRequestBodyPresenceTests
{
    /// <summary>Preserves optional and required body declarations while retaining the same required inner field.</summary>
    /// <param name="version">The OpenAPI dialect whose explicit request-body fixture is translated.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [TestCase("2.0")]
    [TestCase("3.0")]
    [TestCase("3.1")]
    [TestCase("3.2")]
    public async Task RequestBodyPresence_DependsOnBodyDeclarationRatherThanRequiredModelFields(string version)
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read($"CoreIR/request-body-presence-{version}.json"));
        var operations = ir.Clients.SelectMany(client => client.Operations).ToDictionary(operation => operation.Path);
        var omitted = operations["/optional-omitted"].RequestBody!;
        var optional = operations["/optional-explicit"].RequestBody!;
        var required = operations["/required"].RequestBody!;

        omitted.IsRequired.Should().BeFalse("an omitted body-required flag defaults to optional");
        optional.IsRequired.Should().BeFalse("required inner fields constrain a supplied body only");
        required.IsRequired.Should().BeTrue("the body itself explicitly requires presence");

        foreach (var body in new[] { omitted, optional, required })
        {
            body.Type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("Payload");
            body.ContentType.Should().Be("application/json");
        }

        var model = ir.Models.Single(candidate => candidate.Name == "Payload");
        model.Properties.Single(property => property.OriginalName == "id").IsRequired.Should().BeTrue();
        model.Properties.Single(property => property.OriginalName == "note").IsRequired.Should().BeFalse();
    }

    /// <summary>Projects multipart files separately from components reused by JSON requests.</summary>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    public async Task MultipartFiles_PreserveOrderAndLeaveJsonBinaryUnchanged()
    {
        var ir = await IrTestHarness.BuildIrAsync(OpenApiFixture.Read("CoreIR/multipart-file-parts-3.0.json"));
        var operations = ir.Clients.SelectMany(client => client.Operations).ToDictionary(operation => operation.Path);
        var form = ir.Models.Single(model => model.Name == ((IrModelReference)operations["/uploads"].RequestBody!.Type).ModelName);
        form.Properties.Select(property => property.OriginalName).Should().Equal("primary-file", "caption", "attachments");
        form.Properties[0].Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.File);
        form.Properties[2].Type.Should().BeOfType<IrArrayType>().Which.ElementType
            .Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.File);
        form.Properties[0].IsRequired.Should().BeTrue();
        form.Properties[2].IsRequired.Should().BeFalse();

        var inline = ir.Models.Single(model => model.Name == ((IrModelReference)operations["/inline"].RequestBody!.Type).ModelName);
        inline.Properties[0].Type.Should().BeOfType<IrNullableType>().Which.InnerType
            .Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.File);
        inline.Properties[1].Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.ByteArray);
        inline.Properties[2].Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.File);

        operations["/uploads/json"].RequestBody!.Type.Should().BeOfType<IrModelReference>().Which.ModelName.Should().Be("UploadFields");
        var json = ir.Models.Single(model => model.Name == "UploadFields");
        json.Properties[0].Type.Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.Binary);
        json.Properties[2].Type.Should().BeOfType<IrArrayType>().Which.ElementType
            .Should().BeOfType<IrPrimitiveType>().Which.PrimitiveKind.Should().Be(IrPrimitiveKind.Binary);
    }
}
