using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GlassWingClient;
using GlassWingClient.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5123";
var devBypass = builder.Configuration.GetValue<bool>("DevBypass");

builder.Services.AddSingleton(new AuthStateService { IsDevBypass = devBypass });
builder.Services.AddSingleton<PlayerStateService>();
builder.Services.AddSingleton<RewardToastService>();
builder.Services.AddSingleton<ProgressStateService>();
builder.Services.AddSingleton<WelfareStateService>();
builder.Services.AddTransient<GlassWingAuthHandler>();
builder.Services.AddHttpClient<GlassWingApiClient>(client =>
    client.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<GlassWingAuthHandler>();

await builder.Build().RunAsync();
