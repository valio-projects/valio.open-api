using FluentAssertions;
using NUnit.Framework;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Tests.TestHelpers;

namespace ValIO.Generators.TypeScript.Tests.Steps;

/// <summary>
/// Unit tests for <see cref="ValIO.Generators.TypeScript.Steps.AngularRxjsClientStep"/> —
/// emits one <c>{Name}Service.service.ts</c> per IrClient (grouped by tag).
/// </summary>
[TestFixture]
public class AngularRxjsClientStepTests
{
    private PipelineTestHarness _harness = null!;

    [OneTimeSetUp]
    public void Setup() => _harness = new PipelineTestHarness();

    [OneTimeTearDown]
    public void Teardown() => _harness.Dispose();

    [Test]
    public async Task NoAngularTransport_NoServiceFiles()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("simple-schema.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["fetch"] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".service.ts", StringComparison.Ordinal));
    }

    [Test]
    public async Task AngularTransport_EmitsOneServiceFilePerIrClient()
    {
        // Petstore has three tags — pet / store / user — so we expect three service files.
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var serviceFiles = ctx.GeneratedFiles
            .Where(f => f.RelativePath.EndsWith(".service.ts", StringComparison.Ordinal))
            .ToList();

        serviceFiles.Should().HaveCount(3);
        serviceFiles.Should().Contain(f => f.RelativePath == "api/petService.service.ts");
        serviceFiles.Should().Contain(f => f.RelativePath == "api/storeService.service.ts");
        serviceFiles.Should().Contain(f => f.RelativePath == "api/userService.service.ts");
    }

    [Test]
    public async Task AngularService_IsInjectableProvidedInRoot_WithHttpClientInjection()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var petService = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("petService.service.ts", StringComparison.Ordinal));

        // Imports from the Angular packages — required for DI + return type.
        petService.Content.Should().Contain("import { Injectable }");
        petService.Content.Should().Contain("from \"@angular/core\"");
        petService.Content.Should().Contain("from \"@angular/common/http\"");
        petService.Content.Should().Contain("import { Observable }");
        petService.Content.Should().Contain("from \"rxjs\"");

        // @Injectable decorator + class declaration.
        petService.Content.Should().Contain("@Injectable({ providedIn: \"root\" })");
        petService.Content.Should().Contain("export class PetService {");

        // Angular DI constructor with parameter property.
        petService.Content.Should().Contain("constructor(private readonly http: HttpClient) {}");
    }

    [Test]
    public async Task AngularService_MethodsReturnObservable_AndUseThisHttpVerb()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var petService = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("petService.service.ts", StringComparison.Ordinal));

        // getPetById has a path param, GET verb, returns a Pet → Observable<GetPetByIdResult>.
        petService.Content.Should().MatchRegex(
            @"getPetById\(params: GetPetByIdParams\)\s*:\s*Observable<GetPetByIdResult>");
        petService.Content.Should().Contain("this.http.get<GetPetByIdResult>(");

        // POST addPet — passes body argument before any options object.
        petService.Content.Should().Contain("this.http.post<");
        petService.Content.Should().Contain("addPet(params: AddPetParams)");
    }

    [Test]
    public async Task AngularService_QueryParameters_UseHttpParamsChain()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var petService = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("petService.service.ts", StringComparison.Ordinal));

        // findPetsByStatus / findPetsByTags use query parameters → HttpParams chain.
        petService.Content.Should().Contain("new HttpParams()");
        petService.Content.Should().MatchRegex(@"\.set\(\s*""status""");
    }

    [Test]
    public async Task AngularService_HeaderParameters_UseHttpHeadersChain()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var petService = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("petService.service.ts", StringComparison.Ordinal));

        // deletePet has an `api_key` header param — should chain `new HttpHeaders().set(...)`.
        petService.Content.Should().Contain("new HttpHeaders()");
        petService.Content.Should().MatchRegex(@"\.set\(\s*""api_key""");
    }

    [Test]
    public async Task AngularService_PathParamsAreInterpolated_InTemplateUrl()
    {
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        var petService = ctx.GeneratedFiles.First(f =>
            f.RelativePath.EndsWith("petService.service.ts", StringComparison.Ordinal));

        // Path `/pet/{petId}` is rendered with a template literal that references params.
        petService.Content.Should().MatchRegex(@"const url = `/pet/\$\{params\.");
    }

    [Test]
    public async Task AngularService_DoesNotEmitPerOperationFiles()
    {
        // Angular transport is per-IrClient, not per-operation. Make sure we don't
        // accidentally emit per-operation `.angular.ts` files alongside the service.
        var doc = await PipelineTestHarness.LoadDocumentAsync("petstore.json");
        var ctx = await _harness.RunAsync(doc, new TypeScriptGenerationOptions { Transports = ["angular-rxjs"] });

        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".angular.ts", StringComparison.Ordinal));
        ctx.GeneratedFiles.Should().NotContain(f =>
            f.RelativePath.EndsWith(".angular-rxjs.ts", StringComparison.Ordinal));
    }
}
