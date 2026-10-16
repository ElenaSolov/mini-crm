using MiniCrm.Api.Models;

namespace MiniCrm.Api.Dtos;

// What the settings page can change: only the three templates. Name, method and
// trigger status stay as they are, because the code relies on them (e.g. "ping").
// Every rule (required, length, macros) is checked in TrackerEndpointService, so the
// preview can list all problems instead of answering 400.
public class SaveTrackerEndpointRequest
{
    private string _urlTemplate = "";
    private string? _bodyTemplate;
    private string _eventIdTemplate = "";

    public string UrlTemplate { get => _urlTemplate; set => _urlTemplate = value?.Trim() ?? ""; }

    // Not trimmed inside, so the user's formatting is kept. Only blank becomes null.
    public string? BodyTemplate
    {
        get => _bodyTemplate;
        set => _bodyTemplate = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string EventIdTemplate { get => _eventIdTemplate; set => _eventIdTemplate = value?.Trim() ?? ""; }
}

public record TrackerEndpointResponse(
    int Id,
    string Name,
    string Method,
    string? TriggerStatus,
    string UrlTemplate,
    string? BodyTemplate,
    string EventIdTemplate,
    bool IsActive)
{
    public static TrackerEndpointResponse From(TrackerEndpoint e) => new(
        e.Id, e.Name, e.Method, e.TriggerStatus, e.UrlTemplate, e.BodyTemplate, e.EventIdTemplate, e.IsActive);
}

// The macros the settings page lists. event_id templates may only use the stable ones.
public record MacroListResponse(string[] Macros, string[] EventIdMacros);
