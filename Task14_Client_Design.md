# Task 14 — Adoption Agency: Client Design Document

## Overview

Adds a new `/adoption` page (Browse / Adopt-confirm / Surrender tabs) that lets a player browse the shared adoption pool, adopt a rat into a free carry case, and surrender an owned rat back to the pool. The page mirrors the tab + card-grid + modal pattern already used by `Marketplace.razor`.

This doc covers the voluntary flows only (player opens `/adoption` on their own initiative). The *forced* flows — post-tutorial adoption, minimum-2-rats lock, lone-cage resolution — are driven by the welfare system and are specified in **Task15_Client_Design.md**. Where a forced flow needs to adopt a rat, it reuses the same API client methods and card components described here; see Task 15 §5 for the modal/overlay specifics.

Backend reference: `Task14_Design.md` (API repo root). Fully implemented, 888 tests passing (client-observable surface unaffected by later Task 15 test count).

> **Refreshed 2026-07-06** to reflect a backend audit pass (`BackendAuditFindings.md` in the API repo) that changed this task's contract: the `waiveFee` query parameter was removed from `POST /api/adoption/{ratId}/adopt` (it was a client-controlled flag with no server-side eligibility check — a live free-adoption exploit) and the adopt endpoint's response shape changed from a bare `RatResponse` to a wrapper including `CarryCaseId`. Every section below reflects the current contract; also folded in two other already-shipped fixes this doc's Open Questions didn't know about yet (`SurrenderCount` and `AdoptionPoolMaxRandomCount` are both now exposed).

---

## 1. Pages & Components

### `/adoption` (new page, `Pages/Adoption.razor`)

Tab bar (same `nav-tabs` pattern as `Marketplace.razor`):

| Tab | Content |
|---|---|
| **Browse** | Paginated grid of pool rats, sex filter, "Adopt" button per card |
| **Surrender** | Grid/list of the player's own rats eligible for surrender, "Surrender" button per rat, remaining-allowance counter |

No "My Adoptions" tab — adopted rats show up in Home/Rats immediately, there's nothing pool-specific to track post-adoption.

**Nav:** add an `Adoption` link to `Layout/NavMenu.razor` alongside Marketplace.

### Reused / new components

| Component | Source | Notes |
|---|---|---|
| Rat card (browse grid) | New, modeled on `Marketplace.razor`'s listing card | Shows name, sex badge, coat description (`CoatDescription`-style helper from `RatDetail.razor`), age, sprint/agility/endurance, `Source` badge (Generated/Surrendered/Seeded — cosmetic only) |
| Adopt confirm modal | New, modeled on `Marketplace.razor`'s `buyTarget` Bootstrap modal (`modal d-block` overlay) | Shows fee (or "Fee waived" when invoked from a Task 15 forced flow), free-carry-case check, confirm/cancel |
| Surrender confirm modal | New, same modal pattern | Shows remaining surrender count, confirm/cancel |
| No-free-carry-case banner | Reused verbatim from `Marketplace.razor` (`freeCarryCases == 0` alert-warning banner) | Same computation: `home.CarryCases.Count(c => c.RatId is null)` |
| Toast | Reused verbatim from `Marketplace.razor`'s `toastMessage`/`toastIsError` bottom-right toast | Adoption/surrender success & error toasts |

### No changes to Home.razor / RatDetail.razor for Task 14 itself

Surrender is initiated from the new `/adoption` Surrender tab, not from `RatDetail.razor`, for consistency with how listing-for-sale lives on `RatDetail.razor` but *cancelling* a listing lives on `Marketplace.razor` — surrendering is the pool-side operation and belongs on the pool-side page. (Open question in §8 — see alternative.)

---

## 2. API Integration

### `ApiModels.cs` additions

```csharp
// --- Adoption ---

public record PagedResponse<T>(T[] Items, int TotalCount, int Page, int PageSize);

public record AdoptionPoolEntryResponse(
    string Id,
    string Name,
    string Sex,
    DateTime DateOfBirth,
    string LifeStage,
    string Source,              // Seeded | Generated | Surrendered
    RatPhenotype? Phenotype,
    AdoptionPoolStats? Stats,
    double? Weight);

public record AdoptionPoolStats(double Sprint, double Agility, double Endurance);

public record SurrenderRatResponse(int RemainingSurrenders);
```

`AdoptRatAsync` returns a wrapper, not a bare `RatResponse` — the backend added `CarryCaseId` alongside the rat so the client doesn't have to diff `GetHomeAsync()` carry cases before/after adopting to figure out where the new rat landed:

```csharp
public record AdoptResponse(RatResponse Rat, string CarryCaseId);
```

`GameSettingsResponse` gains trailing fields (same additive pattern as the existing `MarketplaceListingFee`/`MarketplaceTransactionFeePercent`) — `AdoptionPoolMaxRandomCount` is included, so the client no longer needs to hardcode the "Generate rats" fallback count (see §3, §7):

```csharp
public record GameSettingsResponse(
    double BiologicalScale,
    double FoodConsumptionScale,
    double WaterConsumptionScale,
    double TrainingCooldownScale,
    double IllnessProgressionScale,
    decimal? MarketplaceListingFee = null,
    double? MarketplaceTransactionFeePercent = null,
    decimal? AdoptionFee = null,
    int? MaxAdoptionSurrenders = null,
    int? AdoptionPoolMaxRandomCount = null);
```

`PlayerProfileResponse` gains `SurrenderCount` (see §3, §7 — this closes what was previously an open question in this doc).

### `GlassWingApiClient.cs` additions

```csharp
// --- Adoption ---

public async Task<PagedResponse<AdoptionPoolEntryResponse>?> GetAdoptionPoolAsync(
    string? sex = null, int page = 1, int pageSize = 20)
{
    var url = $"/api/adoption?page={page}&pageSize={pageSize}";
    if (sex is not null) url += $"&sex={Uri.EscapeDataString(sex)}";
    var resp = await http.GetAsync(url);
    return resp.IsSuccessStatusCode
        ? await resp.Content.ReadFromJsonAsync<PagedResponse<AdoptionPoolEntryResponse>>(JsonOpts)
        : null;
}

public async Task<AdoptionPoolEntryResponse[]?> GetRandomAdoptionPoolAsync(string sex, int count)
{
    var resp = await http.GetAsync($"/api/adoption/random?sex={Uri.EscapeDataString(sex)}&count={count}");
    return resp.IsSuccessStatusCode
        ? await resp.Content.ReadFromJsonAsync<AdoptionPoolEntryResponse[]>(JsonOpts)
        : null;
}

public async Task<(AdoptResponse? Result, string? Error)> AdoptRatAsync(string ratId)
{
    // No waiveFee parameter — the server derives fee-waiver eligibility entirely from the
    // caller's own welfare/reward state (post-tutorial adoption, minimum-rat-count resolution,
    // lone-cage-different-sex resolution, pending daily-reward waiver). There is nothing for
    // the client to request or signal; every one of the Task 15 forced-adoption flows below
    // just calls this exact same method with no special-casing.
    var resp = await http.PostAsync($"/api/adoption/{ratId}/adopt", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<AdoptResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, MapAdoptError(resp.StatusCode, body));
}

public async Task<(SurrenderRatResponse? Result, string? Error)> SurrenderRatAsync(string ratId)
{
    var resp = await http.PostAsync($"/api/adoption/{ratId}/surrender", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<SurrenderRatResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, MapSurrenderError(resp.StatusCode, body));
}
```

`MapAdoptError` / `MapSurrenderError` are small private helpers translating the `409` reason codes in the body (see §6) to friendly strings, following the existing inline `(int)resp.StatusCode == 402 ? "Insufficient funds." : ...` convention used throughout the client. Given the number of distinct reason codes for surrender (7), a helper method is cleaner than inlining.

`GET /api/game/settings` is already wired via `GetGameSettingsAsync()` — no new method needed, just consume the two new fields.

---

## 3. UX Flows

### Browse

1. On tab activate / page load: `GetAdoptionPoolAsync(sex: sexFilter, page: 1, pageSize: 20)`.
2. Sex filter: two-button toggle (`All` / `Male` / `Female`) above the grid, same visual weight as Marketplace's tabs but as a `btn-group`.
3. Card grid, same `col-md-6 col-xl-4` card layout as Marketplace listings. Each card:
   - Name, sex badge, `Source` badge (muted, small — e.g. "Surrendered" hints at ancestry-bearing rats, a soft signal to players who care about lineage)
   - Coat description line (reuse `RatDetail.razor`'s `CoatDescription` logic, moved to a shared static helper or duplicated — client has no shared component library yet, duplication matches existing practice, e.g. `CoatDescription` is already duplicated between `RatDetail.razor` and `Marketplace.razor`'s `CoatBadgeString`)
   - Sprint/Agility/Endurance line (reuse Marketplace's `FormatScore`)
   - "Adopt" button → opens confirm modal
4. Pagination: simple prev/next footer (`page` / `TotalCount`); `pageSize` fixed at 20, no page-size picker.
5. Empty state: if `TotalCount == 0` for the current filter, show a message and an "Generate rats" fallback button that calls `GetRandomAdoptionPoolAsync(sex, count: gameSettings.AdoptionPoolMaxRandomCount ?? 6)` and appends results to the grid client-side (no need to re-fetch page 1, since `GET /api/adoption/random` already returns the new entries directly).

### Adopt confirm

1. Click "Adopt" → modal opens (`adoptTarget` state, mirrors Marketplace's `buyTarget`).
2. Modal body: rat name, fee (`gameSettings.AdoptionFee`, formatted `N0 cr`), "placed in a free carry case" note (verbatim copy from Marketplace's buy modal).
3. If `freeCarryCases == 0`: disable Confirm button, show the reused warning banner inline in the modal instead of the page banner.
4. If `PlayerState.Currency < AdoptionFee`: disable Confirm, show "Insufficient funds" inline (client-side pre-check; server is still authoritative — see §6).
5. Confirm → `AdoptRatAsync(ratId)`.
6. On success: remove the entry from the local grid, `PlayerState.SetCurrency` requires a fresh balance — the adopt endpoint does not return a balance (unlike `BuyListingAsync`), so follow up with `Api.GetPlayerProfileAsync()` (same two-call pattern already used in `Events.razor`'s `EnterLobbyAsync` for fee-bearing actions), decrement local `freeCarryCases`, close modal, toast success ("Adopted! Check your carry cases on the Home page."). The response's `CarryCaseId` (see §2) tells you exactly which carry case the new rat is in, if the UI wants to highlight it on the next Home visit — no need to diff carry-case state.
7. On failure: show inline modal error, keep modal open (same as Marketplace buy-modal error handling).

### Surrender

1. Surrender tab loads the player's rats via `Api.ListRatsAsync()` (already available) filtered client-side to those plausibly eligible — the client cannot fully pre-validate pregnancy/nursing/Weening/marketplace-listed state (those aren't in `RatResponse` today), so **all owned rats are listed** and ineligibility is surfaced via the server's `409` on attempt (see §6). This matches the existing client philosophy of "let the server be the source of truth for hard business rules" (e.g. Training has no client-side cooldown pre-check either).
2. Header banner: "Surrenders used: `SurrenderCount` / `MaxAdoptionSurrenders`" — `PlayerProfileResponse.SurrenderCount` (see §2) is available from page load via `GetPlayerProfileAsync()`, so the banner can show an accurate count immediately rather than waiting for the player's first surrender of the session.
3. Click "Surrender" on a rat row → confirm modal: rat name, "This rat will leave your home and enter the shared adoption pool. This cannot be undone.", remaining-allowance display, Confirm/Cancel.
4. Confirm → `SurrenderRatAsync(ratId)`.
5. On success: remove from local rat list, update remaining-allowance counter from `SurrenderRatResponse.RemainingSurrenders`, refresh Home cage state is not needed on this page but the next Home visit will reflect it, toast success.
6. On failure: show the mapped reason (§6) inline in the modal, keep it open.
7. **Welfare interaction:** a successful surrender may immediately trigger a Task 15 welfare block (Rule 2 or a Rule 3 condition) if the player's rat count drops to 1 or an existing 2-rat lone-cage situation is created. The surrender endpoint itself returns a clean `200` regardless (per backend doc §6) — the client does not need to inspect the surrender response for this. Instead, per Task 15's polling strategy, the surrender action is one of the designated "refresh welfare status after this mutation" triggers; the resulting overlay (if any) appears immediately after the toast. See **Task15_Client_Design.md §4**.

---

## 4. Global Welfare Polling Strategy

Not applicable to this document — Task 14 introduces no polling of its own. `/adoption` is one of the pages Task 15 requires a welfare-status refresh on load (it's also the resolution destination for every forced-adoption flow), and every adopt/surrender action on this page is a "rat count changed" trigger for Task 15's refresh list. See **Task15_Client_Design.md §4** for the authoritative polling rules; `Adoption.razor` simply calls the shared `WelfareStateService.RefreshAsync()` in `OnInitializedAsync` and after each successful adopt/surrender, same as every other page.

---

## 5. Client-Side Validation & Guards

### Adopt — `POST /api/adoption/{ratId}/adopt`

| Status | Reason | Client handling |
|---|---|---|
| `404` | Pool entry gone (already adopted by another player) | Remove card from grid immediately, toast "That rat was just adopted by someone else — try another." Re-fetch page 1 to backfill the grid. |
| `409` | `NoFreeCarryCase` | Pre-empted client-side where possible (§3); if it still occurs (stale `freeCarryCases` count), show inline modal error "You need a free carry case." with a link to `/shop/carry-cases`. |
| `409` | `InsufficientFunds` | Pre-empted client-side; fallback inline error "Insufficient funds." (matches the existing `402`-style message used elsewhere, even though this is a `409` here — client copy stays consistent regardless of status code). |

### Surrender — `POST /api/adoption/{ratId}/surrender`

| Status | Reason | Client handling |
|---|---|---|
| `404` | Not found / not owned | Generic "Rat not found." — shouldn't happen from the UI's own list, treat as stale data, refresh the rat list. |
| `409` | `SurrenderLimitReached` | Disable the Surrender button entirely once `RemainingSurrenders == 0` is known (post-first-surrender); before that, rely on server response: "You've reached your lifetime surrender limit." |
| `409` | `RatRetired` | "Retired rats can't be surrendered." |
| `409` | `RatListed` | "This rat is listed on the marketplace — cancel the listing first." (link to `/marketplace`) |
| `409` | `RatPregnant` | "Pregnant rats can't be surrendered." |
| `409` | `PupTooYoung` | "This pup is too young to leave the nest yet." |
| `409` | `MotherNursing` | "This rat is nursing a litter and can't be surrendered right now." |

All reason codes above are read from the response body as `{ "error": "...", "reason": "..." }` — confirmed uniform across the entire API (see `BackendAuditFindings.md` C1 in the API repo); `reason` carries the machine-readable code (e.g. `"NoFreeCarryCase"`) and `error` the human-readable message. The client's generic `string? Error` pattern can read `reason` directly for the `switch` in `MapAdoptError`/`MapSurrenderError`.

---

## 6. State Management

No new singleton service required for Task 14 itself. Local page state in `Adoption.razor` follows the exact pattern of `Marketplace.razor`: `loaded`, `loadError`, per-item busy sets, `toastMessage`/`toastIsError`, a single `adoptTarget`/`surrenderTarget` modal-state field.

`PlayerStateService.Currency` is refreshed via `GetPlayerProfileAsync()` after a fee-bearing adopt, same call pattern already used in `Events.razor` and `RatDetail.razor`.

`freeCarryCases` is computed locally from `GetHomeAsync()` on page load, decremented optimistically on successful adopt, exactly as `Marketplace.razor` already does.

---

## 7. Open Questions / Deferred

1. ~~**`SurrenderCount` on player profile.**~~ **Resolved** — `PlayerProfileResponse.SurrenderCount` shipped (see §2). No client-side workaround needed.
2. ~~**`AdoptionPoolMaxRandomCount` not exposed to clients.**~~ **Resolved** — shipped on `GET /api/game/settings` (see §2).
3. **Exact `409` body shape — resolved, and simpler than assumed.** All error responses across the API (not just this endpoint) now uniformly use `{ error, reason }` JSON (see `BackendAuditFindings.md` C1 in the API repo) — no free-text fallback needed for `MapAdoptError`/`MapSurrenderError`, and no per-endpoint confirmation required going forward.
4. **Surrender entry point.** Placing surrender only on `/adoption` (not also on `RatDetail.razor`, where "List on Marketplace" lives) is a judgment call for symmetry with the pool-side page. If player testing shows people expect it on the rat's own page (mirroring the marketplace listing flow), add a "Surrender to Adoption Agency" button next to "List on Marketplace" on `RatDetail.razor` that just navigates to `/adoption?surrender={ratId}` with the tab and target pre-selected.
5. **Ancestry display for surrendered pool rats.** The backend enriches ancestry on surrender (§7 of the API doc) — worth surfacing on the browse card ("Has known lineage" badge) as a soft incentive to adopt surrendered rats over generated ones, but not required for MVP. Deferred.
