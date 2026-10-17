namespace MiniCrm.Api.Models;

// One row: how every tracker request (ping and purchase) is authenticated.
// Created at the first startup from the .env defaults, then edited on the settings page.
public class TrackerAuthSettings
{
    public const int RowId = 1;
    public const int MaxHeaderNameLength = 100;
    public const int MaxHeaderTemplateLength = 500;
    public const int MaxApiKeyLength = 256;

    public int Id { get; set; }

    // E.g. "Authorization" or "X-Api-Key".
    public string HeaderName { get; set; } = "";

    // E.g. "Bearer {api_key}". {api_key} is replaced with the key when a request is sent.
    public string HeaderTemplate { get; set; } = "";

    // The key saved on the settings page, stored as plain text (the user's choice).
    // Null means "use Tracker__ApiKey from .env". The API never sends it back.
    public string? ApiKey { get; set; }
}
