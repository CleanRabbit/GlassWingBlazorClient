# Task 12 (Client) — Growth & Life Stages: Design Document

## 1. Overview

The API's Task 12 (`Task12_Design.md`) is fully implemented: four life stages (`Weening → Infant → Juvenile → Adult`), stat-potential accrual/finalization, nursing locks, protective mothers, Free Roam training, the sex-separation hard block, and stage-transition notifications are all live (`LifeStageService.cs`, `PlayerHomeModule.cs`). This is the largest client task of the five — it's the first to touch **every** screen that lists rats (`Home.razor` cage cards, `RatDetail.razor`, `Events.razor`, `Rats.razor`) and the first to introduce a **global, blocking game state** (`SexSeparationRequired`) that the client has no precedent for handling.

This task also finally wires up the cage-level training-session flow (`POST /api/home/cages/{cageId}/train`, `GET /api/home/cages/{cageId}/training`) that Task 11 identified as fully implemented server-side but never exposed on `Home.razor` — Free Roam only makes sense once that exists.

> **Refreshed 2026-07-06.** `StartTrainingSessionResponse` now includes `ExcludedRatIds` (ratId → reason), resolving this doc's own open question about client-inferred exclusion reasons — see §3, §4.

## 2. Pages & Components Affected

| File | Change |
|---|---|
| `GlassWingClient/Pages/Home.razor` | Cage rat badges show life-stage indicator + nursing/protective lock icon; wire "Start Training Session" button (currently missing entirely); render `LifeStageNotifications` as dismissible banners (same pattern as `AutoFills`); render the sex-separation hard-block modal |
| `GlassWingClient/Pages/RatDetail.razor` | Show current life stage, ability-vs-potential (potential now meaningfully varies pre-Adult — see §4), nursing/protective badges; hide/disable training and event-entry affordances per stage; Weening pups get a distinct minimal "pup" view |
| `GlassWingClient/Pages/Events.razor` | Rat picker dropdowns exclude Weening/Infant/nursing rats |
| `GlassWingClient/Layout/MainLayout.razor` | Host the sex-separation modal at the layout level so it blocks navigation-triggered actions app-wide, not just on Home (see §4) |
| `GlassWingClient/Services/ApiModels.cs` | Extend `RatResponse`, `RatSummary`, `HomeResponse`; add `LifeStageNotification`, `CageTrainingStateResponse`, `RatTrainingState` |
| `GlassWingClient/Services/GlassWingApiClient.cs` | Add `StartCageTrainingSessionAsync`, `GetCageTrainingStateAsync` |
| `GlassWingClient/Services/PlayerStateService.cs` | Add `SexSeparationRequired`/`SexSeparationCageId` as observable state so the modal can be triggered from any page load, not just Home's |

## 3. API Integration

### Confirmed current shapes (from `PlayerHomeModule.cs`)

`RatSummary` (nested in `CageResponse.Rats`) already carries, server-side, far more than the client's current minimal `RatSummary(string Id, string Name)`:
```
Id, Name, Phenotype, HealthState, SprintAbility, AgilityAbility, EnduranceAbility,
Diet (DietQuality), LifeStage, IsNursing, IsProtective
```

`HomeResponse` already carries:
```
SexSeparationRequired: bool
SexSeparationCageId: string?
LifeStageNotifications: LifeStageNotificationResponse[]   // { RatId, RatName, PreviousStage, NewStage, Message }
```

**Important, verified behavior:** `GET /api/home` drains `LifeStageNotifications` server-side on every call that returns a non-empty list (`PlayerHomeModule.GetHomeAsync`: `if (home.LifeStageNotifications.Count > 0) await homeSvc.ClearLifeStageNotificationsAsync(playerId);`). This is **exactly** the same one-shot pattern as `AutoFills` — the array is populated once, returned once, then gone. The client does not need a dismiss endpoint; local dismissal is purely a render concern (once dismissed, a page refresh won't bring it back anyway, since the server already cleared it).

**Also verified:** the actual stage-transition messages are simpler than the original API design doc proposed — there is **no** escalating "Prepare to separate males and females" message series at Juvenile/Infant. The real messages (`LifeStageService.BuildNotificationMessage`) are generic per-stage ("Biscuit has opened their eyes and is ready to explore!", "...is growing fast — now a curious juvenile.", "...has reached adulthood."). The sex-separation warning is carried entirely by the separate `SexSeparationRequired`/`SexSeparationCageId` flags, not by notification text — design the UI around that split, not around parsing notification strings for escalation level.

### `ApiModels.cs` additions

```csharp
// RatResponse additions (folds into the same record extended across Tasks 9-13)
bool IsNursing = false,
bool IsProtective = false,
// MotherId/FatherId already added in Task10_Client_Design.md

public record LifeStageNotification(string RatId, string RatName, string PreviousStage, string NewStage, string Message);

// HomeResponse additions
bool SexSeparationRequired = false,
string? SexSeparationCageId = null,
LifeStageNotification[]? LifeStageNotifications = null

// RatSummary (Home.razor cage cards) — currently just (string Id, string Name); expand to:
public record RatSummary(
    string Id,
    string Name,
    string LifeStage,
    bool IsNursing,
    bool IsProtective);
// (Phenotype/HealthState/abilities available server-side on RatSummary too, but not
//  needed by the cage-card badge — pulling them would bloat the Home payload for no
//  current UI benefit; fetch full RatResponse via GetRatAsync when the player opens
//  the rat's own page, as today.)

public record RatTrainingState(string RatId, string RatName, DateTime? TrainingSessionUntil, DateTime? TrainingCooldownUntil);
public record CageTrainingStateResponse(string CageId, CageRegimeInfo? Regime, TrainingBonus Efficacy, DateTime? SessionActiveUntil, RatTrainingState[] Rats);
public record StartTrainingSessionResponse(
    DateTime SessionUntil, string[] RatIds, TrainingBonus Efficacy,
    IReadOnlyDictionary<string, string> ExcludedRatIds);   // ratId -> reason, shipped 2026-07-06 — see §3 note below
```

### `GlassWingApiClient.cs` additions

```csharp
public async Task<CageTrainingStateResponse?> GetCageTrainingStateAsync(string cageId)
{
    var resp = await http.GetAsync($"/api/home/cages/{cageId}/training");
    return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<CageTrainingStateResponse>(JsonOpts) : null;
}

public async Task<(StartTrainingSessionResponse? Result, string? Error, string? Reason)> StartCageTrainingSessionAsync(string cageId)
{
    var resp = await http.PostAsync($"/api/home/cages/{cageId}/train", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<StartTrainingSessionResponse>(JsonOpts), null, null);
    return await ReadConflictAsync<StartTrainingSessionResponse>(resp);
}
```

Note the server's actual eligibility model for cage sessions is **exclusion, not a hard block**: `StartTrainingSessionAsync` filters out Weening pups always, Infant/nursing rats unless Free Roam, and Protective mothers for Free Roam — then trains whoever's left. It only 422s if the filtered set is empty ("No eligible rats in cage for this training session."), it does **not** return a per-rat `MotersLocked`/`PupTooYoung` 409 as the original API design doc implied. **Updated 2026-07-06:** exclusions are no longer silent — `ExcludedRatIds` (ratId → reason) is on the response, with reason values `"Retired"` / `"Weening"` / `"Infant"` / `"Nursing"` / `"Protective"`. The client no longer needs to infer why a rat sat out from its own `LifeStage`/`IsNursing`/`IsProtective` fields — read the reason directly (see §4).

## 4. UX Flows

### Life stage & lock badges — Home cage card

Each rat badge in `Home.razor` (currently a plain `<a class="badge bg-secondary">@name</a>`) grows a stage-appropriate decoration:

| `LifeStage` | Badge treatment |
|---|---|
| `Weening` | Pink badge, pup icon, name replaced with "Pup" (per TASKS.md backlog note: "Weening pups displayed as tiny pink rat pup placeholder; no stats or appearance shown") — still links to `/rats/{id}` but that page shows the minimal pup view (see below) |
| `Infant` / `Juvenile` | Normal badge + small stage-abbreviation superscript (e.g. "Biscuit `Inf`") |
| `Adult` | Normal badge, no decoration |

If `IsNursing`, append a 🔒 lock icon with `title="Nursing — locked to this cage"`. If `IsProtective`, use a distinct icon/title ("Protective mother — free-roam unavailable too").

### Life stage & lock display — Rat Detail

- Add a life-stage badge next to the name (`bg-info` "Infant", etc.).
- If `LifeStage == "Weening"`: render the **minimal pup view** — hide Fitness/Health/Appearance/Tricks/Training/Marketplace sections entirely, show only: name, life stage badge, "born {DateOfBirth}", mother link (if `MotherId` resolvable), and a note: "Too young to show full details. Check back once {Name} reaches the Infant stage." This directly satisfies the TASKS.md backlog note about pups having no stats/appearance shown.
- If `LifeStage` is `Infant`/`Juvenile`/`Adult`: full page as today, plus the Ability card from `Task11_Client_Design.md` §4 now genuinely shows a partial `Potential` for Infant+ pups bred after this task ships (their potential was finalized at the Weening→Infant transition using the mother's condition at that moment — an interesting number worth surfacing, e.g. a small note: "Potential finalized based on nutrition, mother's health, and cage conditions during weening.").
- `IsNursing`/`IsProtective` badges shown for Adult mothers, with the restriction list from the API doc's §5 spelled out in a tooltip/expandable note rather than inline prose (cage move, carry case, marketplace, events, and regime training are all blocked; free-roam is allowed unless Protective).

### Free Roam & the "Start Training Session" action (new)

This is the first client UI for the cage-level session flow. On `Home.razor`, under the existing Regime picker row, add:

```
Regime: Free Roam  [Change]
[Start Session]  (disabled while a session is active or cage has no regime)
Session ends in 00:42:11        ← shown once GetCageTrainingStateAsync reports SessionActiveUntil
```

`[Start Session]` calls `StartCageTrainingSessionAsync(cageId)`. On success, show a small confirmation toast (mirroring `Marketplace.razor`'s bottom-right toast pattern) listing which rats participated: "Training started for Dash, Pip. Ellie sat this one out (too young)." — read the reason directly from `StartTrainingSessionResponse.ExcludedRatIds[ratId]` (map `"Retired"`/`"Weening"`/`"Infant"`/`"Nursing"`/`"Protective"` to friendly copy) rather than inferring it client-side from the rat's own fields.

If `StartTrainingSessionResponse.RatIds` is empty (which can't happen — a 422 is returned instead when nobody's eligible), the client instead surfaces the 422 message: "No eligible rats in cage for this training session."

### Sex separation hard block

This is a **global** modal, not a per-page one — the design doc's spec ("total game lock" on all write endpoints while `SexSeparationRequired == true`) means it can be triggered by an action on any page (training, breeding, cage rename attempt while a stale write races the flag, etc.), not just Home.

Implementation:
1. `PlayerStateService` gains `SexSeparationRequired`/`SexSeparationCageId` fields + `SetSexSeparation(bool, string?)`, following the exact pattern already used for `Currency`.
2. `Home.razor`'s `LoadAsync()` calls `PlayerState.SetSexSeparation(home.SexSeparationRequired, home.SexSeparationCageId)` after every fetch (Home is the only page that currently loads the full `HomeResponse`, so it's the natural place this gets refreshed).
3. Any write action anywhere that gets back a `409 SexSeparationRequired` reason also calls `PlayerState.SetSexSeparation(true, reason's cage id if available)` immediately, so the modal appears without waiting for the next Home load.
4. `MainLayout.razor` renders the modal whenever `PlayerState.SexSeparationRequired == true`, subscribing to `PlayerState.OnChange` like any other consumer would. Modal content:
   ```
   Sex separation required
   One of your cages has both adult males and females. Every other action
   is locked until you resolve this.
   [Go to cage]   (navigates to /home and scrolls/highlights SexSeparationCageId)
   ```
   No "Cancel" button — this modal is not dismissible, matching the API's "total game lock" intent. It disappears on its own the next time any page reloads Home and the flag has cleared (evaluated lazily server-side per the API doc — the client doesn't need to poll, just re-check on the next natural `GetHomeAsync()`).
5. Resolution actions (move to another cage via existing `PlaceRatFromCarryCaseToCageAsync`... actually the existing move endpoint requires a carry case; moving directly cage-to-cage isn't exposed by any current client method — flag as an open question) or sell via `Marketplace.razor`'s existing listing flow (already reachable from `RatDetail.razor`) are just the existing flows; the modal's "Go to cage" button doesn't need new plumbing beyond navigation + a highlight.

## 5. Client-Side Validation & Guards

| Guard | Client behavior | Backend |
|---|---|---|
| Weening pup — any action | Rat Detail shows pup-only view with zero action buttons | N/A — no actions are offered |
| Infant/Weening — event entry | Excluded from `Events.razor` rat pickers | Plain-text rejection ("Pups cannot enter events until they are Juvenile.") mapped to 404 by `EventLobbyService` |
| Infant/Weening — individual training (`POST /api/rats/{id}/train`) | Training buttons hidden on Rat Detail for these stages | `422` ("Rat 'X' is too young for individual training.") |
| Nursing mother — individual training, events, cage move, marketplace, carry case | All relevant buttons hidden/disabled on Rat Detail with a "Nursing" tooltip | `422`/plain-text rejections per action |
| Cage-level session with zero eligible rats | Disable "Start Session" preemptively if the cage's only rats are Weening (client can compute this from `RatSummary.LifeStage`) | `422 No eligible rats...` as a fallback for edge cases the client didn't predict |
| Sex separation active | Global modal blocks the UI; no other guard needed since the block is total | `409 SexSeparationRequired` on every write endpoint |

## 6. State Management

`PlayerStateService` grows beyond currency for the first time — add:
```csharp
public bool SexSeparationRequired { get; private set; }
public string? SexSeparationCageId { get; private set; }
public void SetSexSeparation(bool required, string? cageId)
{
    SexSeparationRequired = required;
    SexSeparationCageId = cageId;
    OnChange?.Invoke();
}
```
This is the first cross-page blocking state the client has; no polling is introduced — it piggybacks on whatever `GetHomeAsync()` calls already happen, plus immediate updates from any 409's `Reason`.

## 7. Notifications

`LifeStageNotifications` render exactly like `AutoFills` on `Home.razor` — same dismissible `alert-info alert-dismissible` banner, same `HashSet<int> dismissedLifeStageNotifications` local-index tracking (no server round-trip to dismiss, since the array is already one-shot per §3). Recommend a distinct color (`alert-success` with a growth/sparkle framing) so they're visually distinguishable from auto-fill banners in the same stack:

```razor
@if (home!.LifeStageNotifications is { Length: > 0 })
{
    @for (int i = 0; i < home.LifeStageNotifications.Length; i++)
    {
        var idx = i;
        if (!dismissedLifeStageNotifications.Contains(idx))
        {
            <div class="alert alert-success alert-dismissible mb-2 py-2" role="alert">
                @home.LifeStageNotifications[idx].Message
                <button type="button" class="btn-close" @onclick="() => dismissedLifeStageNotifications.Add(idx)"></button>
            </div>
        }
    }
}
```

## 8. Open Questions / Deferred

- **No direct cage-to-cage move endpoint** is exposed by `GlassWingApiClient.cs` today — only carry-case → cage (`PlaceRatFromCarryCaseToCageAsync`). The sex-separation resolution flow ("move rats to another cage") may need either a new endpoint or a two-step UI (move to carry case, then place in a different cage) — needs sign-off before implementation.
- ~~**"Sat this one out" reasoning is inferred client-side**~~ — **Resolved**, `ExcludedRatIds` shipped on `StartTrainingSessionResponse` (see §3, §4).
- **Potential-finalization explanation** (nutrition/stress/health breakdown) is not returned by the API beyond the final number — same category of gap as Task 11's "why this gain" question. Deferred.
- **Fussy eating/appetite UI** (TASKS.md backlog, separate task) will eventually want to sit near the life-stage badges on both Home and Rat Detail — no conflict expected, just noting the future neighbor.
