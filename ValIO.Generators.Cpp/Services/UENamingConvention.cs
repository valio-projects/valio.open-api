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
    /// Имя файла модели: F{ModelName}.h
    /// </summary>
    public string ModelFileName(string modelName)
    {
        return $"{StructName(modelName)}.h";
    }

    /// <summary>
    /// Имя файла enum: E{EnumName}.h
    /// </summary>
    public string EnumFileName(string enumName)
    {
        return $"{EnumName(enumName)}.h";
    }

    /// <summary>
    /// Имя файла Params: F{OperationName}Params.h
    /// </summary>
    public string ParamsFileName(string operationName)
    {
        return $"{ParamsStructName(operationName)}.h";
    }

    /// <summary>
    /// Имя файла Result: F{OperationName}Result.h
    /// </summary>
    public string ResultFileName(string operationName)
    {
        return $"{ResultStructName(operationName)}.h";
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
