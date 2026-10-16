using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;

namespace MiniCrm.Tests.Fakes;

public static class TestData
{
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

    // A fresh EF InMemory database per test, with the seeded statuses and endpoints.
    // No SQL Server: no foreign keys and exact-case text compare.
    public static AppDbContext InMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
