using FluentAssertions;
using Microsoft.OpenApi;
using NUnit.Framework;
using ValIO.Core.Interfaces;
using ValIO.Core.Services;
using ValIO.Generators.CSharp.Models;
using ValIO.Generators.TypeScript;

namespace ValIO.Generators.TypeScript.Tests;

/// <summary>
/// Юнит тесты для TypeScriptTypeMapper
/// </summary>
[TestFixture]
public class TypeScriptTypeMapperTests
{
    private TypeScriptTypeMapper _mapper = null!;
    private INamingConverter _namingConverter = null!;
    private SchemaResolver _schemaResolver = null!;
    private OpenApiDocument _document = null!;

    [SetUp]
    public void SetUp()
    {
        _namingConverter = new NamingConverter();
        _mapper = new TypeScriptTypeMapper(_namingConverter);
        _document = new OpenApiDocument();
        _schemaResolver = new SchemaResolver(_document);
    }

    [Test]
    public void MapType_StringSchema_ReturnsString()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        };

        // Act
        var result = _mapper.MapType(schema, _schemaResolver);

        // Assert
        result.Should().Be("string");
    }

    [Test]
    public void MapType_IntegerSchema_ReturnsNumber()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer
        };

        // Act
        var result = _mapper.MapType(schema, _schemaResolver);

        // Assert
        result.Should().Be("number");
    }

    [Test]
    public void MapType_NumberSchema_ReturnsNumber()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number
        };

        // Act
        var result = _mapper.MapType(schema, _schemaResolver);

        // Assert
        result.Should().Be("number");
    }

    [Test]
    public void MapType_BooleanSchema_ReturnsBoolean()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Boolean
        };

        // Act
        var result = _mapper.MapType(schema, _schemaResolver);

        // Assert
        result.Should().Be("boolean");
    }

    [Test]
    public void MapType_ArraySchema_ReturnsArrayType()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        };

        // Act
        var result = _mapper.MapType(schema, _schemaResolver);

        // Assert
        result.Should().Be("string[]");
    }

    [Test]
    public void MapType_ArrayOfObjects_ReturnsArrayType()
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

        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchemaReference("User", _document)
        };

        // Act
        var result = _mapper.MapType(schema, _schemaResolver);

        // Assert
        result.Should().Be("User[]");
    }

    [Test]
    public void MapPropertyType_RequiredString_ReturnsString()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        };

        // Act
        var result = _mapper.MapPropertyType(schema, isRequired: true, _schemaResolver);

        // Assert
        result.Should().Be("string");
    }

    [Test]
    public void MapPropertyType_OptionalString_ReturnsString()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        };

        // Act
        var result = _mapper.MapPropertyType(schema, isRequired: false, _schemaResolver);

        // Assert
        result.Should().Be("string");
    }

    [Test]
    public void MapPropertyType_NullableString_ReturnsString()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null
        };

        // Act
        var result = _mapper.MapPropertyType(schema, isRequired: true, _schemaResolver);

        // Assert
        result.Should().Be("string");
    }

    [Test]
    public void MapTypeInfo_StringSchema_ReturnsSimpleTypeInfo()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("string");
        result.IsPrimitive.Should().BeTrue();
        result.FullTypeName.Should().Be("string");
    }

    [Test]
    public void MapTypeInfo_ArraySchema_ReturnsCollectionTypeInfo()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.IsCollection.Should().BeTrue();
        result.FullTypeName.Should().Be("string[]");
        result.GenericArguments.Should().HaveCount(1);
        result.GenericArguments[0].BaseType.Should().Be("string");
    }

    [Test]
    public void MapTypeInfo_ObjectReference_ReturnsReferenceTypeInfo()
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

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("User");
        result.FullTypeName.Should().Be("User");
        result.IsPrimitive.Should().BeFalse();
    }

    [Test]
    public void MapTypeInfo_Dictionary_ReturnsDictionaryTypeInfo()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.IsDictionary.Should().BeTrue();
        result.FullTypeName.Should().Contain("Record");
    }

    [Test]
    public void MapTypeInfo_AllOf_ReturnsFirstType()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            AllOf = new List<IOpenApiSchema>
            {
                new OpenApiSchema { Type = JsonSchemaType.String },
                new OpenApiSchema { Type = JsonSchemaType.Integer }
            }
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("string");
    }

    [Test]
    public void MapTypeInfo_OneOf_ReturnsFirstType()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            OneOf = new List<IOpenApiSchema>
            {
                new OpenApiSchema { Type = JsonSchemaType.String },
                new OpenApiSchema { Type = JsonSchemaType.Integer }
            }
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("string");
    }

    [Test]
    public void MapPropertyTypeInfo_RequiredInteger_ReturnsNumber()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer
        };

        // Act
        var result = _mapper.MapPropertyTypeInfo(schema, isRequired: true, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("number");
        result.IsPrimitive.Should().BeTrue();
    }

    [Test]
    public void MapPropertyTypeInfo_OptionalInteger_ReturnsNumber()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer
        };

        // Act
        var result = _mapper.MapPropertyTypeInfo(schema, isRequired: false, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("number");
        result.IsNullable.Should().BeTrue();
    }

    [Test]
    public void MapTypeInfo_StringWithDateFormat_ReturnsString()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "date"
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("string");
        result.IsPrimitive.Should().BeTrue();
    }

    [Test]
    public void MapTypeInfo_StringWithDateTimeFormat_ReturnsString()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "date-time"
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("string");
        result.IsPrimitive.Should().BeTrue();
    }

    [Test]
    public void MapTypeInfo_StringWithBinaryFormat_ReturnsBlob()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "binary"
        };

        // Act
        var result = _mapper.MapTypeInfo(schema, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("Blob");
        result.IsPrimitive.Should().BeTrue();
        result.IsFile.Should().BeTrue();
    }

    [Test]
    public void MapTypeInfo_NullSchema_ReturnsAny()
    {
        // Arrange
        IOpenApiSchema? schema = null;

        // Act
        var result = _mapper.MapTypeInfo(schema!, _schemaResolver);

        // Assert
        result.Should().NotBeNull();
        result.BaseType.Should().Be("any");
        result.IsPrimitive.Should().BeTrue();
    }
}
