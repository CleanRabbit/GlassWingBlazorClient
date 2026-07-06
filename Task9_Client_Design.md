# Task 9 (Client) — Rat Retirement: Design Document

## 1. Overview

The API's Task 9 work (`Task9_Design.md`) is fully implemented and shipped: `POST /api/rats/{id}/retire`, three retirement triggers (natural age, player-initiated, critical health), and `RatResponse` fields `IsRetired` / `RetiredAt` / `RetirementReason` / `RetiresAt` are all live on the server today (verified against `GlassWing/Features/RatsAndBreeding/RatsModule.cs`). The client currently has no concept of retirement at all — `RatResponse` in `ApiModels.cs` doesn't carry any of these fields, and `RatDetail.razor` has no retire action. This document specifies the client work to surface retirement: a retire button with confirmation, age/health warning banners, a frozen read-only view for retired rats, and exclusion of retired rats from the training/event/breeding/marketplace flows that already exist (or are being designed alongside this — see `Task10_Client_Design.md`).

This is also the first of the five tasks to need structured 409 error handling (`{Error, Reason}` bodies), so it introduces a small shared error-parsing convention that Tasks 10–13 reuse.

> **Refreshed 2026-07-06.** Two things this doc originally flagged as gaps have since shipped: `CriticalSince` is now on `RatResponse` (enabling the countdown warning §4 originally couldn't build), and every error response across the whole API — not just this task's 409s — is now uniformly `{ error, reason }` (`BackendAuditFindings.md` C1 in the API repo), so the `ProblemDetails`-unwrapping workaround §3 describes is no longer needed. This doc's own `ApiErrorResponse(string? Error, string? Reason)` shape and `ReadConflictAsync` parsing logic were already correctly designed for the shape that's now universal — nothing to change there.

## 2. Pages & Components Affected

| File | Change |
|---|---|
| `GlassWingClient/Pages/RatDetail.razor` | Add retire button + confirm modal; add approaching/imminent retirement warning banners; add frozen "Retired" view that replaces the training/marketplace sections |
| `GlassWingClient/Pages/Rats.razor` | Retired rats stay in the list (server still returns them) — add a muted "Retired" badge per row so the list isn't confusing; sort retired rats to the bottom |
| `GlassWingClient/Pages/Events.razor` | Rat picker (`tutorialRatId`, `activeRatId` dropdowns) must exclude retired rats |
| `GlassWingClient/Services/ApiModels.cs` | Extend `RatResponse`; extend `GameSettingsResponse`; add `ApiErrorResponse` |
| `GlassWingClient/Services/GlassWingApiClient.cs` | Add `RetireRatAsync`; add shared conflict-body parsing helper |

No new pages/routes. No change to `Home.razor` cage cards is required by Task 9 alone — a retired rat is removed from its cage by the server as part of retirement, so the cage's occupancy count simply drops on the next `GetHomeAsync()` refresh (already how Home.razor re-fetches after every mutation).

## 3. API Integration

### Endpoint consumed

`POST /api/rats/{id}/retire` — no body. `200` with the retired `RatResponse` on success. `404` if not found/not owned. `409` with `{ error, reason }` where `reason` is `AlreadyRetired` or `RatInActiveEvent` (confirmed in `RatRetirementService.cs`).

### `ApiModels.cs` additions

```csharp
public record RatResponse(
    string Id,
    string Name,
    string OwnerId,
    TrainingFitness? Fitness,       // superseded in Task 11 — see Task11_Client_Design.md
    HealthState? HealthState,
    RatPhenotype? Phenotype,
    string[]? TricksLearned,
    bool IsRetired = false,
    DateTime? RetiredAt = null,
    string? RetirementReason = null,   // "NaturalAge" | "PlayerInitiated" | "CriticalHealth"
    DateTime RetiresAt = default,
    DateTime? CriticalSince = null);   // shipped since this doc was written — see §4

public record GameSettingsResponse(
    double BiologicalScale,
    double FoodConsumptionScale,
    double WaterConsumptionScale,
    double TrainingCooldownScale,
    double IllnessProgressionScale,
    decimal? MarketplaceListingFee = null,
    double? MarketplaceTransactionFeePercent = null,
    double? RatLifespanDays = null,
    double? CriticalHealthRetirementThresholdDays = null,
    double? RetirementWarningEarlyDays = null,
    double? RetirementWarningLateDays = null);

// New — shared 409 conflict shape used by retire/breed/train/sex-separation endpoints.
public record ApiErrorResponse(string? Error, string? Reason);
```

Note the real `GET /api/game/settings` payload (see `GlassWing/Features/Game/GameModule.cs`) already returns `RatLifespanDays`, `CriticalHealthRetirementThresholdDays`, `RetirementWarningEarlyDays`, `RetirementWarningLateDays` as **plain real-time day counts** — the `BiologicalScale`/rat-year conversion is already baked in server-side. The client does no rat-year math; it just compares `RetiresAt - DateTime.UtcNow` (both already real, absolute UTC) against these day counts.

`RetiresAt` is always present on every rat (retired or not) — it's `DateOfBirth + RatLifespan`, computed server-side regardless of trigger.

### `GlassWingApiClient.cs` additions

```csharp
public async Task<(RatResponse? Rat, string? Error, string? Reason)> RetireRatAsync(string id)
{
    var resp = await http.PostAsync($"/api/rats/{id}/retire", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts), null, null);
    return await ReadConflictAsync(resp);
}

// Shared helper — Tasks 10–13 reuse this for their own 409s.
static async Task<(RatResponse? Rat, string? Error, string? Reason)> ReadConflictAsync(HttpResponseMessage resp)
{
    var body = await resp.Content.ReadAsStringAsync();
    if (string.IsNullOrWhiteSpace(body)) return (null, $"Error {(int)resp.StatusCode}", null);
    try
    {
        var err = JsonSerializer.Deserialize<ApiErrorResponse>(body, JsonOpts);
        if (err?.Reason is not null) return (null, err.Error ?? err.Reason, err.Reason);
    }
    catch (JsonException) { /* not a structured error body — shouldn't happen for any current endpoint, but fall back safely */ }
    return (null, body, null);
}
```

`ReadConflictAsync` above handles every error shape in the API uniformly now — 422 responses (e.g. `RatService.TrainAsync`'s guards) return the same `{ error, reason }` JSON as 409s, not `ProblemDetails`, so there's no special-casing needed for any particular status code.

## 4. UX Flows

### Retire button + confirmation modal

On `RatDetail.razor`, add a "Retire" button next to Rename, visible whenever `!rat.IsRetired`. Clicking opens a confirm modal, styled identically to the buy-confirmation modal in `Marketplace.razor` (`modal d-block` + `rgba(0,0,0,.45)` backdrop, header/body/footer, Cancel + primary action button with busy state):

```
Retire Biscuit?
This is permanent. Biscuit will be removed from gameplay and kept as a
frozen record. This cannot be undone.
[Cancel]  [Retire Rat]
```

On confirm → `RetireRatAsync(id)`. On success, replace `rat` with the returned frozen `RatResponse` and close the modal (page re-renders into the frozen view described below). On failure, show the error inline in the modal body exactly like `buyError` in `Marketplace.razor`.

### Approaching / imminent retirement warnings

Rendered as banners at the top of `RatDetail.razor`, above the name/rename row, only for non-retired rats:

```csharp
var daysLeft = (rat.RetiresAt - DateTime.UtcNow).TotalDays;
```

| Condition | Banner |
|---|---|
| `daysLeft <= settings.RetirementWarningLateDays` | `alert-danger`: "**{Name}** retires in less than a day." |
| `daysLeft <= settings.RetirementWarningEarlyDays` (and not late) | `alert-warning`: "**{Name}** will retire in about {days} days." |

These are plain informational banners (not dismissible) — they recompute every page load, so there is no need to persist dismissal state.

**Critical-health approach warning:** `CriticalSince` is now on `RatResponse` (see §3), so a precise countdown is possible: `CriticalSince + settings.CriticalHealthRetirementThresholdDays - DateTime.UtcNow`. When `rat.HealthState.Vitality == "Critical"` and `CriticalSince.HasValue`, show `alert-danger`: "**{Name}** is Critical and will be force-retired in {hours/days} unless treated." When `Vitality != "Critical"`, `CriticalSince` is always `null` (it's cleared on recovery), so no extra guard is needed to avoid showing a stale countdown.

### Frozen retired view

When `rat.IsRetired`, replace the interactive sections of `RatDetail.razor`:

- Header: name (no rename button), plus a badge: `<span class="badge bg-dark">Retired</span>` and, next to it, the reason in plain text: "Natural age", "Player choice", or "Critical health" (map `RetirementReason` → label).
- Show `RetiredAt.ToString("dd MMM yyyy")`.
- Fitness, Health, Tricks, and Appearance cards remain visible (read-only) — they already render from `rat` data with no write actions.
- Hide entirely: Rename button, Training buttons, Marketplace listing section, Retire button.
- A "Hall of Fame" badge is referenced by TASKS.md's backlog note but there's no Hall of Fame page/API yet (gamification, later task) — out of scope here; the retired-view badge above is a placeholder for that future link.

## 5. Client-Side Validation & Guards

| Guard | Client behavior | Backend enforcement |
|---|---|---|
| Retire button on already-retired rat | Hidden — `!rat.IsRetired` | `409 AlreadyRetired` (belt-and-braces) |
| Retire while rat is in an active event | Not preflighted (client doesn't track lobby participation per-rat on this page) | `409 RatInActiveEvent` — surface `err.Error` in the modal, e.g. "Rat is in an active event. Wait for the event to complete." |
| Train buttons on retired rat | Hidden entirely in the frozen view | `422` (`RatService.TrainAsync` throws when `rat.IsRetired`) |
| Event entry on retired rat | Filter retired rats out of the `<select>` in `Events.razor` (`rats?.Where(r => !r.IsRetired)`) | `EventLobbyService` rejects retired rats with a plain-text 404-style error |
| Marketplace listing on retired rat | Listing section hidden in frozen view | Marketplace service checks `IsRetired` |
| Breeding on retired rat | Excluded from mate pickers — see `Task10_Client_Design.md` §5 | `409 FemaleRetired` / `MaleRetired` |

Since retirement removes the rat from its cage/carry case server-side as part of the same commit, there's no separate client-side "unassign from cage" step needed.

## 6. State Management

No new singleton state. `RatDetail.razor` already owns its own `rat` field and re-fetches on mutation; retirement follows the same pattern as `TrainAsync`/`RenameRatAsync` (replace local `rat` with the response). `Home.razor`'s existing `LoadAsync()` re-fetch after cage-affecting actions is unaffected — a background (Hangfire) natural-age or critical-health retirement that happens while the player is away simply shows up as a smaller cage occupancy count the next time `GetHomeAsync()` runs; no polling is introduced for this.

## 7. Notifications

The API's Task 9 design does not add any Home-level notification list for retirement (unlike Task 12's `LifeStageNotifications`). A background retirement (natural age or critical health) is silent from the client's perspective until the player opens that rat's detail page and sees the frozen view, or notices the cage occupancy count drop on Home. This is a UX gap worth flagging (see Open Questions) but is out of scope to fix by inventing a client-only notification, since there's no server-side event to poll for it.

## 8. Open Questions / Deferred

- ~~**`CriticalSince` not exposed.**~~ **Resolved** — shipped on `RatResponse` (see §3, §4). The precise countdown described in §4 is now buildable.
- **No "you missed it" notification** for background retirements. Deferred — would need a new Home-level notification list (same shape as `LifeStageNotifications` in Task 12) that the backend doesn't currently populate for retirement.
- **Hall of Fame** badge/page referenced in TASKS.md backlog is a gamification feature with no API yet — this doc only reserves a badge slot in the frozen view; the actual Hall of Fame link is deferred entirely.
- ~~**`ProblemDetails` unwrapping** for 422 training errors~~ — **Resolved**, no longer applicable. 422s use the same `{ error, reason }` shape as everything else now.
