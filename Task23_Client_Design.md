# Task 23 (Client) — Secret Rat Easter Egg: Client Design Document

## Backend Dependency

**Refreshed 2026-07-06: the backend has since shipped, mechanism and real content both.** Task 23 was implemented 2026-07-04 — the mechanism first (commit `e106622`), then all 21 real name/genotype/bio entries in the same session (bundled into commit `484cfa1`). `secretMessage` is live on the rename response exactly as this doc specifies. **Also resolved:** the timing side-channel flagged below (§ "Open Questions") as "backend scope, not fully closable client-side" — the backend now runs the `GetActiveEasterEggHolderAsync` lookup unconditionally on *every* rename (a dummy probe id when there's no real match), so a secret-triggering rename and an ordinary one now take an identical code path/round-trip profile. Nothing changes for the client — this was always framed as the backend's fix to make, and it's been made.

## Overview

When a player renames a rat to one of 21 secret names (an homage to the developer's real pet rats), the backend performs a full, real genotype transformation and returns an optional `secretMessage` string on the rename response. The client's entire job is to render that string as an unremarkable in-fiction toast — nothing more.

## Design Principle: Secrecy Is Load-Bearing (Not a Footnote)

This is the primary constraint on the whole feature, not a detail to satisfy after the "real" UX work is done. The API design is explicit: **no endpoint, error, or response shape may ever reveal that a name is "special," reserved, or currently claimed** — the same principle applies to the client, in full:

- The client must never display, log, or hint that names can be special. No tooltip suggesting "try naming your rat something interesting," no achievement, no "special names" list, no changelog entry, no easter-egg-hunt UI of any kind.
- The client must render a successful rename **identically** whether or not `secretMessage` is present, except for the one additional toast. Same loading indicator, same button states, same error handling, same timing of state transitions.
- The client must not introduce **any new observable difference** — visual, temporal, or structural — between an ordinary rename, a rename that happens to collide with an already-claimed secret name (silently falls through to an ordinary rename server-side), and a rename that triggers a fresh transformation. All three are the same request/response cycle from the client's point of view; only the presence of one optional string field distinguishes the third case, and even that must be revealed only as harmless flavor text.

If the client leaks this constraint through inconsistent UI (a spinner that lingers 200ms longer on a transformation, a console log emitted only on `secretMessage`, an analytics event fired only on match), players can datamine the entire secret list by scripting rename attempts and watching for the tell — exactly the outcome the backend design goes out of its way to prevent. A client implementation that gets the toast copy right but adds *any* differential behavior elsewhere defeats the feature.

---

## 1. API Integration

### `RatResponse` — one new optional field

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
    string? SecretMessage = null);   // NEW — present only when a transformation was just claimed
```

- Trailing optional parameter with a default, so existing call sites (`GetRatAsync`, `TrainRatAsync`, etc., all of which also deserialize into `RatResponse`) keep compiling unchanged and simply see `null` for `SecretMessage` on every response except the one rename call that triggers a claim.
- Per the API design, the field is **absent from the JSON**, not `null`-with-a-tell, on ordinary renames (`JsonIgnoreCondition.WhenWritingNull` server-side). `System.Text.Json` deserializes a missing property to the C# default (`null`) with no client-side change needed to handle that — no special-casing required.
- No change needed to `GlassWingApiClient.RenameRatAsync` (`GlassWingClient/Services/GlassWingApiClient.cs`) — it already does `await resp.Content.ReadFromJsonAsync<RatResponse>(JsonOpts)` and returns the whole record; the new field rides along for free.

### No new endpoint, no new error handling

Section 5 of the API design requires that a rename colliding with an already-claimed secret name return the exact same success shape as an ordinary rename. The client's existing `RenameRatAsync` (returns `RatResponse?`, `null` only on a non-success HTTP status) needs **zero changes** to preserve this — there is no new error path to add, and none should ever be added.

---

## 2. UX Flow

`RatDetail.razor` (route `/rats/{Id}`) already has an inline rename flow:

```
ConfirmRenameAsync():
    var updated = await Api.RenameRatAsync(Id, newName);
    if (updated is not null) rat = updated;
    renaming = false;
```

Extend this by one conditional after the existing assignment:

```
if (updated?.SecretMessage is { } msg)
    secretToastMessage = msg;
```

Rendered using the same fixed-position dismissible-alert pattern already established in `Marketplace.razor` (`position-fixed bottom-0 end-0 p-3`, `alert alert-success alert-dismissible shadow`, manual dismiss button) — no new toast infrastructure, just a second instance of the existing local-state toast idiom, scoped to `RatDetail.razor`.

Copy treatment:

- The toast shows **only** the server-supplied `secretMessage` string verbatim (e.g. *"Something about Whiskers feels different..."*). No prefix, no title, no icon that implies "achievement" or "discovery" (no trophy/star iconography — a plain `alert-success` box, same visual weight as "Trained Sprint!" or "Listing cancelled.").
- No client-authored copy is added around it. Do not wrap it in framing like "Easter Egg Found!", "Secret Unlocked", or any meta-commentary — the backend has already composed the in-fiction line; the client's only job is to display it plainly, the same way it displays any other server-driven success message.
- The rat's stat cards (Fitness/Health/Tricks/Appearance) already re-render from the returned `RatResponse` after rename via the existing `rat = updated` assignment — the transformed genotype's derived phenotype/stats simply appear as part of the normal post-rename refresh. No special "reveal" animation, no highlighting of what changed.
- On a **revert** (player renames away from a claimed secret name), the backend sends no `secretMessage` at all — the client shows nothing beyond the ordinary rename completing. This requires no extra code; the `if (updated?.SecretMessage is { } msg)` check simply doesn't fire.

---

## 3. Secrecy Requirements for the Client (Checklist)

- [ ] **No hint UI anywhere.** No tooltip, placeholder text, help copy, FAQ entry, or onboarding tip suggesting names might be special.
- [ ] **No client-side name validation against the secret list.** The client must never possess, bundle, or fetch the 21-entry catalogue in any form (not even hashed/obfuscated) — there is nothing to validate against client-side, and building such a check would itself require shipping the secret data to every browser. Rename input validation stays exactly as minimal as it is today (non-empty check only).
- [ ] **No differential loading state.** The rename button's disabled/loading treatment (currently: none — `ConfirmRenameAsync` is a single unguarded `await`) must not vary based on outcome. Do not add a spinner, a longer "Saving..." state, or any client-side delay that could correlate with whether a transformation occurred server-side. If a "Saving..." indicator is added in the future for UX reasons, it must be driven purely by "request in flight," never keyed off the response content.
- [ ] **No differential error handling.** `RenameRatAsync` returning `null` (an HTTP error) already gets no distinct treatment in `RatDetail.razor` today (the code silently closes the rename box). If error surfacing is improved later, the error path must remain identical regardless of whether the attempted name happens to be a claimed or unclaimed secret name — both are indistinguishable "ordinary rename" cases from the client's perspective and must stay that way.
- [ ] **No analytics/telemetry event tied to `secretMessage`.** The client currently has no analytics/telemetry pipeline (none found in the repo as of this writing). If one is added in the future, an event that fires only when `secretMessage` is present (e.g. `"rat_renamed"` vs. a hypothetical `"easter_egg_triggered"`) would be a durable, queryable leak vector — worse than a UI tell, since it could be datamined from a shared analytics dashboard rather than requiring gameplay observation. Flag this explicitly for whoever adds analytics later: **do not create an event, property, or dimension that is conditioned on `secretMessage`'s presence.** If rename events are tracked at all, they must be tracked identically for every rename.
- [ ] **No special animation/effect gated on transformation.** Do not add a sparkle effect, sound cue, or distinct color treatment that only plays when `secretMessage` is present — this is a purely cosmetic instance of the same rule as the toast copy: the *presence of extra visual behavior itself* is the tell, independent of what that behavior says.
- [ ] **Exactly one network request per rename attempt, always.** Do not add a follow-up call (e.g., to fetch bio/lore text) that is only issued when `secretMessage` is present — a conditional second request is a network-observable tell (extra request count/timing) even if its response body is never rendered. If bio display is ever wanted, the bio must ride in the same rename response, not a separate fetch (see Open Questions).

---

## 4. Open Questions / Deferred

- **Bio display.** `EasterEggEntry.Bio` exists server-side but the API design only surfaces `secretMessage`, not the bio text, on the rename response. If a future task wants to show the bio somewhere (e.g. on the rat detail page permanently once claimed), the API would need to add a bio field to `RatResponse` itself (always present when `EasterEggSecretId` is set) so the client isn't tempted to make a conditional follow-up request. Out of scope here.
- ~~**Timing side-channels are a backend concern, not fully closable client-side.**~~ **Resolved on the backend** — see refresh note at the top. The client-side guidance in the checklist above (no added delay, no follow-up calls) is still the right practice regardless, so nothing to change there.
- **Toast persistence across navigation.** Like the existing `Marketplace.razor` toast, `secretToastMessage` is page-local state — navigating away from `/rats/{Id}` before dismissing it simply loses it. Consistent with existing patterns elsewhere in the client; no special persistence needed for a one-line flavor message.
- **Multiple toasts stacking.** Not handled — if this page ever needs more than one concurrent toast (e.g. combined with a future training-result toast), a shared toast/notification service would be worth introducing, but that's a cross-cutting refactor well beyond this task's scope.
