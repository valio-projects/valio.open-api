using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Тесты Svelte-обёртки TanStack Query (<see cref="TanStackFramework.Svelte"/>).
/// </summary>
[TestFixture]
public class TanStackSvelteStepTests
{
    [Test]
    public async Task Generates_CreateQuery_Wrapper_For_Get_Operation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Svelte);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("import { createQuery } from \"@tanstack/svelte-query\";");
        hook.Should().Contain("export const useGetUserById");
        // Svelte 5 / svelte-query v6+ требует getter
        hook.Should().Contain("createQuery(() => ({");
        hook.Should().Contain("queryKey: getGetUserByIdQueryKey(params)");
        hook.Should().Contain("queryFn: () => getUserById(params)");
        hook.Should().Contain("...(options ?? {");
    }

    [Test]
    public async Task Generates_CreateMutation_For_Post_Operation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Svelte);
        var hook = HookGenerationHarness.FindHook(files, "createPet");

        hook.Should().NotBeNull();
        hook.Should().Contain("import { createMutation } from \"@tanstack/svelte-query\";");
        hook.Should().Contain("export const useCreatePet");
        hook.Should().Contain("createMutation(() => ({");
        hook.Should().Contain("mutationFn: (variables: CreatePetParams) => createPet(variables)");
    }

    [Test]
    public async Task Imports_Primary_Client_Suffix_Fetch()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Svelte, TypeScriptHttpTransports.Fetch);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().Contain("from \"./getUserById.fetch\"");
    }

    [Test]
    public async Task Hook_Function_Has_No_ReturnType_Annotation()
    {
        var files = await HookGenerationHarness.GenerateAsync(TanStackFramework.Svelte);
        var hook = HookGenerationHarness.FindHook(files, "getUserById");

        hook.Should().NotBeNull();
        hook.Should().NotContain("ReturnType<typeof");
        hook.Should().NotMatchRegex(@"useGetUserById\s*=\s*\([^)]*\)\s*:\s*\w");
    }
}
