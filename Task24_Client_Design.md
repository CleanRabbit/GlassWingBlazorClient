# Task 24 (Client) — Expanded Personality Traits: Client Design Document

## Backend Dependency

**Refreshed 2026-07-06: the backend has since shipped — and shipped more than this doc covers.** Task 24 was implemented 2026-07-04 (1355 tests passing at the time, commit `ac1b653`) — the six traits below (`Bold`/`Shy`/`Curious`/`Lazy`/`Playful`/`Affectionate`) are live on `RatResponse.Traits` exactly as designed, zero API reshaping, confirmed. **Important scope note:** two further waves of personality traits shipped after this doc was written — Task 25 (9 more: `Foodie`/`Gregarious`/`Independent`/`Popcorn`/`Sleepy`/`Homebody`/`Driven`/`Snappy`/`DevotedMother`, commit `484cfa1`) and Task 26 (`Troublemaker`, bundled with the same commit) — bringing the real total to 18 trait values in `RatTrait`, not the 6 this document was scoped around. There is no client design doc for Tasks 25/26 yet. Whatever UI this doc's design renders generically (e.g. a trait badge list that just enumerates `rat.Traits`) should already handle the extra values without modification; anything that hardcodes the 6-trait list here will need extending. **Refreshed again 2026-07-06:** all 18 traits sat mechanically neutral until this date — 15 of them (all but `Troublemaker`, `Fussy`, `Overweight`) now carry a real (if intentionally arbitrary/placeholder) gameplay effect; see Section 4's refresh note below.

## Overview

Six new personality trait values are added to the backend's `RatTrait` enum: three fixed/genetic (`Bold`, `Shy`, `Curious` — computed live from genotype, read-only, never stored) and three environmental (`Lazy`, `Playful`, `Affectionate` — stored, triggered/recovered over time, following the same shape as the existing `Fussy` trait). Per the source doc, all six reuse the existing `RatResponse.Traits` collection with **zero API reshaping** — the client work is purely a display extension, not new data plumbing.

The fixed-vs-environmental distinction (how a trait gets onto the list) is entirely a backend concern. The client does not compute, cache, or distinguish trait provenance — it receives one flat collection and displays whatever is in it. This document does not treat the two families differently anywhere in the UI.

---

## Key Finding: There Is No Existing Trait Display to Extend

Before writing this doc, the current client was checked directly (not assumed):

- `GlassWingClient/Services/ApiModels.cs` — `RatResponse` currently has **no `Traits` field at all** (checked: `Fitness`, `HealthState`, `Phenotype`, `TricksLearned`, nothing else).
- `GlassWingClient/Pages/RatDetail.razor` — the rat detail page (`/rats/{Id}`) has cards for Fitness, Health (vitality badge + active illnesses), Tricks, and Appearance (with a "Markings" badge row for coat features like Blaze/Roan/Silvering). **There is no Fussy or Overweight badge, row, or mention anywhere on the page.** The client backlog's "Fussy eating & appetite UI" item is indeed still unbuilt, confirmed by direct inspection, not by trusting the backlog note.

Consequence: this is **net-new UI**, not an extension of prior trait rendering. Given that, the design below covers the *whole* `Traits` collection as it exists once this task ships — all 8 values (`Fussy`, `Overweight` already backend-side, plus the 6 new ones) — rather than special-casing only the 6 new values while continuing to ignore the 2 pre-existing ones. Building bespoke display logic for 6 enum values while leaving 2 sibling values in the same collection permanently unrendered would be an arbitrary and confusing scope cut; the marginal cost of covering all 8 in one generic component is negligible. This keeps the change minimal in the sense the task intends — one small badge-list component, not new architecture — while not leaving an inconsistent gap.

---

## 1. API Integration

### `RatResponse` — one new field

Add to `GlassWingClient/Services/ApiModels.cs`:

```csharp
public record RatResponse(
    string Id,
    string Name,
    string OwnerId,
    TrainingFitness? Fitness,
    HealthState? HealthState,
    RatPhenotype? Phenotype,
    string[]? TricksLearned,
    string? SecretMessage = null,   // Task 23, if shipped first — see Task23_Client_Design.md
    string[]? Traits = null);       // NEW — e.g. ["Fussy", "Bold", "Playful"]
```

- `string[]?`, matching the existing convention for other server-side enum collections surfaced as strings (`TricksLearned` is `string[]?`; `HealthState.Vitality` is a bare `string?`). No client-side enum type is introduced — the client treats trait names as opaque labels looked up in a small display table (Section 4).
- No new endpoint. `GetRatAsync` (`GlassWingClient/Services/GlassWingApiClient.cs`) already returns the full `RatResponse`; `Traits` rides along once the backend adds it, same as `SecretMessage` in Task 23.
- If Task 23 ships first, both optional fields coexist on the same record with no interaction — confirmed no overlap in this design.

---

## 2. Pages & Components

### `RatDetail.razor` — new "Personality" section

Add a small card, positioned alongside the existing Fitness / Health / Tricks row (or immediately below it — either works; suggest a fourth column if screen width allows, otherwise a new full-width row under the existing three-card grid, since a 4-up card row gets cramped at the current `col-md-4` sizing). Content:

```
@if (rat.Traits is { Length: > 0 })
{
    <div class="d-flex flex-wrap gap-2">
        @foreach (var trait in rat.Traits)
        {
            <span class="badge @TraitBadgeClass(trait)" title="@TraitDescription(trait)">@trait</span>
        }
    </div>
}
else
{
    <span class="text-muted small">No notable traits</span>
}
```

This directly mirrors the existing "Markings" badge row pattern already on the same page (Appearance card, `coat.HasBlaze`/`IsRoan`/etc. rendered as `<span class="badge bg-secondary">`) — same visual idiom, same `title` attribute convention used elsewhere in the client for hover detail (`Home.razor`, `ShopCategory.razor` both use plain `title="..."` rather than a JS tooltip plugin; no Bootstrap tooltip JS initialization exists in this codebase, so this design does not introduce one).

`TraitBadgeClass` and `TraitDescription` are two small static lookup helpers (`@code` block, same style as the existing `VitalityBadgeClass`/`CoatDescription` helpers on this page) keyed on the trait string. No new component or shared library needed — this is page-local, consistent with how `RatDetail.razor` already handles small per-field formatting inline.

### No changes anywhere else

Trait values don't appear on `Rats.razor` (list view), `Home.razor`, or `Marketplace.razor` listings today (those show name/stats/appearance summaries only) — out of scope to add them there; this task is the rat detail page only, per the source doc's framing as a display-only task.

---

## 3. API Integration Confirmation

No new endpoint. No reshaping of `GET /api/rats/{id}` (or whatever underlying call `GetRatAsync` wraps) is required — the source doc is explicit that `RatResponse.Traits` needs zero API reshaping, and the client change described above needs nothing beyond the one new field in Section 1. This is the entirety of the client's data-layer work for this task.

---

## 4. UX Flow — Badge Treatment

Each trait gets a label (the raw enum string, no client-side renaming needed — `Bold`, `Shy`, etc. are already player-facing words) and a `title` tooltip with a short, purely descriptive line. Badge color is uniform (`bg-secondary`, matching the existing Markings badges) rather than color-coded per trait — there's no positive/negative axis established yet (see below), so a single neutral color avoids implying value judgment prematurely.

Suggested tooltip copy:

| Trait | Tooltip copy |
|---|---|
| `Fussy` | "Picky about food quality." |
| `Overweight` | "Carrying extra weight." |
| `Bold` | "Fearless and quick to try new things. Slightly boosts event scores. Fixed from birth." |
| `Shy` | "Cautious and easily startled. Slightly hurts event scores. Fixed from birth." |
| `Curious` | "Investigates its surroundings eagerly. Slightly speeds up training. Fixed from birth." |
| `Lazy` | "Hasn't been engaged in play recently. Slightly slows training." |
| `Playful` | "Consistently engaged in play lately. Slightly speeds up training." |
| `Affectionate` | "Deeply bonded with its owner. Slightly speeds up further bonding gains." |

**Refreshed 2026-07-06: mechanical effects have since shipped — this section's original "no implied mechanical effect" framing no longer applies.** All 18 `RatTrait` values (this doc's 6, the 9 added by Task 25, `Troublemaker` from Task 26, plus the pre-existing `Fussy`/`Overweight`) were balanced in one pass, except `Troublemaker` and `Fussy`/`Overweight`: each of the other 15 got a single flavor-matched modifier at an arbitrary 3-10% of its hook's reference scale (`PersonalityTraitEffectCatalogue`, backend-only — explicitly a placeholder magnitude pending real player data, not a tuned value; expect these numbers to move once that data exists). `Troublemaker` stays neutral in the catalogue since its real effect is already the Mischief Incident mechanic (Task26_Design.md §6); `Fussy`/`Overweight` predate this catalogue and were never part of it. The tooltip copy above now names the general direction and rough size ("slightly boosts/slows/hurts") without quoting exact percentages, since those are tuning-in-flux implementation details, not something worth committing to player-facing copy yet — if the numbers move next balancing pass, the copy shouldn't need to. There is still no dedicated client design doc for Task 25's 9 traits (`Foodie`/`Gregarious`/`Independent`/`Popcorn`/`Sleepy`/`Homebody`/`Driven`/`Snappy`/`DevotedMother`) or Task 26's `Troublemaker` — whoever writes their tooltip copy should apply the same "direction and rough size, no exact numbers" treatment for the 8 that now have a real effect.

No distinction is drawn in the UI between "fixed" and "environmental" traits (no separate section, no icon indicating "this one can change") — the source doc confirms this split is a backend implementation detail with no client-facing requirement, and inventing one would be scope creep not asked for.

---

## 5. Open Questions / Deferred

- **Badge card placement.** Whether Personality gets its own card or is folded into the existing Health card (Fussy/Overweight are arguably health-adjacent) is a layout call left to implementation — either is consistent with this doc; a single flat badge list works in both.
- **Mutual-exclusivity display (Bold/Shy, Playful/Lazy).** The backend guarantees these pairs can't co-occur (Section 1/2 of the API design). The client doesn't need to enforce or specially render this — it's structurally impossible in the data, so no client-side guard is needed.
- **Empty/pre-launch state.** Until the backend ships, `rat.Traits` will simply be absent (`null`) for all rats, and the "No notable traits" fallback (or omitting the section entirely when `Traits` is null/empty) handles that gracefully with no feature flag needed.
- ~~**Revisiting tooltip copy once mechanics land.**~~ **Resolved 2026-07-06** — see the refresh note in Section 4.
- **Icons instead of/alongside text badges.** Source doc mentions "icons, etc." as client-side scope but doesn't mandate them. This design keeps to text badges (matching the existing Markings pattern) for minimalism; adding per-trait iconography is a nice-to-have, not required for this pass.
