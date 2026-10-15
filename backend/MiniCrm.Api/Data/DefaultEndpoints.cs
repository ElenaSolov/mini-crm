namespace MiniCrm.Api.Data;

// The tracker URLs from the task, defined in one place.
// Step 5 seeds the TrackerEndpoints table from here.
public static class DefaultEndpoints
{
    public const string BaseUrl = "https://bipro2interface.sseku.com/api/candidate-tracker";

    public const string PingUrl = BaseUrl + "/ping";
}
