using System.ComponentModel.DataAnnotations;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Dtos;

// Token is null only if its database row was deleted by hand (then the URLs don't work).
public record InboundSettingsResponse(string? Token, IReadOnlyList<string> Fields);

// A token the client picks. Long enough that it can't be guessed, and only
// characters that are safe in a URL without encoding.
public class SetInboundTokenRequest
{
    public const int MinLength = 16;

    [Required, MinLength(MinLength, ErrorMessage = "Use at least 16 characters.")]
    [MaxLength(InboundSettings.MaxTokenLength, ErrorMessage = "Use at most 64 characters.")]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Use only letters, digits, - and _.")]
    public string? Token { get; set; }
}
