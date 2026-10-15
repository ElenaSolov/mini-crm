using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;

// Load secrets from the .env file in the repo root (searched upwards).
// NoClobber: real environment variables win over values in .env.
DotNetEnv.Env.TraversePath().NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration["Tracker:ApiKey"]))
{
    app.Logger.LogWarning(
        "Tracker:ApiKey is not set. Add Tracker__ApiKey to .env, otherwise calls to the tracker will fail.");
}

// Apply EF migrations at startup (creates the database if it doesn't exist).
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
