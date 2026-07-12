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
        if (string.IsNullOrWhiteSpace(body))
            return (null, $"Error {(int)resp.StatusCode}");

        try
        {
            var problem = JsonSerializer.Deserialize<ApiProblemDetails>(body, JsonOpts);
            if (problem?.Detail is not null)
                return (null, problem.Detail);
        }
        catch (JsonException) { /* not a ProblemDetails body */ }

        return (null, body);
    }

    public async Task<(RatResponse? Rat, string? Error, string? Reason)> RetireRatAsync(string id)
    {
        var resp = await http.PostAsync($"/api/rats/{id}/retire", null);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts), null, null);
        var (error, reason) = await ParseErrorAsync(resp);
        return (null, error, reason);
    }

    public async Task<(PregnancyResponse? Result, string? Error, string? Reason)> BreedRatsAsync(string femaleRatId, string maleRatId)
    {
        var resp = await http.PostAsJsonAsync("/api/rats/breed", new { femaleRatId, maleRatId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<PregnancyResponse>(JsonOpts), null, null);
        var (error, reason) = await ParseErrorAsync(resp);
        return (null, error, reason);
    }

    // Shared 409/404 conflict-body parser — reused by any endpoint returning the
    // { error, reason } shape (retire today; breed/train/sex-separation later).
    static async Task<(string? Error, string? Reason)> ParseErrorAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return ("Rat not found.", null);

        var body = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return ($"Error {(int)resp.StatusCode}", null);

        try
        {
            var err = JsonSerializer.Deserialize<ApiErrorResponse>(body, JsonOpts);
            if (err?.Reason is not null)
                return (err.Error ?? err.Reason, err.Reason);
        }
        catch (JsonException) { /* not a structured error body */ }

        return (body, null);
    }

    // --- Home ---

    public async Task<(HomeResponse? Home, string? Error)> GetHomeAsync()
    {
        var resp = await http.GetAsync("/api/home/");
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"GET /api/home/ → {(int)resp.StatusCode} {resp.StatusCode}: {body}");
            return (null, string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)resp.StatusCode}" : body);
        }
        return (await resp.Content.ReadFromJsonAsync<HomeResponse>(JsonOpts), null);
    }

    // --- Achievements (Task 18a) ---

    public async Task<(AchievementsResponse? Result, string? Error)> GetAchievementsAsync()
    {
        var resp = await http.GetAsync("/api/achievements/");
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"GET /api/achievements/ → {(int)resp.StatusCode} {resp.StatusCode}: {body}");
            return (null, string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)resp.StatusCode}" : body);
        }
        return (await resp.Content.ReadFromJsonAsync<AchievementsResponse>(JsonOpts), null);
    }

    // --- Challenges (Task 18d) ---

    public async Task<ChallengeWeekResponse?> GetChallengesAsync()
    {
        var resp = await http.GetAsync("/api/challenges/");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ChallengeWeekResponse>(JsonOpts)
            : null;
    }

    // --- Seasonal Events (Task 18e) ---

    public async Task<SeasonalEventResponse?> GetSeasonalEventAsync()
    {
        var resp = await http.GetAsync("/api/seasonal-event/");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SeasonalEventResponse>(JsonOpts)
            : null;
    }

    // --- Titles (Task 18b) ---

    public async Task<(TitlesResponse? Result, string? Error)> GetTitlesAsync()
    {
        var resp = await http.GetAsync("/api/titles/");
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<TitlesResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(bool Success, string? Error)> SetActiveTitleAsync(string? titleId)
    {
        var resp = await http.PutAsJsonAsync("/api/players/me/title", new { titleId }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    // --- Daily Rewards (Task 18c) ---

    public async Task<(ClaimDailyRewardResponse? Result, string? Error)> ClaimDailyRewardAsync()
    {
        var resp = await http.PostAsync("/api/daily-reward/claim", null);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<ClaimDailyRewardResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 409 ? "Already claimed today." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<DailyRewardCalendarResponse?> GetDailyRewardCalendarAsync()
    {
        var resp = await http.GetAsync("/api/daily-reward/calendar");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<DailyRewardCalendarResponse>(JsonOpts)
            : null;
    }

    public async Task<bool> RenameHomeAsync(string name)
    {
        var resp = await http.PatchAsJsonAsync("/api/home/name", new { name }, JsonOpts);
        return resp.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? Error)> RenameCageAsync(string cageId, string name)
    {
        var resp = await http.PatchAsJsonAsync($"/api/home/cages/{cageId}/name", new { name }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<bool> RefillFoodAsync(string cageId, string? foodTypeId = null)
    {
        var url = $"/api/home/cages/{cageId}/food";
        if (foodTypeId is not null) url += $"?foodTypeId={Uri.EscapeDataString(foodTypeId)}";
        var resp = await http.PostAsync(url, null);
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

    public async Task<(bool Success, string? Error)> InstallBowlAsync(string cageId, string bowlTypeId)
    {
        var resp = await http.PostAsJsonAsync($"/api/home/cages/{cageId}/bowls", new { bowlTypeId }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<bool> RemoveBowlAsync(string cageId, string bowlId)
    {
        var resp = await http.DeleteAsync($"/api/home/cages/{cageId}/bowls/{bowlId}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? Error)> InstallBottleAsync(string cageId, string bottleTypeId)
    {
        var resp = await http.PostAsJsonAsync($"/api/home/cages/{cageId}/bottles", new { bottleTypeId }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<bool> RemoveBottleAsync(string cageId, string bottleId)
    {
        var resp = await http.DeleteAsync($"/api/home/cages/{cageId}/bottles/{bottleId}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? Error)> PlaceRatFromCarryCaseAsync(string carryCaseId, string cageId)
    {
        var resp = await http.PostAsync($"/api/home/carry-cases/{carryCaseId}/place/{cageId}", null);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(bool Success, string? Error)> InstallBowlFromDrawerAsync(string cageId, string itemId)
    {
        var resp = await http.PostAsJsonAsync($"/api/home/cages/{cageId}/bowls", new { itemId }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(bool Success, string? Error)> InstallBottleFromDrawerAsync(string cageId, string itemId)
    {
        var resp = await http.PostAsJsonAsync($"/api/home/cages/{cageId}/bottles", new { itemId }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(bool Success, string? Error)> InstallAccessoryFromDrawerAsync(string cageId, string itemId)
    {
        var resp = await http.PostAsJsonAsync($"/api/home/cages/{cageId}/accessories", new { itemId }, JsonOpts);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    // --- Game ---

    public async Task<GameSettingsResponse?> GetGameSettingsAsync()
    {
        var resp = await http.GetAsync("/api/game/settings");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<GameSettingsResponse>(JsonOpts)
            : null;
    }

    public async Task<TrainingRegimeResponse[]?> GetTrainingRegimesAsync()
    {
        var resp = await http.GetAsync("/api/game/training-regimes");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<TrainingRegimeResponse[]>(JsonOpts)
            : null;
    }

    // --- Events ---

    public async Task<TutorialEventResponse?> RunTutorialAsync(string ratId)
    {
        var resp = await http.PostAsJsonAsync("/api/events/tutorial", new { ratId }, JsonOpts);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<TutorialEventResponse>(JsonOpts)
            : null;
    }

    public async Task<LobbyResponse[]?> GetOpenLobbiesAsync()
    {
        var resp = await http.GetAsync("/api/events");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<LobbyResponse[]>(JsonOpts)
            : null;
    }

    public async Task<LobbyResponse?> GetLobbyAsync(string lobbyId)
    {
        var resp = await http.GetAsync($"/api/events/{lobbyId}");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<LobbyResponse>(JsonOpts)
            : null;
    }

    public async Task<(LobbyResultEntryResponse[]? Results, string? Error)> GetLobbyResultsAsync(string lobbyId)
    {
        var resp = await http.GetAsync($"/api/events/{lobbyId}/results");
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<LobbyResultEntryResponse[]>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(LobbyResponse? Lobby, string? Error)> CreateLobbyAsync(string eventDefinitionId, string ratId)
    {
        var resp = await http.PostAsJsonAsync("/api/events/lobbies", new { eventDefinitionId, ratId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<LobbyResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(bool Success, string? Error)> EnterLobbyAsync(string lobbyId, string ratId)
    {
        var resp = await http.PostAsJsonAsync($"/api/events/{lobbyId}/enter", new { ratId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<PlayerEventsResponse?> GetMyEventsAsync(int? limit = null)
    {
        var url = limit.HasValue ? $"/api/events/me?limit={limit}" : "/api/events/me";
        var resp = await http.GetAsync(url);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PlayerEventsResponse>(JsonOpts)
            : null;
    }

    // --- Shop ---

    public async Task<ShopCatalogueResponse?> GetShopCatalogueAsync()
    {
        var resp = await http.GetAsync("/api/shop/");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ShopCatalogueResponse>(JsonOpts)
            : null;
    }

    public async Task<(CagePurchaseResponse? Result, string? Error)> BuyCageAsync(string cageTypeId)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/cage", new { cageTypeId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<CagePurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(InventoryPurchaseResponse? Result, string? Error)> BuyAccessoryAsync(string accessoryTypeId)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/accessory", new { accessoryTypeId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<InventoryPurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(InventoryPurchaseResponse? Result, string? Error)> BuyBowlAsync(string bowlTypeId)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/bowl", new { bowlTypeId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<InventoryPurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(InventoryPurchaseResponse? Result, string? Error)> BuyBottleAsync(string bottleTypeId)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/bottle", new { bottleTypeId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<InventoryPurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(FoodStorageBinPurchaseResponse? Result, string? Error)> BuyFoodStorageBinAsync(int anchorIndex)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/food-storage-bin", new { anchorIndex }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<FoodStorageBinPurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(CarryCasePurchaseResponse? Result, string? Error)> BuyCarryCaseAsync(int anchorIndex)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/carry-case", new { anchorIndex }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<CarryCasePurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(StorageDrawersPurchaseResponse? Result, string? Error)> BuyStorageDrawersAsync(int anchorIndex)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/storage-drawers", new { anchorIndex }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<StorageDrawersPurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(FoodPurchaseResponse? Result, string? Error)> BuyFoodAsync(string binId, string foodTypeId, int ratDays)
    {
        var resp = await http.PostAsJsonAsync("/api/shop/buy/food", new { binId, foodTypeId, ratDays }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<FoodPurchaseResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    // --- Inventory ---

    public async Task<InventoryResponse?> GetInventoryAsync()
    {
        var resp = await http.GetAsync("/api/inventory/");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<InventoryResponse>(JsonOpts)
            : null;
    }

    public async Task<(PlaceInventoryItemResponse? Result, string? Error)> PlaceInventoryItemAsync(string itemId, string cageId)
    {
        var resp = await http.PostAsJsonAsync($"/api/inventory/{itemId}/place", new { cageId }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<PlaceInventoryItemResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<bool> RemoveInventoryItemAsync(string itemId)
    {
        var resp = await http.DeleteAsync($"/api/inventory/{itemId}");
        return resp.IsSuccessStatusCode;
    }

    // --- Marketplace ---

    public async Task<MarketplaceListingResponse[]?> GetMarketplaceListingsAsync()
    {
        var resp = await http.GetAsync("/api/marketplace/listings");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MarketplaceListingResponse[]>(JsonOpts)
            : null;
    }

    public async Task<(CreateListingResponse? Result, string? Error)> CreateListingAsync(string ratId, decimal price)
    {
        var resp = await http.PostAsJsonAsync("/api/marketplace/listings", new { ratId, price }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<CreateListingResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(BuyListingResponse? Result, string? Error)> BuyListingAsync(string listingId)
    {
        var resp = await http.PostAsync($"/api/marketplace/listings/{listingId}/buy", null);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<BuyListingResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    public async Task<(bool Success, string? Error)> CancelListingAsync(string listingId)
    {
        var resp = await http.DeleteAsync($"/api/marketplace/listings/{listingId}");
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }

    // --- Player ---

    public async Task<PlayerProfileResponse?> GetPlayerProfileAsync()
    {
        var resp = await http.GetAsync("/api/players/me");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PlayerProfileResponse>(JsonOpts)
            : null;
    }

    public async Task<(PlayerProfileResponse? Result, string? Error)> UpdatePlayerProfileAsync(
        string? country, string? state, bool weatherEnabled)
    {
        var resp = await http.PatchAsJsonAsync("/api/players/me",
            new { country, state, weatherEnabled }, JsonOpts);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<PlayerProfileResponse>(JsonOpts), null);
        var body = await resp.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
    }
}
