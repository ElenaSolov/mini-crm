using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Controllers;
using MiniCrm.Api.Data;
using MiniCrm.Api.Services;

// Load secrets from the .env file in the repo root (searched upwards).
// NoClobber: real environment variables win over values in .env.
DotNetEnv.Env.TraversePath().NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// JSON names match the task: created_at, not CreatedAt.
// Enums go over JSON as strings: "converted", not 3.
builder.Services.AddControllers(options =>
    {
        // Validation errors use the JSON names too ("email", not "Email").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.SnakeCaseLower));
        // Only explicit [Required] counts. Otherwise broken JSON also reports
        // a confusing "The request field is required."
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<TrackerEventQueue>();
builder.Services.AddScoped<TrackerEventSender>();
builder.Services.AddScoped<TrackerEventService>();
builder.Services.AddScoped<TrackerEndpointService>();
builder.Services.AddScoped<TrackerAuthService>();
builder.Services.AddSingleton<TrackerDefaults>();
builder.Services.AddScoped<InboundLeadService>();
// Outbox worker: sends saved events in the background, with retries.
builder.Services.AddHostedService<TrackerWorker>();
// RedactLoggedHeaders: with Trace logging on, .NET would print the API key in the logs.
// All headers, because the auth header name can be changed on the settings page.
builder.Services.AddHttpClient<TrackerClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .RedactLoggedHeaders(_ => true);

// The inbound URLs are public (they sit in the client's website), so each IP
// gets a limit: 30 requests a minute, then 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(InboundController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Apply EF migrations at startup: creates the database and tables if they don't exist,
// so a reviewer only needs a connection string to run the app.
// Then add the tracker endpoints and auth header from .env if the database doesn't have them yet,
// and create the inbound token once, so the settings page always shows the pixel and postback URLs.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<TrackerEndpointService>().AddMissingDefaults();
    var trackerAuth = scope.ServiceProvider.GetRequiredService<TrackerAuthService>();
    trackerAuth.EnsureSettings();
    if ((await trackerAuth.Get()).KeySource == ApiKeySource.None)
        app.Logger.LogWarning(
            "No tracker API key. Set it on the settings page or as Tracker__ApiKey in .env, otherwise calls to the tracker will fail.");
    scope.ServiceProvider.GetRequiredService<InboundLeadService>().EnsureToken();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.MapControllers();

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
