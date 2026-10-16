using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniCrm.Api.Data;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Integration;

// Starts the real app (all middleware, controllers, validation) on a real SQL Server
// test database. Only two things are swapped:
//   - the tracker is a fake, so tests never call the real one or need the real key;
//   - the background worker is off, so each test sends events itself, when it wants.
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-key";

    // A separate database, never the app's own. Set MINICRM_TEST_DB to use another
    // SQL Server (e.g. Docker on Mac/Linux).
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("MINICRM_TEST_DB")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=MiniCrm_Tests;Trusted_Connection=True";

    public FakeTrackerHandler Tracker { get; } = new();

    public ApiFactory()
    {
        // The tests delete this database and its rows, so refuse anything that doesn't look like a test one.
        var databaseName = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (!databaseName.EndsWith("_Tests", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The test database name must end with _Tests, not '{databaseName}'.");

        // Start from an empty database; the app's startup migration creates the tables again.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
        db.Database.EnsureDeleted();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Command-line level settings: they win over .env and appsettings.json.
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Tracker:ApiKey", TestApiKey);

        builder.ConfigureTestServices(services =>
        {
            var worker = services.Single(d => d.ImplementationType == typeof(TrackerWorker));
            services.Remove(worker);

            services.AddHttpClient<TrackerClient>().ConfigurePrimaryHttpMessageHandler(() => Tracker);
        });
    }

    // Each test starts with no leads and events, and only the default statuses.
    public async Task ResetDatabase()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Safety check before deleting rows: never touch a database other than the test one.
        if (db.Database.GetConnectionString() != ConnectionString)
            throw new InvalidOperationException("The app is not using the test database.");

        await db.TrackerAttempts.ExecuteDeleteAsync();
        await db.TrackerEvents.ExecuteDeleteAsync();
        await db.Leads.ExecuteDeleteAsync();

        var defaultStatuses = DefaultLeadStatuses.All().Select(s => s.Name).ToList();
        await db.LeadStatuses.Where(s => !defaultStatuses.Contains(s.Name)).ExecuteDeleteAsync();

        Tracker.Reset();
    }
}

// All integration tests share one app and one database, so they run one at a time.
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
