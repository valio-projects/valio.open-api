using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Zod.Ast.Builders;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;
using ValIO.Generators.TypeScript.Zod.Ast.Renderers;

namespace ValIO.Generators.TypeScript.Zod;

/// <summary>
/// Рендерер для генерации Zod схем валидации через IR и AST
/// </summary>
public class ZodSchemaRenderer
{
    private readonly INamingConverter _namingConverter;
    private readonly Func<IrType, ValIO.Core.Ast.Nodes.TypeNode> _typeBuilder;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="namingConverter">Конвертер имен</param>
    /// <param name="typeBuilder">Функция для построения TypeNode из IrType</param>
    public ZodSchemaRenderer(
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
    }
    
    /// <summary>
    /// Рендерит Zod схему для IR модели
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <param name="importPath">Путь для импортов (опционально)</param>
    /// <returns>TypeScript код Zod схемы</returns>
    public string RenderFromIr(IrModel model, string? importPath = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        
        var sb = new StringBuilder(4096);
        
        var schemaName = $"{model.Name}Schema";
        
        // Генерируем импорты
        sb.AppendLine("import { z } from 'zod';");
        
        // Собираем зависимости из свойств модели
        var dependencies = ExtractDependencies(model);
        if (dependencies.Count > 0)
        {
            foreach (var dep in dependencies.OrderBy(d => d))
            {
                var depFileName = SchemaNameToFileName(dep);
                var importPathStr = !string.IsNullOrEmpty(importPath) ? $"{importPath}/" : "./";
                sb.AppendLine($"import {{ {dep} }} from '{importPathStr}{depFileName}.schema';");
            }
            sb.AppendLine();
        }
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(model.Description))
        {
            CommentGenerator.GenerateJSDocComment(sb, 0, model.Description);
        }
        
        // Строим Zod AST из IR модели
        var zodBuilder = new ZodAstBuilder(_namingConverter, _typeBuilder);
        var zodObject = zodBuilder.BuildZodObject(model);
        
        // Рендерим AST в код
        var zodRenderer = new ZodAstRenderer();
        var zodObjectCode = zodRenderer.Render(zodObject);
        
        // Генерируем экспорт схемы
        CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = {zodObjectCode};");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Рендерит Zod схему для IR enum
    /// </summary>
    /// <param name="enumModel">IR enum</param>
    /// <param name="importPath">Путь для импортов (опционально)</param>
    /// <returns>TypeScript код Zod enum схемы</returns>
    public string RenderEnumFromIr(IrEnum enumModel, string? importPath = null)
    {
        if (enumModel == null)
            throw new ArgumentNullException(nameof(enumModel));
        
        var sb = new StringBuilder();
        
        var schemaName = $"{enumModel.Name}Schema";
        var typeName = enumModel.Name;
        var isStringEnum = enumModel.BaseType == IrEnumBaseType.String;
        var constArrayName = $"{typeName}Values";
        
        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();
        
        // Строим Zod AST из IR enum
        var zodBuilder = new ZodAstBuilder(_namingConverter, _typeBuilder);
        var zodEnum = zodBuilder.BuildZodEnum(enumModel);
        
        // Генерируем массив значений enum
        if (isStringEnum)
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {constArrayName} = [");
            for (int i = 0; i < enumModel.Values.Count; i++)
            {
                var value = enumModel.Values[i];
                var comma = i < enumModel.Values.Count - 1 ? "," : "";
                var valueString = FormatEnumValue(value.Value?.ToString() ?? "", "string");
                CodeFormatter.AppendLine(sb, 1, $"{valueString}{comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "] as const satisfies readonly string[];");
            CodeFormatter.AppendEmptyLine(sb);
        }
        else
        {
            CodeFormatter.AppendLine(sb, 0, $"export const {constArrayName} = [");
            for (int i = 0; i < enumModel.Values.Count; i++)
            {
                var value = enumModel.Values[i];
                var comma = i < enumModel.Values.Count - 1 ? "," : "";
                var valueString = value.Value?.ToString() ?? "0";
                CodeFormatter.AppendLine(sb, 1, $"{valueString}{comma}");
            }
            CodeFormatter.AppendLine(sb, 0, "] as const satisfies readonly number[];");
            CodeFormatter.AppendEmptyLine(sb);
        }
        
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(enumModel.Description))
        {
            CommentGenerator.GenerateJSDocComment(sb, 0, enumModel.Description);
        }
        
        // Рендерим Zod enum схему
        var zodRenderer = new ZodAstRenderer();
        var zodEnumCode = zodRenderer.VisitZodEnum(zodEnum);
        
        // Генерируем экспорт схемы
        CodeFormatter.AppendLine(sb, 0, $"export const {schemaName} = {zodEnumCode};");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Извлекает зависимости из IR модели
    /// </summary>
    private IReadOnlyList<string> ExtractDependencies(IrModel model)
    {
        var dependencies = new HashSet<string>();
        
        foreach (var property in model.Properties)
        {
            ExtractDependenciesFromIrType(property.Type, dependencies, model.Name);
        }
        
        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
            {
                ExtractDependenciesFromIrType(type, dependencies, model.Name);
            }
        }
        
        return dependencies.ToList();
    }
    
    /// <summary>
    /// Рекурсивно извлекает зависимости из IR типа
    /// </summary>
    private void ExtractDependenciesFromIrType(
        ValIO.Core.IR.Types.IrType irType, 
        HashSet<string> dependencies, 
        string currentModelName)
    {
        switch (irType)
        {
            case ValIO.Core.IR.Types.IrModelReference modelRef:
                if (!modelRef.ModelName.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
                {
                    var schemaName = $"{modelRef.ModelName}Schema";
                    dependencies.Add(schemaName);
                }
                break;
            case ValIO.Core.IR.Types.IrArrayType arrayType:
                ExtractDependenciesFromIrType(arrayType.ElementType, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrDictionaryType dictType:
                ExtractDependenciesFromIrType(dictType.KeyType, dependencies, currentModelName);
                ExtractDependenciesFromIrType(dictType.ValueType, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrUnionType unionType:
                foreach (var t in unionType.Types)
                    ExtractDependenciesFromIrType(t, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrIntersectionType intersectionType:
                foreach (var t in intersectionType.Types)
                    ExtractDependenciesFromIrType(t, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrNullableType nullableType:
                ExtractDependenciesFromIrType(nullableType.InnerType, dependencies, currentModelName);
                break;
            case ValIO.Core.IR.Types.IrOptionalType optionalType:
                ExtractDependenciesFromIrType(optionalType.InnerType, dependencies, currentModelName);
                break;
        }
    }
    
    private string FormatEnumValue(string valueString, string underlyingType)
    {
        if (underlyingType == "string")
        {
            if (valueString.StartsWith("\"") && valueString.EndsWith("\""))
            {
                return valueString;
            }
            return $"\"{valueString}\"";
        }
        return valueString;
    }
    
    private string SchemaNameToFileName(string schemaName)
    {
        var typeName = schemaName.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) 
            ? schemaName.Substring(0, schemaName.Length - 6) 
            : schemaName;
            
        if (string.IsNullOrEmpty(typeName))
            return schemaName.ToLowerInvariant();
        
        return _namingConverter.ToCamelCase(typeName);
    }
}
