using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
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
// RedactLoggedHeaders: with Trace logging on, .NET would print the API key in the logs.
builder.Services.AddHttpClient<TrackerClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .RedactLoggedHeaders(["Authorization"]);

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration["Tracker:ApiKey"]))
{
    app.Logger.LogWarning(
        "Tracker:ApiKey is not set. Add Tracker__ApiKey to .env, otherwise calls to the tracker will fail.");
}

// Apply EF migrations at startup: creates the database and tables if they don't exist,
// so a reviewer only needs a connection string to run the app.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
