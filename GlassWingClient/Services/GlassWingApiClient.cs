using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlassWingClient.Services;

public class GlassWingApiClient(HttpClient http)
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // --- Auth ---

    public async Task<AuthResponse?> RegisterAsync(string username, string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/register",
            new { username, email, password }, JsonOpts);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts)
            : null;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login",
            new { email, password }, JsonOpts);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts)
            : null;
    }

    // --- Rats ---

    public async Task<RatResponse[]?> ListRatsAsync()
    {
        var resp = await http.GetAsync("/api/rats/");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<RatResponse[]>(JsonOpts)
            : null;
    }

    public async Task<RatResponse?> GetRatAsync(string id)
    {
        var resp = await http.GetAsync($"/api/rats/{id}");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts)
            : null;
    }

    public async Task<RatResponse?> CreateStarterAsync()
    {
        var resp = await http.PostAsync("/api/rats/starter", null);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts)
            : null;
    }

    public async Task<RatResponse?> RenameRatAsync(string id, string name)
    {
        var resp = await http.PatchAsJsonAsync($"/api/rats/{id}/name", new { name }, JsonOpts);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts)
            : null;
    }

    public async Task<(RatResponse? Rat, string? Error)> TrainRatAsync(string id, string stat)
    {
        var resp = await http.PostAsJsonAsync($"/api/rats/{id}/train", new { stat }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    // --- Home ---

    public async Task<HomeResponse?> GetHomeAsync()
    {
        var resp = await http.GetAsync("/api/home/");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<HomeResponse>(JsonOpts)
            : null;
    }

    public async Task<bool> RenameHomeAsync(string name)
    {
        var resp = await http.PatchAsJsonAsync("/api/home/name", new { name }, JsonOpts);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RefillFoodAsync(string cageId)
    {
        var resp = await http.PostAsync($"/api/home/cages/{cageId}/food", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RefillWaterAsync(string cageId)
    {
        var resp = await http.PostAsync($"/api/home/cages/{cageId}/water", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> SetRegimeAsync(string cageId, string? regimeId = null)
    {
        var url = $"/api/home/cages/{cageId}/regime";
        if (regimeId is not null) url += $"?regimeId={Uri.EscapeDataString(regimeId)}";
        var resp = await http.PostAsync(url, null);
        return resp.IsSuccessStatusCode;
    }

    // --- Events ---

    public async Task<TutorialEventResponse?> RunTutorialAsync(string ratId)
    {
        var resp = await http.PostAsJsonAsync("/api/events/tutorial", new { ratId }, JsonOpts);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<TutorialEventResponse>(JsonOpts)
            : null;
    }
}
