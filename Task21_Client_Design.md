# Task 21 (Client) — Veterinary Care & Medication: Design Document

## 0. Backend Dependency — READ FIRST

**Refreshed 2026-07-06: the backend has since shipped.** Task 21 was implemented 2026-07-04 (1291 tests passing at the time, commit `e0c1e17`) — the diagnosis-gated `ActiveIllness` schema, vet visit/treat/dose endpoints, and the illness-identity-hidden-until-diagnosed model are all live. `TreatmentRecoveryHours` — flagged below (§8) as "currently isn't in the diagnosis response shape" — has since been added to both the diagnosis and active-illness responses (see `BackendAuditFindings.md`/`IssuesResolutionDesigns.md` C11 in the API repo), so the OneOff recovery countdown this doc deferred is now buildable.

This document was originally a forward-looking client spec written against the backend design doc's proposed contracts, before the backend shipped. It has not been fully re-verified field-by-field against the real implementation as part of this refresh pass — several field names below were inferred from design-doc prose rather than a formal contract at the time of writing. Re-verify against the live API/`openapi.json` before wiring up.

---

## 1. Overview

Task 21 adds a vet-care loop to the rat detail page (`/rats/{id}`, `GlassWingClient/Pages/RatDetail.razor`). Today that page's Health card (lines 54–100) shows a vitality badge, weight/length, and a flat list of illnesses with only an id, start date, and a treated/untreated badge — there is no diagnosis concept, no treatment action, and no course-dosing UI, because none of this existed server-side before Task 21.

The client-side loop mirrors the backend design exactly:
1. An illness is **undiagnosed** by default — the rat shows `Ill`, and the illness card shows only "since when," nothing else.
2. Player clicks **"Take to vet"** — one flat-fee visit diagnoses *every* currently-undiagnosed illness on the rat at once.
3. Diagnosed cards reveal name/category/treatment type/cost. Player clicks **Treat** to pay and start treatment.
4. One-off treatments then just show a "recovering" state with no further action. Course treatments show dose progress and a **"Give today's dose"** button, enabled once per calendar day (`canDoseToday`).

This is purely additive to the existing Health card — no other page changes required for Task 21 alone (Task 22 below shares the same card real estate; see that document's combined-card section).

---

## 2. Pages & Components

### 2.1 `RatDetail.razor` — Health card rework

Replace the current `@foreach (var illness in h.ActiveIllnesses)` block (lines 73–87) with three states per illness:

| State | Condition | Shown |
|---|---|---|
| Undiagnosed | `!illness.IsDiagnosed` | "Unknown illness" placeholder, `StartedAt`, no name/category, no action (vet visit is a rat-level action, not per-card — see 2.2) |
| Diagnosed, untreated | `IsDiagnosed && !TreatmentApplied` | Name, category badge, `StartedAt`, treatment cost/type, **Treat** button (or, for `TreatmentType == "None"`, the `Recommendation` string instead of a button) |
| Diagnosed, treating | `IsDiagnosed && TreatmentApplied` | Name, category badge, "Treatment applied" badge; **OneOff** → static "Recovering…" (no player action, resolves server-side); **Course** → dose progress bar (`DosesAdministered`/`DoseCount`) + **Give today's dose** button, `disabled="@(!illness.CanDoseToday)"` |

A rat-level banner sits above the illness list, not per-card:

```razor
@if (h.ActiveIllnesses?.Any(i => !i.IsDiagnosed) is true)
{
    <div class="alert alert-warning d-flex justify-content-between align-items-center py-2 mb-3">
        <span>@UndiagnosedCount undiagnosed illness(es) detected.</span>
        <button class="btn btn-sm btn-primary" @onclick="VetVisitAsync" disabled="@vetBusy">
            Take to vet (@(settings?.VetDiagnosisFee ?? 15) cr)
        </button>
    </div>
}
```

Rationale for a single rat-level button rather than per-illness: the backend visit endpoint diagnoses *all* undiagnosed illnesses on the rat for one flat fee (`Task21_Design.md` §3) — a per-card "diagnose this one" button would misrepresent the actual charge.

### 2.2 New component: `VetIllnessCard` (razor component, not inline markup)

Given three visual states plus Task 22's parallel OTC state (see that doc's §6, Combined Illness Card Design), extracting the illness card into its own component (`GlassWingClient/Components/VetIllnessCard.razor` or similar — component folder doesn't exist yet, would be new) is worth doing now rather than inlining a third nested conditional block into `RatDetail.razor`. Parameters: `ActiveIllness Illness`, `EventCallback OnTreat`, `EventCallback OnDose`, plus (per Task 22) OTC protection/buffer/cure props passed down from the parent.

If component extraction is out of scope for a first pass, the fallback is inlining as today — call this out as a judgment call for whoever implements, not a hard requirement.

### 2.3 No Shop/Inventory changes for Task 21

Vet care has no shop presence — it's a rat-scoped action, not a purchasable item — so `Shop.razor`/`ShopCategory.razor` are untouched by this task (Task 22's Medications section is the one that touches the shop).

---

## 3. API Integration

### 3.1 `ApiModels.cs` changes

Replace the existing `ActiveIllness` record (currently 4 fields) — this is a breaking shape change, coordinate the swap with backend deployment:

```csharp
public record ActiveIllness(
    bool IsDiagnosed,
    string? IllnessId,           // null pre-diagnosis
    string? Name,                // null pre-diagnosis
    string? Category,            // null pre-diagnosis
    DateTime StartedAt,          // always visible, even pre-diagnosis
    bool TreatmentApplied,
    DateTime? TreatedAt,
    string? TreatmentType,       // "None" | "OneOff" | "Course" — null pre-diagnosis
    int? TreatmentCost,
    int? DosesAdministered,      // Course only
    int? DoseCount,              // Course only
    bool? CanDoseToday,          // Course only
    string? Recommendation);     // TreatmentType == "None" only (Malnourishment)
```

New response types:

```csharp
public record VetDiagnosisResponse(VetDiagnosisEntry[] Diagnoses, decimal NewBalance);
public record VetDiagnosisEntry(
    string IllnessId, string Name, string Category, DateTime StartedAt,
    string TreatmentType, int TreatmentCost, int? DoseCount, string? Recommendation);

public record VetTreatResponse(ActiveIllness Illness, decimal NewBalance);
public record VetDoseResponse(ActiveIllness? Illness, bool Cured, string? CureMessage);
```

`GameSettingsResponse` gains `VetDiagnosisFee` (per `Task21_Design.md` §8):

```csharp
public record GameSettingsResponse(
    // ...existing fields...
    int? VetDiagnosisFee = null);
```

`HomeResponse` gains vet-treatment notifications, following the exact `AutoFillNotification` precedent already on the record:

```csharp
public record VetTreatmentNotification(string RatId, string RatName, string IllnessName);
// added to HomeResponse: VetTreatmentNotification[]? VetTreatmentNotifications = null
```

### 3.2 `GlassWingApiClient.cs` additions

Following the existing `TrainRatAsync`/`BuyCageAsync` tuple-return, 402-mapping convention:

```csharp
public async Task<(VetDiagnosisResponse? Result, string? Error)> VetVisitAsync(string ratId)
{
    var resp = await http.PostAsync($"/api/rats/{ratId}/vet/visit", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<VetDiagnosisResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode switch
    {
        402 => "Insufficient funds.",
        409 => "Nothing to diagnose.",
        _   => string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body
    });
}

public async Task<(VetTreatResponse? Result, string? Error)> VetTreatAsync(string ratId, string illnessId)
{
    var resp = await http.PostAsync($"/api/rats/{ratId}/vet/treat/{illnessId}", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<VetTreatResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode switch
    {
        402 => "Insufficient funds.",
        409 => "Already being treated, or not yet diagnosed.",
        400 => "This illness has no vet-purchasable treatment.",
        _   => string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body
    });
}

public async Task<(VetDoseResponse? Result, string? Error)> VetDoseAsync(string ratId, string illnessId)
{
    var resp = await http.PostAsync($"/api/rats/{ratId}/vet/dose/{illnessId}", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<VetDoseResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode switch
    {
        409 => "Course already complete, or already dosed today.",
        400 => "Not a course treatment.",
        _   => string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body
    });
}
```

After any of the three calls succeeds, re-fetch the rat (`Api.GetRatAsync(Id)`) rather than hand-patching local state — matches the existing `ConfirmRenameAsync`/`TrainAsync` pattern in `RatDetail.razor` of trusting the server response over local mutation. `VetTreatResponse`/`VetDoseResponse` only return the single affected illness, not the full rat, so a full re-fetch is simplest and avoids illness-array-merge bugs.

### 3.3 `Home.razor` — vet-treatment notifications

Add a read-once banner sourced from `HomeResponse.VetTreatmentNotifications`, following the exact existing pattern for `AutoFills` (`GlassWingClient/Pages/Home.razor`) — display on load, no explicit dismiss needed since the backend clears the list server-side on the next `GET /api/home`.

---

## 4. UX Flows

### 4.1 The "undiagnosed = mystery" beat

This is the flow's whole point and must not be undermined by over-eager client caching or state guessing:
- Before a vet visit, the client must never infer or display `IllnessId`/`Name`/`Category` — even if the client happens to know it from a previous session (e.g. the same illness id recurring), the *current* instance is a fresh `ActiveIllness` object and must be shown as unknown until this instance's `IsDiagnosed` flips true.
- `StartedAt` is the only illness-specific detail visible pre-diagnosis — render it plainly ("Unwell since 1 Jul") so a player has a reason to go to the vet ("it's been 3 days") without knowing what for.
- Do not show a "guess" of category from other signals (e.g. a concurrent stress type) — Task 22 §7 explicitly leaves this as a player-inference thing, out of scope for the client to short-circuit.

### 4.2 Vet visit

1. Player clicks "Take to vet" (rat-level banner, §2.1).
2. `VetVisitAsync(ratId)`. On success: update currency (`PlayerState.SetCurrency`), re-fetch rat, show a transient success toast/alert listing what was diagnosed (reuse the `trainMessage`/`listingMessage` alert pattern already on the page).
3. On `409 NothingToDiagnose`: this should be unreachable from the UI since the banner only renders when an undiagnosed illness exists — treat as a stale-state race (another tab/session already visited) and just re-fetch silently.
4. On `402`: standard insufficient-funds alert, no state change.

### 4.3 Treat

1. Diagnosed, untreated card's **Treat (N cr)** button → `VetTreatAsync(ratId, illnessId)`.
2. Success: re-fetch rat; card flips to "treating" state immediately (OneOff shows "Recovering…", Course shows dose bar at 0/N).
3. `409 AlreadyTreating` / `NotDiagnosed`: shouldn't be reachable given the button is only rendered in the untreated-diagnosed state; treat as stale-state, re-fetch.
4. `400 NotTreatable` (i.e. `TreatmentType == "None"`, malnourishment): the client should never render a Treat button for this case in the first place — show `Recommendation` text instead (§2.1). This guard belongs client-side even though the server also enforces it.
5. `402`: insufficient funds alert.

### 4.4 Give today's dose

1. Course card's **Give today's dose** button, `disabled="@(!illness.CanDoseToday)"`.
2. Click → `VetDoseAsync(ratId, illnessId)`.
3. If response `Cured == true`: illness disappears from the list on re-fetch; show the `CureMessage` (e.g. "Whiskers has recovered from Upper Respiratory Infection") as a success alert — this is the client-side echo of the same string the backend also pushes via `VetTreatmentNotifications` on next home load, so don't double-surface it if the player is still on this page when it later also appears on Home.
4. If not cured: dose bar increments, button disables again (next `GET` will return `CanDoseToday: false` until the next UTC calendar day).
5. `409 AlreadyDosedToday`/`CourseComplete`: shouldn't be reachable given the disabled state, but keep the message mapping in `VetDoseAsync` as defense-in-depth against clock-skew edge cases (player's local "today" vs. server's UTC "today" disagreeing right at midnight).

---

## 5. Client-Side Validation & Guards

None of these replace server validation — they exist purely to keep the UI from offering actions that are guaranteed to fail, matching the existing pattern of `disabled="@busy"` guards elsewhere on this page.

| Guard | Client check | Server error if bypassed |
|---|---|---|
| Vet visit only offered when needed | `ActiveIllnesses.Any(i => !i.IsDiagnosed)` | `409 NothingToDiagnose` |
| Treat button only on diagnosed, untreated, treatable illnesses | `IsDiagnosed && !TreatmentApplied && TreatmentType != "None"` | `409 NotDiagnosed`/`AlreadyTreating`, `400 NotTreatable` |
| Dose button respects daily cap | `disabled="@(!illness.CanDoseToday)"` | `409 AlreadyDosedToday` |
| Dose button hidden once course complete | Illness removed from list entirely on cure (server-driven) | `409 CourseComplete` |
| Insufficient funds | Compare `PlayerState.Currency` to fee/cost before enabling button (soft check, still attempt call and surface `402` if stale) | `402` |
| One busy-flag per action type | `vetBusy`/`treatBusy[illnessId]`/`doseBusy[illnessId]` dictionaries, mirroring `busy` on `ShopCategory.razor` | n/a (client-only, prevents double-submit) |

---

## 6. Combined Illness Card — Cross-Reference

Task 22 (OTC medication) adds independent state to the *same* `ActiveIllness` object (`OtcCriticalBufferHours`, `OtcCureProgress`) and displays it on rat-wide, category-level terms rather than per-illness. Since both systems render into the same Health-card real estate on `RatDetail.razor`, the combined layout — showing vet-treatment state and OTC-protection/buffer/cure state on one card without confusing the two — is specified once, in **`Task22_Client_Design.md` §6 ("Combined Illness Card Design")**, and should be treated as the authoritative layout for the illness card regardless of which task ships first. Do not design a separate Task-21-only card layout that later needs to be redone for Task 22 — build to the combined spec from the start if both tasks are in flight together.

---

## 7. Open Questions / Deferred

- ~~**Response shapes are inferred, not confirmed.**~~ **Resolved, and simpler than proposed.** All four vet/OTC endpoints (`/vet/treat/{illnessId}`, `/vet/dose/{illnessId}`, `/otc-medication/use/{medicationId}`, `/otc-medication/administer/{storedItemId}`) simply `Produces<RatResponse>()` — there is no custom `VetTreatResponse`/`VetDoseResponse` shape as §3.1 proposed. Confirmed final, not a placeholder (see `IssuesResolutionDesigns.md` B7/B8 in the API repo) — just re-fetch the full rat.
- ~~**No client-side countdown for OneOff recovery.**~~ **Resolved** — `TreatmentRecoveryHours` is now on the diagnosis/active-illness response (see refresh note above). `TreatmentStartedAt + TreatmentRecoveryHours` is a real, buildable ETA now, not a hypothetical.
- **Component extraction (`VetIllnessCard`) is a suggestion, not a requirement** — see §2.2.
- **Vet NPC/clinic flavor content** (visuals, copy, a vet character) is explicitly out of scope per `Task21_Design.md` §10 ("Client vet UI/flavor content... client-side scope, not addressed here") — this document covers functional UI only, not art/flavor direction.
- **This entire document is contingent on Task 21 backend design being finalized and implemented.** Re-validate every field name/endpoint path against the shipped API before or during implementation.
