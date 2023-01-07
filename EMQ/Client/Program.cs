using System;
using System.Net.Http;
using Blazored.LocalStorage;
using Blazorise;
using Blazorise.Bootstrap5;
using EMQ.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<ClientUtils>();
builder.Services.AddSingleton<ClientConnectionManager>();

builder.Services.AddBlazorise(options => { options.Immediate = true; }).AddBootstrap5Providers();

builder.Services.AddBlazoredLocalStorageAsSingleton();

await builder.Build().RunAsync();
