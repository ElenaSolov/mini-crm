using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

// The shared auth header and API key: rules, where the key comes from, and the startup row.
public class TrackerAuthServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestData.InMemoryDb();

    public void Dispose() => _db.Dispose();

    private TrackerAuthService Service(string? envKey = "env-key", Dictionary<string, string?>? settings = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tracker:ApiKey"] = envKey })
            .Build();
        var defaults = new TrackerDefaults(new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? TestData.TrackerSettings).Build());
        return new TrackerAuthService(_db, defaults, config);
    }

    private static SaveTrackerAuthRequest Request(Action<SaveTrackerAuthRequest>? change = null)
    {
        var request = new SaveTrackerAuthRequest
        {
            HeaderName = TestData.AuthHeaderName,
            HeaderTemplate = TestData.AuthHeaderTemplate
        };
        change?.Invoke(request);
        return request;
    }

    [Fact]
    public void Default_and_custom_headers_are_valid()
    {
        Assert.Empty(TrackerAuthService.Validate(Request()));
        Assert.Empty(TrackerAuthService.Validate(Request(r =>
        {
            r.HeaderName = "X-Api-Key";
            r.HeaderTemplate = "{api_key}";
            r.ApiKey = "abc123";
        })));
    }

    [Theory]
    [InlineData("")]
    [InlineData("X Api Key")]     // a space is not allowed in a header name
    [InlineData("X-Key:")]
    [InlineData("Host")]          // set by .NET from the URL
    [InlineData("content-type")]  // set by .NET from the body
    public void Bad_header_name_is_rejected(string name)
    {
        Assert.Contains(TrackerAuthService.HeaderNameField, TrackerAuthService.Validate(Request(r => r.HeaderName = name)).Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bearer my-real-key")]             // the key goes in its own write-only field
    [InlineData("Bearer {api_key} {email}")]       // lead values don't belong in the auth header
    [InlineData("Bearer {api_key}\r\nX-Other: 1")] // a line break would add a second header
    public void Bad_header_value_is_rejected(string template)
    {
        Assert.Contains(TrackerAuthService.HeaderTemplateField,
            TrackerAuthService.Validate(Request(r => r.HeaderTemplate = template)).Keys);
    }

    [Theory]
    [InlineData("abc 123")]
    [InlineData("abc\n123")]
    public void Api_key_with_spaces_or_line_breaks_is_rejected(string key)
    {
        var errors = TrackerAuthService.Validate(Request(r => r.ApiKey = key));

        Assert.Contains(TrackerAuthService.ApiKeyField, errors.Keys);
    }

    [Fact]
    public async Task Without_a_saved_key_the_env_key_is_used()
    {
        var header = await Service(envKey: "env-key").CurrentHeader();

        Assert.Equal(new KeyValuePair<string, string>("Authorization", "Bearer env-key"), header);
        Assert.Equal(ApiKeySource.Env, (await Service().Get()).KeySource);
    }

    [Fact]
    public async Task Saved_key_wins_and_clearing_it_goes_back_to_env()
    {
        var service = Service(envKey: "env-key");

        await service.Update(Request(r => r.ApiKey = "settings-key"));
        Assert.Equal("Bearer settings-key", (await service.CurrentHeader())!.Value.Value);
        Assert.Equal(ApiKeySource.Settings, (await service.Get()).KeySource);

        await service.ClearKey();
        Assert.Equal("Bearer env-key", (await service.CurrentHeader())!.Value.Value);
    }

    [Fact]
    public async Task Saving_without_a_key_keeps_the_saved_key()
    {
        var service = Service();
        await service.Update(Request(r => r.ApiKey = "settings-key"));

        await service.Update(Request(r => r.HeaderName = "X-Api-Key"));

        var saved = await _db.TrackerAuthSettings.AsNoTracking().SingleAsync();
        Assert.Equal("X-Api-Key", saved.HeaderName);
        Assert.Equal("settings-key", saved.ApiKey);
    }

    [Fact]
    public async Task No_key_anywhere_means_no_header()
    {
        var service = Service(envKey: null);

        Assert.Null(await service.CurrentHeader());
        Assert.Equal(ApiKeySource.None, (await service.Get()).KeySource);
    }

    [Fact]
    public async Task Invalid_save_changes_nothing()
    {
        var errors = await Service().Update(Request(r =>
        {
            r.HeaderName = "Bad Name";
            r.ApiKey = "settings-key";
        }));

        Assert.Contains(TrackerAuthService.HeaderNameField, errors.Keys);
        var saved = await _db.TrackerAuthSettings.AsNoTracking().SingleAsync();
        Assert.Equal(TestData.AuthHeaderName, saved.HeaderName);
        Assert.Null(saved.ApiKey);
    }

    [Fact]
    public async Task Reset_puts_back_the_env_header_and_keeps_the_key()
    {
        var service = Service();
        await service.Update(Request(r =>
        {
            r.HeaderName = "X-Api-Key";
            r.HeaderTemplate = "{api_key}";
            r.ApiKey = "settings-key";
        }));

        await service.Reset();

        var saved = await _db.TrackerAuthSettings.AsNoTracking().SingleAsync();
        Assert.Equal(TestData.AuthHeaderName, saved.HeaderName);
        Assert.Equal(TestData.AuthHeaderTemplate, saved.HeaderTemplate);
        Assert.Equal("settings-key", saved.ApiKey);
    }

    [Fact]
    public void Startup_creates_the_row_from_env_only_when_missing()
    {
        _db.TrackerAuthSettings.RemoveRange(_db.TrackerAuthSettings);
        _db.SaveChanges();

        Service().EnsureSettings();
        var created = _db.TrackerAuthSettings.AsNoTracking().Single();
        Assert.Equal(TestData.AuthHeaderTemplate, created.HeaderTemplate);

        var other = new Dictionary<string, string?>(TestData.TrackerSettings) { ["Tracker:AuthHeaderName"] = "X-Other" };
        Service(settings: other).EnsureSettings();
        Assert.Equal(TestData.AuthHeaderName, _db.TrackerAuthSettings.AsNoTracking().Single().HeaderName); // saved row kept
    }

    [Theory]
    [InlineData("Tracker:AuthHeaderName", null)]                  // missing in .env
    [InlineData("Tracker:AuthHeaderTemplate", "Bearer real-key")] // no {api_key}
    public void Startup_stops_when_the_env_auth_default_is_missing_or_invalid(string key, string? value)
    {
        var settings = new Dictionary<string, string?>(TestData.TrackerSettings) { [key] = value };

        Assert.Throws<InvalidOperationException>(() => Service(settings: settings).EnsureSettings());
    }
}
