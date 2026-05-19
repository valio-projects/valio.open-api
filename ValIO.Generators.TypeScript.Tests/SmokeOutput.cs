using System;
using System.IO;
using NUnit.Framework;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Angular.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Tests;

[TestFixture]
[Explicit("Smoke — writes a single .ts file under TestResults so it can be compiled with tsc.")]
public class AngularServiceSmokeWriter
{
    [Test]
    public void WriteUserService_ToDisk()
    {
        var naming = new NamingConverter();
        var typeMapper = new TypeScriptTypeMapper(naming);
        var astBuilder = new TypeScriptAstBuilder(naming, typeMapper, new TypeScriptGenerationOptions
        {
            HttpTransports = TypeScriptHttpTransports.AngularRxjs
        });
        var builder = new AngularServiceAstBuilder(naming, astBuilder.BuildType);
        var renderer = new TypeScriptAstRenderer();

        var op = new IrOperation
        {
            MethodName = "getUserById",
            HttpMethod = "GET",
            Path = "/users/{id}",
            Summary = "Get user by id.",
            PathParameters = new[]
            {
                new IrParameter
                {
                    Name = "id",
                    Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.String },
                    IsRequired = true
                }
            },
            QueryParameters = System.Array.Empty<IrParameter>(),
            HeaderParameters = System.Array.Empty<IrParameter>(),
            Responses = new[]
            {
                new IrResponse { StatusCode = "200", Type = new IrModelReference { ModelName = "User" } }
            }
        };

        var client = new IrClient { Name = "User", Operations = new[] { op } };

        var source = builder.BuildServiceSourceFile(client);
        var rendered = renderer.Render(source);

        var outDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "angular-output");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "userService.service.ts"), rendered);

        TestContext.Out.WriteLine(rendered);
    }
}
