using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Builders;
using ValIO.Core.Services;
using ValIO.Parsers.Interfaces;

namespace ValIO.Cli.Commands;

/// <summary>
/// Validates an OpenAPI schema and its optional SignalR extension.
/// </summary>
public sealed class ValidateCommand : Command
{
    /// <summary>Creates a validation command using the same schema lowering as generation.</summary>
    /// <param name="serviceProvider">The shared command services.</param>
    public ValidateCommand(IServiceProvider serviceProvider)
        : base("validate", "Validate OpenAPI schema")
    {
        var schemaOption = new Option<string>("--schema", "-s")
        {
            Description = "Path or URL to OpenAPI schema file",
            Arity = ArgumentArity.ExactlyOne
        };

        Options.Add(schemaOption);

        SetAction(async (ParseResult parseResult) =>
        {
            var schema = parseResult.GetValue(schemaOption);
            if (string.IsNullOrWhiteSpace(schema))
            {
                await Console.Error.WriteLineAsync("Schema path or URL is required");
                return 1;
            }

            var logger = serviceProvider.GetRequiredService<ILogger<ValidateCommand>>();
            var parser = serviceProvider.GetRequiredService<IOpenApiDocumentParser>();

            try
            {
                logger.LogInformation("Validating OpenAPI schema from: {Schema}", schema);

                var document = Uri.TryCreate(schema, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "file"
                    ? await parser.ParseFromUrlAsync(schema)
                    : await parser.ParseFromFileAsync(schema);

                var resolver = new CachingSchemaResolver(new SchemaResolver(document));
                var builder = new IrBuilder(resolver, serviceProvider.GetRequiredService<INamingConverter>());
                builder.ValidateSignalR(document);

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
        });
    }
}
