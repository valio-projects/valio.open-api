using System.Text.Json;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Services;

namespace ValIO.Generators.Cpp;

/// <summary>
/// Маппер типов OpenAPI схемы в C++/UE типы.
/// Используется для DI и BaseCodeGenerator. 
/// Основной маппинг IR -> AST TypeNode выполняется в UETypeMapper.
/// </summary>
public sealed class CppTypeMapper : BaseTypeMapper
{
    public CppTypeMapper(INamingConverter namingConverter)
        : base(namingConverter, GenerationLanguage.Cpp)
    {
    }

    public override string MapType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        var resolved = schemaResolver.ResolveSchema(schema);
        var refName = ResolveSchemaReference(resolved, schemaResolver);
        if (!string.IsNullOrEmpty(refName))
            return $"F{NamingConverter.ToPascalCase(refName)}";

        if (IsCollection(resolved))
        {
            var itemType = resolved.Items != null
                ? MapType(resolved.Items, schemaResolver)
                : "FString";
            return $"TArray<{itemType}>";
        }

        if (IsDictionary(resolved))
        {
            var valueType = resolved.AdditionalProperties != null
                ? MapType(resolved.AdditionalProperties, schemaResolver)
                : "FString";
            return $"TMap<FString, {valueType}>";
        }

        return MapPrimitiveType(resolved);
    }

    public override string MapPropertyType(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        return MapType(schema, schemaResolver);
    }

    public override string MapEnumType(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        var name = ResolveSchemaReference(schema, schemaResolver);
        return !string.IsNullOrEmpty(name) ? $"E{NamingConverter.ToPascalCase(name)}" : "uint8";
    }

    public override TypeInfo MapTypeInfo(IOpenApiSchema schema, ISchemaResolver schemaResolver)
    {
        var typeName = MapType(schema, schemaResolver);
        return TypeInfo.Simple(typeName, language: GenerationLanguage.Cpp);
    }

    public override TypeInfo MapPropertyTypeInfo(IOpenApiSchema schema, bool isRequired, ISchemaResolver schemaResolver)
    {
        return MapTypeInfo(schema, schemaResolver);
    }

    protected override TypeInfo CreateCollectionTypeInfo(TypeInfo itemType, bool isNullable)
    {
        return TypeInfo.Collection("TArray", itemType, isNullable, language: GenerationLanguage.Cpp);
    }

    protected override TypeInfo CreateDictionaryTypeInfo(TypeInfo keyType, TypeInfo valueType, bool isNullable)
    {
        return TypeInfo.Dictionary("TMap", keyType, valueType, isNullable, language: GenerationLanguage.Cpp);
    }

    protected override TypeInfo CreatePrimitiveTypeInfo(string baseTypeName, bool isPrimitive, bool isFile, bool isNullable)
    {
        return TypeInfo.Simple(baseTypeName, isPrimitive: isPrimitive, language: GenerationLanguage.Cpp);
    }

    protected override TypeInfo CreateObjectTypeInfo(string? typeName, bool isNullable)
    {
        return TypeInfo.Simple(typeName ?? "FString", language: GenerationLanguage.Cpp);
    }

    protected override TypeInfo HandleAllOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        if (schema.AllOf?.Count > 0)
        {
            var first = schema.AllOf[0];
            return MapTypeInfo(first, schemaResolver);
        }
        return TypeInfo.Simple("FString", language: GenerationLanguage.Cpp);
    }

    protected override TypeInfo HandleOneOfAnyOf(IOpenApiSchema schema, ISchemaResolver schemaResolver, bool isNullable)
    {
        var schemas = schema.OneOf?.Count > 0 ? schema.OneOf : schema.AnyOf;
        if (schemas?.Count > 0)
            return MapTypeInfo(schemas[0], schemaResolver);
        return TypeInfo.Simple("FString", language: GenerationLanguage.Cpp);
    }

    /// <summary>
    /// Маппит примитивный тип OpenAPI в C++/UE тип
    /// </summary>
    private static string MapPrimitiveType(IOpenApiSchema schema)
    {
        if (!schema.Type.HasValue)
            return "FString";

        var type = schema.Type.Value & ~JsonSchemaType.Null;
        var format = schema.Format?.ToLowerInvariant();

        return type switch
        {
            JsonSchemaType.String => format switch
            {
                "date-time" => "FString",
                "date" => "FString",
                "time" => "FString",
                "uuid" => "FString",
                "binary" => "TArray<uint8>",
                "byte" => "TArray<uint8>",
                _ => "FString"
            },
            JsonSchemaType.Integer => format switch
            {
                "int32" => "int32",
                "int64" => "int64",
                _ => "int64"
            },
            JsonSchemaType.Number => format switch
            {
                "float" => "float",
                "double" => "double",
                _ => "double"
            },
            JsonSchemaType.Boolean => "bool",
            _ => "FString"
        };
    }
}
