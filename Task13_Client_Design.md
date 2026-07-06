# Task 13 (Client) — Rat Ancestry / Lineage: Design Document

## 1. Overview

The API's Task 13 (`Task13_Design.md`) is fully implemented: a 4-generation ancestry tree, populated at birth and enriched to an immutable snapshot at point-of-sale/retirement/surrender, is returned on `GET /api/rats/{id}` (detail only — not the list, confirmed by `RatsModule.GetRatAsync` passing `includeAncestry: true` only in the single-rat handler). This is the last of the five tasks and depends on `MotherId`/`FatherId` from `Task10_Client_Design.md`/`Task12_Client_Design.md` already being on `RatResponse`, and on the life-stage badge conventions from `Task12_Client_Design.md` for rendering pup nodes consistently.

> **Refreshed 2026-07-06.** `GET /api/rats/{id}/public` now exists (`BackendAuditFindings.md` C2 in the API repo) — a genuinely public, non-owner-viewable projection including `Ancestry`, so following a "live" lightweight ancestor link no longer dead-ends at a 404 for a rat you don't own (see §4). This doesn't change anything else in this doc — the ancestry data itself, and how it's rendered, is unaffected.

This adds one new UI surface: a lineage tree section on `RatDetail.razor`. It's read-only and purely additive — no new write endpoints.

## 2. Pages & Components Affected

| File | Change |
|---|---|
| `GlassWingClient/Pages/RatDetail.razor` | New "Lineage" section below Appearance; new `AncestryNodeCard` rendering (recursive, 4 levels) |
| `GlassWingClient/Services/ApiModels.cs` | Add `Ancestry`, `AncestorNode`, `CoatSnapshot`; add `Ancestry` field to `RatResponse` |
| `GlassWingClient/Services/GlassWingApiClient.cs` | No new methods — `GetRatAsync` already returns the detail response; ancestry just rides along once the field is added |

No new route. Given the tree can be genuinely large (up to 2×(2^4-1) = 30 nodes at full depth), rendering it as a section within the existing page (not a modal/new page) matches the API's framing of it as "detail response" data, and matches TASKS.md's backlog note ("Lineage tab/section on rat detail page").

## 3. API Integration

### Confirmed shape (from `Ancestry.cs` domain model — server serializes this directly, PascalCase-insensitive matches the client's existing `JsonSerializerOptions`)

```
Ancestry { Father: AncestorNode?, Mother: AncestorNode? }

AncestorNode {
  RatId: string, Name: string, Sex: "Male"|"Female", IsEnriched: bool,
  // null when IsEnriched == false:
  DateOfBirth: DateTime?, RetiredAt: DateTime?, RetirementReason: string?,
  SprintAbility: double?, AgilityAbility: double?, EnduranceAbility: double?,
  SprintPotential: int?, AgilityPotential: int?, EndurancePotential: int?,
  Coat: CoatSnapshot?,
  Father: AncestorNode?, Mother: AncestorNode?   // recursive, bounded to 4 generations total
}

CoatSnapshot {
  Colour: string, Pattern: string, HoodQuality: string?, SilveringIntensity: string,
  HasBlaze: bool, IsRoan: bool, IsDownunder: bool
}
```

Note the server field is `Coat` (per `AncestorNode.cs`: `public CoatSnapshot? Coat { get; set; }`), not `CoatSnapshot` as the original API design doc's JSON example named it — match the real field name.

### `ApiModels.cs` additions

```csharp
public record Ancestry(AncestorNode? Father, AncestorNode? Mother);

public record AncestorNode(
    string RatId,
    string Name,
    string Sex,
    bool IsEnriched,
    DateTime? DateOfBirth,
    DateTime? RetiredAt,
    string? RetirementReason,
    double? SprintAbility,
    double? AgilityAbility,
    double? EnduranceAbility,
    int? SprintPotential,
    int? AgilityPotential,
    int? EndurancePotential,
    CoatSnapshot? Coat,
    AncestorNode? Father,
    AncestorNode? Mother);

public record CoatSnapshot(
    string Colour,
    string Pattern,
    string? HoodQuality,
    string SilveringIntensity,
    bool HasBlaze,
    bool IsRoan,
    bool IsDownunder);
```

Add `Ancestry? Ancestry = null` to the shared `RatResponse` record. Empty ancestry comes back as `{ "father": null, "mother": null }` (per the API doc) — an all-null `Ancestry` object, not a null `Ancestry` field, so the client checks `rat.Ancestry is { Father: null, Mother: null }` (or just `is not { Father: not null } and not { Mother: not null }`) to decide whether to render the section at all.

### `GET /api/rats/{id}/public` (added 2026-07-06 — see refresh note above)

A separate, smaller response type — not `RatResponse` with fields nulled out, so the client can tell "genuinely empty" apart from "hidden because you're not the owner." Always includes `Ancestry` (the owner endpoint only includes it via `includeAncestry: true`, which the public endpoint always sets):

```csharp
public record PublicRatResponse(
    string Id, string Name, string OwnerId, DateTime DateOfBirth, DateTime CreatedAt,
    string? FatherId, string? MotherId, int Generation, string Sex, string LifeStage,
    RatPhenotype Phenotype,
    double SprintAbility, double AgilityAbility, double EnduranceAbility,
    int SprintPotential, int AgilityPotential, int EndurancePotential,
    IReadOnlyList<string> Traits, IReadOnlyList<string> TricksLearned,
    IReadOnlyList<CompetitionResult> CompetitionHistory,
    DateTime RetiresAt, bool IsRetired, DateTime? RetiredAt, string? RetirementReason,
    Ancestry? Ancestry, string? ActiveCosmeticId);
```

No `HealthState`, `WeightGrams`/`TargetWeightGrams`, `CurrentTrickTraining`, `Bonding`, `PlaySession`, `TrainingCooldownUntil`, or breeding fields (`IsNursing`/`IsProtective`/`LitterCount`/`IsPregnant`/`DueAt`) — those are the owner's active-management state and aren't exposed here.

```csharp
public async Task<PublicRatResponse?> GetPublicRatAsync(string ratId)
{
    var resp = await http.GetAsync($"/api/rats/{ratId}/public");
    return resp.IsSuccessStatusCode
        ? await resp.Content.ReadFromJsonAsync<PublicRatResponse>(JsonOpts)
        : null;
}
```

No ownership check on this endpoint — 404 only if the rat doesn't exist at all.

## 4. UX Flows

### Lineage section — top level

Below the Appearance card on `RatDetail.razor`:

```razor
@if (rat.Ancestry is { Father: not null } or { Mother: not null })
{
    <div class="card mb-4">
        <div class="card-body">
            <h6 class="card-subtitle mb-3 text-muted">Lineage</h6>
            <div class="d-flex gap-4">
                <AncestorColumn Label="Father" Node="rat.Ancestry.Father" Depth="1" />
                <AncestorColumn Label="Mother" Node="rat.Ancestry.Mother" Depth="1" />
            </div>
        </div>
    </div>
}
```

If both are null (starter rat, agency-adopted rat), render nothing — no empty "Lineage" section, per TASKS.md's backlog note ("Starter and adopted rats: lineage section is blank (not rendered)").

### Node rendering — two states per node

Each node card shows: name, sex icon, and coat swatch/description. Below that, branches into two states:

**Lightweight (`IsEnriched == false`)** — the ancestor is still a live, potentially player-owned rat:
```
Cosmo ♂
[live — click to view]
```
Rendered as a link `<a href="/rats/{ratId}">` unconditionally — the destination page now handles both ownership cases itself, so the link never needs to guess. `RatDetail.razor`'s load logic becomes: call `GetRatAsync(ratId)` first (the full owner view); if that 404s (not owned, or genuinely doesn't exist), fall back to `GetPublicRatAsync(ratId)` (see §3); if *that* also 404s, the rat truly doesn't exist — show "Rat not found." When the page is showing the public projection, hide every owner-only action (Rename, Train, List on Marketplace, Retire, cosmetic controls) and any field the public response doesn't carry (health, weight, training/bonding/play-session state, breeding status — see §3) rather than showing blank/zeroed values for them. This same fallback pattern is shared by whichever other page links into `RatDetail.razor` for a possibly-non-owned rat (see `Task17_Client_Design.md` §6, which hits the identical question from the leaderboard side) — implement it once, in `RatDetail.razor` itself, not per-caller.

**Enriched (`IsEnriched == true`)** — permanently frozen snapshot, shown entirely inline, no navigation:
```
Pip ♀  [Retired — Natural age]
Born 12 Mar 2025
Sprint 72.4/85  Agility 68.1/78  Endurance 80.0/90
Blue Berkshire
```
No link — per the API doc, "the rat may no longer be accessible to the current player," and since the snapshot is self-contained there's nothing more to fetch even if it were.

**Null node** — render an empty placeholder slot, not nothing, so the tree shape stays legible at a glance:
```
— unknown —
```

### Depth & navigation rules

The tree is exactly 4 generations deep (rat → parents → grandparents → great-grandparents → great-great-grandparents, per the API doc's bounding at population time). Render the first two generations (parents, grandparents) expanded by default; collapse generations 3–4 behind a "Show more ancestors" toggle per branch, since a fully expanded 4-deep binary tree is visually heavy in a page section rather than a dedicated page. This is a UI simplification, not an API constraint — all 4 generations are already present in the single response payload.

```
[Father: Cosmo] ▸ [Grandfather: Rex] [Grandmother: Nib]
                     ▾ show 2 more ancestors
```

## 5. Client-Side Validation & Guards

There are no write actions in this feature — it's entirely read-only rendering of data already present on the rat the player is already allowed to view. The only "guard" is the owner/public fallback in `RatDetail.razor` described in §4, needed when navigating to a lightweight ancestor node the player no longer owns.

## 6. State Management

None. The ancestry tree is part of the single `GetRatAsync` response already loaded by the page; no caching, polling, or additional fetches are introduced (aside from the ordinary navigation to another rat's detail page when following a live lightweight-node link, which is just the existing page-load flow).

## 7. Notifications

Not applicable — Task 13 has no notification list in the API design.

## 8. Open Questions / Deferred

- **Coat swatch rendering** — `CoatSnapshot` has the same fields as the live `CoatPhenotype` used elsewhere on the page (`Colour`/`Pattern`/`HoodQuality`/`HasBlaze`/`IsRoan`/`IsDownunder`, plus `SilveringIntensity` where the live model uses `Silvering`). Recommend reusing `RatDetail.razor`'s existing `CoatDescription`/marking-badge helpers with a small adapter rather than writing new formatting logic — a minor implementation detail, not a design fork, but flagged since the field names don't line up 1:1 (`Silvering` vs. `SilveringIntensity`) and a naive shared helper will need a light wrapper.
- **Achievements in the enriched snapshot** — TASKS.md's backlog note mentions showing "achievements" on sold/marketplace ancestor nodes ("see achievements feature TBD"). There is no achievements field on `AncestorNode` today and no achievements system yet — explicitly out of scope until that feature exists.
- **Depth-collapsing UX** (default-expand 2 generations, collapse 3–4) is a judgment call in this doc, not specified by the API design — open to revision once someone sees a real 4-generation tree rendered; could also be a simple always-expanded tree if testing shows it isn't too heavy.
- **Cross-reference:** node badges for `LifeStage`/`IsNursing` are intentionally **not** shown on ancestry nodes — ancestors are either fully-grown live rats or permanently frozen snapshots; the accruing-potential mid-Weening state from `Task12_Client_Design.md` doesn't apply here since a rat can only become an ancestor by having already bred (implying `Adult`).
