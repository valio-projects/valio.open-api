using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Parsers.Interfaces;

namespace ValIO.CLI;

/// <summary>
/// Команда валидации OpenAPI схемы
/// </summary>
public class ValidateCommand : Command
{
    public ValidateCommand(IServiceProvider serviceProvider)
        : base("validate", "Validate OpenAPI schema")
    {
        var schemaOption = new Option<string>(
            aliases: new[] { "--schema", "-s" },
            description: "Path or URL to OpenAPI schema file")
        {
            IsRequired = true
        };

        this.AddOption(schemaOption);

        this.SetHandler(async (schema) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ValidateCommand>>();
            var parser = serviceProvider.GetRequiredService<IOpenApiDocumentParser>();

            try
            {
                logger.LogInformation("Validating OpenAPI schema from: {Schema}", schema);

                var document = Uri.TryCreate(schema, UriKind.Absolute, out _)
                    ? await parser.ParseFromUrlAsync(schema)
                    : await parser.ParseFromFileAsync(schema);

                logger.LogInformation("OpenAPI schema is valid");
                logger.LogInformation("API Title: {Title}", document.Info?.Title);
                logger.LogInformation("API Version: {Version}", document.Info?.Version);
                logger.LogInformation("Paths count: {Count}", document.Paths?.Count ?? 0);

                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OpenAPI schema validation failed");
                return 1;
            }
        }, schemaOption);
    }
}

