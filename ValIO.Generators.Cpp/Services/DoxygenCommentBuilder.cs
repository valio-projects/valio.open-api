using System.Text;
using ValIO.Core.IR;

namespace ValIO.Generators.Cpp.Services;

/// <summary>
/// Builds Doxygen-style documentation comments for C++ UE code.
/// Uses tags: @brief, @param, @return, @note, @see, @details, @code / @endcode
/// </summary>
public static class DoxygenCommentBuilder
{
    /// <summary>
    /// Builds documentation for Library class (UBlueprintFunctionLibrary).
    /// </summary>
    public static string BuildLibraryClassDocumentation(IrOperation operation, string clientName)
    {
        var sb = new StringBuilder();
        var methodName = ToPascal(operation.MethodName);
        var httpMethod = operation.HttpMethod.ToUpperInvariant();
        var path = operation.Path;

        AppendLine(sb, "@brief Blueprint Function Library for API operation {0}.", methodName);
        AppendLine(sb, "");
        AppendLine(sb, "@details Provides BuildPathUrl, Parse, Build, BuildRequestBuilder, and Send methods.");
        AppendLine(sb, "Operation: {0} {1}", httpMethod, path);
        if (!string.IsNullOrWhiteSpace(operation.Summary))
            AppendLine(sb, operation.Summary);
        if (!string.IsNullOrWhiteSpace(operation.Description))
            AppendLine(sb, operation.Description);
        AppendLine(sb, "");
        AppendLine(sb, "@see Build, BuildRequestBuilder, Send");
        AppendLine(sb, "@see U{0}{1}Async", ToPascal(clientName), ToPascal(operation.MethodName));
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for BuildPathUrl method.
    /// </summary>
    public static string BuildBuildPathUrlDocumentation(IrOperation operation, string paramsTypeName)
    {
        var sb = new StringBuilder();
        var httpMethod = operation.HttpMethod.ToUpperInvariant();
        var path = operation.Path;

        AppendLine(sb, "@brief Builds the URL path for this operation.");
        AppendLine(sb, "");
        AppendLine(sb, "@details Substitutes path parameters from Params into {0} {1}", httpMethod, path);
        AppendParam(sb, "Params", $"Parameters for {operation.MethodName} operation (Route or flat path fields)");
        AppendReturn(sb, "The constructed path string");
        AppendLine(sb, "");
        AppendLine(sb, "@see {0}", paramsTypeName);
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Parse method.
    /// </summary>
    public static string BuildParseDocumentation(IrOperation operation, string resultTypeName)
    {
        var sb = new StringBuilder();
        var methodName = ToPascal(operation.MethodName);

        AppendLine(sb, "@brief Parses FValIOResponse into {0}.", resultTypeName);
        AppendLine(sb, "");
        AppendParam(sb, "Response", "Raw HTTP response from the API");
        AppendReturn(sb, $"Parsed result struct with StatusCode, Response, SuccessBody, and error bodies (e.g. Body400)");
        AppendLine(sb, "");
        AppendLine(sb, "@see Send, {0}", resultTypeName);
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Build method.
    /// </summary>
    public static string BuildBuildDocumentation(IrOperation operation, string paramsTypeName)
    {
        var sb = new StringBuilder();
        var httpMethod = operation.HttpMethod.ToUpperInvariant();
        var path = operation.Path;

        AppendLine(sb, "@brief Builds FValIORequestBuilder from parameters.");
        AppendLine(sb, "");
        AppendLine(sb, "@details Constructs request for {0} {1}. Sets path, query params, verb, and request name.", httpMethod, path);
        AppendParam(sb, "Params", $"Parameters for {operation.MethodName} operation");
        AppendReturn(sb, "FValIORequestBuilder ready for Build() and Send");
        AppendLine(sb, "");
        AppendLine(sb, "@see BuildPathUrl, BuildRequestBuilder, Send");
        AppendLine(sb, "@see {0}", paramsTypeName);
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for BuildRequestBuilder method (Blueprint).
    /// </summary>
    public static string BuildBuildRequestBuilderDocumentation(IrOperation operation, string paramsTypeName)
    {
        var sb = new StringBuilder();
        var methodName = ToPascal(operation.MethodName);

        AppendLine(sb, "@brief Builds UValIORequestBuilder from Params (Blueprint-callable).");
        AppendLine(sb, "");
        AppendLine(sb, "@details Uses same logic as Build(); wraps result in UValIORequestBuilder via From().");
        AppendLine(sb, "Use from Blueprint to get a UObject* request builder for further chaining.");
        AppendParam(sb, "Params", $"Parameters for {methodName} operation");
        AppendReturn(sb, "UValIORequestBuilder* (Blueprint-compatible, caller owns lifetime)");
        AppendLine(sb, "");
        AppendLine(sb, "@see Build, Send");
        AppendLine(sb, "@see UValIORequestBuilder::From");
        AppendLine(sb, "");
        AppendCode(sb, "// Blueprint usage: BuildRequestBuilder(Params) -> chain Set* -> Build -> Send");
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Send method.
    /// </summary>
    public static string BuildSendDocumentation(IrOperation operation, string resultTypeName, string paramsTypeName, string libraryClassName)
    {
        var sb = new StringBuilder();

        AppendLine(sb, "@brief Sends the request and returns TFuture with parsed result.");
        AppendLine(sb, "");
        AppendLine(sb, "@details Calls Build, then Client->Send, then parses response via Parse.");
        AppendParam(sb, "Client", "ValIO HTTP client reference");
        AppendParam(sb, "Params", $"Parameters for {operation.MethodName} operation");
        AppendReturn(sb, "TFuture<{0}> with parsed result", resultTypeName);
        AppendLine(sb, "");
        AppendLine(sb, "@see Build, Parse");
        AppendLine(sb, "@see {0} for Blueprint async action", libraryClassName.Replace("Library", "Async"));
        AppendLine(sb, "");
        AppendCode(sb, "// C++ usage:\n// Library::Send(Client, Params).Next([](const FResult& R) { ... });");
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Async class (UValIOAsyncActionBase).
    /// </summary>
    public static string BuildAsyncClassDocumentation(IrOperation operation, string clientName)
    {
        var sb = new StringBuilder();
        var methodName = ToPascal(operation.MethodName);
        var httpMethod = operation.HttpMethod.ToUpperInvariant();
        var path = operation.Path;

        AppendLine(sb, "@brief Blueprint async action for {0}.", methodName);
        AppendLine(sb, "");
        AppendLine(sb, "@details Operation: {0} {1}", httpMethod, path);
        if (!string.IsNullOrWhiteSpace(operation.Summary))
            AppendLine(sb, operation.Summary);
        AppendLine(sb, "");
        AppendLine(sb, "@see OnCompleted, LastResult");
        AppendLine(sb, "@see U{0}{1}Library::Send", ToPascal(clientName), methodName);
        AppendLine(sb, "");
        AppendCode(sb, $"// Blueprint: {methodName}(WorldContext, ClientName, Params) -> bind OnCompleted");
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for async factory method.
    /// </summary>
    public static string BuildAsyncFactoryDocumentation(IrOperation operation, string paramsTypeName)
    {
        var sb = new StringBuilder();
        var methodName = ToPascal(operation.MethodName);

        AppendLine(sb, "@brief Creates and returns an async action instance for {0}.", methodName);
        AppendLine(sb, "");
        AppendParam(sb, "WorldContextObject", "World context (for latent action, typically GetWorld())");
        AppendParam(sb, "ClientName", "Name of the ValIO client registered in game instance");
        AppendParam(sb, "Params", $"Parameters for {methodName} operation");
        AppendReturn(sb, "New async action instance; bind OnCompleted and call Activate()");
        AppendLine(sb, "");
        AppendLine(sb, "@see Activate, OnCompleted");
        AppendLine(sb, "@see {0}", paramsTypeName);
        AppendLine(sb, "");
        AppendCode(sb, $"// Blueprint: {methodName} node -> Client Name, Params -> On Completed");
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Params aggregate struct.
    /// </summary>
    public static string BuildParamsStructDocumentation(IrOperation operation, string paramsName, string methodNamePascal, bool hasBody)
    {
        var sb = new StringBuilder();
        var httpMethod = operation.HttpMethod.ToUpperInvariant();
        var path = operation.Path;

        AppendLine(sb, "@brief Parameters for {0} API operation.", methodNamePascal);
        AppendLine(sb, "");
        AppendLine(sb, "@details Operation: {0} {1}", httpMethod, path);
        if (hasBody)
            AppendLine(sb, "Includes Route, Query, and Body/Form parameters.");
        if (!string.IsNullOrWhiteSpace(operation.Summary))
            AppendLine(sb, operation.Summary);
        if (!string.IsNullOrWhiteSpace(operation.Description))
            AppendLine(sb, operation.Description);
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Route params struct.
    /// </summary>
    public static string BuildRouteParamsDocumentation(IrOperation operation, string methodNamePascal)
    {
        var sb = new StringBuilder();
        var path = operation.Path;

        AppendLine(sb, "@brief Path parameters for {0}.", methodNamePascal);
        AppendLine(sb, "");
        AppendLine(sb, "@details Values are substituted into URL path placeholders (e.g. {0}).", path);
        AppendLine(sb, "");
        AppendLine(sb, "@see Params.Route");
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Query params struct.
    /// </summary>
    public static string BuildQueryParamsDocumentation(IrOperation operation, string methodNamePascal)
    {
        var sb = new StringBuilder();

        AppendLine(sb, "@brief Query parameters for {0}.", methodNamePascal);
        AppendLine(sb, "");
        AppendLine(sb, "@details Appended to the URL as ?key=value pairs.");
        AppendLine(sb, "");
        AppendLine(sb, "@see Params.Query");
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Body/Form params struct.
    /// </summary>
    public static string BuildBodyParamsDocumentation(string? schemaDescription, string methodNamePascal, string defaultText)
    {
        var sb = new StringBuilder();

        AppendLine(sb, "@brief {0} for {1}.", defaultText, methodNamePascal);
        AppendLine(sb, "");
        if (!string.IsNullOrWhiteSpace(schemaDescription))
            AppendLine(sb, schemaDescription);
        return sb.ToString();
    }

    /// <summary>
    /// Builds documentation for Result struct (F{Method}Result).
    /// </summary>
    public static string BuildResultStructDocumentation(IrOperation operation, string resultTypeName)
    {
        var sb = new StringBuilder();
        var methodName = ToPascal(operation.MethodName);

        AppendLine(sb, "@brief Result of {0} operation.", methodName);
        AppendLine(sb, "");
        AppendLine(sb, "@details Contains StatusCode, Response, SuccessBody, and error bodies.");
        AppendLine(sb, "Use IsSuccess() to check if StatusCode is 2xx.");
        if (!string.IsNullOrWhiteSpace(operation.Summary))
            AppendLine(sb, operation.Summary);
        AppendLine(sb, "");
        AppendLine(sb, "@see Parse, Send");
        return sb.ToString();
    }

    private static readonly UENamingConvention _naming = new();

    private static string ToPascal(string input) => UENamingConvention.ToPascalCase(input);

    private static void AppendLine(StringBuilder sb, string format, params object[] args)
    {
        sb.AppendLine(args.Length > 0 ? string.Format(format, args) : format);
    }

    private static void AppendParam(StringBuilder sb, string name, string description)
    {
        sb.AppendLine($"@param {name} {description}");
    }

    private static void AppendReturn(StringBuilder sb, string description, params object[] args)
    {
        sb.AppendLine(args.Length > 0 ? string.Format($"@return {description}", args) : $"@return {description}");
    }

    private static void AppendCode(StringBuilder sb, string example)
    {
        sb.AppendLine("@code");
        foreach (var line in example.Split('\n'))
            sb.AppendLine(line);
        sb.AppendLine("@endcode");
    }
}
