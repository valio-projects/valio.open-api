using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Тесты «лёгкой» Angular-обёртки TanStack Query (<see cref="TanStackFramework.Angular"/>).
/// </summary>
[TestFixture]
public class TanStackAngularStepTests
{
    [Test]
    public async Task Generates_InjectQuery_Wrapper_For_Get_Operation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Angular);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("import { injectQuery } from \"@tanstack/angular-query-experimental\";");
        hook.Should().Contain("export const useGetUserById");
        hook.Should().Contain("injectQuery(() => ({");
        hook.Should().Contain("queryKey: getGetUserByIdQueryKey(params)");
        hook.Should().Contain("queryFn: () => getUserById(params)");
        hook.Should().Contain("...(options ?? {");
    }

    [Test]
    public async Task Generates_InjectMutation_Wrapper_For_Post_Operation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Angular);
        var hook = HookGenerationHarness.FindHook(files, "createPet");

        hook.Should().NotBeNull();
        hook.Should().Contain("import { injectMutation } from \"@tanstack/angular-query-experimental\";");
        hook.Should().Contain("export const useCreatePet");
        hook.Should().Contain("injectMutation(() => ({");
        hook.Should().Contain("mutationFn: (variables: CreatePetParams) => createPet(variables)");
    }

    [Test]
    public async Task Imports_Primary_Client_Suffix_Fetch()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Angular, TypeScriptHttpTransports.Fetch);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("from \"./getUserById.fetch\"");
    }

    [Test]
    public async Task Imports_Primary_Client_Suffix_Axios_When_Both_Configured()
    {
        var files = await HookGenerationHarness.GenerateAsync(
            TanStackFramework.Angular,
            TypeScriptHttpTransports.Axios | TypeScriptHttpTransports.Fetch);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        // Когда оба транспорта включены, Axios предпочтительнее (см. PreferredHookTransport)
        hook.Should().Contain("from \"./getUserById.axios\"");
    }

    [Test]
    public async Task Hook_Function_Has_No_ReturnType_Annotation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Angular);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        // useGetUserById не должен содержать ReturnType<typeof ...> аннотацию
        hook.Should().NotContain("ReturnType<typeof");
        // Также явно не должно быть ": UseQueryResult" или похожих аннотаций
        hook.Should().NotMatchRegex(@"useGetUserById\s*=\s*\([^)]*\)\s*:\s*\w");
    }
}
