using MiniCrm.Api.Models;

namespace MiniCrm.Api.Data;

// The tracker endpoints from the task, defined in one place. The migration seeds
// the TrackerEndpoints table from here, and "Reset to default" (step 10) uses it too.
public static class DefaultEndpoints
{
    public const string BaseUrl = "https://bipro2interface.sseku.com/api/candidate-tracker";

    public const string PingName = "ping";
    public const string PurchaseName = "purchase";

    // The task's JSON with macros. "amount" has no quotes, so it is sent as a number.
    // ReplaceLineEndings: git may check this file out with \r\n on Windows, and the
    // seeded value must be the same on every machine.
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

    // Fixed ids, because EF seeding needs them.
    public static TrackerEndpoint[] All() =>
    [
        new()
        {
            Id = 1,
            Name = PingName,
            Method = "GET",
            UrlTemplate = BaseUrl + "/ping"
        },
        new()
        {
            Id = 2,
            Name = PurchaseName,
            Method = "POST",
            TriggerStatus = LeadStatusNames.Converted,
            UrlTemplate = BaseUrl + "/conversions",
            BodyTemplate = PurchaseBody,
            EventIdTemplate = "conv_{lead_id}"
        }
    ];
}
