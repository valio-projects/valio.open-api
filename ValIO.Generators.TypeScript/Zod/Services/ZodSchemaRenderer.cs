using System.Collections.Generic;
using System.Linq;
using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Types;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Ast.Builders;
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
    private readonly bool _useBigInt;
    private readonly string _zodImportPath;

    public ZodSchemaRenderer(
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder,
        bool useBigInt = false,
        string zodVersion = "zod3")
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
        _useBigInt = useBigInt;
        _zodImportPath = zodVersion == "zod4" ? "zod/v4" : "zod";
    }

    private string ZodImportLine => $"import {{ z, ZodSchema }} from '{_zodImportPath}';";
    
    /// <summary>
    /// Рендерит Zod схему для IR модели
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <param name="importPath">Путь для импортов (опционально)</param>
    /// <param name="currentFileName">Имя текущего файла (для вычисления относительных путей)</param>
    /// <param name="schemasPath">Путь к папке со схемами (для вычисления относительных путей)</param>
    /// <returns>TypeScript код Zod схемы</returns>
    public string RenderFromIr(IrModel model, string? importPath = null, string? currentFileName = null, string? schemasPath = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        
        var sb = new StringBuilder(4096);
        
        var schemaName = $"{model.Name}Schema";
        var modelFileName = _namingConverter.ToCamelCase(model.Name);
        
        sb.AppendLine(ZodImportLine);
        sb.AppendLine();
        var hasConstraints = model.Properties.Any(p => p.Constraints != null && HasAnyConstraint(p.Constraints));
        if (hasConstraints)
        {
            sb.AppendLine($"import type {{ {model.Name} }} from \"./{modelFileName}\";");
            sb.AppendLine($"import {{ {model.Name}Constraints }} from \"./{modelFileName}\";");
        }
        else
        {
            sb.AppendLine($"import type {{ {model.Name} }} from \"./{modelFileName}\";");
        }
        
        var dependencies = ExtractDependencies(model);
        if (dependencies.Count > 0)
        {
            foreach (var dep in dependencies.OrderBy(d => d))
            {
                var depFileName = SchemaNameToFileName(dep);
                var relativePath = CalculateImportPath(currentFileName, depFileName, schemasPath, importPath);
                sb.AppendLine($"import {{ {dep} }} from '{relativePath}';");
            }
            sb.AppendLine();
        }
        
        if (!string.IsNullOrEmpty(model.Description))
        {
            CommentGenerator.GenerateJSDocComment(sb, 0, model.Description);
        }
        
        var zodBuilder = new ZodAstBuilder(_namingConverter, _typeBuilder, _useBigInt);
        var zodSchema = zodBuilder.BuildZodSchemaFromIrModel(model);
        
        var zodRenderer = new ZodAstRenderer();
        var zodSchemaCode = zodRenderer.RenderSchema(zodSchema);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {schemaName}: ZodSchema<{model.Name}> = {zodSchemaCode};");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Рендерит Zod схему для IR enum
    /// </summary>
    /// <param name="enumModel">IR enum</param>
    /// <param name="importPath">Путь для импортов (опционально)</param>
    /// <returns>TypeScript код Zod enum схемы</returns>
    public string RenderEnumFromIr(IrEnum enumModel, string? importPath = null, string? currentFileName = null, string? schemasPath = null)
    {
        if (enumModel == null)
            throw new ArgumentNullException(nameof(enumModel));
        
        var sb = new StringBuilder();
        
        var schemaName = $"{enumModel.Name}Schema";
        var typeName = enumModel.Name;
        var enumFileName = _namingConverter.ToCamelCase(enumModel.Name);
        var isStringEnum = enumModel.BaseType == IrEnumBaseType.String;
        var constArrayName = $"{typeName}Values";
        
        sb.AppendLine(ZodImportLine);
        sb.AppendLine();
        sb.AppendLine($"import type {{ {typeName} }} from \"./{enumFileName}\";");
        sb.AppendLine();
        
        // Строим Zod AST из IR enum
        var zodBuilder = new ZodAstBuilder(_namingConverter, _typeBuilder, _useBigInt);
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
        
        var zodRenderer = new ZodAstRenderer();
        var zodEnumCode = zodRenderer.VisitZodEnum(zodEnum);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {schemaName}: ZodSchema<{typeName}> = {zodEnumCode};");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Извлекает зависимости из IR модели
    /// </summary>
    private static bool HasAnyConstraint(IrPropertyConstraints c)
    {
        return c.Min.HasValue || c.Max.HasValue || c.ExclusiveMin.HasValue || c.ExclusiveMax.HasValue
            || c.MinLength.HasValue || c.MaxLength.HasValue || !string.IsNullOrEmpty(c.Pattern);
    }

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
    
    /// <summary>
    /// Вычисляет относительный путь для импорта схемы.
    /// При структуре models/modelName/ — из currentModel к depModel: ../depModel/depModel.schema
    /// </summary>
    private string CalculateImportPath(string? currentFileName, string dependencyFileName, string? schemasPath, string? importPath)
    {
        if (!string.IsNullOrEmpty(importPath))
        {
            var normalizedPath = NormalizePath(importPath);
            return $"{normalizedPath}/{dependencyFileName}/{dependencyFileName}.schema";
        }
        
        if (!string.IsNullOrEmpty(schemasPath) && !string.IsNullOrEmpty(currentFileName))
        {
            var modelsPath = NormalizePath(schemasPath);
            var currentBasePath = $"{modelsPath}/{currentFileName}";
            var dependencyDir = $"{modelsPath}/{dependencyFileName}";
            var relativePath = CalculateRelativePath(currentBasePath, dependencyDir, $"{dependencyFileName}.schema");
            return NormalizePath(relativePath);
        }
        
        return $"./{dependencyFileName}.schema";
    }
    
    /// <summary>
    /// Вычисляет относительный путь между двумя файлами
    /// </summary>
    private string CalculateRelativePath(string fromPath, string toPath, string fileName)
    {
        fromPath = NormalizePath(fromPath);
        toPath = NormalizePath(toPath);
        
        // Если пути одинаковые, возвращаем просто имя файла
        if (fromPath.Equals(toPath, StringComparison.OrdinalIgnoreCase))
        {
            return $"./{fileName}";
        }
        
        // Упрощенная логика: если оба пути в одной папке, используем относительный путь
        var fromParts = fromPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var toParts = toPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Находим общий префикс
        int commonLength = 0;
        int minLength = Math.Min(fromParts.Length, toParts.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (fromParts[i].Equals(toParts[i], StringComparison.OrdinalIgnoreCase))
            {
                commonLength++;
            }
            else
            {
                break;
            }
        }
        
        // Если есть общий префикс, строим относительный путь
        if (commonLength > 0)
        {
            var upLevels = fromParts.Length - commonLength;
            var downPath = string.Join("/", toParts.Skip(commonLength));
            var relativePath = string.Join("/", Enumerable.Repeat("..", upLevels));
            if (!string.IsNullOrEmpty(downPath))
            {
                relativePath = string.IsNullOrEmpty(relativePath) ? downPath : $"{relativePath}/{downPath}";
            }
            return $"{relativePath}/{fileName}";
        }
        
        // Если нет общего префикса, используем абсолютный путь
        return $"{toPath}/{fileName}";
    }
    
    /// <summary>
    /// Нормализует путь (убирает двойные слеши, заменяет обратные слеши)
    /// </summary>
    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";
        
        return path
            .Replace('\\', '/')
            .Replace("//", "/")
            .TrimEnd('/');
    }

    /// <summary>
    /// Рендерит Zod схемы для params операции (RouteParamsSchema, QueryParamsSchema)
    /// </summary>
    public string RenderParamsSchemasFromOperation(
        IrOperation operation,
        string? operationFileName = null,
        string? modelsPath = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var sb = new StringBuilder();
        var opName = _namingConverter.ToPascalCase(operation.MethodName);
        var zodBuilder = new ZodAstBuilder(_namingConverter, _typeBuilder, _useBigInt);
        var zodRenderer = new ZodAstRenderer();
        var paramsAst = new TypeScriptParamsAstBuilder(_namingConverter, _typeBuilder);

        var hasRoute = operation.PathParameters.Count > 0;
        var hasQuery = operation.QueryParameters.Count > 0;
        var rb = operation.RequestBody;
        var hasBody = rb != null && rb.IsJson;
        var hasForm = rb != null && rb.IsForm;
        var hasHeaders = operation.HeaderParameters.Count > 0;
        var groupCount = (hasRoute ? 1 : 0) + (hasQuery ? 1 : 0) + (hasBody ? 1 : 0) + (hasForm ? 1 : 0) + (hasHeaders ? 1 : 0);
        var useFlatOnly = groupCount <= 1;

        string? routeInterfaceForGeneric = null;
        if (hasRoute)
        {
            if (!useFlatOnly)
                routeInterfaceForGeneric = paramsAst.RouteParamsInterfaceName(operation.MethodName);
            else if (groupCount == 1)
                routeInterfaceForGeneric = paramsAst.ParamsInterfaceName(operation.MethodName);
        }

        string? queryInterfaceForGeneric = null;
        if (hasQuery)
        {
            if (!useFlatOnly)
                queryInterfaceForGeneric = paramsAst.QueryParamsInterfaceName(operation.MethodName);
            else if (groupCount == 1)
                queryInterfaceForGeneric = paramsAst.ParamsInterfaceName(operation.MethodName);
        }

        var fileBase = operationFileName ?? _namingConverter.ToCamelCase(operation.MethodName);
        var typesToImport = new List<string>();
        if (routeInterfaceForGeneric != null)
            typesToImport.Add(routeInterfaceForGeneric);
        if (queryInterfaceForGeneric != null && queryInterfaceForGeneric != routeInterfaceForGeneric)
            typesToImport.Add(queryInterfaceForGeneric);

        sb.AppendLine(ZodImportLine);
        if (typesToImport.Count > 0)
        {
            sb.AppendLine($"import type {{ {string.Join(", ", typesToImport.Distinct())} }} from './{fileBase}.params';");
        }
        sb.AppendLine();

        if (hasRoute)
        {
            var routeSchema = zodBuilder.BuildZodObjectFromParameters(operation.PathParameters);
            var routeSchemaCode = zodRenderer.Render(routeSchema);
            var routeGeneric = routeInterfaceForGeneric != null ? $"<{routeInterfaceForGeneric}>" : "";
            sb.AppendLine($"export const {opName}RouteParamsSchema: ZodSchema{routeGeneric} = {routeSchemaCode};");
            sb.AppendLine();
        }

        if (hasQuery)
        {
            var querySchema = zodBuilder.BuildZodObjectFromParameters(operation.QueryParameters);
            var querySchemaCode = zodRenderer.Render(querySchema);
            var queryGeneric = queryInterfaceForGeneric != null ? $"<{queryInterfaceForGeneric}>" : "";
            sb.AppendLine($"export const {opName}QueryParamsSchema: ZodSchema{queryGeneric} = {querySchemaCode};");
        }

        return sb.ToString();
    }
}
