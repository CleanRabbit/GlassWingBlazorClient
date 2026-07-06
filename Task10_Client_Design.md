# Task 10 (Client) — Breeding: Design Document

## 1. Overview

The API's Task 10 (`Task10_Design.md`) is fully implemented: `POST /api/rats/breed`, sex chromosomes, pregnancy, gestation, and the hourly delivery job are all live (confirmed against `BreedingService.cs`, 888 passing tests). The API doc explicitly punted client work ("Client work is tracked separately in TASKS.md... No client changes are in scope for this task") and TASKS.md still lists "Breeding UI — blocked on API backlog" even though the API is done — that bullet is stale. This document is the client design that unblocks it.

Breeding introduces three new rat-level concepts the client has never rendered: `Sex`, `LifeStage` (stub only here — full rendering rules are in `Task12_Client_Design.md`), and `Pregnancy`. This doc covers the breeding action itself and the minimum pregnancy/sex UI needed to use it; life-stage badges, nursing locks, and the sex-separation hard block are Task 12's job.

## 2. Pages & Components Affected

| File | Change |
|---|---|
| `GlassWingClient/Pages/RatDetail.razor` | New "Breeding" section: mate picker for female rats, pregnancy status card, read-only note for male rats |
| `GlassWingClient/Pages/Rats.razor` | Add a Sex column; add a "Pregnant" badge next to the name |
| `GlassWingClient/Pages/Home.razor` | Cage rat badges (currently plain name pills, `home.Cages[].Rats[].Name`) grow a small pregnancy indicator; full life-stage/pup rendering deferred to Task 12 |
| `GlassWingClient/Services/ApiModels.cs` | Extend `RatResponse`; extend `RatSummary`; add `PregnancyResponse` |
| `GlassWingClient/Services/GlassWingApiClient.cs` | Add `BreedRatsAsync` |

## 3. API Integration

### Endpoint consumed

`POST /api/rats/breed` — body `{ "femaleRatId": "...", "maleRatId": "..." }`. Returns `201`/`200` with the raw `PregnancyRecord` on success (not a wrapped `motherRatId`/`fatherRatId` object as the original API design doc sketched — verified in `RatsModule.BreedAsync`: `return Results.Ok(result.Pregnancy);`). `404` if either rat isn't found/owned. `409` with `{ error, reason }`.

**Real conflict reason codes** (from `BreedingService.cs` and `BreedingServiceTests.cs` — these differ from the original API design doc's proposed names, which were written before implementation):

| Reason | Meaning | Suggested client message |
|---|---|---|
| `WrongSexPairing` | Both rats are the same sex | "One rat must be male and one female." (should be unreachable if the mate picker is filtered correctly — see §5) |
| `FemaleRetired` | Female is retired | "This rat has retired and can no longer breed." |
| `MaleRetired` | Male is retired | "The selected mate has retired." |
| `AlreadyPregnant` | Female already pregnant | (shouldn't reach the server — breeding section is replaced by the pregnancy card once pregnant) |
| `CooldownActive` | Youngest pup from last litter hasn't reached independence age | "Still recovering from her last litter." |
| `LitterLimitReached` | `LitterCount >= MaxLittersPerFemale` | "This rat has reached her lifetime litter limit." |
| `FemaleNotAdult` | Female isn't `LifeStage.Adult` | "Must be fully grown to breed." |
| `FemaleTooYoung` / `FemaleTooOld` | Outside the breeding age window | "Outside the breeding age window." |
| `MaleTooOld` | Past max siring age | "The selected mate is too old to sire a litter." |
| `SexSeparationRequired` | Home has a pending sex-separation lock (Task 12) | See `Task12_Client_Design.md` §5 — surface the same modal as every other blocked write endpoint |

### `ApiModels.cs` additions

```csharp
public record RatResponse(
    string Id,
    string Name,
    string OwnerId,
    TrainingFitness? Fitness,
    HealthState? HealthState,
    RatPhenotype? Phenotype,
    string[]? TricksLearned,
    bool IsRetired = false,
    DateTime? RetiredAt = null,
    string? RetirementReason = null,
    DateTime RetiresAt = default,
    string Sex = "Female",           // "Male" | "Female"
    string LifeStage = "Adult",      // "Weening" | "Infant" | "Juvenile" | "Adult" — full handling in Task 12
    int LitterCount = 0,
    bool IsPregnant = false,
    DateTime? DueAt = null,
    string? MotherId = null,
    string? FatherId = null);

public record PregnancyResponse(
    string FatherId,
    int GestationRatDays,
    DateTime DueAt,
    double ConceptionWellnessScore);
```

`RatResponse` already carries `MotherId`/`FatherId` server-side (confirmed in `RatsModule.ToResponse`) even though they're not part of the original Task 10 API doc's response table — useful now for a "sired/born from" note and essential later for `Task13_Client_Design.md`'s ancestry tree.

`RatSummary` (nested in `CageResponse.Rats` on Home) now also carries `IsPregnant`/`DueAt` (shipped alongside the existing `IsNursing`/`IsProtective`, same pattern) — see `Task12_Client_Design.md` §2 for the full `RatSummary` shape. The Home cage-card pregnancy badge in §4 below can use it directly with no workaround.

### `GlassWingApiClient.cs` addition

```csharp
public async Task<(PregnancyResponse? Result, string? Error, string? Reason)> BreedRatsAsync(string femaleRatId, string maleRatId)
{
    var resp = await http.PostAsJsonAsync("/api/rats/breed", new { femaleRatId, maleRatId }, JsonOpts);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<PregnancyResponse>(JsonOpts), null, null);
    return await ReadConflictAsync<PregnancyResponse>(resp); // generic form of the Task 9 helper
}
```

## 4. UX Flows

### Breeding section on `RatDetail.razor`

Only rendered for non-retired rats. Behavior branches on `rat.Sex` and `rat.IsPregnant`:

**Female, not pregnant, adult:**
```
Breeding
[ pick a mate ▾ ]  [Breed]
```
The mate dropdown lists the player's own male rats (`Api.ListRatsAsync()` filtered `Sex == "Male" && !IsRetired`), following the exact `<select>` pattern already used for rat pickers in `Events.razor` ("— pick a rat —" placeholder option, `@bind` to a string id). "Breed" is disabled until a mate is selected, then calls `BreedRatsAsync(rat.Id, selectedMaleId)`.

On success: reload the rat (`await LoadRatAsync()`), which now has `IsPregnant = true` — the section re-renders as the pregnancy card below. On failure: show `err.Error` inline beneath the picker, same `alert-danger py-2` treatment used for `trainMessage`/`listingMessage` elsewhere on this page.

**Female, pregnant:**
```
Breeding
🐭 Pregnant — due in 4 days (sired by Cosmo)
```
"Due in X days" computed client-side from `DueAt - DateTime.UtcNow`; "sired by {name}" requires resolving `FatherId` to a name. **Updated 2026-07-06:** use `Api.GetPublicRatAsync(fatherId)` (see `Task13_Client_Design.md` §3) rather than the owner-only `GetRatAsync` — it has no ownership check (404 only if the rat doesn't exist at all), so a sold/retired father the player no longer owns still resolves to a real name instead of falling back to "sired by another rat." The public projection includes `Name`, which is all this lookup needs.

**Male:**
```
Breeding
This rat can sire litters. Start breeding from the mother's page.
```
No action available on a male's own page — breeding is always initiated from the female.

**Non-adult (`LifeStage != "Adult"`):** breeding section is hidden entirely (both sexes) — full life-stage-aware UI is `Task12_Client_Design.md`'s job, but since the field exists now, hiding on `LifeStage != Adult` avoids a guaranteed `FemaleNotAdult` round-trip in the meantime.

### Home cage card pregnancy indicator

In `Home.razor`, the per-slot rat badge (currently `<a href="/rats/@id" class="badge bg-secondary">@name</a>`) needs the mother's pregnancy surfaced. `RatSummary.IsPregnant`/`DueAt` are now available directly on `CageResponse.Rats` (see §2) — no cross-referencing against a separate `ListRatsAsync()` fetch needed. Add a small "🐭 Pregnant" badge/icon next to the rat name when `IsPregnant` is true, same visual weight as the existing nursing indicator.

## 5. Client-Side Validation & Guards

The mate picker's own filtering (`Sex == "Male" && !IsRetired`) makes `WrongSexPairing` and one side of `*Retired` structurally unreachable from the UI — but the dropdown can't cheaply pre-filter on age window, litter count, or cooldown without the `GameBalance` values described below, so those remain server-round-trip errors surfaced via `err.Error`.

| Guard | Client-side | Server 409 |
|---|---|---|
| Same-sex pairing | Picker only lists opposite sex | `WrongSexPairing` |
| Either rat retired | Both pickers/pages exclude retired rats | `FemaleRetired` / `MaleRetired` |
| Female already pregnant | Section swaps to pregnancy card, no picker shown | `AlreadyPregnant` |
| Litter limit / age window / male age / cooldown | **Not preflighted** — `MaxLittersPerFemale`, `MinBreedingAgeRatWeeks`, `MaxBreedingAgeRatWeeks`, `MaleMaxSiringAgeRatMonths`, and the post-partum cooldown duration are not exposed via `GET /api/game/settings` today (verified against `GameModule.cs` — only retirement/adoption keys are exposed). Client can only show the litter count it already has (`rat.LitterCount`) without a ceiling to compare it to. See Open Questions. | `LitterLimitReached` / `FemaleTooYoung` / `FemaleTooOld` / `MaleTooOld` / `CooldownActive` |
| Non-adult breeding | Section hidden for `LifeStage != Adult` | `FemaleNotAdult` |
| Sex separation lock | N/A here — global modal, see Task 12 | `SexSeparationRequired` |

## 6. State Management

No new singleton. The mate-picker rat list is fetched fresh each time the breeding section is expanded (mirrors `Events.razor`'s `LoadRatsAsync()` pattern) rather than cached, since ownership/retirement/age can change between page visits.

## 7. Notifications

Task 10 doesn't add any Home notification list — litter delivery happens invisibly via the hourly Hangfire job, and the pups simply appear in the mother's cage the next time `GetHomeAsync()` is called (Weening pups count as 0 capacity, so no overcrowding surprise). There is no "your litter arrived" banner in the API's design. Whether to add a client-only heuristic (e.g. diff cage rat-count between loads) is deferred — see Open Questions. Task 12's `LifeStageNotifications` do cover the pup's *first* stage transition (Weening → Infant) but not the birth event itself.

## 8. Open Questions / Deferred

- ~~**`GameBalance` breeding keys are not exposed via `GET /api/game/settings`.**~~ **Resolved** — `MinBreedingAgeDays`/`MaxBreedingAgeDays`/`MaleMaxSiringAgeDays`/`MaxLittersPerFemale`/`MinGestationRatDays`/`MaxGestationRatDays`/`PostPartumCooldownDays` all shipped on `GameSettingsResponse`. Real age/litter-limit/gestation-range UI copy no longer needs to be generic.
- ~~**`RatSummary.IsPregnant`/`DueAt` missing**~~ — **Resolved**, both fields shipped on `RatSummary`, same pattern as `IsNursing`. The Home cage-card pregnancy badge in §4 can use them directly.
- **No "litter arrived" notification.** Deferred; would need a new Home-level notification, not currently in the API's Task 10/12 design.
- ~~**Father resolution for sold/retired sires**~~ — **Resolved**, see §4 above: use `GetPublicRatAsync` instead of `GetRatAsync`.
