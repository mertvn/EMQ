using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Server.Hubs;
using EMQ.Shared.Core;
using FFMpegCore;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

const bool hasDb = true;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EMQ Internal API", Version = "v1" });
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//     .AddCookie(options =>
//     {
//         options.Cookie = new CookieBuilder()
//         {
//             Name = "EMQSessionCookie",
//             SameSite = SameSiteMode.Strict,
//             HttpOnly = false,
//         };
//         options.ExpireTimeSpan = TimeSpan.FromHours(6);
//         options.SlidingExpiration = true;
//         options.AccessDeniedPath = "/Forbidden/";
//     });

builder.Services.AddSignalR().AddHubOptions<QuizHub>(opt => { opt.EnableDetailedErrors = true; })
//     .AddJsonProtocol(options =>
// {
//     // Configure the serializer to not change the casing of property names, instead of the default "camelCase" names.
//     // Default camelCase is not supported by used Blazor.Extensions.SignalR, because it does NOT allow to specify JSON deserialization options - it just uses eg. JsonSerializer.Deserialize<TResult1>(payloads[0]) and there is no option to pass JsonSerializerOptions to System.Text.Json.JsonSerializer.Deserialize():
//     // https://github.com/BlazorExtensions/SignalR/blob/v1.0.0/src/Blazor.Extensions.SignalR/HubConnection.cs#L108 + https://github.com/BlazorExtensions/SignalR/issues/64
//     // Idea taken from: https://docs.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-3.1&tabs=dotnet#jsonmessagepack-serialization-options
//     options.PayloadSerializerOptions.PropertyNamingPolicy = null;
// })
    ;

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

// builder.Services.AddCors(options =>
// {
//     options.AddDefaultPolicy(policyBuilder =>
//         policyBuilder.WithOrigins("https://localhost:7021")
//             .AllowAnyMethod()
//             .AllowAnyHeader()
//             .AllowCredentials());
// });

var app = builder.Build();

// app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<GeneralHub>("/GeneralHub");
app.MapHub<QuizHub>("/QuizHub");
app.MapFallbackToFile("index.html");

static IServiceProvider CreateServices()
{
    return new ServiceCollection()
        // Add common FluentMigrator services
        .AddFluentMigratorCore()
        .ConfigureRunner(rb => rb
            // Add Postgres support to FluentMigrator
            .AddPostgres()
            // Set the connection string
            .WithGlobalConnectionString(ConnectionHelper.GetConnectionString())
            // Define the assembly containing the migrations
            .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations())
        // Enable logging to console in the FluentMigrator way
        .AddLogging(lb => lb.AddFluentMigratorConsole())
        // Build the service provider
        .BuildServiceProvider(false);
}

async Task Init()
{
    string wwwrootFolder = app.Environment.IsDevelopment() ? "../Client/wwwroot" : "wwwroot";

    if (hasDb)
    {
        var serviceProvider = CreateServices();

        // Put the database update into a scope to ensure
        // that all resources will be disposed.
        using (var scope = serviceProvider.CreateScope())
        {
            // Instantiate the runner
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            // Execute the migrations
            runner.MigrateUp();
        }

        string autocompleteFolder = $"{wwwrootFolder}/autocomplete";
        Directory.CreateDirectory(autocompleteFolder);

        await File.WriteAllTextAsync($"{autocompleteFolder}/mst.json",
            await DbManager.SelectAutocompleteMst());
        await File.WriteAllTextAsync($"{autocompleteFolder}/c.json",
            await DbManager.SelectAutocompleteC());
        await File.WriteAllTextAsync($"{autocompleteFolder}/a.json",
            await DbManager.SelectAutocompleteA());
    }

    try
    {
        var mediaInfo = await FFProbe.AnalyseAsync($"{wwwrootFolder}/soft-piano-100-bpm-121529.mp3");
        Console.WriteLine(JsonSerializer.Serialize(mediaInfo, Utils.JsoIndented));
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
}

await Init();

app.Run();
