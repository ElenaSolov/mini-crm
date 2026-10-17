using MiniCrm.Api.Models;

namespace MiniCrm.Api.Data;

// The default tracker endpoints and the shared auth header. The URLs and the auth header come from .env
// (Tracker__PingUrl, Tracker__ConversionsUrl, Tracker__AuthHeaderName, Tracker__AuthHeaderTemplate).
// The JSON body and the event_id stay here: multi-line JSON in .env is easy to break.
// Used at startup to add missing rows to TrackerEndpoints and TrackerAuthSettings,
// and by "Reset to default".
public class TrackerDefaults(IConfiguration config)
{
    public const string PingName = "ping";
    public const string PurchaseName = "purchase";
    public const string PurchaseEventId = "conv_{lead_id}";

    // The task's JSON with macros. "amount" has no quotes, so it is sent as a number.
    // ReplaceLineEndings: git may check this file out with \r\n on Windows, and the
    // default must be the same on every machine.
    public static readonly string PurchaseBody = """
        {
          "event_id": "{event_id}",
          "event_name": "purchase",
          "email": "{email}",
          "phone": "{phone}",
          "lead_id": "{lead_id}",
          "amount": {amount},
          "currency": "{currency}",
          "occurred_at": "{occurred_at}"
        }
        """.ReplaceLineEndings("\n");

    public TrackerEndpoint[] All() =>
    [
        new()
        {
            Name = PingName,
            Method = "GET",
            UrlTemplate = Required("Tracker:PingUrl")
        },
        new()
        {
            Name = PurchaseName,
            Method = "POST",
            TriggerStatus = LeadStatusNames.Converted,
            UrlTemplate = Required("Tracker:ConversionsUrl"),
            BodyTemplate = PurchaseBody,
            EventIdTemplate = PurchaseEventId
        }
    ];

    // Shared by ping and purchase. No key: without one saved on the settings page,
    // Tracker__ApiKey from .env is used.
    public TrackerAuthSettings Auth() => new()
    {
        Id = TrackerAuthSettings.RowId,
        HeaderName = Required("Tracker:AuthHeaderName"),
        HeaderTemplate = Required("Tracker:AuthHeaderTemplate")
    };

    private string Required(string key) =>
        config[key] is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException(
                $"{key.Replace(":", "__")} is not set. Copy it from .env.example to .env.");
}
