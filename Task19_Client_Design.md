# Task 19 (Client) — Cognitive Abilities & Trick Learning: Client Design Document

## Backend Dependency Note

**Refreshed 2026-07-06: the backend has since shipped.** Task 19 was implemented 2026-07-04 (1248 tests passing at the time, commit `c7521e6`) — `GET /api/tricks`, `PUT /api/rats/{id}/trick-training`, the `TricksPerformance` event type, and `bonding`/`tricksLearned`/`currentTrickTraining` fields are all live. One real deviation from the design doc worth knowing: the genome gained 3 new Cognition loci during implementation (a mid-implementation design pass, not part of the original contract this client doc was written against) — `RatPhenotype.Cognition` has more fields than this doc may assume. Re-verify shapes against the live API/`openapi.json` before wiring up; this doc is otherwise unverified against the real implementation as part of this refresh pass.

## 1. Overview

Task 19 adds three player-facing surfaces to the client:

1. A **trick catalogue** view — all 15 tricks across 3 categories, with per-rat learning status.
2. A **trick-training assignment control** on the rat detail page — assign/cancel a trick to actively train, mirroring the existing cage "Regime" inline-picker pattern.
3. A **routine-builder step** in the event entry flow, shown only when entering a `TricksPerformance` lobby.

Bonding (`currentBondingLevel` / `capacity`) is a passive, read-only stat surfaced alongside tricks in this task. Task 20 (`Task20_Client_Design.md`) builds the active play-session UI on top of the trick-training control introduced here and should be read second.

---

## 2. Pages & Components

### 2.1 New page: `Pages/Tricks.razor` (`/tricks`)

A dedicated catalogue page, added to `NavMenu.razor` between "Rats" and "Events". Mirrors the `GET /api/tricks` grouping: one section per `TrickCategory` (Athletic, ComplexSequence, Performative), each rendering its 5 tricks as cards or table rows.

Per trick, per rat, render a compact status chip using the existing badge-class convention (see `RatDetail.razor` `VitalityBadgeClass`):

| Status | Badge | Notes |
|---|---|---|
| `Learned` | `bg-success` | ✓ + trick name |
| `InTraining` | `bg-primary` | shows `progress`% |
| `SocialLearning` | `bg-info text-dark` | shows `progress`%, labelled "picking up from cage-mates" |
| `Locked` | `bg-secondary` (dim/opacity-50) | shows `aptitudeThreshold` vs rat's own `aptitude` |
| `NotStarted` | outline/`border-secondary` | eligible but untouched |

This page is a **read + navigate** surface, not where assignment happens — clicking a rat's chip navigates to `/rats/{id}` (the assignment control lives there, consistent with training/regime living on the owning entity rather than a catalogue index). Loaded via a new `Api.GetTrickCatalogueAsync()` call in `OnInitializedAsync`, same "Loading..." / null-guard pattern as other pages.

### 2.2 `RatDetail.razor` — Tricks card becomes a full section

The existing minimal "Tricks" card (lines 102–122, just a bullet list of `TricksLearned`) is expanded into a dedicated `<h5>Tricks</h5>` section below "Training", following the same layout rhythm as the existing Training/Marketplace sections:

- **Learned tricks**: list with tier badge, same as today but with tier shown.
- **Bonding**: a labelled progress bar, `CurrentBondingLevel / BondingCapacity`, e.g. `Bonding 42.5 / 80.0`. Read-only in this task (Task 20 adds the mechanism that grows it faster).
- **Trick-training assignment control**: follows the **exact interaction shape of the Home.razor cage "Regime" control** (`Change` link → inline `<select>` + `Set`/`Cancel` buttons, busy-disabled state, inline error line). See `Home.razor` lines 344–372 for the reference pattern.
  - Collapsed state: `Currently training: Backflip (45%)` or `Currently training: None` + a `Change` link.
  - Expanded state: `<select>` of tricks grouped by category (`<optgroup>`), showing only tricks not already `Learned`; each `<option>` disabled (greyed, with a lock icon or `🔒` prefix) when `status == Locked` for this rat. A `None` option cancels training.
  - `Set` button calls `PUT /api/rats/{id}/trick-training`; `Cancel` closes the picker without calling the API (matches Home.razor's `Cancel` semantics — dismissing the picker, not calling the endpoint with `null`). Explicitly choosing the `None` option and pressing `Set` is what triggers cancellation server-side.

### 2.3 `Events.razor` — routine-builder step for `TricksPerformance`

`Events.razor` currently hardcodes three event types (`Sprint`, `AgilityCourse`, `Endurance`) in `PlayerInitiatedDefs` and `TypeBadgeClass`/`TypeLabel`. Task 19 adds a fourth:

- Add `new("open-tricks-performance", "Open Tricks Performance", "TricksPerformance")` to `PlayerInitiatedDefs`.
- Extend `TypeBadgeClass`/`TypeLabel` with a `TricksPerformance` → `bg-purple` (or reuse `bg-dark`)/`"Tricks"` case.
- **Live Lobbies and Create Event entry rows**: when the selected event's `EventType == "TricksPerformance"` AND a rat is picked, render an additional routine-builder block inline (below the rat `<select>`, above the `Confirm`/`Create & Enter` button):
  - Multi-select (checkboxes, not a `<select multiple>`, for clarity) of the **chosen rat's `TricksLearned`** tricks only — fetched via the already-cached `GetTrickCatalogueAsync()` result, filtered client-side to `status == Learned` for `activeRatId`.
  - A running counter: `3 / 5 selected` (`TrickMaxRoutineSize` from `GET /api/game/settings`, see §3.2).
  - Checkboxes beyond the max are disabled once the cap is reached (client-side pre-validation, not just server rejection).
  - If the rat has **zero** learned tricks, show `This rat hasn't learned any tricks yet.` and disable the Confirm/Create button — the routine field is required and non-empty for this event type per the API contract.

---

## 3. API Integration

### 3.1 New endpoints consumed

| Endpoint | Method | Purpose |
|---|---|---|
| `GET /api/tricks` | new `GetTrickCatalogueAsync()` | Full catalogue + per-rat status, powers `Tricks.razor` and the routine-builder filter |
| `PUT /api/rats/{id}/trick-training` | new `SetTrickTrainingAsync(ratId, trickId?)` | Assign/cancel training |
| `POST /api/events/{lobbyId}/enter` | existing `EnterLobbyAsync`, **new overload** taking `string[]? routine` | Routine passed only for `TricksPerformance` lobbies |
| `GET /api/game/settings` | existing `GetGameSettingsAsync()` | Extend response with `TrickMaxRoutineSize`, `SocialLearningAptitudeThreshold` |

### 3.2 `ApiModels.cs` additions

```csharp
// --- Tricks ---

public record TrickCatalogueResponse(TrickCategoryGroup[] Categories);
public record TrickCategoryGroup(string Category, TrickDefinition[] Tricks);
public record TrickDefinition(
    string Id, string Name, int Tier, double BaseScore, int AptitudeThreshold,
    TrickRatStatus[] Rats);
public record TrickRatStatus(
    string RatId, string RatName, string Status, // Learned | InTraining | SocialLearning | Locked | NotStarted
    double Progress, double Aptitude);

public record TrickTrainingRequest(string? TrickId);

// --- RatResponse additions (new fields, existing record extended) ---
// TricksLearned already exists. Add:
//   CurrentTrickTraining   TrickTrainingStateDto?
//   Bonding                BondingInfo?
public record TrickTrainingStateDto(string TrickId, double Progress, DateTime StartedAt);
public record BondingInfo(double CurrentLevel, double Capacity);
```

`RatResponse` gains `CurrentTrickTraining` and `Bonding` as trailing optional parameters (default `null`), consistent with how `HomeResponse`/`GameSettingsResponse` already extend via optional trailing params — avoids a breaking constructor change.

`GameSettingsResponse` gains `int? TrickMaxRoutineSize = null` and `int? SocialLearningAptitudeThreshold = null`, same optional-trailing-param convention already used for `MarketplaceListingFee`.

### 3.3 `GlassWingApiClient.cs` additions

```csharp
public async Task<TrickCatalogueResponse?> GetTrickCatalogueAsync()
{
    var resp = await http.GetAsync("/api/tricks");
    return resp.IsSuccessStatusCode
        ? await resp.Content.ReadFromJsonAsync<TrickCatalogueResponse>(JsonOpts)
        : null;
}

public async Task<(RatResponse? Rat, string? Error)> SetTrickTrainingAsync(string ratId, string? trickId)
{
    var resp = await http.PutAsJsonAsync($"/api/rats/{ratId}/trick-training", new { trickId }, JsonOpts);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode switch
    {
        409 when body.Contains("InsufficientAptitude") => "This rat isn't ready for that trick yet.",
        409 when body.Contains("AlreadyLearned")        => "Already learned.",
        409 when body.Contains("RatNotInCage")          => "Rat must be in a cage to train.",
        _ => string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body
    });
}
```

`EnterLobbyAsync` gains an optional `routine` parameter (`string[]? routine = null`), included in the request body only when non-null, preserving the existing call sites in `Events.razor` for the three pre-existing event types.

---

## 4. UX Flows

### 4.1 Browsing and assigning tricks

1. Player opens `/tricks` or scrolls to the Tricks section on `/rats/{id}`.
2. Categories render as collapsible sections (default expanded) — 15 tricks total is small enough to not strictly need collapse, but keeps the page scannable per-category.
3. Assignment happens only from `RatDetail.razor` (§2.2). Selecting a trick and pressing `Set` shows an optimistic "Assigning..." state, then either updates `rat` from the response or shows the mapped error inline (same `trainMessage`/`trainSuccess` pattern already used for `TrainAsync`).

### 4.2 Aptitude-gating feedback — showing "can't learn this yet" before the player tries

The `GET /api/tricks` response already includes `aptitude` per rat per trick (the rat's raw composite score) alongside `status: "Locked"`. The client uses this to pre-empt the `409 InsufficientAptitude` round-trip entirely:

- In the trick-training `<select>`, locked tricks are rendered as `disabled` `<option>`s with label `Backflip (requires 70 Athletic, has 55)`.
- On the catalogue page, locked chips show the same threshold-vs-actual text on hover/in a `title` attribute (no dedicated tooltip component exists in the client yet — plain `title=""` is consistent with the zero-dependency style seen elsewhere).
- Because locked options are disabled client-side, the `409 InsufficientAptitude` path becomes a defensive fallback (e.g. stale cache, aptitude data race) rather than the primary feedback mechanism — but `SetTrickTrainingAsync` still maps it to a friendly message per §3.3 in case it fires.

### 4.3 Bonding display

Read-only progress bar (reuse the `progress`/`progress-bar` Bootstrap pattern already used for cage Food/Water levels and storage bins): `width: (CurrentLevel/Capacity)*100%`. No colour-tiering needed (bonding is always "good," unlike food/water which warn at low values) — a single neutral colour (`bg-info` or `bg-primary`) is enough since bonding never decays in this task.

### 4.4 Entering a `TricksPerformance` event with routine selection

1. Player selects a `TricksPerformance` lobby/create-def and picks a rat (existing flow).
2. Routine-builder block appears (§2.3), pre-filtered to that rat's learned tricks.
3. Client-side guards before enabling `Confirm`/`Create & Enter`:
   - At least 1 trick selected.
   - No more than `TrickMaxRoutineSize` selected (checkboxes disable beyond the cap rather than allowing an invalid submission).
4. On submit, `routine: [...]` is included in the `POST /api/events/{lobbyId}/enter` body. Duplicate-selection is structurally impossible with checkboxes bound to distinct trick ids, so client-side dedup isn't needed.
5. Errors (`400 RoutineTooLong`, `400 UnknownTrick`, `409 TrickNotLearned`) map to the existing `entryError`/`createError` alert slot — these should be rare given client-side pre-validation, but are not treated as unreachable (catalogue could be stale if the player has two tabs open).

---

## 5. Client-Side Validation & Guards

| Guard | Where | Rationale |
|---|---|---|
| Hide/disable trick options where `status == Locked` | Trick-training `<select>` | Pre-empt `409 InsufficientAptitude` |
| Hide trick options where `status == Learned` | Trick-training `<select>` | Pre-empt `409 AlreadyLearned` |
| Disable routine checkboxes beyond `TrickMaxRoutineSize` | Routine builder | Pre-empt `400 RoutineTooLong` |
| Require ≥1 routine selection to enable Confirm | Routine builder | Pre-empt `400` missing/empty routine |
| Filter routine choices to `TricksLearned` only | Routine builder | Pre-empt `409 TrickNotLearned` |
| Disable Set button while a request is in flight | Trick-training control | Existing `training`/`regimeBusy`-style double-submit guard |

None of these replace server validation — they exist purely to avoid a round-trip for the common case, matching how the aptitude-threshold `<option disabled>` pattern is the primary mechanism in §4.2.

---

## 6. Open Questions / Deferred

- **Trick catalogue caching**: `GET /api/tricks` returns state for *all* the player's rats in one call. Should `Tricks.razor` and `RatDetail.razor`/`Events.razor` share a cached copy (e.g. via a scoped service) instead of each independently fetching? Given the client has no existing caching layer (every page calls `Api.Get...Async()` fresh in `OnInitializedAsync`), this doc defers to that convention and re-fetches per page. Worth revisiting once trick data volume/latency is measured against the real backend.
- **Locked-trick tooltip UX**: plain `title=""` attributes are a placeholder; if the client ever adopts a proper tooltip/popover component, revisit the aptitude-gating hint the same way.
- **Routine ordering**: the `routine` array is presumably scored in list order but the source design doesn't specify whether order affects anything beyond `rawScore` summation (order-independent per the formula in Task19_Design.md §10). This doc assumes order is cosmetic and does not build a reorder UI (checkboxes, not a sortable list).
- **Social-learning attribution on the catalogue page**: `SocialLearning` status has no assignment action (it's automatic) — the catalogue should make clear these rows are informational, not clickable-to-assign. Exact copy ("passively learning from a cage-mate") is a content/polish detail left to implementation.
- **Bonding decay**: explicitly out of scope per Task19_Design.md §15 and not revisited here; if Task 20 or later introduces decay, the bonding progress bar in §4.3 will need a "trend" indicator (up/down arrow) — not designed here.
