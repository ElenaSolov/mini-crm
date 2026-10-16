using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

// The token and the default source, with EF InMemory.
public class InboundLeadServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestData.InMemoryDb();
    private readonly InboundLeadService _service;

    public InboundLeadServiceTests()
    {
        _service = new InboundLeadService(_db, new LeadService(_db, new TrackerEventQueue(_db)));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task EnsureToken_creates_a_random_token_once()
    {
        _service.EnsureToken();
        var first = await _service.GetToken();
        _service.EnsureToken();

        Assert.Equal(64, first!.Length);
        Assert.Equal(first, await _service.GetToken());
        Assert.Single(_db.InboundSettings);
    }

    [Fact]
    public void Each_database_gets_its_own_token()
    {
        using var otherDb = TestData.InMemoryDb();
        var other = new InboundLeadService(otherDb, new LeadService(otherDb, new TrackerEventQueue(otherDb)));

        _service.EnsureToken();
        other.EnsureToken();

        Assert.NotEqual(_db.InboundSettings.Single().Token, otherDb.InboundSettings.Single().Token);
    }

    [Fact]
    public async Task The_right_token_is_valid()
    {
        _service.EnsureToken();

        Assert.True(await _service.IsValidToken(await _service.GetToken()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    public async Task A_missing_or_wrong_token_is_not_valid(string? token)
    {
        _service.EnsureToken();

        Assert.False(await _service.IsValidToken(token));
    }

    [Fact]
    public async Task Without_the_row_no_token_is_valid()
    {
        Assert.Null(await _service.GetToken());
        Assert.False(await _service.IsValidToken(""));
    }

    [Fact]
    public async Task A_lead_without_a_source_gets_the_default_source()
    {
        var lead = await _service.Create(new SaveLeadRequest { Name = "Ann", Email = "ann@example.com" }, InboundLeadService.PixelSource);

        Assert.Equal("pixel", lead.Source);
        Assert.Equal(LeadStatusNames.New, lead.Status);
    }

    [Fact]
    public async Task SetToken_replaces_the_token()
    {
        _service.EnsureToken();
        var old = await _service.GetToken();

        await _service.SetToken("my-own-token-value");

        Assert.True(await _service.IsValidToken("my-own-token-value"));
        Assert.False(await _service.IsValidToken(old));
    }

    [Fact]
    public async Task SetToken_creates_the_row_if_it_was_deleted()
    {
        await _service.SetToken("my-own-token-value");

        Assert.Equal("my-own-token-value", await _service.GetToken());
    }

    [Fact]
    public async Task GenerateToken_makes_a_new_random_token()
    {
        _service.EnsureToken();
        var old = await _service.GetToken();

        var generated = await _service.GenerateToken();

        Assert.NotEqual(old, generated);
        Assert.Equal(64, generated.Length);
        Assert.Equal(generated, await _service.GetToken());
    }

    [Fact]
    public async Task The_forms_own_source_wins()
    {
        var request = new SaveLeadRequest { Name = "Ann", Email = "ann@example.com", Source = "Landing" };

        var lead = await _service.Create(request, InboundLeadService.PostbackSource);

        Assert.Equal("landing", lead.Source);
    }
}
