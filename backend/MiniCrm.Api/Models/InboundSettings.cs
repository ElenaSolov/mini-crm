namespace MiniCrm.Api.Models;

// One row: the token of the pixel and postback URLs. Created with a random value
// at the first startup, never in the migration: a token written in code would be
// the same for everyone who clones the repo. The client can set their own value
// or generate a new one on the settings page.
public class InboundSettings
{
    public const int RowId = 1;
    public const int MaxTokenLength = 64;

    public int Id { get; set; }
    public string Token { get; set; } = "";
}
