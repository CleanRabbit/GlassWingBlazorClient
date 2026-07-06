# Task 15 — Minimum Rat Welfare Rules: Client Design Document

## Overview

The backend gates the entire game behind three welfare rules (see `Task15_Design.md`, API repo root — fully implemented, 888 tests). The client's job is purely reactive: poll `GET /api/welfare/status`, hold the result in a new global singleton, and render the correct blocking UI for whichever rule is active. The client never computes welfare state itself — it only displays what the server already computed.

This is the single most invasive client change in the two docs: it introduces a **global lock overlay** that can render above every page, a **new singleton service**, and touches `MainLayout.razor`, `Events.razor`, and indirectly every page that mutates rat count. It depends on Task 14's adoption endpoints for all three resolution paths — see **Task14_Client_Design.md** for the adopt/browse UI being invoked here.

> **Refreshed 2026-07-06.** Several gaps this doc originally flagged as blocking have since shipped, across two separate backend passes:
> - From an earlier client-review resolution pass (`IssuesResolutionDesigns.md` in the API repo, commit `4d5fe0a`): (1) `POST /api/home/cages/{cageId}/pickup/{ratId}` now exists (its A1/A2), resolving Rule 3a and Rule 3b's "no cage-to-cage move" gap; (2) `AdoptRatAsync`'s response now includes `CarryCaseId` (its A3), resolving Rule 3b's second-step gap; (3) `WelfareBlockType.LoneCageResolution` is confirmed as the real `activeBlock` string for Rule 3 (its B2); (4) Rule 2 now waives the adoption fee automatically (its A4), closing the insufficient-funds dead end.
> - From a later backend audit pass (`BackendAuditFindings.md` in the API repo): the `waiveFee` client parameter this doc references throughout was removed (its A1) — it was a live free-adoption exploit (client-controlled with no server-side check) and all waiver conditions, including the two above, are now derived entirely server-side; error response bodies are now uniformly `{ error, reason }` everywhere (its C1).
>
> Updates are inline below; sections not touched are unaffected.

---

## 1. Rules Recap (client-relevant only)

| Rule | Scope | Resolution action | Fee |
|---|---|---|---|
| Rule 1 — Post-tutorial adoption | Events page only | Adopt 1 female rat | Waived |
| Rule 2 — Minimum 2 rats | Total game lock | Adopt any 1 rat | Waived (server-derived — see refresh note above) |
| Rule 3a — Lone cage, same sex | Total game lock | Move one rat to merge cages | N/A |
| Rule 3b — Lone cage, different sex | Total game lock | Adopt 2 rats (one per cage), sequential | Waived (both) |

Priority: Rule 1/2 (stored) always take precedence over Rule 3 (computed). The server never reports more than one active rule at a time — the client renders exactly one blocking UI element at a time, chosen by `activeBlock`/`blockScope`, no client-side priority logic needed.

---

## 2. Pages & Components

### New: `WelfareStateService` (singleton) — see §6

### New: `Components/WelfareLockOverlay.razor`

A full-viewport overlay component, rendered inside `Layout/MainLayout.razor` (added as a sibling to `<article class="content px-4">@Body</article>`, so it paints above all page content regardless of route). Modeled visually on the existing Bootstrap modal pattern (`Marketplace.razor`'s `modal d-block` + `rgba(0,0,0,.45)` backdrop) but:
- **Non-dismissible** — no close button, no backdrop-click-to-close, `@onclick:stopPropagation` isn't needed because there's nothing to click through to.
- Renders one of three inner bodies depending on `WelfareStateService.Status`:
  - `MinimumRatCount` → **Forced Adoption panel**: short explanation, embeds a compact version of Task 14's browse grid (reuse `GetAdoptionPoolAsync`/`GetRandomAdoptionPoolAsync`/`AdoptRatAsync`) filtered to no sex restriction, fee shown normally (not waived).
  - `LoneCage` (`SameSex`) → **Cage Merge panel**: names both cages/rats (from `loneCage.cageA/cageB` — needs a cage/rat-name lookup via `GetHomeAsync()`, matched by `cageId`), single "Move rat" button.
  - `LoneCage` (`DifferentSex`) → **Companion Adoption panel**: two-step sequential adopt (see §5).
- Only rendered when `WelfareStateService.Status?.BlockScope == "TotalGameLock"`. Rule 1 (`EventsOnly`) does **not** use this overlay — see below.

### `Layout/MainLayout.razor` changes

- Inject `WelfareStateService`.
- `OnInitializedAsync`: after the existing profile-currency fetch, call `WelfareStateService.RefreshAsync()`.
- Subscribe to `WelfareStateService.OnChange` (same `+=` / `Dispose` pattern already used for `PlayerState.OnChange`).
- Subscribe to `NavigationManager.LocationChanged` and call `RefreshAsync()` on every navigation (this is the "poll on navigation" requirement from TASKS.md — centralizing it here means individual pages don't each need their own navigation hook).
- Render `<WelfareLockOverlay />` after `@Body` when `WelfareStateService.IsTotalLocked`.

### `Pages/Events.razor` changes

- Inject `WelfareStateService`.
- When `WelfareStateService.Status?.ActiveBlock == "PostTutorialAdoption"`: render a non-dismissible banner at the top of the page (alert-warning, same visual weight as the existing "Getting Started" card) — "Adopt a companion rat to continue entering events." with a button/link to `/adoption`.
- Disable the "Enter" button on every lobby row and the "Create & Enter" button (both call the gated `POST /api/events/{lobbyId}/enter`) while this block is active; show a tooltip-style small text under the disabled button ("Blocked — adopt a companion first").
- The Tutorial card itself stays enabled — Rule 1 only blocks *subsequent* event entry, and the tutorial completion is literally what triggers the block in the first place.
- Route guard: if a player deep-links to `/events` while `MinimumRatCount`/`LoneCage` (total lock) is active, the global overlay in `MainLayout` already covers the whole viewport, so no page-level guard is needed for the total-lock cases. For the events-only Rule 1 case there is no route guard needed either — Events remains visible, just with entry actions disabled, per the "Events only" lock scope.

### No changes needed to `Home.razor` or `RatDetail.razor`

Rule 3's cage/rat identification comes entirely from the `GET /api/welfare/status` payload (`cageId` + `sex`), cross-referenced against `GetHomeAsync()` purely for display names inside the overlay — the player never has to go find the lone-cage themselves.

---

## 3. API Integration

### `ApiModels.cs` additions

```csharp
// --- Welfare ---

public record WelfareStatusResponse(
    string? ActiveBlock,     // "PostTutorialAdoption" | "MinimumRatCount" | "LoneCageResolution" | null
    string? BlockScope,      // "EventsOnly" | "TotalGameLock" | null
    WelfareRules Rules);

public record WelfareRules(
    PostTutorialAdoptionRuleStatus PostTutorialAdoption,
    MinimumRatCountRuleStatus MinimumRatCount,
    LoneCageRuleStatus LoneCage);

public record PostTutorialAdoptionRuleStatus(bool Active, bool FemaleOnly, bool FeeWaived);
public record MinimumRatCountRuleStatus(bool Active);
public record LoneCageRuleStatus(bool Active, string? Type, LoneCageInfo? CageA, LoneCageInfo? CageB);
public record LoneCageInfo(string CageId, string Sex);
```

Note: the sample JSON in `Task15_Design.md` §7 shows `activeBlock: "PostTutorialAdoption"` for Rule 1/2 naming, but doesn't show what string Rule 3 uses for `activeBlock` — infer `"LoneCageResolution"` pending confirmation against a live response (flagged §8).

### `GlassWingApiClient.cs` addition

```csharp
// --- Welfare ---

public async Task<WelfareStatusResponse?> GetWelfareStatusAsync()
{
    var resp = await http.GetAsync("/api/welfare/status");
    return resp.IsSuccessStatusCode
        ? await resp.Content.ReadFromJsonAsync<WelfareStatusResponse>(JsonOpts)
        : null;
}
```

Resolution reuses Task 14's `AdoptRatAsync` (no `waiveFee` parameter — see refresh note above; the server figures out on its own whether this adoption is fee-waived) plus one more endpoint needed for the cage-merge/cage-to-cage flows:

```csharp
public async Task<(bool Success, string? Error)> PickUpRatFromCageAsync(string cageId, string ratId)
{
    var resp = await http.PostAsync($"/api/home/cages/{cageId}/pickup/{ratId}", null);
    if (resp.IsSuccessStatusCode) return (true, null);
    var body = await resp.Content.ReadAsStringAsync();
    return (false, body);
}
```

Rule 3a's cage-merge is a *rat-to-cage move between two occupied cages*, which resolves as a two-hop composition of `PickUpRatFromCageAsync` (moves a rat out of its current cage into a free carry case — 400/`NoFreeCarryCase`-style `ErrorResponse` if there isn't one) followed by the existing `PlaceRatFromCarryCaseAsync(carryCaseId, targetCageId)`. The player sees this as one action ("Move rat"); the client just makes both calls in sequence.

---

## 4. Global Welfare Polling Strategy

### When the client calls `GET /api/welfare/status`

| Trigger | Where wired |
|---|---|
| App shell load (any first page render) | `MainLayout.OnInitializedAsync` |
| Every client-side navigation | `MainLayout` subscribes to `NavigationManager.LocationChanged` |
| Idle timer fallback (30–60s) | `MainLayout` starts a `PeriodicTimer` in `OnInitializedAsync`, disposed in `IDisposable.Dispose` |
| After any action that changes the player's rat count | Called explicitly by the initiating page, in addition to the navigation-triggered refresh that will naturally follow most of these (adopt/surrender don't navigate away, so they need their own explicit call) |

**Rat-count-changing actions requiring an explicit post-action refresh:**

| Action | Page | Notes |
|---|---|---|
| Adopt (voluntary or forced) | `Adoption.razor`, `WelfareLockOverlay.razor` | Rat count +1 |
| Surrender | `Adoption.razor` | Rat count −1 |
| Buy marketplace listing | `Marketplace.razor` | Buyer +1 |
| Sell marketplace listing (buyer completes purchase) | N/A client-side — seller isn't present when their listing sells | See idle-timer note below |
| Breeding delivery (pups born) | N/A — background Hangfire job | See idle-timer note below |
| Retirement (any trigger) | N/A — can be server-driven (age/critical health) without any client action | See idle-timer note below |

**Why the idle timer matters:** three of the six backend mutation points (marketplace sale as seller, breeding delivery, retirement) are **not** initiated by a client action at all — they can fire from a background job while the player is sitting on the Home page doing nothing. A pure "poll after my own actions" strategy would miss all three, potentially leaving a player staring at a suddenly-invalid game state (e.g. their last rat just retired) with no lock rendered until their next navigation. The `PeriodicTimer` (30–60s while any page is mounted) is the safety net for these. This is a stronger polling strategy than the minimum TASKS.md bullet ("poll on page load/navigation and after any rat-count-changing action") — recommended because those three server-only triggers are exactly the scariest case (a total lock the player didn't cause and won't discover without either polling or their next click failing with a `409`).

### How the result drives global lock state

`WelfareStateService.RefreshAsync()`:
1. Call `GetWelfareStatusAsync()`.
2. `SetStatus(result)` — stores it, fires `OnChange`.
3. Consumers (`MainLayout`, `Events.razor`) re-render via their own `OnChange` subscriptions, same pattern as `PlayerStateService`.

`WelfareStateService` exposes:
```csharp
public bool IsTotalLocked => Status?.BlockScope == "TotalGameLock";
public bool IsEventsLocked => Status?.ActiveBlock == "PostTutorialAdoption";
```

### Interaction with routing

- **Total lock (Rule 2/3):** the overlay renders above `@Body` in `MainLayout`, covering every route. The player *can* still technically navigate underneath it (the `Router`/`RouteView` keep working), but the overlay has no backdrop-dismiss and `pointer-events` on the backdrop blocks interaction with the page behind it — practically equivalent to a full route block without needing actual navigation interception. `/adoption` itself is not specially exempted from the overlay in the client (the backend exempts the *adopt endpoint*, not the *page* — and the overlay already embeds its own adopt panel, so the player resolves the block without ever needing to reach the standalone `/adoption` page while locked). If a player manually navigates to `/adoption` while locked, they just see both the normal page and the overlay on top — harmless but slightly redundant; not worth suppressing for MVP.
- **Events-only lock (Rule 1):** no overlay, no navigation restriction at all — every other page functions normally. Only the Events page itself reflects the block, via disabled buttons.
- **Race/staleness:** if a player resolves a block from a stale client state (e.g. two browser tabs), the resolving action's own `409 WelfareBlock` response is the ultimate guard — the client always calls `WelfareStateService.RefreshAsync()` after catching such a `409` from *any* endpoint (see §5), which will then flip the overlay/banner state correctly even if the periodic timer hadn't caught up yet.

---

## 5. UX Flows

### Rule 1 — Post-tutorial adoption modal

Not a modal in the strict sense — surfaced as an in-page banner on `Events.razor` (see §2) plus disabled entry buttons. Rationale: the backend lock scope is `EventsOnly`, so a full-screen modal would over-communicate the severity (the player can still do everything else — train, shop, breed). Clicking the banner's "Adopt a companion" link navigates to `/adoption`, pre-filtered to `sex=Female` (`/adoption?sex=Female`, read by `Adoption.razor` as an initial filter query param).

On `/adoption`, the browse grid empty-state fallback (Task 14 §3) is exactly what's needed if the pool has no females: "Generate rats" → `GetRandomAdoptionPoolAsync("Female", 3)` (per backend doc §4, 3 is the fallback count for this specific case, not the general 6 used in Task 14's own empty-pool fallback — `Adoption.razor` accepts a `randomFallbackCount` the caller can override via query param, defaulting to 6, set to 3 by the Events banner link: `/adoption?sex=Female&fallbackCount=3`).

`Adoption.razor` checks `WelfareStateService.Status.Rules.PostTutorialAdoption.Active` on load and, if true, hides the fee line entirely and shows "Fee waived" instead — this is purely a client-side display choice now, since the server decides the actual waiver on its own regardless of what the UI shows. Calling `AdoptRatAsync(ratId)` (no fee-related parameter) behaves correctly either way.

### Rule 2 — Forced adoption (total lock)

`WelfareLockOverlay`'s **Forced Adoption panel**: compact embedded version of Task 14's browse grid (no tabs, no sex filter — any sex qualifies), `AdoptRatAsync(ratId)`. **The fee is now waived automatically by the server** while Rule 2 is the active block (resolved after this doc originally shipped — see the refresh note at the top; was previously a real dead-end for a broke player, since Events were also blocked under the total lock). Show "Fee waived" the same way Rule 1's panel does — no currency check, no insufficient-funds state to design for here. On success: `WelfareStateService.RefreshAsync()` immediately — this clears `ActiveBlock`, `OnChange` fires, `MainLayout` stops rendering the overlay. No manual dismiss needed.

### Rule 3a — Cage merge prompt

`WelfareLockOverlay`'s **Cage Merge panel**: "Rats need company — `RatA` (Cage `cageA.Name`) and `RatB` (Cage `cageB.Name`) each need a cage-mate." One button: "Move `RatB` into `cageA.Name`" (arbitrary direction — always move the second cage's occupant into the first, no player choice needed since either arrangement satisfies the rule). Implemented as `PickUpRatFromCageAsync(cageB.CageId, ratB.Id)` followed by `PlaceRatFromCarryCaseAsync(carryCaseId, cageA.CageId)` (see §3) — the button click drives both calls as one action from the player's perspective; only show a spinner/disable state across both, don't surface an intermediate "picked up" state. If the pickup call fails (400 `NoFreeCarryCase` — shouldn't normally happen here since resolving this rule doesn't require a *free-standing* extra carry case, just one to shuttle the rat through, but the panel should still handle it defensively), show the mapped error inline and don't attempt the place call.

### Rule 3b — Sequential companion adoption

`WelfareLockOverlay`'s **Companion Adoption panel**: two-step wizard, not two separate overlay states:
1. Step 1/2: "Adopt a companion for `cageA.Name` (needs a `cageA.Sex` rat)." Embedded browse grid filtered to `sex=cageA.Sex`, fee-waived (server-derived automatically, same display-only treatment as Rule 1). Confirm → `AdoptRatAsync(ratId)`.
2. On success, advance to Step 2/2 in the same panel (no re-render of the whole overlay, just internal wizard state) — the newly-adopted rat needs to land in cage A specifically, not just "a free carry case." `AdoptRatAsync`'s response now includes `CarryCaseId` directly (see Task14_Client_Design.md §2), so the wizard's step 1 completion is simply `PlaceRatFromCarryCaseAsync(result.CarryCaseId, cageAId)` — no diffing `GetHomeAsync()` carry-case state needed.
3. Step 2/2: same for cage B / `cageB.Sex`.
4. After both: `WelfareStateService.RefreshAsync()` clears the block.

---

## 6. Client-Side Validation & Guards

The client performs no independent welfare validation — it only reacts to `409 WelfareBlock` responses as a safety net for stale local state (the primary UX is overlay/banner-driven, this is the fallback).

| Response | Trigger | Client handling |
|---|---|---|
| `409 WelfareBlock`, reason `PostTutorialAdoptionRequired` | `POST /api/events/{lobbyId}/enter` when local state thought it was unlocked | Show inline error on the lobby row (same `entryError` field already in `Events.razor`), call `WelfareStateService.RefreshAsync()` to resync the banner. |
| `409 WelfareBlock`, reason `MinimumRatCountRequired` | Any write endpoint, if local state is stale | Toast a generic "Action blocked — resolve your rat welfare issue first," call `WelfareStateService.RefreshAsync()` to bring the overlay up. |
| `409 WelfareBlock`, reason `LoneCageResolutionRequired` | Any write endpoint, if local state is stale | Same handling as above. |

Because the exempt-endpoint list (adopt, carry-case-place, all reads, welfare/status itself) is small and stable, the client does not need a generic "wrap every mutating call with a welfare-aware handler" abstraction for MVP — each page's existing per-call error handling already surfaces the body text inline, and a `409` here is expected to be rare (only reachable via stale state, since the overlay/banner should have already prevented the attempt).

---

## 7. State Management

### New: `Services/WelfareStateService.cs` (singleton, registered in `Program.cs` next to `PlayerStateService`)

```csharp
public class WelfareStateService(GlassWingApiClient api)
{
    public WelfareStatusResponse? Status { get; private set; }
    public event Action? OnChange;

    public bool IsTotalLocked  => Status?.BlockScope == "TotalGameLock";
    public bool IsEventsLocked => Status?.ActiveBlock == "PostTutorialAdoption";

    public async Task RefreshAsync()
    {
        var result = await api.GetWelfareStatusAsync();
        if (result is not null)
        {
            Status = result;
            OnChange?.Invoke();
        }
    }
}
```

`Program.cs` addition: `builder.Services.AddSingleton<WelfareStateService>();` — matches `PlayerStateService`'s registration exactly. (Constructor-injecting `GlassWingApiClient` into a singleton is safe here since `GlassWingApiClient` is itself registered via `AddHttpClient<T>`, which resolves to a scoped/transient-per-request-equivalent client under Blazor WASM's single-scope-per-app model — consistent with how singletons already consume it implicitly via injected pages today.)

### Refresh triggers (recap from §4)

- `MainLayout.OnInitializedAsync` (app load)
- `NavigationManager.LocationChanged` (every route change) — wired in `MainLayout`
- `PeriodicTimer` every 30–60s while the app is open — wired in `MainLayout`
- Explicit calls after adopt/surrender/buy actions on the pages that perform them

### Why `MainLayout` and not a new top-level component

`MainLayout.razor` already owns the one long-lived subscription pattern in the app (`PlayerState.OnChange += StateHasChanged`) and already renders outside/around `@Body` for every route. Adding the overlay and the polling loop here avoids introducing a second layout-like wrapper.

---

## 8. Open Questions / Deferred

1. ~~**Rule 3a has no supporting endpoint.**~~ **Resolved** — `POST /api/home/cages/{cageId}/pickup/{ratId}` shipped (see §3, §5).
2. ~~**`AdoptRatAsync` doesn't return which carry case the rat landed in.**~~ **Resolved** — `CarryCaseId` is on the response now (see Task14_Client_Design.md §2).
3. ~~**Rule 3's `activeBlock` string value is unconfirmed.**~~ **Confirmed** — `"LoneCageResolution"` is correct, exactly as this doc assumed.
4. ~~**Rule 2 insufficient-funds dead end.**~~ **Resolved** — the fee is now waived server-side whenever Rule 2 is the active block, the same way Rule 1/3b already were (see §1, §5).
5. ~~**Exact `409` body shape for `WelfareBlockException`.**~~ **Resolved** — confirmed uniform `{ error, reason }` across the whole API (see `BackendAuditFindings.md` C1 in the API repo); `reason` carries codes like `MinimumRatCountRequired`/`LoneCageResolutionRequired`.
6. **Multi-tab / stale-timer edge case.** Two tabs open, one resolves a block; the other's `PeriodicTimer` will eventually catch up (within 30–60s) but until then its UI may show a resolved block as still active, or attempt an action that now 409s harmlessly (handled per §6). Not worth solving further for MVP — single-tab play is the assumed common case.
