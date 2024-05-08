using System;
using System.Net.Http;
using Blazored.LocalStorage;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
// using BlazorWasmProfiler;
using EMQ.Client;
using EMQ.Client.Components;
using EMQ.Shared.Core;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

// [assembly: BlazorTimer]

// [assembly: MethodTimer]
// [assembly: RenderTimer]

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
    // surely nothing could go wrong setting this globally
    Timeout = TimeSpan.FromSeconds(UploadConstants.TimeoutSeconds),
});
builder.Services.AddSingleton<ClientUtils>();
builder.Services.AddSingleton<ClientConnectionManager>();
// builder.Services.AddSingleton<PlayerPreferencesComponent>();

builder.Services.AddBlazorise(options => { options.Immediate = true; }).AddBootstrap5Providers().AddFontAwesomeIcons();

builder.Services.AddBlazoredLocalStorageAsSingleton();

await builder.Build().RunAsync();
