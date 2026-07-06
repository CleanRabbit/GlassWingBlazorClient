# Task 11 (Client) — TrainingLevel → Realized Ability: Design Document

## 1. Overview

The API's Task 11 breaking change (`Task11_Design.md`) is fully implemented and shipped. There never was a `TrainingLevel` concept on the client — the client's current `RatResponse.Fitness` (`TrainingFitness` with per-stat `Score`/`TrainingCount`) is a **different, already-stale shape** that doesn't match anything the current backend returns at all. The real, current `RatResponse` (verified in `GlassWing/Features/RatsAndBreeding/RatsModule.cs`) has no `Fitness` object — it has six flat fields: `SprintAbility`/`AgilityAbility`/`EnduranceAbility` (realized ability, `double`, range `[1, Potential]`) and `SprintPotential`/`AgilityPotential`/`EndurancePotential` (`int`, ceiling for that stat). This task is therefore not really "migrate off TrainingLevel" on the client — it's "delete the fictional `Fitness` model and build the real one," which is the higher-priority, isolated, do-first change the backend also treated it as.

This also affects `Home.razor`'s cage-card `TrainingEfficacy` display, which currently renders regime bonuses as multipliers (`×0.95`) — the real `TrainingBonus` on `CageResponse.TrainingEfficacy` is now absolute per-session ability gain (e.g. `2.0`), not a multiplier, per the redefinition in the API's Task 11 §6.

## 2. Pages & Components Affected

| File | Change |
|---|---|
| `GlassWingClient/Pages/RatDetail.razor` | Replace the Fitness card (Sprint/Agility/Endurance table using `StatFitness.Score`/`TrainingCount`) with an Ability/Potential card; fix training-error display to use unwrapped messages, not raw response bodies |
| `GlassWingClient/Pages/Rats.razor` | Replace `FormatScore(rat.Fitness?.Sprint)` column values with `rat.SprintAbility.ToString("F1")` etc. |
| `GlassWingClient/Pages/Home.razor` | Change `TrainingEfficacy` rendering from `×N.NN` multiplier badges to `+N.N/session` gain badges; `EfficacyClass` coloring logic needs redefining (see §4); add `FreeRoam` to the hardcoded `TrainingRegimes` picker array |
| `GlassWingClient/Pages/Marketplace.razor` | `MarketplaceListingStats` (`Sprint`/`Agility`/`Endurance` nullable doubles) already matches ability semantics reasonably (it's already just raw doubles, not `StatFitness`) — no structural change needed, just confirm the field maps to `SprintAbility` etc. server-side |
| `GlassWingClient/Services/ApiModels.cs` | Remove `TrainingFitness`/`StatFitness`; add ability/potential fields to `RatResponse`; redefine `TrainingBonus` doc comment (fields unchanged, semantics changed) |
| `GlassWingClient/Services/GlassWingApiClient.cs` | No signature changes — `TrainRatAsync` still calls `POST /api/rats/{id}/train`, unchanged route |

## 3. API Integration

### Confirmed current `RatResponse` fields relevant here (from `RatsModule.cs`)

```
SprintAbility: double        // 1..SprintPotential
AgilityAbility: double
EnduranceAbility: double
SprintPotential: int         // 1..100 (0 while a Weening pup — see Task 12)
AgilityPotential: int
EndurancePotential: int
```

### `ApiModels.cs` changes

Remove entirely:
```csharp
public record TrainingFitness(StatFitness? Sprint, StatFitness? Agility, StatFitness? Endurance);
public record StatFitness(double Score, int TrainingCount);
```

Add to `RatResponse` (folding into the same extended record from Tasks 9/10):
```csharp
double SprintAbility = 1,
double AgilityAbility = 1,
double EnduranceAbility = 1,
int SprintPotential = 100,
int AgilityPotential = 100,
int EndurancePotential = 100
```

`TrainingBonus(double Sprint, double Agility, double Endurance)` — **no field rename needed**, but every call site that formats these as `×N.NN` must change to `+N.N`. The record itself doesn't need to change shape, only its display.

`MarketplaceListingStats(double? Sprint, double? Agility, double? Endurance)` in `ApiModels.cs` is already ability-shaped (no `TrainingLevel`/`StatFitness` reference) — leave as-is; just confirm at implementation time that the server maps these from `SprintAbility` etc. (it does, per the pattern used elsewhere).

### No `GlassWingApiClient.cs` signature changes

`TrainRatAsync(id, stat)` still posts `{ stat }` to `POST /api/rats/{id}/train` and gets back a full `RatResponse` — the response now just has ability fields instead of a fitness object. The training button flow in `RatDetail.razor` is otherwise untouched.

## 4. UX Flows

### Rat Detail — Ability card (replaces Fitness card)

Current code (to replace):
```razor
<tr><th>Sprint</th><td>@FormatStat(rat.Fitness?.Sprint)</td></tr>
```
formatted as `"{Score:F2} ({TrainingCount} sessions)"`.

New rendering — show ability against its potential ceiling with a progress bar (there's no existing progress-bar-with-label pattern on this page, but `Home.razor`'s food/water bars are the established convention to reuse):

```razor
<h6 class="card-subtitle mb-3 text-muted">Ability</h6>
@foreach (var (label, ability, potential) in new[] {
    ("Sprint", rat.SprintAbility, rat.SprintPotential),
    ("Agility", rat.AgilityAbility, rat.AgilityPotential),
    ("Endurance", rat.EnduranceAbility, rat.EndurancePotential) })
{
    var pct = potential > 0 ? ability / potential * 100 : 0;
    <div class="d-flex justify-content-between mb-1">
        <span class="small text-muted">@label</span>
        <span class="small">@ability.ToString("F1") / @potential</span>
    </div>
    <div class="progress mb-2" style="height:8px">
        <div class="progress-bar bg-primary" style="width:@(pct.ToString("F0"))%"></div>
    </div>
}
```

This directly shows the player "how much headroom is left" (ability vs. potential ceiling), which is the entire point of the Task 11/12 stat model — a flat number alone loses that context.

### Home cage card — TrainingEfficacy badges

Current:
```razor
<span class="@EfficacyClass(eff.Sprint)">Sprint ×@eff.Sprint.ToString("F2")</span>
```

New — values are absolute gains, not multipliers, so `×` is actively misleading now:
```razor
<span class="@EfficacyClass(eff.Sprint)">Sprint +@eff.Sprint.ToString("F1")</span>
```

`EfficacyClass` currently colors `> 1.0` green / `< 1.0` amber / else muted — that threshold was tuned for a multiplier centered on 1.0 and is meaningless for absolute gains (a focused stat is `2.0`, secondary `1.0`, untrained `0`, per `TrainingRegimeCatalogue.cs`). Replace with a simple three-tier scale matching the catalogue's actual value bands:

```csharp
static string EfficacyClass(double v) =>
    v >= 1.5 => "text-success",   // focused stat
    v > 0    => "text-muted",     // secondary/cross-training stat
    _        => "text-muted opacity-50"; // untrained for this regime
```

### Regime picker — add Free Roam

`Home.razor`'s hardcoded `TrainingRegimes` array is missing the sixth regime the backend already serves (`TrainingRegimeCatalogue.FreeRoam`, id `free-roam`). Add it now so the picker matches the server catalogue exactly:

```csharp
static readonly (string Id, string Name)[] TrainingRegimes =
[
    ("track-runs",         "Track Runs"),
    ("furniture-scramble", "Furniture Scramble"),
    ("dash-drills",        "Dash Drills"),
    ("tunnel-rush",        "Tunnel Rush"),
    ("endurance-circuit",  "Endurance Circuit"),
    ("free-roam",          "Free Roam"),
];
```

Selecting it and setting the regime works today via the existing `SetRegimeAsync` — no API change needed for the picker itself. Free Roam's *eligibility rules* (Infant+ only, not Protective mothers) and its distinct "flat gain, clears specific stressors" behavior are Task 12 concerns — see `Task12_Client_Design.md` §4 for the cage-level training-session eligibility UI (the `POST /api/home/cages/{cageId}/train` / `GET .../training` endpoints, which are a separate, cage-level session flow not yet wired into `Home.razor` at all — today's regime picker only sets which regime is assigned, it doesn't start a session). Recommend that whoever picks up Task 12's client work also wires the "Start Training Session" action, since eligibility filtering only makes sense once that button exists.

Consider hardcoding this array altogether is now a minor liability — since the backend already exposes `GET /api/game/training-regimes` (confirmed in `GameModule.cs`), a lower-risk long-term fix is to fetch the regime list instead of hand-copying it, so a seventh regime doesn't require another client patch. Not required for Task 11, but flagged since this task is already touching the array.

## 5. Client-Side Validation & Guards

No new guards specific to this task — the ability/potential model doesn't add new blocking states (Weening/nursing/retired guards on `POST /api/rats/{id}/train` belong to Tasks 9/12). **Updated 2026-07-06:** a 422 from `TrainAsync` (thrown as `InvalidOperationException`, e.g. "Rat 'X' is resting — training available at ...") used to come back as a `ProblemDetails` JSON body that `RatDetail.razor`'s naive `ReadAsStringAsync()` would show verbatim as a JSON blob — this is resolved now that every error response in the API uniformly uses `{ error, reason }` (`BackendAuditFindings.md` C1 in the API repo). Reuse the `ReadConflictAsync` helper from `Task9_Client_Design.md` §3 as-is; no `ProblemDetails`-specific unwrapping is needed.

## 6. State Management

None new. This is a pure data-shape and display change.

## 7. Notifications

Not applicable — no notification list changes in this task.

## 8. Open Questions / Deferred

- **Genetic-modifier / diet-multiplier breakdown is not shown.** The Ability card shows the resulting number, not *why* a session gained what it gained (diet quality × genetic modifier × regime base). A "why did this only gain +0.6?" tooltip is a nice-to-have, deferred — would need the server to expose the modifier components, which it doesn't today.
- **Fetching `GET /api/game/training-regimes` instead of hardcoding** — flagged above as a low-risk improvement, not required for this task; deferred to whoever next touches `Home.razor`'s regime picker.
- **Cage-level training-session flow** (`POST /api/home/cages/{cageId}/train`, `GET /api/home/cages/{cageId}/training`) is fully implemented server-side but has **no client entry point at all** today — `Home.razor` only sets a regime, it never starts a session. Wiring that up is arguably a prerequisite for Task 12's Free Roam / eligibility UI to mean anything, but it predates this task and is explicitly deferred to `Task12_Client_Design.md`.
