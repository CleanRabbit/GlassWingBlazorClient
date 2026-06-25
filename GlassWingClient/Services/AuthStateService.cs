namespace GlassWingClient.Services;

public class AuthStateService
{
    public bool IsDevBypass { get; set; }
    public string? Token { get; set; }
    public bool IsAuthenticated => IsDevBypass || !string.IsNullOrEmpty(Token);

    public string? GetAuthHeaderValue() =>
        IsDevBypass ? "Bearer dev" :
        Token is not null ? $"Bearer {Token}" : null;
}
