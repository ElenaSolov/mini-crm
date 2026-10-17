using MiniCrm.Api.Services;

namespace MiniCrm.Api.Dtos;

// What the "Tracker auth" card saves. Every rule is checked in TrackerAuthService.
public class SaveTrackerAuthRequest
{
    private string _headerName = "";
    private string _headerTemplate = "";
    private string? _apiKey;

    public string HeaderName { get => _headerName; set => _headerName = value?.Trim() ?? ""; }

    public string HeaderTemplate { get => _headerTemplate; set => _headerTemplate = value?.Trim() ?? ""; }

    // Blank or missing: keep the current key.
    public string? ApiKey
    {
        get => _apiKey;
        set => _apiKey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

// No key in here on purpose: only where it comes from.
public record TrackerAuthResponse(string HeaderName, string HeaderTemplate, ApiKeySource KeySource);
