using System.Text;
using System.Text.RegularExpressions;

namespace ValIO.Generators.Cpp.Services;

/// <summary>
/// Сервис именования для Unreal Engine C++.
/// Применяет UE-конвенции: F-префикс для структур, U-префикс для классов,
/// E-префикс для enum, PascalCase для свойств и методов.
/// </summary>
public sealed class UENamingConvention
{
    /// <summary>
    /// Имя структуры (F-префикс): FMyModelDto
    /// </summary>
    public string StructName(string modelName)
    {
        var pascal = ToPascalCase(modelName);
        return pascal.StartsWith('F') ? pascal : $"F{pascal}";
    }

    /// <summary>
    /// Имя класса (U-префикс): UMyAsyncAction
    /// </summary>
    public string ClassName(string name)
    {
        var pascal = ToPascalCase(name);
        return pascal.StartsWith('U') ? pascal : $"U{pascal}";
    }

    /// <summary>
    /// Имя enum (E-префикс): EMyStatus
    /// </summary>
    public string EnumName(string name)
    {
        var pascal = ToPascalCase(name);
        return pascal.StartsWith('E') ? pascal : $"E{pascal}";
    }

    /// <summary>
    /// Имя свойства/поля (PascalCase): MyProperty
    /// </summary>
    public string PropertyName(string name)
    {
        return ToPascalCase(name);
    }

    /// <summary>
    /// Имя типа делегата: FOn{OperationName}Completed
    /// </summary>
    public string DelegateName(string operationName)
    {
        return $"FOn{ToPascalCase(operationName)}Completed";
    }

    /// <summary>
    /// Имя результата: F{OperationName}Result
    /// </summary>
    public string ResultStructName(string operationName)
    {
        return $"F{ToPascalCase(operationName)}Result";
    }

    /// <summary>
    /// Имя Params-структуры: F{OperationName}Params
    /// </summary>
    public string ParamsStructName(string operationName)
    {
        return $"F{ToPascalCase(operationName)}Params";
    }

    /// <summary>
    /// Имя под-структуры Params для route: F{OperationName}RouteParams
    /// </summary>
    public string RouteParamsStructName(string operationName)
    {
        return $"F{ToPascalCase(operationName)}RouteParams";
    }

    /// <summary>
    /// Имя под-структуры Params для query: F{OperationName}QueryParams
    /// </summary>
    public string QueryParamsStructName(string operationName)
    {
        return $"F{ToPascalCase(operationName)}QueryParams";
    }

    /// <summary>
    /// Имя под-структуры Params для body: F{OperationName}BodyParams
    /// </summary>
    public string BodyParamsStructName(string operationName)
    {
        return $"F{ToPascalCase(operationName)}BodyParams";
    }

    /// <summary>
    /// Имя под-структуры Params для form: F{OperationName}FormParams
    /// </summary>
    public string FormParamsStructName(string operationName)
    {
        return $"F{ToPascalCase(operationName)}FormParams";
    }

    /// <summary>
    /// Имя нативной free-функции: {ClientName}_{MethodName}
    /// </summary>
    public string NativeFunctionName(string clientName, string methodName)
    {
        return $"{ToPascalCase(clientName)}_{ToPascalCase(methodName)}";
    }

    /// <summary>
    /// Имя функции Build (создание FValIORequest): {ClientName}_Build{MethodName}Request
    /// </summary>
    public string BuildRequestFunctionName(string clientName, string methodName)
    {
        return $"{ToPascalCase(clientName)}_Build{ToPascalCase(methodName)}Request";
    }

    /// <summary>
    /// Имя функции Send (отправка запроса): {ClientName}_Send{MethodName}
    /// </summary>
    public string SendFunctionName(string clientName, string methodName)
    {
        return $"{ToPascalCase(clientName)}_Send{ToPascalCase(methodName)}";
    }

    /// <summary>
    /// Имя поля тела ответа по status code: Body400, Body404, BodyDefault
    /// </summary>
    public string ResultBodyFieldName(string statusCode)
    {
        return statusCode == "default" ? "BodyDefault" : $"Body{statusCode}";
    }

    /// <summary>
    /// Имя Parse-функции: Parse{MethodName}Result
    /// </summary>
    public string ParseFunctionName(string methodName)
    {
        return $"Parse{ToPascalCase(methodName)}Result";
    }

    /// <summary>
    /// Имя async action класса: U{ClientName}{MethodName}Async
    /// </summary>
    public string AsyncActionClassName(string clientName, string methodName)
    {
        return $"U{ToPascalCase(clientName)}{ToPascalCase(methodName)}Async";
    }

    /// <summary>
    /// Имя PathsLibrary класса: U{ClientName}PathsLibrary
    /// </summary>
    public string PathsLibraryClassName(string clientName)
    {
        return $"U{ToPascalCase(clientName)}PathsLibrary";
    }

    /// <summary>
    /// Имя метода построения пути: Path{MethodName}
    /// </summary>
    public string PathMethodName(string methodName)
    {
        return $"Path{ToPascalCase(methodName)}";
    }

    /// <summary>
    /// Имя файла модели (без F-префикса): {ModelName}.h
    /// </summary>
    public string ModelFileName(string modelName)
    {
        return $"{ToPascalCase(modelName)}.h";
    }

    /// <summary>
    /// Путь include для модели (от корня output): Models/{ModelName}.h
    /// </summary>
    public string ModelIncludePath(string modelName)
    {
        return $"Models/{ModelFileName(modelName)}";
    }

    /// <summary>
    /// Путь include для enum (от корня output): Enums/{EnumName}.h
    /// </summary>
    public string EnumIncludePath(string enumName)
    {
        return $"Enums/{EnumFileName(enumName)}";
    }

    /// <summary>
    /// Имя файла enum (без E-префикса): {EnumName}.h
    /// </summary>
    public string EnumFileName(string enumName)
    {
        return $"{ToPascalCase(enumName)}.h";
    }

    /// <summary>
    /// Имя файла Params (без F-префикса): {OperationName}Params.h
    /// </summary>
    public string ParamsFileName(string operationName)
    {
        return $"{ToPascalCase(operationName)}Params.h";
    }

    /// <summary>
    /// Имя файла Result (без F-префикса): {OperationName}Result.h
    /// </summary>
    public string ResultFileName(string operationName)
    {
        return $"{ToPascalCase(operationName)}Result.h";
    }

    /// <summary>
    /// Путь include для Result (от корня output): Results/F{OperationName}Result.h
    /// </summary>
    public string ResultIncludePath(string operationName)
    {
        return $"Results/{ResultFileName(operationName)}";
    }

    /// <summary>
    /// Путь include для Params (от корня output): Params/F{OperationName}Params.h
    /// </summary>
    public string ParamsIncludePath(string operationName)
    {
        return $"Params/{ParamsFileName(operationName)}";
    }

    /// <summary>
    /// Имя файла endpoint (один метод = один файл): {MethodName}.h
    /// </summary>
    public string EndpointHeaderFileName(string methodName)
    {
        return $"{ToPascalCase(methodName)}.h";
    }

    /// <summary>
    /// Имя файла реализации endpoint: {MethodName}.cpp
    /// </summary>
    public string EndpointSourceFileName(string methodName)
    {
        return $"{ToPascalCase(methodName)}.cpp";
    }

    /// <summary>
    /// Имя заголовочного файла Paths: {ClientName}Paths.h
    /// </summary>
    public string PathsHeaderFileName(string clientName)
    {
        return $"{ToPascalCase(clientName)}Paths.h";
    }

    /// <summary>
    /// Имя файла реализации Paths: {ClientName}Paths.cpp
    /// </summary>
    public string PathsSourceFileName(string clientName)
    {
        return $"{ToPascalCase(clientName)}Paths.cpp";
    }

    /// <summary>
    /// Имя заголовочного файла API: {ClientName}Api.h
    /// </summary>
    public string ApiHeaderFileName(string clientName)
    {
        return $"{ToPascalCase(clientName)}Api.h";
    }

    /// <summary>
    /// Имя файла реализации API: {ClientName}Api.cpp
    /// </summary>
    public string ApiSourceFileName(string clientName)
    {
        return $"{ToPascalCase(clientName)}Api.cpp";
    }

    /// <summary>
    /// Category для Models: BaseCategory|Models
    /// </summary>
    public static string ModelCategory(string baseCategory) =>
        $"{baseCategory}|Models";

    /// <summary>
    /// Category для Enums: BaseCategory|Enums
    /// </summary>
    public static string EnumCategory(string baseCategory) =>
        $"{baseCategory}|Enums";

    /// <summary>
    /// Category для Operation-специфичных типов (Result, Params, AsyncAction): BaseCategory|ClientName|OperationId
    /// </summary>
    public static string OperationCategory(string baseCategory, string clientName, string operationId) =>
        $"{baseCategory}|{ToPascalCase(clientName)}|{ToPascalCase(operationId)}";

    /// <summary>
    /// Category для Paths Library: BaseCategory|ClientName|Paths
    /// </summary>
    public static string PathsCategory(string baseCategory, string clientName) =>
        $"{baseCategory}|{ToPascalCase(clientName)}|Paths";

    /// <summary>
    /// Формирует Category для типа модели: BlueprintCategory|Models
    /// </summary>
    public string ModelTypeCategory(string blueprintCategory) =>
        $"{blueprintCategory}|Models";

    /// <summary>
    /// Формирует Category для enum: BlueprintCategory|Enums
    /// </summary>
    public string EnumTypeCategory(string blueprintCategory) =>
        $"{blueprintCategory}|Enums";

    /// <summary>
    /// Формирует Category для operation-специфичных типов (Result, Params, AsyncAction): BlueprintCategory|ClientName|OperationId
    /// </summary>
    public string OperationTypeCategory(string blueprintCategory, string clientName, string operationId) =>
        $"{blueprintCategory}|{ToPascalCase(clientName)}|{ToPascalCase(operationId)}";

    /// <summary>
    /// Формирует Category для Paths Library: BlueprintCategory|ClientName|Paths
    /// </summary>
    public string PathsTypeCategory(string blueprintCategory, string clientName) =>
        $"{blueprintCategory}|{ToPascalCase(clientName)}|Paths";

    /// <summary>
    /// Конвертирует строку в PascalCase.
    /// Обрабатывает camelCase, snake_case, kebab-case.
    /// </summary>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Разбиваем по разделителям (_, -, пробел) и границам camelCase
        var words = Regex.Split(input, @"[-_\s]+|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
            .Where(w => !string.IsNullOrEmpty(w));

        var sb = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                    sb.Append(word[1..]);
            }
        }

        return sb.ToString();
    }
}
