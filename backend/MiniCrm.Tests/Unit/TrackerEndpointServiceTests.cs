using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

// The settings page rules: which templates can be saved, preview and reset.
public class TrackerEndpointServiceTests : IDisposable
{
    private const int PingId = 1;
    private const int PurchaseId = 2;

    private readonly AppDbContext _db = TestData.InMemoryDb();
    private readonly TrackerEndpointService _service;

    public TrackerEndpointServiceTests()
    {
        _service = new TrackerEndpointService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static TrackerEndpoint Purchase() => DefaultEndpoints.All().Single(e => e.Name == DefaultEndpoints.PurchaseName);
    private static TrackerEndpoint Ping() => DefaultEndpoints.All().Single(e => e.Name == DefaultEndpoints.PingName);

    // The default purchase templates, with one change.
    private static SaveTrackerEndpointRequest PurchaseRequest(Action<SaveTrackerEndpointRequest>? change = null)
    {
        var request = new SaveTrackerEndpointRequest
        {
            UrlTemplate = Purchase().UrlTemplate,
            BodyTemplate = Purchase().BodyTemplate,
            EventIdTemplate = Purchase().EventIdTemplate
        };
        change?.Invoke(request);
        return request;
    }

    [Fact]
    public void Default_templates_are_valid()
    {
        Assert.Empty(TrackerEndpointService.Validate(Purchase(), PurchaseRequest()));
        Assert.Empty(TrackerEndpointService.Validate(Ping(), new SaveTrackerEndpointRequest { UrlTemplate = Ping().UrlTemplate }));
    }

    [Fact]
    public void Unknown_macro_in_the_body_is_rejected()
    {
        var errors = TrackerEndpointService.Validate(Purchase(),
            PurchaseRequest(r => r.BodyTemplate = """{"event_id":"{event_id}","x":"{emial}"}"""));

        Assert.Equal("Unknown macro: {emial}.", errors[TrackerEndpointService.BodyField]);
    }

    [Fact]
    public void Unknown_macro_in_the_url_is_rejected()
    {
        var errors = TrackerEndpointService.Validate(Purchase(),
            PurchaseRequest(r => r.UrlTemplate = "https://tracker.test/c?id={leadid}"));

        Assert.Contains(TrackerEndpointService.UrlField, errors.Keys);
    }

    [Fact]
    public void Body_that_is_not_json_with_the_sample_lead_is_rejected()
    {
        var errors = TrackerEndpointService.Validate(Purchase(),
            PurchaseRequest(r => r.BodyTemplate = """{"event_id":"{event_id}",}"""));

        Assert.Contains(TrackerEndpointService.BodyField, errors.Keys);
    }

    [Fact]
    public void Post_endpoint_needs_a_body()
    {
        var errors = TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.BodyTemplate = "  "));

        Assert.Equal("The body is required.", errors[TrackerEndpointService.BodyField]);
    }

    [Theory]
    [InlineData("http://tracker.test/conversions")] // the key would go over plain HTTP
    [InlineData("tracker.test/conversions")]
    [InlineData("ftp://tracker.test/conversions")]
    public void Url_must_be_a_full_https_address(string url)
    {
        var errors = TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.UrlTemplate = url));

        Assert.Contains(TrackerEndpointService.UrlField, errors.Keys);
    }

    [Theory]
    [InlineData("conv_{email}")]        // changes when the lead is edited
    [InlineData("conv_{lead_id}_{name}")]
    [InlineData("conv_{event_id}")]
    public void Event_id_can_only_use_stable_macros(string template)
    {
        var errors = TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.EventIdTemplate = template));

        Assert.StartsWith("The event_id can only use {lead_id}, {event_type}", errors[TrackerEndpointService.EventIdField]);
    }

    [Theory]
    [InlineData("conversion")]           // every lead would get the same event_id
    [InlineData("{event_type}")]
    [InlineData("")]
    public void Event_id_must_contain_the_lead_id(string template)
    {
        var errors = TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.EventIdTemplate = template));

        Assert.Contains(TrackerEndpointService.EventIdField, errors.Keys);
    }

    [Fact]
    public void Event_id_is_checked_with_the_longest_lead_id()
    {
        // 118 + 10 digits = 128 fits; one more character doesn't.
        var fits = new string('x', 118) + "{lead_id}";
        var tooLong = new string('x', 119) + "{lead_id}";

        Assert.Empty(TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.EventIdTemplate = fits)));
        Assert.Contains(TrackerEndpointService.EventIdField,
            TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.EventIdTemplate = tooLong)).Keys);
    }

    [Fact]
    public void Url_is_required_and_limited_to_the_column_size()
    {
        var empty = TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.UrlTemplate = "  "));
        var tooLong = TrackerEndpointService.Validate(Purchase(),
            PurchaseRequest(r => r.UrlTemplate = "https://tracker.test/" + new string('x', 2000)));

        Assert.Equal("The URL is required.", empty[TrackerEndpointService.UrlField]);
        Assert.StartsWith("The URL template can be up to 2000", tooLong[TrackerEndpointService.UrlField]);
    }

    [Fact]
    public void Event_id_template_that_renders_short_is_still_limited_to_the_column_size()
    {
        // 11 x {event_type} = 132 characters, but renders to "purchase" x 11 = 88.
        var template = string.Concat(Enumerable.Repeat("{event_type}", 11)) + "{lead_id}";

        var errors = TrackerEndpointService.Validate(Purchase(), PurchaseRequest(r => r.EventIdTemplate = template));

        Assert.StartsWith("The event_id template can be up to 128", errors[TrackerEndpointService.EventIdField]);
    }

    [Fact]
    public void Ping_cannot_use_macros_a_body_or_an_event_id()
    {
        var errors = TrackerEndpointService.Validate(Ping(), new SaveTrackerEndpointRequest
        {
            UrlTemplate = DefaultEndpoints.BaseUrl + "/ping?lead={lead_id}",
            BodyTemplate = "{}",
            EventIdTemplate = "ping_{lead_id}"
        });

        Assert.Equal(
            [TrackerEndpointService.BodyField, TrackerEndpointService.EventIdField, TrackerEndpointService.UrlField],
            errors.Keys.Order());
    }

    [Fact]
    public async Task Update_saves_valid_templates()
    {
        var result = await _service.Update(PurchaseId, PurchaseRequest(r =>
        {
            r.UrlTemplate = "https://tracker.test/conversions?source={source}";
            r.EventIdTemplate = "purchase_{lead_id}";
        }));

        Assert.Equal(EndpointOutcome.Success, result.Outcome);
        var saved = await _db.TrackerEndpoints.AsNoTracking().SingleAsync(e => e.Id == PurchaseId);
        Assert.Equal("https://tracker.test/conversions?source={source}", saved.UrlTemplate);
        Assert.Equal("purchase_{lead_id}", saved.EventIdTemplate);
    }

    [Fact]
    public async Task Update_with_an_invalid_template_saves_nothing()
    {
        var result = await _service.Update(PurchaseId, PurchaseRequest(r => r.EventIdTemplate = "conv_{email}"));

        Assert.Equal(EndpointOutcome.Invalid, result.Outcome);
        var saved = await _db.TrackerEndpoints.AsNoTracking().SingleAsync(e => e.Id == PurchaseId);
        Assert.Equal("conv_{lead_id}", saved.EventIdTemplate);
    }

    // Two people open the page. A changes only the URL, B only the body. Without writing
    // all three columns, the row would end with A's URL and B's body, which nobody saved.
    [Fact]
    public async Task Two_saves_leave_the_last_ones_templates_not_a_mix()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var dbA = new AppDbContext(options);
        await using var dbB = new AppDbContext(options);
        dbA.Database.EnsureCreated();
        await dbA.TrackerEndpoints.FindAsync(PurchaseId); // both load the row before either saves
        await dbB.TrackerEndpoints.FindAsync(PurchaseId);

        await new TrackerEndpointService(dbA).Update(PurchaseId, PurchaseRequest(r => r.UrlTemplate = "https://tracker.test/a"));
        await new TrackerEndpointService(dbB).Update(PurchaseId, PurchaseRequest(r => r.BodyTemplate = """{"event_id":"{event_id}","b":1}"""));

        await using var check = new AppDbContext(options);
        var saved = await check.TrackerEndpoints.SingleAsync(e => e.Id == PurchaseId);
        Assert.Equal(Purchase().UrlTemplate, saved.UrlTemplate); // B's URL, not A's
        Assert.Equal("""{"event_id":"{event_id}","b":1}""", saved.BodyTemplate);
    }

    [Fact]
    public async Task Update_of_an_unknown_endpoint_is_not_found()
    {
        var result = await _service.Update(999, PurchaseRequest());

        Assert.Equal(EndpointOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Reset_puts_back_the_defaults()
    {
        await _service.Update(PurchaseId, PurchaseRequest(r =>
        {
            r.UrlTemplate = "https://tracker.test/other";
            r.BodyTemplate = """{"event_id":"{event_id}"}""";
            r.EventIdTemplate = "x_{lead_id}";
        }));

        var result = await _service.Reset(PurchaseId);

        Assert.Equal(EndpointOutcome.Success, result.Outcome);
        var saved = await _db.TrackerEndpoints.AsNoTracking().SingleAsync(e => e.Id == PurchaseId);
        Assert.Equal(Purchase().UrlTemplate, saved.UrlTemplate);
        Assert.Equal(Purchase().BodyTemplate, saved.BodyTemplate);
        Assert.Equal(Purchase().EventIdTemplate, saved.EventIdTemplate);
    }

    [Fact]
    public async Task Reset_of_an_endpoint_with_no_default_is_a_conflict()
    {
        _db.TrackerEndpoints.Add(new TrackerEndpoint { Id = 3, Name = "custom", UrlTemplate = "https://tracker.test/custom" });
        await _db.SaveChangesAsync();

        var result = await _service.Reset(3);

        Assert.Equal(EndpointOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task Preview_renders_the_sample_lead_without_saving()
    {
        var preview = await _service.Preview(PurchaseId, PurchaseRequest(r => r.EventIdTemplate = "purchase_{lead_id}"));

        Assert.NotNull(preview);
        Assert.Equal("purchase_42", preview.EventId);
        Assert.Equal(DefaultEndpoints.BaseUrl + "/conversions", preview.Url);
        Assert.Contains("\"event_id\": \"purchase_42\"", preview.Body);
        Assert.Contains("\"email\": \"lead@example.com\"", preview.Body);
        Assert.Empty(preview.Errors);
        Assert.Equal("conv_{lead_id}", (await _db.TrackerEndpoints.AsNoTracking().SingleAsync(e => e.Id == PurchaseId)).EventIdTemplate);
    }

    [Fact]
    public async Task Preview_lists_the_errors_too()
    {
        var preview = await _service.Preview(PurchaseId, PurchaseRequest(r => r.BodyTemplate = """{"x":"{nope}"}"""));

        Assert.Contains(TrackerEndpointService.BodyField, preview!.Errors.Keys);
    }

    [Fact]
    public async Task Preview_of_ping_has_no_event_id()
    {
        var preview = await _service.Preview(PingId, new SaveTrackerEndpointRequest { UrlTemplate = Ping().UrlTemplate });

        Assert.Null(preview!.EventId);
        Assert.Null(preview.Body);
        Assert.Empty(preview.Errors);
    }
}
