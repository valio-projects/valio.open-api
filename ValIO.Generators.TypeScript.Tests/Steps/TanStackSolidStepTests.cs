using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Тесты Solid-обёртки TanStack Query (<see cref="TanStackFramework.Solid"/>).
/// </summary>
[TestFixture]
public class TanStackSolidStepTests
{
    [Test]
    public async Task Generates_CreateQuery_Getter_Wrapper_For_Get_Operation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Solid);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("import { createQuery } from \"@tanstack/solid-query\";");
        hook.Should().Contain("export const useGetUserById");
        // Solid требует getter () => ({ ... })
        hook.Should().Contain("createQuery(() => ({");
        hook.Should().Contain("queryKey: getGetUserByIdQueryKey(params)");
        hook.Should().Contain("queryFn: () => getUserById(params)");
        hook.Should().Contain("...(options ?? {");
    }

    [Test]
    public async Task Generates_CreateMutation_Getter_For_Post_Operation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Solid);
        var hook = HookGenerationHarness.FindHook(files, "createPet");

        hook.Should().NotBeNull();
        hook.Should().Contain("import { createMutation } from \"@tanstack/solid-query\";");
        hook.Should().Contain("export const useCreatePet");
        hook.Should().Contain("createMutation(() => ({");
        hook.Should().Contain("mutationFn: (variables: CreatePetParams) => createPet(variables)");
    }

    [Test]
    public async Task Imports_Primary_Client_Suffix_Fetch()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Solid, TypeScriptHttpTransports.Fetch);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("from \"./getUserById.fetch\"");
    }

    [Test]
    public async Task QueryKey_Getter_Has_Readonly_Unknown_Array_Return_Type()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Solid);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("export const getGetUserByIdQueryKey");
        hook.Should().Contain(": readonly unknown[] =>");
    }

    [Test]
    public async Task Hook_Function_Has_No_ReturnType_Annotation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Solid);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().NotContain("ReturnType<typeof");
        hook.Should().NotMatchRegex(@"useGetUserById\s*=\s*\([^)]*\)\s*:\s*\w");
    }
}
