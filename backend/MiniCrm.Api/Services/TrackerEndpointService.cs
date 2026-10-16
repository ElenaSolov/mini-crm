using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Settings page use cases: list, preview, edit and reset the tracker endpoint templates.
// Templates are checked with a sample lead, so a bad template is rejected on save,
// not found later when a real lead converts.
public class TrackerEndpointService(AppDbContext db)
{
    public const string UrlField = "url_template";
    public const string BodyField = "body_template";
    public const string EventIdField = "event_id_template";

    // The tracker accepts event_id up to 128 characters, and so do our columns.
    private const int MaxEventIdLength = 128;
    private const int MaxUrlTemplateLength = 2000;

    // The lead the preview and the checks fill the templates with.
    private static Lead SampleLead() => new()
    {
        Id = 42,
        Name = "Ann Lee",
        Email = "lead@example.com",
        Phone = "+12025550123",
        Source = "facebook",
        Amount = 199.00m,
        Currency = "USD"
    };

    public async Task<List<TrackerEndpoint>> GetAll() =>
        await db.TrackerEndpoints.AsNoTracking().OrderBy(e => e.Id).ToListAsync();

    // Null when there is no such endpoint. Nothing is saved.
    public async Task<EndpointPreview?> Preview(int id, SaveTrackerEndpointRequest request)
    {
        var endpoint = await db.TrackerEndpoints.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);
        if (endpoint is null)
            return null;

        var values = MacroRenderer.ValuesFor(SampleLead(), endpoint.Name, DateTimeOffset.UtcNow);
        var eventId = SendsForLead(endpoint) ? MacroRenderer.RenderEventId(request.EventIdTemplate, values) : null;
        values["event_id"] = eventId ?? "";

        return new EndpointPreview(
            eventId,
            MacroRenderer.RenderUrl(request.UrlTemplate, values),
            request.BodyTemplate is null ? null : MacroRenderer.RenderBody(request.BodyTemplate, values),
            Validate(endpoint, request));
    }

    // Events already created keep their own copy of the URL and body, so an edit
    // only changes events created after it.
    public async Task<TrackerEndpointResult> Update(int id, SaveTrackerEndpointRequest request)
    {
        var endpoint = await db.TrackerEndpoints.FindAsync(id);
        if (endpoint is null)
            return TrackerEndpointResult.Missing();

        var errors = Validate(endpoint, request);
        if (errors.Count > 0)
            return TrackerEndpointResult.Invalid(errors);

        await SaveTemplates(endpoint, request.UrlTemplate, request.BodyTemplate, request.EventIdTemplate);
        return TrackerEndpointResult.Success(endpoint);
    }

    // Puts the templates back to the values from DefaultEndpoints (the same ones the migration seeds).
    public async Task<TrackerEndpointResult> Reset(int id)
    {
        var endpoint = await db.TrackerEndpoints.FindAsync(id);
        if (endpoint is null)
            return TrackerEndpointResult.Missing();

        var defaults = DefaultEndpoints.All().SingleOrDefault(d => d.Name == endpoint.Name);
        if (defaults is null)
            return TrackerEndpointResult.Conflict($"The endpoint '{endpoint.Name}' has no default to reset to.");

        await SaveTemplates(endpoint, defaults.UrlTemplate, defaults.BodyTemplate, defaults.EventIdTemplate);
        return TrackerEndpointResult.Success(endpoint);
    }

    // EF normally writes only the columns that changed. Two saves at the same time could
    // then leave a mix: one save's URL with the other's body, which nobody saved or checked
    // together. Writing all three makes the row always one save's templates ("last save wins").
    private async Task SaveTemplates(TrackerEndpoint endpoint, string url, string? body, string eventId)
    {
        endpoint.UrlTemplate = url;
        endpoint.BodyTemplate = body;
        endpoint.EventIdTemplate = eventId;

        var entry = db.Entry(endpoint);
        entry.Property(e => e.UrlTemplate).IsModified = true;
        entry.Property(e => e.BodyTemplate).IsModified = true;
        entry.Property(e => e.EventIdTemplate).IsModified = true;

        await db.SaveChangesAsync();
    }

    // One message per field (the first problem found), keyed by the JSON field name.
    public static Dictionary<string, string> Validate(TrackerEndpoint endpoint, SaveTrackerEndpointRequest request)
    {
        var errors = new Dictionary<string, string>();
        var values = MacroRenderer.ValuesFor(SampleLead(), endpoint.Name, DateTimeOffset.UtcNow);
        values["event_id"] = MacroRenderer.RenderEventId(request.EventIdTemplate, values);

        // Checked here, not with [Required]/[MaxLength] on the request: those would turn
        // the preview into a 400 and hide the rest of it while the user is typing.
        if (request.UrlTemplate == "")
            errors[UrlField] = "The URL is required.";
        else if (request.UrlTemplate.Length > MaxUrlTemplateLength)
            errors[UrlField] = $"The URL template can be up to {MaxUrlTemplateLength} characters long.";
        else if (MacroError(endpoint, request.UrlTemplate) is { } urlMacroError)
            errors[UrlField] = urlMacroError;
        else if (!IsHttpsUrl(MacroRenderer.RenderUrl(request.UrlTemplate, values)))
            errors[UrlField] = "The URL must be a full https:// address, so the API key is never sent unencrypted.";

        if (endpoint.Method == HttpMethod.Get.Method)
        {
            if (request.BodyTemplate is not null)
                errors[BodyField] = "A GET request has no body.";
        }
        else if (request.BodyTemplate is null)
            errors[BodyField] = "The body is required.";
        else if (MacroError(endpoint, request.BodyTemplate) is { } bodyMacroError)
            errors[BodyField] = bodyMacroError;
        else if (!MacroRenderer.IsValidJson(MacroRenderer.RenderBody(request.BodyTemplate, values)))
            errors[BodyField] = "With the sample lead, the body is not valid JSON. Check quotes and commas.";

        if (EventIdError(endpoint, request.EventIdTemplate) is { } eventIdError)
            errors[EventIdField] = eventIdError;

        return errors;
    }

    private static string? EventIdError(TrackerEndpoint endpoint, string template)
    {
        // E.g. ping: no events are created for it, so it has no event_id.
        if (!SendsForLead(endpoint))
            return template == "" ? null : "This endpoint is not sent for a lead, so it has no event_id.";

        if (template == "")
            return "The event_id template is required.";

        // The column size. The rendered check below isn't enough: {event_type} (12 characters)
        // renders to "purchase" (8), so a too long template can still render short.
        if (template.Length > MaxEventIdLength)
            return $"The event_id template can be up to {MaxEventIdLength} characters long.";

        var used = MacroRenderer.MacrosIn(template).ToList();
        var unstable = used.Except(MacroRenderer.EventIdMacros).ToList();
        if (unstable.Count > 0)
            return $"The event_id can only use {Braced(MacroRenderer.EventIdMacros)}, not {Braced(unstable)}. " +
                   "Other values can change, and then the same conversion would get a new event_id.";

        // event_id is unique: without the lead id, the second lead's event would clash with the first.
        if (!used.Contains("lead_id"))
            return "The event_id must contain {lead_id}, so every lead gets its own event_id.";

        // Checked with the longest possible lead id, not the sample's 42.
        var longest = new Dictionary<string, string>
        {
            ["lead_id"] = int.MaxValue.ToString(CultureInfo.InvariantCulture),
            ["event_type"] = endpoint.Name
        };
        if (MacroRenderer.RenderEventId(template, longest).Length > MaxEventIdLength)
            return $"The event_id can be up to {MaxEventIdLength} characters long.";

        return null;
    }

    // Null when every macro in the template is allowed here.
    private static string? MacroError(TrackerEndpoint endpoint, string template)
    {
        var used = MacroRenderer.MacrosIn(template).ToList();

        if (!SendsForLead(endpoint) && used.Count > 0)
            return $"This endpoint is not sent for a lead, so it can't use macros ({Braced(used)}).";

        var unknown = used.Except(MacroRenderer.AllMacros).ToList();
        return unknown.Count > 0 ? $"Unknown macro: {Braced(unknown)}." : null;
    }

    // Only endpoints with a trigger status get events, and so lead values.
    private static bool SendsForLead(TrackerEndpoint endpoint) => endpoint.TriggerStatus is not null;

    private static bool IsHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static string Braced(IEnumerable<string> macros) => string.Join(", ", macros.Select(m => $"{{{m}}}"));
}
