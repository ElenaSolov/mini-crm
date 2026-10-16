using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Leads sent from the client's own website: the pixel and the postback URL.
// These URLs are public, so every request must carry the token from the InboundSettings table.
public class InboundLeadService(AppDbContext db, LeadService leads)
{
    public const string PixelSource = "pixel";
    public const string PostbackSource = "postback";

    // The lead fields a form can send (the fields of SaveLeadRequest, keep them in step).
    // Status is not one of them: a new lead is always "new".
    public static readonly IReadOnlyList<string> Fields = ["name", "email", "phone", "source", "amount", "currency"];

    // Called once at startup, so the settings page always has URLs to show.
    // At startup nothing else runs yet, so two requests can't both create the row.
    public void EnsureToken()
    {
        if (db.InboundSettings.Any())
            return;

        // 32 random bytes as hex: 64 characters, safe in a URL, impossible to guess.
        db.InboundSettings.Add(new InboundSettings { Id = InboundSettings.RowId, Token = RandomNumberGenerator.GetHexString(64) });
        db.SaveChanges();
    }

    // Null only if the row was deleted by hand; then every request is denied.
    public async Task<string?> GetToken() =>
        await db.InboundSettings.Select(s => s.Token).SingleOrDefaultAsync();

    public async Task<bool> IsValidToken(string? token)
    {
        var expected = await GetToken();
        if (token is null || expected is null)
            return false;

        // Fixed-time compare, so the response time doesn't reveal how much of the token was right.
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected));
    }

    // The form's own source wins; otherwise the lead shows where it came from ("pixel" or "postback").
    public Task<Lead> Create(SaveLeadRequest request, string defaultSource)
    {
        request.Source ??= defaultSource;
        return leads.Create(request);
    }
}
