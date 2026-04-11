using FluentAssertions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using NUnit.Framework;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Models;
using System.IO;
using System.Linq;

namespace ValIO.Generators.TypeScript.Tests;

/// <summary>
/// Интеграционные тесты для TypeScript генератора
/// </summary>
[TestFixture]
public class TypeScriptCodeGeneratorIntegrationTests
{
    private TypeScriptCodeGenerator _generator = null!;
    private string _testDataPath = null!;

    [SetUp]
    public void Setup()
    {
        _testDataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
        
        // Создаем минимальные зависимости для генератора
        var typeMapper = new TypeScriptTypeMapper(new Core.Services.NamingConverter());
        var namingConverter = new Core.Services.NamingConverter();
        var fileWriter = new Core.Services.FileWriter();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TypeScriptCodeGenerator>.Instance;
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        
        // TODO: SchemaGeneratorFactory и ISchemaGenerator временно отключены из-за циклических зависимостей
        // var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        // serviceCollection.AddSingleton<INamingConverter>(namingConverter);
        // serviceCollection.AddSingleton<ValIO.Generators.TypeScript.Zod.ZodTypeMapper>();
        // serviceCollection.AddSingleton<ValIO.Core.Interfaces.ISchemaGenerator>(sp =>
        // {
        //     var zodTypeMapper = new ValIO.Generators.TypeScript.Zod.ZodTypeMapper(namingConverter);
        //     return new ValIO.Generators.TypeScript.Zod.ZodSchemaRenderer(
        //         namingConverter,
        //         zodTypeMapper);
        // });
        // var serviceProvider = serviceCollection.BuildServiceProvider();
        var serviceProvider = (IServiceProvider?)null;
        
        _generator = new TypeScriptCodeGenerator(
            typeMapper,
            namingConverter,
            fileWriter,
            logger,
            loggerFactory);
    }

    [Test]
    public async Task GenerateFromSimpleSchema_WithInterfaces_GeneratesValidCode()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "simple-schema.json");
        var schemaContent = await File.ReadAllTextAsync(schemaPath);
        
        await using var stream = File.OpenRead(schemaPath);
        var readResult = await OpenApiDocument.LoadAsync(stream, "json");
        var document = readResult.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
        
        var config = new GenerationConfiguration
        {
            Language = GenerationLanguage.TypeScript,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            Options = new GenerationOptions
            {
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    ModelFormat = ModelFormat.Interfaces,
                    HttpTransports = TypeScriptHttpTransports.Fetch
                }
            },
            FileOrganization = new Core.Models.FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "api"
            }
        };

        // Act
        var result = await _generator.GenerateAsync(document, config);

        // Assert
        if (!result.Success)
        {
            var errors = string.Join("; ", result.Errors);
            var warnings = string.Join("; ", result.Warnings);
            Assert.Fail($"Generation failed. Errors: {errors}. Warnings: {warnings}");
        }
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.GeneratedFiles.Should().NotBeEmpty();
        
        // Проверяем, что сгенерированы модели
        var modelFiles = result.GeneratedFiles.Keys.Where(k => k.Contains(".model.ts")).ToList();
        modelFiles.Should().NotBeEmpty("Should generate model files");
        
        // Проверяем, что сгенерированы fetch-клиенты
        var clientFiles = result.GeneratedFiles.Keys.Where(k => k.Contains(".fetch.ts")).ToList();
        clientFiles.Should().NotBeEmpty("Should generate fetch client files");
        
        // Проверяем содержимое модели
        var userModelFile = modelFiles.FirstOrDefault(f => f.Contains("user.model.ts"));
        userModelFile.Should().NotBeNull("Should generate User model");
        
        if (userModelFile != null)
        {
            var userModelContent = result.GeneratedFiles[userModelFile];
            userModelContent.Should().Contain("export interface User");
            userModelContent.Should().Contain("id:");
            userModelContent.Should().Contain("name:");
            userModelContent.Should().Contain("email:");
        }
        
        // Проверяем содержимое клиента
        var clientFile = clientFiles.FirstOrDefault();
        clientFile.Should().NotBeNull("Should generate client file");
        
        if (clientFile != null)
        {
            var clientContent = result.GeneratedFiles[clientFile];
            clientContent.Should().Contain("export async function");
            clientContent.Should().Contain("getUsers");
        }
    }

    [Test]
    public async Task GenerateFromSimpleSchema_WithZodSchemas_GeneratesValidCode()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "simple-schema.json");
        var schemaContent = await File.ReadAllTextAsync(schemaPath);
        
        await using var stream = File.OpenRead(schemaPath);
        var readResult = await OpenApiDocument.LoadAsync(stream, "json");
        var document = readResult.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
        
        var config = new GenerationConfiguration
        {
            Language = GenerationLanguage.TypeScript,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            Options = new GenerationOptions
            {
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    ModelFormat = ModelFormat.ValidationSchemas,
                    ValidationLibrary = ValidationLibrary.Zod,
                    HttpTransports = TypeScriptHttpTransports.Fetch
                }
            },
            FileOrganization = new Core.Models.FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "api"
            }
        };

        // Act
        var result = await _generator.GenerateAsync(document, config);

        // Assert
        if (!result.Success)
        {
            var errors = string.Join("; ", result.Errors);
            var warnings = string.Join("; ", result.Warnings);
            Assert.Fail($"Generation failed. Errors: {errors}. Warnings: {warnings}");
        }
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.GeneratedFiles.Should().NotBeEmpty();
        
        // Проверяем, что сгенерированы Zod схемы
        var schemaFiles = result.GeneratedFiles.Keys.Where(k => k.Contains(".schema.ts")).ToList();
        schemaFiles.Should().NotBeEmpty("Should generate schema files");
        
        // Проверяем содержимое схемы
        var userSchemaFile = schemaFiles.FirstOrDefault(f => f.Contains("user.schema.ts"));
        userSchemaFile.Should().NotBeNull("Should generate User schema");
        
        if (userSchemaFile != null)
        {
            var userSchemaContent = result.GeneratedFiles[userSchemaFile];
            userSchemaContent.Should().Contain("import { z } from 'zod'");
            userSchemaContent.Should().Contain("export const UserSchema");
            userSchemaContent.Should().Contain("z.object");
        }
    }

    [Test]
    public async Task GenerateFromSimpleSchema_WithAxios_GeneratesValidCode()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "simple-schema.json");
        var schemaContent = await File.ReadAllTextAsync(schemaPath);
        
        await using var stream = File.OpenRead(schemaPath);
        var readResult = await OpenApiDocument.LoadAsync(stream, "json");
        var document = readResult.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
        
        var config = new GenerationConfiguration
        {
            Language = GenerationLanguage.TypeScript,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            Options = new GenerationOptions
            {
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    ModelFormat = ModelFormat.Interfaces,
                    HttpTransports = TypeScriptHttpTransports.Axios
                }
            },
            FileOrganization = new Core.Models.FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "api"
            }
        };

        // Act
        var result = await _generator.GenerateAsync(document, config);

        // Assert
        if (!result.Success)
        {
            var errors = string.Join("; ", result.Errors);
            var warnings = string.Join("; ", result.Warnings);
            Assert.Fail($"Generation failed. Errors: {errors}. Warnings: {warnings}");
        }
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        
        // Проверяем содержимое клиента с Axios
        var clientFiles = result.GeneratedFiles.Keys.Where(k => k.Contains(".axios.ts")).ToList();
        clientFiles.Should().NotBeEmpty();
        
        var clientFile = clientFiles.FirstOrDefault();
        if (clientFile != null)
        {
            var clientContent = result.GeneratedFiles[clientFile];
            clientContent.Should().Contain("import axios");
            clientContent.Should().Contain("AxiosInstance");
        }
    }

    [Test]
    public async Task GenerateFromSimpleSchema_WithTanStackQuery_GeneratesValidCode()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "simple-schema.json");
        var schemaContent = await File.ReadAllTextAsync(schemaPath);
        
        await using var stream = File.OpenRead(schemaPath);
        var readResult = await OpenApiDocument.LoadAsync(stream, "json");
        var document = readResult.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
        
        var config = new GenerationConfiguration
        {
            Language = GenerationLanguage.TypeScript,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            Options = new GenerationOptions
            {
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    ModelFormat = ModelFormat.Interfaces,
                    HttpTransports = TypeScriptHttpTransports.Fetch,
                    UseTanStackQuery = true
                }
            },
            FileOrganization = new Core.Models.FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "api"
            }
        };

        // Act
        var result = await _generator.GenerateAsync(document, config);

        // Assert
        if (!result.Success)
        {
            var errors = string.Join("; ", result.Errors);
            var warnings = string.Join("; ", result.Warnings);
            Assert.Fail($"Generation failed. Errors: {errors}. Warnings: {warnings}");
        }
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        
        // Проверяем, что сгенерированы TanStack Query хуки
        var hookFiles = result.GeneratedFiles.Keys.Where(k => k.Contains(".hook.ts")).ToList();
        hookFiles.Should().NotBeEmpty("Should generate hook files");
        
        // Проверяем содержимое hook файла
        var hookFile = hookFiles.FirstOrDefault();
        if (hookFile != null)
        {
            var hookContent = result.GeneratedFiles[hookFile];
            hookContent.Should().Contain("import { useQuery");
            hookContent.Should().Contain("useGetUsers");
        }
    }

    [Test]
    public async Task GenerateFromSimpleSchema_WithAllFeatures_GeneratesValidCode()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "simple-schema.json");
        var schemaContent = await File.ReadAllTextAsync(schemaPath);
        
        await using var stream = File.OpenRead(schemaPath);
        var readResult = await OpenApiDocument.LoadAsync(stream, "json");
        var document = readResult.Document ?? throw new InvalidOperationException("Failed to parse OpenAPI document");
        
        var config = new GenerationConfiguration
        {
            Language = GenerationLanguage.TypeScript,
            OutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            GenerateModels = true,
            GenerateClients = true,
            GenerateEnums = true,
            Options = new GenerationOptions
            {
                TypeScriptOptions = new TypeScriptGenerationOptions
                {
                    ModelFormat = ModelFormat.ValidationSchemas,
                    ValidationLibrary = ValidationLibrary.Zod,
                    HttpTransports = TypeScriptHttpTransports.Axios,
                    UseTanStackQuery = true,
                    SerializeWithSchemas = true,
                    DeserializeWithSchemas = true
                }
            },
            FileOrganization = new Core.Models.FileOrganization
            {
                SeparateModels = true,
                SeparateClients = true,
                ModelsPath = "models",
                ClientsPath = "api"
            }
        };

        // Act
        var result = await _generator.GenerateAsync(document, config);

        // Assert
        if (!result.Success)
        {
            var errors = string.Join("; ", result.Errors);
            var warnings = string.Join("; ", result.Warnings);
            Assert.Fail($"Generation failed. Errors: {errors}. Warnings: {warnings}");
        }
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        
        // Проверяем, что сгенерированы все компоненты
        result.GeneratedFiles.Keys.Should().Contain(k => k.Contains(".schema.ts"), "Should generate schema files");
        result.GeneratedFiles.Keys.Should().Contain(k => k.Contains(".hook.ts"), "Should generate hook files");
        
        // Проверяем, что в hook файлах есть валидация
        var hookFiles = result.GeneratedFiles.Keys.Where(k => k.Contains(".hook.ts")).ToList();
        if (hookFiles.Any())
        {
            var hookContent = result.GeneratedFiles[hookFiles.First()];
            hookContent.Should().Contain("schema.parse", "Should include schema validation");
        }
    }
}
