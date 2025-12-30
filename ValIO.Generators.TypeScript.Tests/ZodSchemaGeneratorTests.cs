using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Moq;
using NUnit.Framework;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Zod;

namespace ValIO.Generators.TypeScript.Tests;

/// <summary>
/// Юнит тесты для ZodSchemaGenerator
/// </summary>
[TestFixture]
public class ZodSchemaGeneratorTests
{
    private ZodSchemaGenerator _generator = null!;
    private INamingConverter _namingConverter = null!;
    private TypeScriptTypeMapper _typeMapper = null!;
    private ZodTypeMapper _zodTypeMapper = null!;
    private Mock<ITemplateRegistry> _templateRegistryMock = null!;
    private SchemaResolver _schemaResolver = null!;
    private OpenApiDocument _document = null!;

    [SetUp]
    public void SetUp()
    {
        _namingConverter = new NamingConverter();
        _typeMapper = new TypeScriptTypeMapper(_namingConverter);
        _zodTypeMapper = new ZodTypeMapper(_namingConverter);
        _templateRegistryMock = new Mock<ITemplateRegistry>();
        _document = new OpenApiDocument();
        _schemaResolver = new SchemaResolver(_document);

        _generator = new ZodSchemaGenerator(
            _namingConverter,
            _typeMapper,
            _zodTypeMapper,
            _templateRegistryMock.Object,
            Mock.Of<ILogger<ZodSchemaGenerator>>());
    }

    [Test]
    public void GenerateSchema_SimpleModel_ReturnsSchemaCode()
    {
        // Arrange
        var model = new ApiModel
        {
            Name = "User",
            Description = "User model",
            Properties = new List<ApiProperty>
            {
                new ApiProperty
                {
                    Name = "name",
                    Type = "string",
                    IsRequired = true
                },
                new ApiProperty
                {
                    Name = "age",
                    Type = "number",
                    IsRequired = false
                }
            },
            OriginalSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["age"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
                }
            }
        };

        _templateRegistryMock
            .Setup(x => x.Render(It.IsAny<string>(), It.IsAny<object>()))
            .Returns("export const UserSchema = z.object({ name: z.string(), age: z.number().int().optional() });");

        // Act
        var result = _generator.GenerateSchema(model, _schemaResolver);

        // Assert
        result.Should().NotBeNullOrEmpty();
        _templateRegistryMock.Verify(x => x.Render("zod-schema.cshtml", It.IsAny<object>()), Times.Once);
    }

    [Test]
    public void GenerateSchema_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        ApiModel? model = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _generator.GenerateSchema(model!, _schemaResolver));
    }

    [Test]
    public void GenerateEnumSchema_StringEnum_ReturnsEnumSchemaCode()
    {
        // Arrange
        var apiEnum = new ApiEnum
        {
            Name = "Status",
            Description = "Status enum",
            UnderlyingType = "string",
            Values = new List<ApiEnumValue>
            {
                new ApiEnumValue { Name = "Active", Value = "active" },
                new ApiEnumValue { Name = "Inactive", Value = "inactive" }
            }
        };

        _templateRegistryMock
            .Setup(x => x.Render(It.IsAny<string>(), It.IsAny<object>()))
            .Returns("export const StatusSchema = z.enum([\"active\", \"inactive\"]);");

        // Act
        var result = _generator.GenerateEnumSchema(apiEnum);

        // Assert
        result.Should().NotBeNullOrEmpty();
        _templateRegistryMock.Verify(x => x.Render("zod-enum.cshtml", It.IsAny<object>()), Times.Once);
    }

    [Test]
    public void GenerateEnumSchema_EmptyEnum_ReturnsStringSchema()
    {
        // Arrange
        var apiEnum = new ApiEnum
        {
            Name = "EmptyEnum",
            UnderlyingType = "string",
            Values = new List<ApiEnumValue>()
        };

        // Act
        var result = _generator.GenerateEnumSchema(apiEnum);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("EmptyEnumSchema");
        result.Should().Contain("z.string()");
    }

    [Test]
    public void GenerateEnumSchema_NullEnum_ThrowsArgumentNullException()
    {
        // Arrange
        ApiEnum? apiEnum = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _generator.GenerateEnumSchema(apiEnum!));
    }

    [Test]
    public void GenerateSchema_ModelWithRequiredProperties_GeneratesRequiredFields()
    {
        // Arrange
        var model = new ApiModel
        {
            Name = "Product",
            Properties = new List<ApiProperty>
            {
                new ApiProperty
                {
                    Name = "id",
                    Type = "string",
                    IsRequired = true
                },
                new ApiProperty
                {
                    Name = "name",
                    Type = "string",
                    IsRequired = true
                }
            },
            OriginalSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["id"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                Required = new HashSet<string> { "id", "name" }
            }
        };

        _templateRegistryMock
            .Setup(x => x.Render(It.IsAny<string>(), It.IsAny<object>()))
            .Returns("export const ProductSchema = z.object({ id: z.string(), name: z.string() });");

        // Act
        var result = _generator.GenerateSchema(model, _schemaResolver);

        // Assert
        result.Should().NotBeNullOrEmpty();
        _templateRegistryMock.Verify(x => x.Render("zod-schema.cshtml", It.IsAny<object>()), Times.Once);
    }
}
