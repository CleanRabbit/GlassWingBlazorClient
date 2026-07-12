# Client Build-Out Plan — Closing the Gamification/Systems Gap

**Context:** the backend is fully shipped through Task 28 (genome deferrals). The Blazor client covers
Home/Rats/Marketplace/Shop/Inventory/Events well, but has **zero client surface** for eight systems shipped
across Tasks 18a–26. This doc sequences the catch-up. Each phase references an existing
`Task{N}_Client_Design.md`, already written and refreshed against real backend state (2026-07-06) — except
Task 26, noted below.

Phases are ordered by dependency, not necessarily priority — Phase 1 is foundational infrastructure that
every later phase in the "Progress" family reuses, so it goes first regardless.

---

## Phase 1 — Progress hub + shared infra + Achievements (Task 18a)

The canonical shared-infrastructure phase — everything in Phase 2 plugs into this with minimal new code.

- `Services/RewardToastService.cs` + `Components/RewardToastHost.razor` — the single unlock-notification
  mechanism, reused by all of 18b–18f
- `Services/ProgressStateService.cs` — pending-badge singleton (drives the nav dot + per-tile "N new" badges)
- `Pages/Progress.razor` — hub page, 6-tile grid mirroring `Shop.razor`'s category grid
- `Pages/ProgressAchievements.razor` — the first real tile, category-grouped list with progress bars
- `Layout/NavMenu.razor` entry (between Marketplace and Profile) + `Layout/MainLayout.razor` toast host wiring
- Reference: `Task18a_Client_Design.md` (full doc — §2 defines the shared conventions every later phase relies on)

## Phase 2 — Titles, Daily Rewards, Challenges, Seasonal Events, Cosmetics (18b–18f)

Each is a page plus a bit of Home-page/Profile integration, plugging into Phase 1's shared services. One
sub-task at a time, same order as the backlog. Cosmetics (18f) is the most involved of the five — it adds
equip controls to both `Home.razor` (cage cosmetics) and `RatDetail.razor` (rat cosmetics), not just its own page.

- References: `Task18b_Client_Design.md` through `Task18f_Client_Design.md`

## Phase 3 — Tricks + Play Sessions (19–20)

- New `Pages/Tricks.razor` (`/tricks`), a full Tricks section on `RatDetail.razor`, and a routine-builder step
  on `Events.razor` for `TricksPerformance` entries (19)
- `RatDetail.razor` Play Session block + cage-card indicators on `Home.razor` (20)
- References: `Task19_Client_Design.md`, `Task20_Client_Design.md`

## Phase 4 — Vet Care + OTC Medication, combined illness card (21–22)

These two are explicitly designed as one merged UI, not two separate cards — read Task 22 §6–8 before
starting, since it supersedes parts of Task 21's original card layout.

- `RatDetail.razor` Health card rework + new `VetIllnessCard` component (21)
- Shop "Medications" section + Inventory "Medication Devices" section, plus OTC actions on `RatDetail.razor` (22)
- References: `Task21_Client_Design.md`, `Task22_Client_Design.md`

## Phase 5 — Easter Egg reveal (23)

Small — one optional field on `RatResponse` plus a UX beat on the rename flow. Good candidate to fold into
whichever phase is convenient rather than doing it standalone.

- Reference: `Task23_Client_Design.md`

## Phase 6 — Cage Husbandry / Cleanliness (26)

No client design doc exists yet — it shipped after the 2026-07-06 doc-refresh pass. Needs a short design
note before building, but this is mostly "surface fields that already exist" rather than new mechanism
design: the 8 accessory response records, `CageResponse`, and `StoredItemResponse` already expose
`Cleanliness`/`Condition`/`CleaningEndsAt` per the Task 25/26 API-spec-update work. Likely shape: a
cleanliness meter on cage cards (`Home.razor`) and condition/cleaning-status on Inventory items.

---

## Addenda — small, fold into whichever phase is convenient

- **Personality traits (24/25)**: zero client surface today. `Task24_Client_Design.md`'s `RatDetail.razor`
  "Personality" section design already covers both waves — same `Traits` field, Task 25 only added more enum
  values, and the design renders the list generically.
- **Coat markings gap (27)**: `RatDetail.razor`'s "Markings" badge row is hardcoded to
  Blaze/Roan/Downunder/Silvering — missing `Pearl`/`HasWhiteFeet`/`IsHuskyCarrier` badges from Task 27's coat
  expansion. A ~10-line fix, no new page needed.
- **Task 28's new coat values** (Quicksilver, Platinum, Platinum Agouti, Wavy, WavySatin): confirmed these
  render automatically — `RatDetail.razor` displays `BaseColor` and the other coat fields as raw strings from
  the API. No client work needed here.

## Not in scope for this plan

- The Unity client (`GlassWingApp`) — separate effort, not being worked in parallel per the decision to keep
  prioritizing Blazor for now.
