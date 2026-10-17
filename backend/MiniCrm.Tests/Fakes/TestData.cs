using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;

namespace MiniCrm.Tests.Fakes;

public static class TestData
{
    // Test values for the .env defaults. Not the real tracker, so a test that checks
    // these URLs proves the defaults come from the configuration, not from the code.
    public const string PingUrl = "https://tracker.test/ping";
    public const string ConversionsUrl = "https://tracker.test/conversions";
    public const string AuthHeaderName = "Authorization";
    public const string AuthHeaderTemplate = "Bearer {api_key}";

    public static readonly Dictionary<string, string?> TrackerSettings = new()
    {
        ["Tracker:PingUrl"] = PingUrl,
        ["Tracker:ConversionsUrl"] = ConversionsUrl,
        ["Tracker:AuthHeaderName"] = AuthHeaderName,
        ["Tracker:AuthHeaderTemplate"] = AuthHeaderTemplate
    };

    public static TrackerDefaults Defaults() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(TrackerSettings).Build());

    public static TrackerEndpoint DefaultPurchase() =>
        Defaults().All().Single(e => e.Name == TrackerDefaults.PurchaseName);

    public static TrackerEndpoint DefaultPing() =>
        Defaults().All().Single(e => e.Name == TrackerDefaults.PingName);

    // A lead that is allowed to convert: it has a contact and an amount.
    public static Lead ConvertibleLead(int id = 42) => new()
    {
        Id = id,
        Name = "Ann",
        Email = "lead@example.com",
        Phone = "+12025550123",
        Source = "facebook",
        Amount = 199.00m,
        Currency = "USD"
    };

    // A fresh EF InMemory database per test, with the seeded statuses, the auth row and the default
    // endpoints (ping gets id 1, purchase id 2, as on a new SQL Server database).
    // No SQL Server: no foreign keys and exact-case text compare.
    public static AppDbContext InMemoryDb() => InMemoryDb(Guid.NewGuid().ToString());

    public static AppDbContext InMemoryDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var db = new AppDbContext(options);
        if (db.Database.EnsureCreated())
        {
            db.TrackerEndpoints.AddRange(Defaults().All());
            db.TrackerAuthSettings.Add(Defaults().Auth());
            db.SaveChanges();
        }
        return db;
    }
}
