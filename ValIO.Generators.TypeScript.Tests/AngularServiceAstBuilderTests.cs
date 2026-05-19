using System.Collections.Generic;
using FluentAssertions;
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

/// <summary>
/// Юнит-тесты для AngularServiceAstBuilder. Минимальный IrClient строится прямо в тесте,
/// чтобы не зависеть от OpenAPI фикстур (которых в репо нет).
/// </summary>
[TestFixture]
public class AngularServiceAstBuilderTests
{
    private NamingConverter _naming = null!;
    private TypeScriptAstBuilder _astBuilder = null!;
    private AngularServiceAstBuilder _serviceBuilder = null!;
    private TypeScriptAstRenderer _renderer = null!;

    [SetUp]
    public void Setup()
    {
        _naming = new NamingConverter();
        var typeMapper = new TypeScriptTypeMapper(_naming);
        var tsOptions = new TypeScriptGenerationOptions
        {
            HttpTransports = TypeScriptHttpTransports.AngularRxjs
        };
        _astBuilder = new TypeScriptAstBuilder(_naming, typeMapper, tsOptions);
        _serviceBuilder = new AngularServiceAstBuilder(_naming, _astBuilder.BuildType, tsOptions);
        _renderer = new TypeScriptAstRenderer();
    }

    private static IrOperation BuildGetUserById()
    {
        return new IrOperation
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
                new IrResponse
                {
                    StatusCode = "200",
                    Type = new IrModelReference { ModelName = "User" }
                }
            }
        };
    }

    private static IrOperation BuildListUsers()
    {
        return new IrOperation
        {
            MethodName = "listUsers",
            HttpMethod = "GET",
            Path = "/users",
            Summary = "List users.",
            PathParameters = System.Array.Empty<IrParameter>(),
            QueryParameters = new[]
            {
                new IrParameter
                {
                    Name = "limit",
                    Type = new IrPrimitiveType { PrimitiveKind = IrPrimitiveKind.Integer },
                    IsRequired = false
                }
            },
            HeaderParameters = System.Array.Empty<IrParameter>(),
            Responses = new[]
            {
                new IrResponse
                {
                    StatusCode = "200",
                    Type = new IrModelReference { ModelName = "User" }
                }
            }
        };
    }

    private static IrOperation BuildCreateUser()
    {
        return new IrOperation
        {
            MethodName = "createUser",
            HttpMethod = "POST",
            Path = "/users",
            Summary = "Create a new user.",
            PathParameters = System.Array.Empty<IrParameter>(),
            QueryParameters = System.Array.Empty<IrParameter>(),
            HeaderParameters = System.Array.Empty<IrParameter>(),
            RequestBody = new IrRequestBody
            {
                Type = new IrModelReference { ModelName = "User" },
                IsRequired = true,
                ContentType = "application/json"
            },
            Responses = new[]
            {
                new IrResponse
                {
                    StatusCode = "201",
                    Type = new IrModelReference { ModelName = "User" }
                }
            }
        };
    }

    private static IrClient BuildUserClient()
    {
        return new IrClient
        {
            Name = "User",
            Operations = new[] { BuildGetUserById(), BuildListUsers(), BuildCreateUser() }
        };
    }

    [Test]
    public void ServiceFileName_AppendsService_AndIsCamelCase()
    {
        var client = BuildUserClient();

        var fileName = _serviceBuilder.ServiceFileName(client);

        fileName.Should().Be("userService");
    }

    [Test]
    public void ServiceClassName_IsPascalCaseWithServiceSuffix()
    {
        var client = BuildUserClient();

        var className = _serviceBuilder.ServiceClassName(client);

        className.Should().Be("UserService");
    }

    [Test]
    public void BuildServiceSourceFile_GeneratesInjectableClass_WithHttpClientCtor()
    {
        var client = BuildUserClient();

        var source = _serviceBuilder.BuildServiceSourceFile(client);
        var rendered = _renderer.Render(source);

        rendered.Should().Contain("import { Injectable } from \"@angular/core\";");
        rendered.Should().Contain("import { HttpClient, HttpHeaders, HttpParams } from \"@angular/common/http\";");
        rendered.Should().Contain("import { Observable } from \"rxjs\";");
        rendered.Should().Contain("@Injectable({ providedIn: \"root\" })");
        rendered.Should().Contain("export class UserService {");
        rendered.Should().Contain("constructor(private readonly http: HttpClient) {}");
    }

    [Test]
    public void BuildServiceSourceFile_GeneratesOneMethodPerOperation_ReturningObservable()
    {
        var client = BuildUserClient();

        var source = _serviceBuilder.BuildServiceSourceFile(client);
        var rendered = _renderer.Render(source);

        // Один метод на операцию. Возвращаемый тип — Observable<{Op}Result>.
        rendered.Should().Contain("getUserById(params: GetUserByIdParams): Observable<GetUserByIdResult>");
        rendered.Should().Contain("listUsers(params: ListUsersParams): Observable<ListUsersResult>");
        rendered.Should().Contain("createUser(params: CreateUserParams): Observable<CreateUserResult>");
    }

    [Test]
    public void BuildServiceSourceFile_UsesAppropriateHttpVerb_AndUrlTemplate()
    {
        var client = BuildUserClient();

        var source = _serviceBuilder.BuildServiceSourceFile(client);
        var rendered = _renderer.Render(source);

        // GET с path-параметром: template literal через `params.id`.
        rendered.Should().Contain("this.http.get<GetUserByIdResult>(url");
        rendered.Should().Contain("`/users/${params.id}`");

        // GET без route, но с query → new HttpParams().set("limit", String(params.limit)).
        // Многоарг-вызовы рендерятся многострочно; проверяем по отдельным фрагментам.
        rendered.Should().Contain("this.http.get<ListUsersResult>(");
        rendered.Should().Contain("\"limit\"");
        rendered.Should().Contain("String(params.limit)");

        // POST с body → body аргумент после url.
        rendered.Should().Contain("this.http.post<CreateUserResult>(");
        rendered.Should().MatchRegex(@"this\.http\.post<CreateUserResult>\(\s+url,\s+params\s+\)");
    }

    [Test]
    public void BuildServiceSourceFile_ImportsParamsAndResultsAsTypeOnly()
    {
        var client = BuildUserClient();

        var source = _serviceBuilder.BuildServiceSourceFile(client);
        var rendered = _renderer.Render(source);

        rendered.Should().Contain("import type { GetUserByIdParams } from \"./getUserById/getUserById.params\";");
        rendered.Should().Contain("import type { GetUserByIdResult } from \"./getUserById/getUserById.results\";");
        rendered.Should().Contain("import type { ListUsersResult } from \"./listUsers/listUsers.results\";");
        rendered.Should().Contain("import type { CreateUserResult } from \"./createUser/createUser.results\";");
    }

    [Test]
    public void BuildServiceSourceFile_AcceptsClientNameAlreadyEndingInService_NoDoubleSuffix()
    {
        var client = new IrClient
        {
            Name = "OrdersService",
            Operations = new[] { BuildListUsers() with { MethodName = "listOrders", Path = "/orders" } }
        };

        var className = _serviceBuilder.ServiceClassName(client);
        var fileName = _serviceBuilder.ServiceFileName(client);

        className.Should().Be("OrdersService");
        fileName.Should().Be("ordersService");
    }
}
