# Task 22 (Client) — Unrestricted (OTC) Medication: Design Document

## 0. Backend Dependency — READ FIRST

**Refreshed 2026-07-06: the backend has since shipped, and its one open design question is resolved.** Task 22 was implemented 2026-07-04 (1309 tests passing at the time, commit `65308e5`). The shop-data-shape question (§6/§9) was decided: medicated food stays in `ShopCatalogue.Foods` tagged with `TargetIllnessCategory` (the client-merge option this doc already took a position on for planning, §4.2) — `MedicationCatalogue.cs`'s own header comment states this explicitly as final, not "happy to adjust either way" anymore. §4.2's client-merge logic is correct as designed.

**Also resolved:** the "multiple installed devices of the same type" open question below (§8) — the backend now blocks purchasing a second device of a type already installed anywhere in the home (400 on `BuyMedicationDeviceAsync`), so this doc's assumption of single-device-per-type is confirmed correct; no device picker is needed.

As with Task 21, this document was originally written before the backend shipped and hasn't been fully re-verified field-by-field against the real implementation as part of this refresh pass, particularly the `otc-medication/use`/`administer` response shapes in §3.1 (per `IssuesResolutionDesigns.md` B8 in the API repo, both simply `Produces<RatResponse>()` — confirming this doc's own fallback proposal was correct, but re-verify the exact shape before wiring up).

---

## 1. Overview

Task 22 adds a vet-free, self-service medication path that coexists with — but never reads — Task 21's vet system. Three delivery shapes, three different UI homes:

| Delivery | Example | Where bought | Where used |
|---|---|---|---|
| Home accessory device | Nebuliser | Shop → Medications tab, install into a home accessory slot (like Carry Cases/Storage Drawers) | `RatDetail.razor` — "Use Nebuliser" button, usable on any rat in the home |
| Direct-dose consumable | Soothing Drops | Shop → Medications tab, stored in a Storage Drawer (like bowls/bottles) | `RatDetail.razor` — "Administer" button, consumed on use |
| Medicated food | Gastro-Safe Food | Shop → Food tab (existing pipeline), fed via a cage exactly like any food | No player action beyond feeding — effect applies automatically per rat-day |

All three produce up to three effects on a rat, tracked **per illness category, not per illness** (unlike Task 21's precise, illness-specific diagnosis): prevention (dampens stress→illness escalation), a Critical buffer (delays forced-Critical vitality), and a slow cure (repeated use eventually clears a matching-category illness outright). None of this requires — or even looks at — Task 21's `IsDiagnosed`/`TreatmentApplied` state. A rat that is simply `Ill`, with zero vet visits ever, is a fully valid target for every OTC action in this document.

---

## 2. Pages & Components

### 2.1 Shop — new "Medications" section

`Shop.razor`'s `Categories` array (line 28) gains one entry:

```csharp
new("medications", "Medications", "Vet-free treatments — devices, doses, and medicated food"),
```

`ShopCategory.razor` gains a `Category == "medications"` branch. Per §0, this branch's data source depends on the unresolved shop-list question — written here against the client-merge default:

```razor
else if (Category == "medications")
{
    var devices    = (catalogue.Medications ?? []).Where(m => m.DeliveryType == "HomeAccessoryDevice");
    var directDoses = (catalogue.Medications ?? []).Where(m => m.DeliveryType == "DirectDose");
    var medicatedFoods = catalogue.Foods.Where(f => f.TargetIllnessCategory is not null);
    // render three sub-groups, one card style borrowed from accessories (devices),
    // bowls (direct doses — consumable, drawer-bound), and food (medicated foods — reuse existing food card + buy panel verbatim)
}
```

- **Device cards** (Nebuliser): same anchor-slot buy flow as Carry Cases/Storage Drawers/Food Storage Bins (`OpenAnchorPanel` → pick a free home slot → `ConfirmHomeAccessoryBuyAsync`) — this is a direct reuse of existing `ShopCategory.razor` machinery, just wired to a new purchase endpoint (§3.2).
- **Direct-dose cards** (Soothing Drops): same "Buy → Inventory/Drawer" one-click flow as bowls/bottles/accessories (`BuyBowlAsync`-style, no anchor panel — drawer capacity is a server-side concern only, matching how bowls work today).
- **Medicated food cards**: literally the existing `Category == "food"` card markup (lines 274–374), filtered to `TargetIllnessCategory is not null` — no new buy panel needed, it's the same bin/rat-days picker. Add a small "Medicinal — targets @item.TargetIllnessCategory" badge so it reads as belonging to the Medications tab conceptually even though the purchase mechanics are identical to regular food. Whether this card is *also* still reachable from the plain "Food" tab (yes, if `Medications` stays client-merged — it's the same underlying list) should be made clear to the player rather than hidden, since it is in fact ordinary food from the API's point of view.

### 2.2 Inventory — Medication Devices section

`Inventory.razor` gains a fourth section, parallel to Carry Cases/Storage Drawers/Food Storage (lines 34–208):

```razor
<h4 class="mb-3">Medication Devices</h4>
@if ((home.MedicationDevices ?? []).Length == 0)
{
    <p class="text-muted small mb-4">No medication devices. <a href="/shop/medications">Buy one from the shop</a>.</p>
}
else
{
    @foreach (var dev in home.MedicationDevices ?? [])
    {
        <!-- installed-state card: TypeId, AnchorIndex — no "place in cage" action, devices
             are used directly on a rat from RatDetail.razor, not routed through a cage -->
    }
}
```

Unlike Carry Cases, a device has no "move to cage" action — it's a shared home resource usable on any rat (`Task22_Design.md` §4). Inventory only needs to show *that* it's installed and where (slot number), for parity with the other accessory sections; the actionable "Use" button lives on `RatDetail.razor` (§2.3), because using a device is inherently a per-rat decision made in a per-rat context, not a per-cage or per-inventory one.

Direct-dose items (Soothing Drops) *do* still show under the existing **Storage Drawers** section (`KindLabel` gains a `"Medication" => "Medication"` case) — but with no "Install" button, since they're never installed into a cage. Show them as read-only stock ("Soothing Drops ×2 in Storage Drawers — administer from a rat's Health page") to avoid implying an Install action that doesn't apply. This is a deliberate deviation from the existing drawer-item pattern (`OpenDrawerItemPanel` → pick a cage → `ConfirmInstallItemAsync`), which only makes sense for cage-scoped items (bowls/bottles/accessories) — administering a dose targets a rat, not a cage, so the actionable button is relocated to `RatDetail.razor` for the same reason as the device (§2.3).

### 2.3 `RatDetail.razor` — OTC actions and category protection display

New sub-section in the Health card, alongside Task 21's illness cards (see §6 for how they interleave on one card):

- **"Use Nebuliser" button** (or one button per installed device type, if more than one device type exists in the home) — shown if `home.MedicationDevices` contains a matching-category device. Cooldown-aware: if `OtcMedicationCooldowns` contains an entry for this medication with `AvailableAt > now`, render a disabled button with a live "Ready in 2h 14m" countdown instead of "Use".
- **"Administer [dose name]" button** per direct-dose item currently sitting in a reachable storage drawer — same cooldown treatment.
- **Category protection strip** — one row per `IllnessCategory` with an active `OtcProtectionInfo` entry (`ExpiresAt > now`), rat-wide (not per-illness): e.g. "Respiratory protection active (×1.5) — expires in 18h."

These are rat-scoped rows above/around the illness list, since protection is category-wide and devices/doses aren't illness-specific — they should visually read as "this rat's medication status" rather than being nested inside any one illness card, reserving the per-illness card only for the buffer/cure-progress numbers that *do* belong to a specific `ActiveIllness` (§6).

---

## 3. API Integration

### 3.1 `ApiModels.cs` changes

`HealthState` gains two rat-scoped, non-illness-specific arrays, and `ActiveIllness` gains two illness-scoped OTC fields (additive to whatever Task 21 already put there — see `Task21_Client_Design.md` §3.1):

```csharp
public record HealthState(
    string? Vitality,
    double WeightGrams,
    double BodyLengthCm,
    ActiveIllness[]? ActiveIllnesses,
    OtcProtectionInfo[]? OtcProtections = null,        // NEW — category-wide, rat-scoped
    OtcCooldownInfo[]? OtcMedicationCooldowns = null);  // NEW — per medication, rat-scoped

public record OtcProtectionInfo(string Category, DateTime ExpiresAt, double Factor);
public record OtcCooldownInfo(string MedicationId, DateTime AvailableAt);

// ActiveIllness additions (independent of Task 21's TreatmentApplied/DosesAdministered):
//   double? OtcCriticalBufferHours
//   double? OtcCureProgress   // 0-100 scale; illness cures at 100
```

Shop catalogue additions:

```csharp
public record ShopCatalogueResponse(
    // ...existing fields (Task 21/current)...
    ShopMedicationType[]? Medications = null);   // devices + direct-dose only, see §0

public record ShopMedicationType(
    string Id, string Name, string Description,
    string DeliveryType,        // "HomeAccessoryDevice" | "DirectDose"
    string TargetCategory, int InGamePrice,
    double UseCooldownHours, double PreventionWindowHours, double PreventionFactor,
    double CriticalBufferPerUseHours, double MaxCriticalBufferHours,
    double CureProgressPerUse, double SideEffectStressHoursPerUse);

// ShopFoodType gains: string? TargetIllnessCategory = null
```

Home model additions (device install state, parallel to `HomeFoodStorageBinInfo`):

```csharp
public record HomeMedicationDeviceInfo(string Id, string TypeId, int AnchorIndex);
// added to HomeResponse: HomeMedicationDeviceInfo[]? MedicationDevices = null
```

`HomeStorageDrawerItem.Kind` gains a `"Medication"` value (string, no enum client-side today — matches existing convention where `Kind` is already a bare string compared against `"FoodBowl"`/`"WaterBottle"`/`"Accessory"` in `Inventory.razor`).

Purchase/use response types (proposed — see §0 caveat):

```csharp
public record MedicationDevicePurchaseResponse(string DeviceId, int AnchorIndex, decimal NewBalance);
public record MedicationDosePurchaseResponse(string ItemId, decimal NewBalance);   // lands in a drawer, like bowls
public record OtcUseResponse(RatResponse Rat);       // full rat re-fetch shape — simplest given no NewBalance involved
public record OtcAdministerResponse(RatResponse Rat);
```

Returning the full `RatResponse` for use/administer (rather than a thin "just this illness/category" patch, as Task 21's `VetTreatResponse` does) is this document's recommendation: OTC effects are category-wide and can touch multiple `ActiveIllness` entries and the rat-wide `OtcProtections` array at once, so a full re-fetch avoids a multi-field merge on the client. Flag this choice to backend during contract negotiation — it doesn't need to be a full `RatResponse` if that's heavier than the backend wants to compute per-call, but it does need to cover the same ground shown in §2.3.

### 3.2 `GlassWingApiClient.cs` additions

Shop purchases follow the existing anchor-index (`BuyCarryCaseAsync`) and plain-drawer-buy (`BuyBowlAsync`) conventions exactly:

```csharp
public async Task<(MedicationDevicePurchaseResponse? Result, string? Error)> BuyMedicationDeviceAsync(string medicationTypeId, int anchorIndex)
{
    var resp = await http.PostAsJsonAsync("/api/shop/buy/medication-device", new { medicationTypeId, anchorIndex }, JsonOpts);
    // ...same 402-mapping pattern as BuyCarryCaseAsync...
}

public async Task<(MedicationDosePurchaseResponse? Result, string? Error)> BuyMedicationDoseAsync(string medicationTypeId)
{
    var resp = await http.PostAsJsonAsync("/api/shop/buy/medication-dose", new { medicationTypeId }, JsonOpts);
    // ...same 402-mapping pattern as BuyBowlAsync...
}
```

Use/administer, matching the two endpoints named explicitly in `Task22_Design.md` §6:

```csharp
public async Task<(OtcUseResponse? Result, string? Error)> UseOtcMedicationDeviceAsync(string ratId, string medicationId)
{
    var resp = await http.PostAsync($"/api/rats/{ratId}/otc-medication/use/{medicationId}", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<OtcUseResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode == 409
        ? "On cooldown, or device not installed."
        : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}

public async Task<(OtcAdministerResponse? Result, string? Error)> AdministerOtcDirectDoseAsync(string ratId, string storedItemId)
{
    var resp = await http.PostAsync($"/api/rats/{ratId}/otc-medication/administer/{storedItemId}", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<OtcAdministerResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode == 409
        ? "On cooldown, or item unavailable."
        : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}
```

Medicated food needs **no new client method at all** — `BuyFoodAsync`/`RefillFoodAsync` already exist and are unchanged; only the display layer treats `TargetIllnessCategory`-tagged foods specially (§2.1).

---

## 4. UX Flows

### 4.1 Device use (Nebuliser)

1. Player on `RatDetail.razor` clicks "Use Nebuliser."
2. `UseOtcMedicationDeviceAsync(ratId, medicationId)`. On success, re-fetch/replace rat state from the response; the button immediately shows the new cooldown countdown (`UseCooldownHours` after now).
3. `409 OnCooldown`: shouldn't be reachable since the button is disabled during cooldown (§2.3) — defense-in-depth message only.
4. No currency involved at use-time (already paid at purchase) — no balance update needed here, unlike vet actions.

### 4.2 Direct dose (Soothing Drops)

1. Player clicks "Administer" next to a listed dose on `RatDetail.razor`.
2. `AdministerOtcDirectDoseAsync(ratId, storedItemId)`. On success: re-fetch rat, **and** the item disappears from the available-doses list (it's consumed, unlike the device) — re-fetch home/drawer state too if that list isn't derived from the rat response alone.
3. Same cooldown guard/message pattern as devices.

### 4.3 Medicated food

No dedicated OTC flow — it's the existing food-buy-and-refill flow (`Shop.razor` → `Home.razor`'s cage food picker, `GlassWingClient/Pages/Home.razor` lines 173–190) with zero client changes beyond the Medications-tab display grouping in §2.1. This is worth stating explicitly so nobody builds a redundant "administer food" action: the effect application is a fully server-side per-rat-day tick (`Task22_Design.md` §4), the client's only job is letting the player buy and feed it, which it already can.

### 4.4 The critical UX beat: OTC never requires diagnosis

Every OTC action above must be available on a rat that is `Ill` with **zero** vet interaction — no `IsDiagnosed` check, no "diagnose first" gate, anywhere in this flow. Concretely:
- The "Use Nebuliser"/"Administer" buttons in §2.3 must render based on `ActiveIllnesses.Any(i => i.Category == medication.TargetCategory)` — but note `Category` is `null` pre-diagnosis on Task 21's shape (`Task21_Client_Design.md` §3.1)! Since OTC only needs the *category*, not the *identity*, and the category is exactly the thing diagnosis would reveal, the client **cannot** determine which undiagnosed illness matches a given medication's category without the player already knowing/guessing it themselves (`Task22_Design.md` §7 confirms this is deliberately left as player inference, not client logic).
- Practical resolution: OTC device/dose buttons should not be gated on category-matching an *undiagnosed* illness at all — always show all installed devices / available doses as usable on any `Ill` rat, regardless of diagnosis state. The category match only matters for whether the use actually *does* anything (prevention/buffer/cure) — that's a server-side concern (`Task22_Design.md` §3), not something the client should pre-filter into invisibility. Hiding a device button because the client can't prove category-relevance would silently defeat the entire "no diagnosis required" premise.
- Corollary: it is entirely valid, and should render without any warning/error state, for a player to nebulise a rat whose sole illness turns out (invisibly, to the player) to be a non-Respiratory illness — the use still consumes cooldown and applies side-effect stress, it just has no effect on that particular illness. This is intentional per `Task22_Design.md` §3/§7, not a bug to guard against client-side.

---

## 5. Client-Side Validation & Guards

| Guard | Client check | Server error if bypassed |
|---|---|---|
| Device use respects cooldown | `OtcMedicationCooldowns` entry `AvailableAt > now` → disable + countdown | `409 OnCooldown` |
| Direct dose respects cooldown | Same as device, keyed by medication id | `409 OnCooldown` |
| Direct dose only offered while stock exists | Item still present in drawer list (removed from local state immediately post-consume) | `404`/item-not-found equivalent |
| Insufficient funds at purchase time | Soft pre-check against `PlayerState.Currency` before enabling Buy, same as every other shop flow | `402` |
| No diagnosis gate anywhere in this task | Explicitly absent — see §4.4 | n/a — OTC never checks `IsDiagnosed` server-side either |
| One busy-flag per action | `deviceUseBusy`/`administerBusy[itemId]`, mirroring `busy` on `ShopCategory.razor`/`Inventory.razor` | n/a (client-only) |

---

## 6. Combined Illness Card Design

This section is the authoritative layout for the Health-card illness display when both Task 21 (vet) and Task 22 (OTC) are live — referenced from `Task21_Client_Design.md` §6.

**The core problem:** vet state (`IsDiagnosed`, `TreatmentApplied`, dose progress) is illness-specific and precise; OTC state (`OtcCriticalBufferHours`, `OtcCureProgress`, category protection) is category-wide and comes from a rat-level, not illness-level, source for the protection strip — but `OtcCriticalBufferHours`/`OtcCureProgress` themselves *do* live on the individual `ActiveIllness`. So a single illness card needs to show one precise, named thing (vet) and one fuzzy, shared thing (OTC) at once, without the player mistaking one for the other.

**Proposed layout** — two visually distinct zones within one card, separated by a thin rule, never interleaved field-by-field:

```
┌─────────────────────────────────────────────┐
│ Upper Respiratory Infection      [Respiratory]│   ← name + category (blank/muted if undiagnosed)
│ Unwell since 1 Jul                            │
│ ───────────────────────────────────────────  │
│ 🩺 Vet:  Course treatment — dose 2 / 4        │   ← vet zone: icon-prefixed, only rendered if
│         [Give today's dose]                   │      any Task-21 field is non-default
│ ───────────────────────────────────────────  │
│ 🌿 OTC:  Cure progress ▓▓▓▓▓▓░░░░ 62%         │   ← OTC zone: icon-prefixed, only rendered if
│         Critical buffer: +9h (cap 24h)        │      OtcCureProgress > 0 or OtcCriticalBufferHours > 0
└─────────────────────────────────────────────┘
```

Rules:
- **Two clearly labeled zones ("Vet:" / "OTC:"), never merged into one progress bar or one badge set.** A player must never have to guess which system a given number belongs to.
- **Each zone renders independently and can be entirely absent.** An untreated, unmedicated illness shows neither zone (just name/category/since). A vet-only illness shows just the Vet zone. An OTC-only illness (including one the player never diagnosed — see §4.4, category unknown to the player but the fields may still be populated if they happened to medicate the right category) shows just the OTC zone, name/category blank if undiagnosed.
- **Category-wide protection (`OtcProtections`) does NOT appear inside individual illness cards** — it's rat-wide, not illness-specific, and belongs in the separate strip described in §2.3, above the illness list entirely. Putting a "Respiratory protection active" line inside one specific Respiratory illness's card would wrongly imply the protection is scoped to that illness instance rather than the whole category (it also protects against a *second*, not-yet-triggered Respiratory illness, which has no card to attach it to at all).
- **Undiagnosed + OTC-active is a valid, expected combination** and should render cleanly: name/category blank ("Unknown illness"), Vet zone absent (nothing to show pre-diagnosis), OTC zone present if the player has been medicating that category regardless. This is the single most important case to get right — it's the concrete proof-point that the two systems are independent, and it will be the first thing a design reviewer checks.
- **Color/icon distinction, not just text**: suggested — vet zone gets a neutral/clinical accent (blue), OTC zone gets a natural/herbal accent (green), reinforcing the "precise clinical fix" vs. "patient home remedy" framing already present in the backend design docs' own language.
- **When both zones show a cure signal simultaneously** (course about to finish its last dose *and* `OtcCureProgress` near 100), no special reconciliation UI is needed — per `Task22_Design.md` §7, whichever finishes first silently removes the illness and the other's progress evaporates with it. The client doesn't need a "race" indicator; it just stops rendering the card once the illness is gone from `ActiveIllnesses` on the next fetch, and shows whichever cure notification arrives (vet's `VetTreatmentNotification` or OTC's own "recovered naturally" message per `Task22_Design.md` §3).

---

## 7. Combined Card — Component Implication

If `VetIllnessCard` is extracted as a component (`Task21_Client_Design.md` §2.2), it should be designed from the start to accept both zones' data (it's the same component either task would otherwise duplicate) rather than built once for Task 21 and retrofitted. Parameters: `ActiveIllness Illness` (carries both vet and OTC fields once both schemas land), no separate "which task" flag needed — the component simply renders each zone conditionally based on which fields are non-default, per §6.

---

## 8. Open Questions / Deferred

- **Medicated-food shop-list duplication (the headline open question, `Task22_Design.md` §6/§9).** Does `ShopCatalogue.Medications` end up containing medicated foods too (full duplication), or does the client keep doing the `Foods`-list-filter merge assumed throughout §2.1/§3.1 of this document? This changes `ShopCategory.razor`'s data-fetch logic for the Medications tab (simpler if duplicated — one list, no filter/merge — marginally more backend bookkeeping) and should be settled with the backend team before the Medications shop tab is built, not discovered mid-implementation.
- **Use/administer response shape is unconfirmed** (§3.1) — `Task22_Design.md` doesn't provide a worked JSON example for either endpoint, only prose ("return updated illness/protection state"). This document proposes a full-`RatResponse` wrapper for simplicity; confirm against actual backend implementation.
- ~~**Multiple installed devices of the same type.**~~ **Resolved** — see refresh note above. The backend blocks the purchase, confirming this doc's single-device-per-type assumption; no device picker needed.
- **Fatigue/Environmental category coverage** is explicitly deferred in the backend design (`Task22_Design.md` §9) — no client work needed until those catalogue rows exist, but the Medications tab's rendering (§2.1) is written to be category-agnostic already, so no rework should be needed when they're added.
- **This entire document is contingent on Task 22 backend design being finalized and implemented** — re-validate every field name/endpoint path, and especially the shop-list decision above, before or during implementation.
