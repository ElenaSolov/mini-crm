using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Where the key in use comes from. The key itself is never sent to the browser.
public enum ApiKeySource { Settings, Env, None }

// The settings page's "Tracker auth" card: one auth header for every tracker request
// (ping and purchase), and the API key. A key saved here wins over Tracker__ApiKey in .env.
public class TrackerAuthService(AppDbContext db, TrackerDefaults defaults, IConfiguration config)
{
    public const string HeaderNameField = "header_name";
    public const string HeaderTemplateField = "header_template";
    public const string ApiKeyField = "api_key";

    public const string ApiKeyMacroName = "api_key";
    public const string ApiKeyMacro = "{" + ApiKeyMacroName + "}";

    // A header name is one word of letters, digits and "-" (e.g. X-Api-Key).
    private static readonly Regex HeaderNamePattern = new("^[A-Za-z0-9-]+$");

    // Headers .NET sets itself from the URL and the body. Using one would break the request.
    private static readonly string[] ReservedHeaders = ["Host", "Content-Type", "Content-Length", "Transfer-Encoding", "Connection"];

    // Called once at startup, like InboundLeadService.EnsureToken. Creates the row from the
    // .env defaults if there is none; a saved row is left as it is. The defaults are checked
    // on every start, so a typo in .env stops the app with a clear message.
    public void EnsureSettings()
    {
        var auth = defaults.Auth();
        var errors = Validate(new SaveTrackerAuthRequest { HeaderName = auth.HeaderName, HeaderTemplate = auth.HeaderTemplate });
        if (errors.Count > 0)
            throw new InvalidOperationException($"The .env default auth header is not valid: {string.Join(" ", errors.Values)}");

        if (db.TrackerAuthSettings.Any())
            return;

        db.TrackerAuthSettings.Add(auth);
        db.SaveChanges();
    }

    public async Task<TrackerAuthResponse> Get() => Response(await Load());

    // Empty errors means saved. A blank key keeps the current one: the page never gets the
    // key back, so it can't send it again with every save.
    public async Task<Dictionary<string, string>> Update(SaveTrackerAuthRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return errors;

        await Save(request.HeaderName, request.HeaderTemplate, request.ApiKey);
        return errors;
    }

    // Puts the header back to the .env defaults. The saved key stays: it has its own "Clear".
    public async Task Reset()
    {
        var auth = defaults.Auth();
        await Save(auth.HeaderName, auth.HeaderTemplate, apiKey: null);
    }

    // After this, Tracker__ApiKey from .env is used again.
    public async Task ClearKey()
    {
        var settings = await LoadTracked();
        settings.ApiKey = null;
        await db.SaveChangesAsync();
    }

    // The header TrackerClient adds to every request. Read at send time, so a new key or
    // header also applies to retries of events already queued. Null when there is no key
    // anywhere: the request is sent without it, and the tracker answers 401.
    public async Task<KeyValuePair<string, string>?> CurrentHeader(CancellationToken cancellationToken = default)
    {
        var settings = await Load(cancellationToken);
        var apiKey = ApiKeyFor(settings);
        if (apiKey is null)
            return null;

        return new(settings.HeaderName, settings.HeaderTemplate.Replace(ApiKeyMacro, apiKey));
    }

    // EF normally writes only the columns that changed. Writing both header columns keeps the
    // row one save's header, never a mix of two saves made at the same time ("last save wins").
    private async Task Save(string headerName, string headerTemplate, string? apiKey)
    {
        var settings = await LoadTracked();
        settings.HeaderName = headerName;
        settings.HeaderTemplate = headerTemplate;
        if (apiKey is not null)
            settings.ApiKey = apiKey;

        var entry = db.Entry(settings);
        if (entry.State != EntityState.Added)
        {
            entry.Property(s => s.HeaderName).IsModified = true;
            entry.Property(s => s.HeaderTemplate).IsModified = true;
        }

        await db.SaveChangesAsync();
    }

    private async Task<TrackerAuthSettings> Load(CancellationToken cancellationToken = default) =>
        await db.TrackerAuthSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == TrackerAuthSettings.RowId, cancellationToken)
        ?? defaults.Auth();

    // The row is created at startup; adding it here only covers a row deleted by hand.
    private async Task<TrackerAuthSettings> LoadTracked()
    {
        var settings = await db.TrackerAuthSettings.FindAsync(TrackerAuthSettings.RowId);
        if (settings is not null)
            return settings;

        settings = defaults.Auth();
        db.TrackerAuthSettings.Add(settings);
        return settings;
    }

    private TrackerAuthResponse Response(TrackerAuthSettings settings)
    {
        var source = settings.ApiKey is not null ? ApiKeySource.Settings
            : ApiKeyFor(settings) is not null ? ApiKeySource.Env
            : ApiKeySource.None;
        return new TrackerAuthResponse(settings.HeaderName, settings.HeaderTemplate, source);
    }

    // The key saved on the settings page wins; otherwise Tracker__ApiKey from .env.
    private string? ApiKeyFor(TrackerAuthSettings settings) =>
        settings.ApiKey ?? (config["Tracker:ApiKey"] is { } key && !string.IsNullOrWhiteSpace(key) ? key.Trim() : null);

    // One message per field (the first problem found), keyed by the JSON field name.
    public static Dictionary<string, string> Validate(SaveTrackerAuthRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (HeaderNameError(request.HeaderName) is { } nameError)
            errors[HeaderNameField] = nameError;

        if (HeaderTemplateError(request.HeaderTemplate) is { } templateError)
            errors[HeaderTemplateField] = templateError;

        if (request.ApiKey is not null && ApiKeyError(request.ApiKey) is { } keyError)
            errors[ApiKeyField] = keyError;

        return errors;
    }

    private static string? HeaderNameError(string name)
    {
        if (name == "")
            return "The header name is required.";
        if (name.Length > TrackerAuthSettings.MaxHeaderNameLength)
            return $"The header name can be up to {TrackerAuthSettings.MaxHeaderNameLength} characters long.";
        if (!HeaderNamePattern.IsMatch(name))
            return "The header name can only use letters, digits and '-', e.g. Authorization or X-Api-Key.";
        if (ReservedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
            return $"{name} is set from the URL and the body, so it can't be the auth header.";
        return null;
    }

    // {api_key} and nothing else: the key goes in its own field, which the API never sends back.
    // A key typed into the template would be shown on the page to anyone who opens it.
    private static string? HeaderTemplateError(string template)
    {
        if (template == "")
            return "The header value is required.";
        if (template.Length > TrackerAuthSettings.MaxHeaderTemplateLength)
            return $"The header value can be up to {TrackerAuthSettings.MaxHeaderTemplateLength} characters long.";
        // A line break would let the value add a second header of its own.
        if (template.Any(char.IsControl))
            return "The header value can't contain line breaks or other control characters.";

        var used = MacroRenderer.MacrosIn(template).ToList();
        var others = used.Where(m => m != ApiKeyMacroName).ToList();
        if (others.Count > 0)
            return $"The header value can only use {ApiKeyMacro}, not {string.Join(", ", others.Select(m => $"{{{m}}}"))}.";
        if (used.Count == 0)
            return $"The header value must contain {ApiKeyMacro}. Put the key itself in the API key field.";
        return null;
    }

    private static string? ApiKeyError(string apiKey)
    {
        if (apiKey.Length > TrackerAuthSettings.MaxApiKeyLength)
            return $"The API key can be up to {TrackerAuthSettings.MaxApiKeyLength} characters long.";
        // Same reason as the header value: no line breaks, and a key never has spaces.
        if (apiKey.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
            return "The API key can't contain spaces or line breaks.";
        return null;
    }
}
