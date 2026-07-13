using System.Text;
using System.Text.Json;

namespace GlassWingClient.Services;

public class AuthStateService
{
    public bool IsDevBypass { get; set; }
    public string? Token { get; set; }
    public string? PlayerId { get; set; }
    public string? Username { get; set; }
    public bool IsAuthenticated => IsDevBypass || !string.IsNullOrEmpty(Token);

    // Task 32 §4 — populated from the JWT's `role` claim after login/register (via SetToken).
    // Purely cosmetic: used only to conditionally show the "Admin" nav entry and to redirect
    // non-admins away from /admin/* pages in OnInitializedAsync. The API re-checks Player.Role
    // against the database live on every /api/admin/* request (AdminOnlyFilter) — nothing here is
    // ever trusted server-side. If admin is revoked mid-session, this flag won't flip until the
    // account's next login (the nav link/page shell would still show), even though the API
    // starts 403ing immediately — acceptable since this was never the security boundary.
    public bool IsAdmin { get; set; }

    public string? GetAuthHeaderValue() =>
        IsDevBypass ? "Bearer dev" :
        Token is not null ? $"Bearer {Token}" : null;

    // Sets Token and derives IsAdmin from its `role` claim in the same step, so the two can never
    // drift out of sync — call this instead of assigning Token directly (Login.razor/Register.razor).
    public void SetToken(string token)
    {
        Token = token;
        IsAdmin = string.Equals(TryReadRoleClaim(token), "Admin", StringComparison.Ordinal);
    }

    private static string? TryReadRoleClaim(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty("role", out var role) ? role.GetString() : null;
        }
        catch
        {
            // Malformed/unexpected token shape — never let cosmetic claim-reading throw and
            // break login; just treat it as non-admin.
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
