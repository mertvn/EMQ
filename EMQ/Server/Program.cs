using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Server.Hubs;
using EMQ.Shared.Core;
using FFMpegCore;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EMQ Internal API", Version = "v1" });
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

builder.Services.Configure<MvcOptions>(options =>
{
    options.InputFormatters.OfType<SystemTextJsonInputFormatter>().First().SupportedMediaTypes.Add(
        new MediaTypeHeaderValue("application/csp-report"));
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options => { options.Level = CompressionLevel.Optimal; });
builder.Services.Configure<GzipCompressionProviderOptions>(options => { options.Level = CompressionLevel.Optimal; });

// builder.Services.AddCors(options =>
// {
//     options.AddDefaultPolicy(policyBuilder =>
//         policyBuilder.WithOrigins("https://localhost:7021")
//             .AllowAnyMethod()
//             .AllowAnyHeader()
//             .AllowCredentials());
// });

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddHostedService<CleanupService>();
builder.Services.AddHostedService<OpportunisticGcService>();

var app = builder.Build();
app.UseResponseCompression();

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
    app.UseHsts();
}

app.UseHttpsRedirection();

// <!--    we have to use unsafe-eval because blazor wasm requires it (check again after .NET 8) -->
// <!--    https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-8-preview-5/#blazor-content-security-policy-csp-compatibility -->
// <!--    we have to use unsafe-inline because unsafe-hashes is very new, -->
// <!--    and we have to use unsafe-hashes because styles don't work otherwise -->
// <!--    hashes for styles as of 2023-06-24 -->
// <!--    'sha256-WH8R6xeOeuJVdbp+/qEeNlldljI6BQWXRzvQ3aY5WaI='-->
// <!--    'sha256-LNyLHt0iPlXA2SjFUXL9wqxHp5dJGBldj7LSb1gNgjA='-->
// <!--    'sha256-q37xv29FWTxrl539g+ajXTokv196Spat4bpoxeQqDTw='-->
// <!--    'sha256-eznNOzOF8kRuSmqjmCsetTase4gDYgWA0sSMry6PUKY='-->
// <!--    'sha256-KbrxNa5b5DaDi2OvquFvHtsYxspVRCBjG7Kek5I4mRI='-->
const string csp = @"
               base-uri 'self';
               default-src 'self';
               connect-src 'self' *.vndb.org *.catbox.moe fonts.gstatic.com;
               font-src 'self' fonts.gstatic.com;
               media-src 'self' blob: *.catbox.moe;
               img-src data: https:;
               object-src 'none';
               script-src 'self'
                          'unsafe-eval'
                          'sha256-PEWbiIHhO1ky9ebNF+s/VcvPmNW2891qrVb4BzgNVQM='
                          ;
               style-src 'self'
                         'unsafe-inline'
                         ;
               form-action 'self';
               frame-ancestors 'none';
               upgrade-insecure-requests;
               report-uri /Auth/CspReport/;
               report-to csp-endpoint;
";

const string reportTo = @"
{
""group"": ""csp-endpoint"",
""max_age"": 10886400,
""endpoints"": [
                  { ""url"": ""/Auth/CspReport/"" }
               ]
}
";

app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy", $"{csp.Replace("\n", " ")}");
    context.Response.Headers.Add("Report-To", $"{reportTo.Replace("\n", " ")}");
    await next();
});

app.UseBlazorFrameworkFiles();

app.UseStaticFiles(new StaticFileOptions
{
    HttpsCompression = HttpsCompressionMode.Compress,
    OnPrepareResponse = ctx =>
    {
        var maxAge = TimeSpan.FromDays(30);
        if (ctx.File.Name is "mst.json" or "c.json" or "a.json")
        {
            maxAge = TimeSpan.FromHours(1);
        }

        if (ctx.File.Name is "index.html" or "/")
        {
            maxAge = TimeSpan.FromMinutes(1);
        }

        var headers = ctx.Context.Response.GetTypedHeaders();
        headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
        {
            Public = true, MaxAge = maxAge
        };
    }
});

if (Constants.UseLocalSongFilesForDevelopment)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(@"K:\emq\emqsongsbackup"),
        RequestPath = "/emqsongsbackup"
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Constants.LocalMusicLibraryPath),
        RequestPath = "/emqlocalmusiclibrary"
    });
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<GeneralHub>("/GeneralHub");
app.MapHub<QuizHub>("/QuizHub");
app.MapFallbackToFile("index.html");

const bool hasDb = true;
bool precacheSongs = false && !app.Environment.IsDevelopment();

static IServiceProvider CreateServices()
{
#pragma warning disable ASP0000
    return new ServiceCollection()
#pragma warning restore ASP0000
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
    AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    {
        Console.WriteLine($"UnhandledException: {(Exception)args.ExceptionObject}");
    };

    TaskScheduler.UnobservedTaskException += (_, args) =>
    {
        Console.WriteLine($"UnobservedTaskException: {args.Exception}");
    };

    string wwwrootFolder = app.Environment.IsDevelopment() ? "../Client/wwwroot" : "wwwroot";

    // test ffMPEG install
    try
    {
        var mediaInfo = await FFProbe.AnalyseAsync($"{wwwrootFolder}/soft-piano-100-bpm-121529.mp3");
        Console.WriteLine(JsonSerializer.Serialize(mediaInfo.Duration, Utils.JsoIndented));
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }

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

        if (precacheSongs)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await DbManager.GetRandomSongs(int.MaxValue, true, new List<string>());

            stopWatch.Stop();
            double ms = (stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency;
            Console.WriteLine($"Precached songs in {Math.Round(ms / 1000, 2)}s");
        }

        ServerUtils.RunAggressiveGc();
    }
}

await Init();

app.Run();
