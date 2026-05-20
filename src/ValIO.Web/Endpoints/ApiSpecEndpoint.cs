namespace ValIO.Web.Endpoints;

/// <summary>
/// GET /api/spec — hand-rolled mini-OpenAPI document describing the public REST
/// endpoints. We don't depend on Swashbuckle / Microsoft.AspNetCore.OpenApi to
/// avoid pulling in their <c>Microsoft.OpenApi 1.x/2.x</c> baseline which clashes
/// with the 3.x package the parser uses.
/// </summary>
public static class ApiSpecEndpoint
{
    private static readonly object Spec = new
    {
        openapi = "3.0.3",
        info = new
        {
            title = "ValIO API",
            version = "v1",
            description =
                "Programmatic access to the ValIO code generator. List supported languages, " +
                "describe their options, validate OpenAPI schemas, and generate client code as a zip.",
        },
        paths = new Dictionary<string, object>
        {
            ["/api/languages"] = new
            {
                get = new
                {
                    summary = "List supported target languages",
                    responses = new { _200 = new { description = "Array of LanguageDto" } }
                }
            },
            ["/api/options/{language}"] = new
            {
                get = new
                {
                    summary = "Describe plugin options for a language",
                    parameters = new[] { new { name = "language", @in = "path", required = true, schema = new { type = "string" } } },
                    responses = new { _200 = new { description = "Array of OptionDto" }, _404 = new { description = "Unknown language" } }
                }
            },
            ["/api/validate"] = new
            {
                post = new
                {
                    summary = "Parse and summarise an OpenAPI schema",
                    requestBody = new
                    {
                        required = true,
                        content = new Dictionary<string, object>
                        {
                            ["application/json"] = new
                            {
                                schema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        schemaUrl = new { type = "string", format = "uri" },
                                        schemaFileName = new { type = "string" },
                                        schemaContentBase64 = new { type = "string", format = "byte" }
                                    }
                                }
                            }
                        }
                    },
                    responses = new { _200 = new { description = "ValidateResponse JSON" } }
                }
            },
            ["/api/generate"] = new
            {
                post = new
                {
                    summary = "Generate code, stream result as zip",
                    requestBody = new
                    {
                        required = true,
                        content = new Dictionary<string, object>
                        {
                            ["application/json"] = new
                            {
                                schema = new
                                {
                                    type = "object",
                                    required = new[] { "languageId" },
                                    properties = new
                                    {
                                        languageId = new { type = "string" },
                                        schemaUrl = new { type = "string" },
                                        schemaFileName = new { type = "string" },
                                        schemaContentBase64 = new { type = "string", format = "byte" },
                                        @namespace = new { type = "string" },
                                        generateModels = new { type = "boolean" },
                                        generateClients = new { type = "boolean" },
                                        generateEnums = new { type = "boolean" },
                                        pluginOptions = new { type = "object", additionalProperties = true }
                                    }
                                }
                            }
                        }
                    },
                    responses = new
                    {
                        _200 = new
                        {
                            description = "Generated code archive",
                            content = new Dictionary<string, object>
                            {
                                ["application/zip"] = new { schema = new { type = "string", format = "binary" } }
                            }
                        }
                    }
                }
            },
            ["/healthz"] = new { get = new { summary = "Liveness probe", responses = new { _200 = new { description = "Healthy" } } } }
        }
    };

    public static IResult Handle() => Results.Json(Spec);
}
