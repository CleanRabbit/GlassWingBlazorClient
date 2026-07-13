using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GlassWingClient;
using GlassWingClient.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The "Testing" launch profile (run-e2e-tests.ps1) serves this client on a fixed port (5011) and
// needs it talking to the Testing-profile API (5223), not the interactive dev API (5123) —
// deliberately not driven by wwwroot/appsettings.Testing.json: Blazor WASM's dotnet-run dev
// server doesn't reliably resolve the environment name for a standalone (non-hosted) WASM app,
// so appsettings.{Environment}.json layering never actually kicked in (confirmed live — the
// Testing client kept calling :5123 and got CORS-rejected by the interactive API's :5001-only
// policy). Deriving straight from this page's own known, fixed port sidesteps that entirely.
var apiBase = builder.HostEnvironment.BaseAddress.Contains(":5011")
    ? "http://localhost:5223"
    : builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5123";
var devBypass = builder.Configuration.GetValue<bool>("DevBypass");

builder.Services.AddSingleton(new AuthStateService { IsDevBypass = devBypass });
builder.Services.AddSingleton<PlayerStateService>();
builder.Services.AddSingleton<RewardToastService>();
builder.Services.AddSingleton<ProgressStateService>();
builder.Services.AddSingleton<WelfareStateService>();
builder.Services.AddSingleton<WelfareBlockSignal>();
builder.Services.AddTransient<GlassWingAuthHandler>();
builder.Services.AddTransient<WelfareBlockDetectionHandler>();
builder.Services.AddHttpClient<GlassWingApiClient>(client =>
    client.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<GlassWingAuthHandler>()
    .AddHttpMessageHandler<WelfareBlockDetectionHandler>();
builder.Services.AddHttpClient<OpenMeteoClient>();

await builder.Build().RunAsync();
