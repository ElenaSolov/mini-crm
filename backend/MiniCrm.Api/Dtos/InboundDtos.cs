namespace MiniCrm.Api.Dtos;

// Token is null only if its database row was deleted by hand (then the URLs don't work).
public record InboundSettingsResponse(string? Token, IReadOnlyList<string> Fields);
