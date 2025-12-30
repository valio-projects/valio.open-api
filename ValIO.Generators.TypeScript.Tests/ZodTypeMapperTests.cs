using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Zod;

namespace ValIO.Generators.TypeScript.Tests;

/// <summary>
/// Юнит тесты для ZodTypeMapper
/// </summary>
[TestFixture]
public class ZodTypeMapperTests
{
    private ZodTypeMapper _mapper = null!;
    private INamingConverter _namingConverter = null!;
    private SchemaResolver _schemaResolver = null!;
    private OpenApiDocument _document = null!;

    [SetUp]
    public void SetUp()
    {
        _namingConverter = new NamingConverter();
        _mapper = new ZodTypeMapper(_namingConverter);
        _document = new OpenApiDocument();
        _schemaResolver = new SchemaResolver(_document);
    }

    [Test]
    public void MapToZodSchema_StringSchema_ReturnsZString()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var property = new ApiProperty { Name = "name" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string()");
    }

    [Test]
    public void MapToZodSchema_IntegerSchema_ReturnsZNumberInt()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        var property = new ApiProperty { Name = "age" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.number().int()");
    }

    [Test]
    public void MapToZodSchema_NumberSchema_ReturnsZNumber()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Number };
        var property = new ApiProperty { Name = "price" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.number()");
    }

    [Test]
    public void MapToZodSchema_BooleanSchema_ReturnsZBoolean()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
        var property = new ApiProperty { Name = "isActive" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.boolean()");
    }

    [Test]
    public void MapToZodSchema_ArraySchema_ReturnsZArray()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var property = new ApiProperty { Name = "tags" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.array(z.string())");
    }

    [Test]
    public void MapToZodSchema_StringWithMinLength_ReturnsZStringWithMin()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var property = new ApiProperty
        {
            Name = "name",
            MinLength = 3
        };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string().min(3)");
    }

    [Test]
    public void MapToZodSchema_StringWithMaxLength_ReturnsZStringWithMax()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var property = new ApiProperty
        {
            Name = "name",
            MaxLength = 100
        };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string().max(100)");
    }

    [Test]
    public void MapToZodSchema_StringWithPattern_ReturnsZStringWithRegex()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var property = new ApiProperty
        {
            Name = "email",
            Pattern = "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"
        };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Contain("z.string()");
        result.Should().Contain(".regex(/");
        result.Should().Contain(property.Pattern);
    }

    [Test]
    public void MapToZodSchema_NumberWithMin_ReturnsZNumberWithMin()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Number };
        var property = new ApiProperty
        {
            Name = "price",
            Min = 0
        };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.number().min(0)");
    }

    [Test]
    public void MapToZodSchema_NumberWithMax_ReturnsZNumberWithMax()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Number };
        var property = new ApiProperty
        {
            Name = "price",
            Max = 1000
        };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.number().max(1000)");
    }

    [Test]
    public void MapToZodSchema_StringWithDateFormat_ReturnsZStringDate()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "date"
        };
        var property = new ApiProperty { Name = "birthDate" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string().date()");
    }

    [Test]
    public void MapToZodSchema_StringWithDateTimeFormat_ReturnsZStringDatetime()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "date-time"
        };
        var property = new ApiProperty { Name = "createdAt" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string().datetime()");
    }

    [Test]
    public void MapToZodSchema_StringWithUuidFormat_ReturnsZStringUuid()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "uuid"
        };
        var property = new ApiProperty { Name = "id" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string().uuid()");
    }

    [Test]
    public void MapToZodSchema_StringWithEmailFormat_ReturnsZStringEmail()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "email"
        };
        var property = new ApiProperty { Name = "email" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.string().email()");
    }

    [Test]
    public void MapToZodSchema_StringWithBinaryFormat_ReturnsZInstanceofBlob()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "binary"
        };
        var property = new ApiProperty { Name = "file" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("z.instanceof(Blob)");
    }

    [Test]
    public void MapToZodSchema_EnumSchema_ReturnsZEnum()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = new List<JsonNode>
            {
                JsonValue.Create("active"),
                JsonValue.Create("inactive")
            }
        };
        var property = new ApiProperty { Name = "status" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Contain("z.enum([");
        result.Should().Contain("\"active\"");
        result.Should().Contain("\"inactive\"");
    }

    [Test]
    public void MapToZodSchema_ObjectSchema_ReturnsZObject()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["age"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
            }
        };
        var property = new ApiProperty { Name = "user" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Contain("z.object({");
        result.Should().Contain("name:");
        result.Should().Contain("age:");
    }

    [Test]
    public void MapToZodSchema_DictionarySchema_ReturnsZRecord()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String }
        };
        var property = new ApiProperty { Name = "metadata" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Contain("z.record(z.string()");
        result.Should().Contain("z.string()");
    }

    [Test]
    public void MapToZodSchema_ReferenceSchema_ReturnsSchemaName()
    {
        // Arrange
        _document.Components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, IOpenApiSchema>
            {
                ["User"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            }
        };
        _document.Workspace = new OpenApiWorkspace();
        _document.Workspace.RegisterComponents(_document);

        var schema = new OpenApiSchemaReference("User", _document);
        var property = new ApiProperty { Name = "user" };

        // Act
        var result = _mapper.MapToZodSchema(schema, property, _schemaResolver);

        // Assert
        result.Should().Be("UserSchema");
    }

    [Test]
    public void MapToZodSchema_NullSchema_ReturnsZAny()
    {
        // Arrange
        IOpenApiSchema? schema = null;
        var property = new ApiProperty { Name = "unknown" };

        // Act
        var result = _mapper.MapToZodSchema(schema!, property, _schemaResolver);

        // Assert
        result.Should().Be("z.any()");
    }
}
