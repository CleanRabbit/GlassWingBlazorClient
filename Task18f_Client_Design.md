# Task 18f — Cosmetics: Client Design Document

## 1. Overview

Cosmetics are purely visual cage decorations and rat accessories, acquired via direct shop purchase, achievement rewards, seasonal event completion, or the daily calendar. The client needs a browsable/purchasable catalogue (reusing the existing Shop buy-flow pattern) plus equip controls on the Home page (cages) and rat detail page (rats).

**Backend Dependency — Refreshed 2026-07-06: shipped.** Task 18f was implemented 2026-07-03 (1195 tests passing at the time, commit `9277541`). The cosmetic catalogue and shop/equip endpoints are live — **note cage/rat cosmetic routes use this API's existing `/api/home/cages/...`/`/api/rats/...` conventions, not a top-level `/api/cosmetics/...` path**, if this doc assumed the latter anywhere below. Re-verify shapes against the live API/`openapi.json` before wiring up.

---

## 2. Shared Conventions

See Task18a_Client_Design.md §2 for the canonical "Progress" nav entry, Progress hub page, `RewardToastService`/`RewardToastHost`, `ProgressStateService`, and terminology conventions. Cosmetic grants use `RewardToastKind.Cosmetic` regardless of source (shop purchase, achievement, seasonal completion, daily calendar) — the `Headline` text differentiates the source ("Cosmetic Purchased!" vs "Cosmetic Unlocked!"), but the visual toast shape is identical. Shop-purchased cosmetics additionally follow the currency-gain/spend display convention: a **negative** delta shown as `-{price:N0} cr` in the confirm step, consistent with how `Shop.razor`/`ShopCategory.razor` show prices before every "Buy" button.

---

## 3. Pages & Components

### `Pages/ProgressCosmetics.razor` — route `/progress/cosmetics`

Structurally mirrors `ShopCategory.razor` almost exactly — this page **is** a shop category in spirit (a `row g-3` of `card`s), just filtered to ownership state rather than always-buyable:

- Breadcrumb: `Progress / Cosmetics`.
- Two sections (tabs or simple `<h4>` dividers): **Cage Decorations** and **Rat Accessories**, matching `CosmeticType`.
- Each cosmetic card:
  - Name, description, rarity badge (`Common` = `bg-secondary`, `Rare` = `bg-warning text-dark`, `Seasonal` = `bg-info text-dark` — reusing the existing tier-badge color convention rather than inventing a fourth palette)
  - If `availability == "Shop"`: price (`{shopPrice} cr`) and a **Buy** button — identical confirm-then-buy interaction to `ShopCategory.razor`'s existing accessory/bowl/bottle buy flow (single click, `busy` disable, success/error alert, `PlayerState.SetCurrency` on success). No separate confirmation panel is needed here (unlike carry-cases/storage-drawers, which need anchor-slot selection) since a cosmetic purchase has no placement step of its own.
  - If `availability != "Shop"`: no price, just `GrantSource` text (e.g. "Achievement: Top of the Class") in muted small text, mirroring how locked achievements/titles show their unlock source instead of a buy button (18a/18b's transparency convention).
  - `owned: true` cards show an "Owned" badge instead of Buy/GrantSource.

### Equip controls — cage cosmetics on `Pages/Home.razor`

Add a compact cosmetic picker to each cage card, near the existing Regime control (same `small` section styling):

```razor
<div class="d-flex align-items-center gap-2 mb-1 flex-wrap">
    <span class="text-muted">Decoration</span>
    <select class="form-select form-select-sm d-inline-block" style="max-width:165px"
            value="@(cage.ActiveCosmeticId ?? "")"
            @onchange="e => SetCageCosmeticAsync(cage.Id, e.Value?.ToString())">
        <option value="">— none —</option>
        @foreach (var c in ownedCageDecorations) { <option value="@c.Id">@c.Name</option> }
    </select>
</div>
```

Populated from the player's owned `CageDecoration` cosmetics (fetched once via `GET /api/cosmetics`, same pattern as the existing `catalogue` field already loaded on Home). Changing the dropdown calls `PUT /api/cages/{cageId}/cosmetic` immediately (no separate confirm button — this is a free, reversible, no-welfare-impact action per the backend doc, so the low-friction inline-select pattern already used for the Regime picker is appropriate rather than the heavier buy-confirm pattern).

### Equip controls — rat cosmetics on `Pages/RatDetail.razor`

Same inline-select pattern, added to the rat detail page for `RatAccessory`-type cosmetics, calling `PUT /api/rats/{ratId}/cosmetic`. (Existing `RatDetail.razor` structure not reproduced here — this doc specifies only the new control, to be inserted alongside the rat's existing trait/stat display.)

---

## 4. API Integration

### `GET /api/cosmetics`

```csharp
public async Task<CosmeticsResponse?> GetCosmeticsAsync()
```

```csharp
public record CosmeticsResponse(CosmeticEntry[] CageDecorations, CosmeticEntry[] RatAccessories);
public record CosmeticEntry(
    string Id, string Name, string Description, string Rarity, string Availability,
    decimal? ShopPrice, string? GrantSource, bool Owned, string[] EquippedOn);
```

Per Task18f_Design.md §7, `EquippedOn` is recommended to always return `[]` from this endpoint (lazy evaluation) — the client should not rely on it and instead read `ActiveCosmeticId` off the relevant cage/rat response directly (see below).

### `POST /api/cosmetics/{cosmeticId}/buy`

```csharp
public async Task<(CosmeticPurchaseResponse? Result, string? Error)> BuyCosmeticAsync(string cosmeticId)
{
    var resp = await http.PostAsync($"/api/cosmetics/{cosmeticId}/buy", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<CosmeticPurchaseResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode switch
    {
        402 => "Insufficient funds.",
        409 => "Already owned or not purchasable.",
        _   => string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body
    });
}
public record CosmeticPurchaseResponse(decimal NewBalance, CosmeticEntry Cosmetic);
```

Note the `402`/`409` handling follows the exact same `switch`-on-status-code shape already used throughout `GlassWingApiClient`'s existing `Buy*Async` methods (e.g. `BuyCageAsync`) — no new error-handling convention introduced.

### `PUT /api/cages/{cageId}/cosmetic` / `PUT /api/rats/{ratId}/cosmetic`

```csharp
public async Task<(bool Success, string? Error)> SetCageCosmeticAsync(string cageId, string? cosmeticId)
{
    var resp = await http.PutAsJsonAsync($"/api/cages/{cageId}/cosmetic", new { cosmeticId }, JsonOpts);
    if (resp.IsSuccessStatusCode) return (true, null);
    var body = await resp.Content.ReadAsStringAsync();
    return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}

public async Task<(bool Success, string? Error)> SetRatCosmeticAsync(string ratId, string? cosmeticId)
{
    var resp = await http.PutAsJsonAsync($"/api/rats/{ratId}/cosmetic", new { cosmeticId }, JsonOpts);
    if (resp.IsSuccessStatusCode) return (true, null);
    var body = await resp.Content.ReadAsStringAsync();
    return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}
```

### `CageResponse` / `RatResponse` extensions

```csharp
public record CageResponse(..., string? ActiveCosmeticId = null);
public record RatResponse(..., string? ActiveCosmeticId = null);
```

Both `Home.razor`'s cage rendering and `RatDetail.razor` read `ActiveCosmeticId` directly off these existing responses rather than from `GET /api/cosmetics`'s `EquippedOn` (per the backend's own recommendation, §4 above).

---

## 5. UX Flows

1. **Shop browse and buy.** Player navigates Progress → Cosmetics, sees "Blue Theme — 150 cr — Buy" under Cage Decorations. Clicks Buy; button disables, request completes, success alert shows "Blue Theme added to your collection. New balance: X cr", currency badge updates. This is the identical flow to buying an accessory in `ShopCategory.razor` today — no new interaction pattern.
2. **Equip after buying.** Player returns to Home, opens the newly-owned "Blue Theme" from the cage's Decoration dropdown. Cage visually updates (asset rendering itself is a client-rendering concern outside this doc's scope — see §7) immediately via the `PUT` call; no page reload needed beyond re-rendering the one cage card.
3. **Earned cosmetic.** Player completes the "Top of the Class" achievement, receiving `rat-crown-gold`. Toast: "Achievement Unlocked! +400 cr, new title: Leaderboard Legend, new cosmetic: Gold Crown." Player later visits their rat's detail page and equips it from the Accessory dropdown — same as a bought cosmetic, since ownership is unlimited-use and source-agnostic once granted (per backend doc §1's "unlimited-use unlocks" principle).
4. **Locked/earned-only cosmetic browsing.** Player viewing `/progress/cosmetics` sees "Champion's Banner — Achievement: Veteran Racer (50 wins)" with no Buy button — same locked/transparent treatment as achievements and titles.
5. **Clearing a decoration.** Player selects "— none —" from the cage's Decoration dropdown. `PUT` with `cosmeticId: null`. Always free, no confirmation needed (matches Task18b's title-clearing flow).

---

## 6. Client-Side Validation & Guards

- Cosmetic dropdowns on Home/RatDetail are built only from the player's **owned** cosmetics of the matching type — a `403 NotOwned` from the server should be unreachable through normal navigation, but handled defensively the same way as Task18b's title-equip guard: show the error, then refetch `GET /api/cosmetics` to resync.
- The Buy button on `/progress/cosmetics` is disabled when `owned == true` (already covered by not rendering a Buy button for owned items) and when `player.Currency < shopPrice` — mirrors the existing `disabled="@busy"` pattern plus an additional insufficient-funds pre-check so the button visibly disables before the round trip, not just after a `402` comes back (a small UX improvement over the current Shop pages, which only find out about insufficient funds after clicking — worth adopting here since `PlayerStateService.Currency` is already available client-side for a cheap pre-check).
- No validation needed on the equip dropdowns themselves beyond the owned-only population — `cosmeticId: null` (clear) is always valid.

---

## 7. Open Questions / Deferred

- **Actual asset rendering.** Per Task18f_Design.md §12, the API returns only `activeCosmeticId` strings — "no image data or server-side rendering." This doc specifies the data plumbing and equip UI but does **not** design the actual visual rendering of a cosmetic on a cage/rat card (e.g. an overlay image keyed by cosmetic id). That's a separate client-only design question (asset pipeline, sprite keys) to resolve before implementation — recommend a follow-up mini-design once art direction exists.
- **Insufficient-funds pre-check** (§6) is a minor UX improvement not present in today's Shop pages; flagging in case the team prefers strict 1:1 parity with existing Shop behavior instead (i.e. only surface `402` after the round trip, like every other Buy button today).
- **Daily calendar cosmetic backfill** (Task18f_Design.md §10) — once implemented, `Task18c_Client_Design.md`'s `DailyRewardEntry.Type == "Cosmetic"` case should reuse this doc's `RewardToastKind.Cosmetic` toast shape; no new toast type needed, just wiring.
- **Marketplace cosmetic transfer** — per backend doc §12, a sold rat's equipped cosmetic transfers with it. `Marketplace.razor`'s listing display should eventually show `activeCosmeticId` on listed rats (mirrors the adoption-pool note in Task18f_Design.md §8) — out of scope for this doc, flagged for whoever next touches the Marketplace page.
