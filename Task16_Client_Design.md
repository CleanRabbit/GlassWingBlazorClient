# Task 16 (Client) — Weather System: Design Document

## Backend Dependency

> **Refreshed 2026-07-06: the backend has since shipped.** Task 16 was implemented 2026-07-03 (918 tests passing at the time) — `POST /api/home/weather`, the `weather` block on `GET /api/home`, and weather-threshold keys on `GET /api/game/settings` are all live today. This doc was written against the pre-implementation design and hasn't been re-verified field-by-field against the real shipped response since — the shapes below are a reasonable starting point, not a guarantee. Re-verify against the live API/`openapi.json` before wiring up, same caution as before, just for a different reason now (possible drift during implementation, not non-existence).

## Overview

Weather is a **home-level** reading (one temperature + one humidity value per home, not per cage) that the client derives from a real-world location (`Country`/`State` on the player profile, already captured on `/profile`) and periodically reports to the API. The API stores it, applies it lazily during `FastForwardHealthAsync`, and returns a computed `weather` block on `GET /api/home` describing current conditions and which of the four stressors (`TooWarm`/`TooCold`/`TooHumid`/`TooDry`) are currently active, and whether the corresponding suppression accessory is installed.

The client's job, per the existing TASKS.md backlog entry ("Weather system"), is:
1. On home load, if `WeatherEnabled`, fetch current conditions for `Country`/`State` from Open-Meteo (a public, keyless, CORS-enabled weather API — client-external, not part of the GlassWing API).
2. POST the reading to `POST /api/home/weather`.
3. Load `GET /api/home` as normal (now reflecting the freshly stored reading).
4. Render the returned `weather` block and any installed weather accessories.

No new page. This is entirely a **Home page** enhancement plus a **new shop category** for the four weather accessories.

---

## 1. Pages & Components

### 1.1 Home.razor — Weather block

New card rendered directly under the cage-rename/currency header row (`Pages/Home.razor` lines ~62-78), above the cage grid. Visible whenever `home.Weather is not null` (i.e., the backend has started returning the block at all — the field is always present per the API doc, just with `null` numeric fields when nothing has been stored).

- **Enabled state** (`weather.isEnabled == true`):
  - Temperature and humidity readout, with an "as of" timestamp derived from `updatedAt` (e.g. "Updated 4m ago"; fall back to "Not yet reported" if `updatedAt` is `null`, which happens before the first successful Open-Meteo round trip).
  - A row of condition badges — `TooWarm` / `TooCold` / `TooHumid` / `TooDry` — rendered only for stressors where `conditions.<x> == true`. Colour-code per stressor type (e.g. warm = orange/`bg-warning`, cold = blue/`bg-info`, humid = teal, dry = `bg-secondary`/tan) — reuse the existing `badge bg-*` idiom seen throughout Home.razor (`TierBadgeClass`, `BarClass`).
  - **Suppressed-but-would-be-active** indicator: the API's `conditions` block already has suppression baked in (§5c of `Task16_Design.md` — `tooWarm = temp > threshold && !suppressTooWarm`), so a suppressed stressor never shows as active. To surface "your A/C is doing its job" the client independently recomputes the raw comparison client-side using the raw `temperatureCelsius`/`relativeHumidityPercent` plus the four threshold values from `GameSettingsResponse` (already fetched on Home load), and shows a muted "Suppressed by Air Conditioning Unit" chip when `rawExceedsThreshold && accessories.hasAirConditioning`. This is a client-only derived affordance — no backend field carries "would-be-active".
  - If no weather has ever been stored (`updatedAt == null`) but `isEnabled == true`, show a neutral "Awaiting first weather reading" state instead of zeroed-out numbers.
- **Disabled state** (`weather.isEnabled == false`): collapse the block to a single muted line — "Weather effects disabled — enable in Profile" — linking to `/profile`. No numeric readout, no badges. This directly satisfies the design brief's "toggle-off state should hide/dim the weather block entirely."
- Comfort status is **home-wide, not per-cage or per-rat**: temperature/humidity are single readings for the whole home, so unlike Food/Water bars (which are per-cage) this renders once, above the cage grid. Per-rat weather-stress attribution (which specific rat currently has an open `TooWarm` stress period) is **not surfaced** — `RatResponse`/`HealthState` carry no stressor list today (see Open Questions).

### 1.2 Home.razor — Weather Control section

New section parallel to the existing "Food Storage" section (Home.razor lines 391-416), titled "Weather Control", listing installed weather accessories:

- Empty state: "No weather accessories installed. <a href="/shop/weather">Buy one from the shop</a>." (mirrors the Food Storage empty state exactly).
- Populated state: one card per installed accessory type showing its name and which stressor it suppresses, plus a small "Active" / "Not needed right now" hint based on current conditions (purely cosmetic — reuses the same raw-vs-threshold comparison described above).

### 1.3 ShopCategory.razor / Shop.razor — new "weather" category

Weather accessories use **home accessory slots** (the same `TotalAccessorySlots`/`AnchorIndex` pool as `FoodStorageBin`, `CarryCase`, `StorageDrawers` — confirmed by `Task16_Design.md` §6: "same system... Players can install one of each"), **not** the cage-level enrichment-accessory system (`ShopAccessoryType`/`InstallAccessoryFromDrawerAsync`, which is a different pipeline: buy → Inventory → place-in-cage-via-drawer). This matters because it determines which existing UI pattern to clone:

- Add a `"weather"` branch to `ShopCategory.razor`'s `Category` switch, cloned from the `"food-bins"` branch (lines 237-271): same `GetOccupiedSlots()` / `AnchorPanel` slot-selection flow, same disabled-when-no-space guard.
- Add a `Shop.razor` category tile: `new("weather", "Weather Control", "Climate control for your home")`.
- Since installing a duplicate of the same accessory type is explicitly allowed but redundant (`Task16_Design.md` §6), no client-side "already installed" block — same permissiveness as the other anchor-slot categories today.

### 1.4 Profile.razor

No structural change needed — `Country`, `State`, and the `WeatherEnabled` checkbox already exist and are wired to `GET/PATCH /api/players/me` (`Pages/Profile.razor` lines 60-71). Optional copy tweak (not required for launch): note under the checkbox that enabling it now has a live gameplay effect, since today the copy is generic ("Uses your location to apply local weather conditions...") but no gameplay currently consumed it.

---

## 2. API Integration

### 2.1 Endpoints consumed

| Method & path | Purpose |
|---|---|
| `POST /api/home/weather` | Report a reading; body `{ temperatureCelsius, relativeHumidityPercent }`; `200` with no body |
| `GET /api/home` | Existing call, now returns a `weather` block (see below) |
| `GET /api/game/settings` | Existing call, gains the four weather threshold keys |
| `POST /api/shop/buy/weather-accessory` | **Inferred** — not specified by `Task16_Design.md`, which only says "assign appropriate prices in `ShopCatalogue`." Modelled directly on the existing `POST /api/shop/buy/food-storage-bin` shape (`{ accessoryTypeId, anchorIndex }` → purchase response with `NewBalance`). Confirm the actual route/body with the backend session before implementing. |

### 2.2 `ApiModels.cs` additions

```csharp
// --- Weather (Task 16) ---

public record HomeWeatherInfo(
    double? TemperatureCelsius,
    double? RelativeHumidityPercent,
    DateTime? UpdatedAt,
    bool IsEnabled,
    HomeWeatherConditions Conditions,
    HomeWeatherAccessoryFlags Accessories);

public record HomeWeatherConditions(bool TooWarm, bool TooCold, bool TooHumid, bool TooDry);

public record HomeWeatherAccessoryFlags(
    bool HasAirConditioning, bool HasRadiator, bool HasDehumidifier, bool HasHumidifier);

// Proposed — not in the API doc, needed for the anchor-slot buy/display flow.
// Mirrors HomeFoodStorageBinInfo. CONFIRM with backend before relying on this shape.
public record HomeWeatherAccessoryInfo(string Id, string TypeId, int AnchorIndex = 0);

public record ShopWeatherAccessoryType(string Id, string Name, string Suppresses, int Price);

public record WeatherAccessoryPurchaseResponse(
    string AccessoryId, string TypeId, int AnchorIndex, decimal NewBalance);
```

Extend existing records (all additions are trailing optional params, matching the existing pattern used for e.g. `HomeCarryCaseInfo`/`MarketplaceListingFee`):

```csharp
public record HomeResponse(
    /* ...existing... */
    HomeWeatherInfo? Weather = null,
    HomeWeatherAccessoryInfo[]? WeatherAccessories = null);   // proposed, see above

public record GameSettingsResponse(
    /* ...existing... */
    double? WeatherTooWarmCelsius = null,
    double? WeatherTooColdCelsius = null,
    double? WeatherTooHumidPercent = null,
    double? WeatherTooDryPercent = null);

public record ShopCatalogueResponse(
    /* ...existing... */
    ShopWeatherAccessoryType[]? WeatherAccessories = null);
```

### 2.3 `GlassWingApiClient.cs` additions

```csharp
// --- Weather ---

public async Task<bool> PostWeatherAsync(double temperatureCelsius, double relativeHumidityPercent)
{
    var resp = await http.PostAsJsonAsync("/api/home/weather",
        new { temperatureCelsius, relativeHumidityPercent }, JsonOpts);
    return resp.IsSuccessStatusCode;
}

public async Task<(WeatherAccessoryPurchaseResponse? Result, string? Error)> BuyWeatherAccessoryAsync(
    string accessoryTypeId, int anchorIndex)
{
    var resp = await http.PostAsJsonAsync("/api/shop/buy/weather-accessory",
        new { accessoryTypeId, anchorIndex }, JsonOpts);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<WeatherAccessoryPurchaseResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode == 402 ? "Insufficient funds." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}
```

### 2.4 Open-Meteo (client-external — not part of `GlassWingApiClient`)

Open-Meteo requires **no API key** and serves CORS-enabled JSON, so it can be called directly from Blazor WASM with a plain `HttpClient` (a second named client, since it targets a different host than the GlassWing API's configured `BaseAddress`). Propose a small `WeatherLookupService` (new file, same DI-singleton style as `PlayerStateService`) encapsulating both calls:

1. **Geocode** — `Country`/`State` are broad region names, not a city, so there's no exact "look up this region's coordinates" Open-Meteo call. Use the geocoding search endpoint with `State` as the query (more specific than `Country`), falling back to `Country` if `State` is blank:
   `GET https://geocoding-api.open-meteo.com/v1/search?name={state-or-country}&count=1`
   Take the first result's `latitude`/`longitude`. This is a coarse proxy (typically the largest city/place matching the name) — acceptable given weather here is a comfort-flavour mechanic, not a core system. See Open Questions.
2. **Forecast** — `GET https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,relative_humidity_2m`, reading `current.temperature_2m` / `current.relative_humidity_2m`.

---

## 3. UX Flows

### 3.1 Home load sequence

```
OnInitializedAsync:
  1. Fetch player profile (NEW — Home.razor does not call GetPlayerProfileAsync today; needed for
     Country/State/WeatherEnabled). Can run in parallel with the existing GetGameSettingsAsync /
     GetShopCatalogueAsync calls already in LoadAsync().
  2. If profile.WeatherEnabled and (profile.Country or profile.State) is set:
       a. Resolve lat/lon via WeatherLookupService (cached — see State Management)
       b. Fetch current temperature/humidity from Open-Meteo (cached — see State Management)
       c. If both steps succeeded: PostWeatherAsync(temp, humidity)
       d. Any failure at (a)/(b)/(c) → swallow, log to console, continue — never blocks home load
  3. GetHomeAsync() (existing call) — reflects whatever was just stored, or neutral values if the
     POST was skipped/failed or WeatherEnabled is false
```

Steps 2a–2c run with a short per-call timeout (~5s via `CancellationTokenSource`) so a slow or unreachable Open-Meteo never meaningfully delays the page. This preserves the ordering `Task16_Design.md` §9 specifies (fetch → POST → GET) rather than the alternative of firing the POST in the background after an initial `GetHomeAsync` — that alternative would mean the *first* home load of a session never reflects fresh weather, which defeats the point.

### 3.2 Visual treatment of active conditions

See §1.1 — badges per active stressor, colour-coded, with a client-computed "suppressed by X" chip for stressors that would be active but for an installed accessory.

### 3.3 Toggle-off (`WeatherEnabled = false`)

Weather block collapses to a single muted line (§1.1). Weather accessories remain purchasable and visible in the shop/home (per `Task16_Design.md` §10 — "still appear in the shop... benign no-op") but the "Weather Control" section shows a small note that effects are currently disabled.

---

## 4. Client-Side Validation & Guards

- Clamp/validate before POSTing: skip the POST (not just clamp) if Open-Meteo returns a temperature outside `[-50, 60]` or humidity outside `[0, 100]` — treat as a bad/garbled reading rather than trying to coerce it, since the backend would reject it with `400` anyway and there's no user-facing error surface for a background fetch.
- If `Country` and `State` are both empty while `WeatherEnabled == true`, skip the Open-Meteo calls entirely and show a hint in the weather block: "Set your location in Profile to enable weather" (linking to `/profile`) rather than silently doing nothing.
- All Open-Meteo calls wrapped in `try/catch`; network failure, timeout, non-2xx, or malformed JSON are all treated identically (skip POST, proceed to home load).
- No client-side enforcement of accessory slot uniqueness — matches the backend's explicit "not hard-blocked" stance (`Task16_Design.md` §6).

---

## 5. State Management

Weather is **re-derived every home load**, but the two external lookups are cached client-side to avoid hammering Open-Meteo on every navigation to `/home`:

| Cached value | Key | TTL | Rationale |
|---|---|---|---|
| Geocode result (lat/lon) | `$"{Country}|{State}"` | Session lifetime (until Country/State changes) | Region coordinates don't change during a session |
| Current conditions (temp/humidity) | n/a (singleton) | 15 minutes | Open-Meteo's `current` block updates roughly hourly; no need to re-fetch on every Home visit |

Held in a new singleton `WeatherLookupService` (registered in `Program.cs` alongside `PlayerStateService`), not persisted to browser storage — a fresh page load re-fetches once, which is an acceptable cold-start cost.

`PostWeatherAsync` is still called on every Home load when a (possibly cached) reading is available, even if the reading itself didn't change — this keeps `WeatherUpdatedAt` fresh server-side and is explicitly harmless per `Task16_Design.md` §4 ("Does not trigger `FastForwardHealthAsync`").

`GameSettingsResponse` (thresholds) uses the existing per-page-load fetch already in `Home.razor` — no new caching needed.

---

## 6. Open Questions / Deferred

1. **Geocoding strategy is a judgment call, not a spec.** Using `State` (or `Country` as fallback) as an Open-Meteo place-name search is an approximation — there's no "give me coordinates for this English region" endpoint. If this proves too coarse in practice, a future iteration could ask players for a specific city on `/profile` instead of/alongside Country+State.
2. **`HomeWeatherAccessoryInfo[]` (per-accessory identity + `AnchorIndex`) is proposed, not confirmed.** `Task16_Design.md` only documents four boolean flags on the `weather.accessories` block — enough to render suppression state, but not enough to drive an anchor-slot purchase/placement flow (which needs to know occupied slots, same as `FoodStorageBins`/`CarryCases`). Confirm the real shape before implementing the shop flow.
3. **`POST /api/shop/buy/weather-accessory` route/body is invented** by analogy with the existing anchor-slot purchase endpoints — the API doc explicitly punts this detail to the implementer ("assign appropriate prices in `ShopCatalogue`"). Update once the real endpoint exists.
4. **No removal/uninstall flow is designed** — the API doc doesn't mention one, and duplicates are explicitly allowed-but-pointless, so there's currently no product need for it. Revisit if backend adds one.
5. **Per-rat weather-stress attribution is not surfaced.** There's no field today (on `RatResponse`, `HealthState`, or elsewhere) exposing which specific stressor(s) are currently accumulating on a given rat, only the home-wide `conditions` block. If/when the backend exposes per-rat active stressors, `RatDetail.razor`'s Health card would be the natural place to show it.
6. **Open-Meteo CORS/reliability** is assumed fine (it's a public, keyless, CORS-enabled API commonly used from browsers) but hasn't been verified against this specific Blazor WASM hosting setup — worth a quick smoke test early in implementation.
