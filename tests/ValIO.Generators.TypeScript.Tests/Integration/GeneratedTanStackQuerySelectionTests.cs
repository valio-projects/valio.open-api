using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Integration;

/// <summary>Compiles consumer selections against the generated TanStack Query API and real library types.</summary>
[TestFixture]
[Category("NodeIntegration")]
public sealed class GeneratedTanStackQuerySelectionTests
{
    /// <summary>Verifies selected results, raw cache callbacks, and compatibility with untransformed calls.</summary>
    /// <param name="transport">The generated transport backing the hook.</param>
    /// <returns>The generation and strict TypeScript compilation task.</returns>
    [TestCase("fetch")]
    [TestCase("axios")]
    public async Task OrdinaryQuery_InfersSelectedDataAndPreservesWireCallbacks(string transport)
    {
        using var harness = new PipelineTestHarness();
        var document = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var context = await harness.RunAsync(document, new TypeScriptGenerationOptions
        {
            Transports = [transport],
            QueryWrapper = "tanstack-react",
        });

        var files = context.GeneratedFiles.Where(file => file.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToList();
        var usersQuery = FindImportPath(files, "getUsers");
        var userQuery = FindImportPath(files, "getUserById");
        files.Add(new GeneratedFile("ordinary-query-consumer.ts", $$"""
            import type { QueryClient, UseQueryResult } from "@tanstack/react-query";
            import { getGetUsersQueryOptions, prefetchGetUsersQuery, useGetUsers } from "{{usersQuery}}";
            import type { GetGetUsersQueryOptionsParams, GetGetUsersQueryOptionsResult, GetUsersQueryResult } from "{{usersQuery}}";
            import { useGetUserById } from "{{userQuery}}";

            declare const queryClient: QueryClient;

            const legacyOptions: GetGetUsersQueryOptionsParams = { query: { enabled: true } };
            const legacyResult: UseQueryResult<GetUsersQueryResult, Error> = useGetUsers(legacyOptions);
            const rawResult: GetUsersQueryResult | undefined = legacyResult.data;

            const projected = useGetUsers({ query: {
                select: response => response.isSuccessful ? response.status : 0,
                placeholderData: previous => {
                    const raw: GetUsersQueryResult | undefined = previous;
                    return raw;
                },
                refetchInterval: query => {
                    const raw: GetUsersQueryResult | undefined = query.state.data;
                    return raw?.isSuccessful ? 1000 : false;
                },
                enabled: true,
            } }, queryClient);
            const observerData: number | undefined = projected.data;
            // @ts-expect-error The observer result cannot be read as the raw envelope after selection.
            const mistakenWireData: GetUsersQueryResult | undefined = projected.data;

            const options: GetGetUsersQueryOptionsResult<number> = getGetUsersQueryOptions({
                query: { select: response => response.status },
            });
            const prefetch: Promise<QueryClient> = prefetchGetUsersQuery(queryClient, {
                query: { select: response => response.status },
            });
            const singleUser = useGetUserById({ id: 7 }, {
                query: { select: response => response.isSuccessful ? response.body.name : null },
            });
            const userName: string | null | undefined = singleUser.data;

            void [rawResult, observerData, mistakenWireData, options, prefetch, userName];
            """));

        await GeneratedTypeScriptCompiler.Compile(files, $"ordinary TanStack Query selection with {transport}");
    }

    /// <summary>Resolves the generated module path for a consumer placed at the output root.</summary>
    /// <param name="files">The emitted TypeScript source files.</param>
    /// <param name="operationName">The operation's generated file stem.</param>
    /// <returns>The relative module specifier without its TypeScript extension.</returns>
    private static string FindImportPath(IReadOnlyList<GeneratedFile> files, string operationName)
    {
        var suffix = $"/{operationName}.query.ts";
        var matches = files.Select(file => file.RelativePath.Replace('\\', '/'))
            .Where(path => path.EndsWith(suffix, StringComparison.Ordinal));
        var source = matches.Should().ContainSingle().Which;
        return $"./{source[..^3]}";
    }
}
