using System.Globalization;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using ValIO.Application.DependencyInjection;
using ValIO.Localization.DependencyInjection;
using ValIO.Output.DependencyInjection;
using ValIO.Plugins.Bootstrap;
using ValIO.Web.Components;
using ValIO.Web.Endpoints;
using ValIO.Web.Services;
using ValIO.Web.State;

var builder = WebApplication.CreateBuilder(args);

// Blazor + UI
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// HTTP and Core
builder.Services.AddHttpClient();
builder.Services.AddValIOCore();
builder.Services.AddValIOLocalization();
builder.Services.AddValIOOutputs();
builder.Services.AddValIOApplication();
builder.Services.AddAllGeneratorPlugins();

// Web-specific
builder.Services.AddScoped<GenerationFormState>();
builder.Services.AddSingleton<ISafeSchemaFetcher, SafeSchemaFetcher>();

// Localization for Web UI
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

var supportedCultureNames = new[] { "en", "ru", "de", "zh-CN" };
var supportedCultures = supportedCultureNames.Select(c => new CultureInfo(c)).ToArray();
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture("en");
    o.SupportedCultures = supportedCultures;
    o.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Minimal API endpoints
app.MapPost("/api/generate", GenerateEndpoint.HandleAsync).DisableAntiforgery();
app.MapGet("/culture/set", CultureEndpoint.SetCulture);

app.Run();

// expose Program for testing
public partial class Program { }
