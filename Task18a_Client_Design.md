# Task 18a — Achievements: Client Design Document

## 1. Overview

Achievements are a read-mostly progression list: a static catalogue of milestones, each with player progress and a reward. The client's job is to display the catalogue grouped by category with progress bars, and to surface unlock moments as they happen via the shared reward-toast pattern (defined in this document and reused by Tasks 18b–18f).

**Backend Dependency — Refreshed 2026-07-06: shipped.** Task 18a was implemented 2026-07-03 (1006 tests passing at the time). `AchievementService`, the catalogue, and `GET /api/achievements` are all live. See §4/§7 below for two specific fixes that shipped later (the event-scoring envelope and the home-summary pending-unlocks flag) — both re-verified against the real implementation as part of this refresh pass.

This document also defines, canonically, the three pieces of shared infrastructure used by all six Task 18 client docs:
- `RewardToastService` / `RewardToastHost.razor` — the single unlock-notification mechanism
- `ProgressStateService` — the pending-unlock badge state singleton
- `Pages/Progress.razor` — the hub page linking to all six sub-areas

Sibling docs (18b–18f) reference this section rather than redefining it.

---

## 2. Shared Conventions (canonical — referenced by 18b–18f)

**Nav entry point.** A single "Progress" link is added to `Layout/NavMenu.razor`, between `Marketplace` and `Profile`, routed to `/progress`. It carries a small `badge bg-danger rounded-pill` dot when `ProgressStateService.HasAnyPending` is true.

**Progress hub — `Pages/Progress.razor`.** Mirrors `Shop.razor`'s category-tile grid exactly: a `row g-3` of `col-sm-6 col-lg-4` cards, each linking to a sub-page. Six tiles: Achievements (`/progress/achievements`), Titles (`/progress/titles`), Daily Reward (`/progress/daily`), Challenges (`/progress/challenges`), Seasonal Event (`/progress/seasonal`), Cosmetics (`/progress/cosmetics`). Each tile shows a small pending-count badge (from `ProgressStateService`) when applicable, e.g. "2 new" on the Achievements tile.

**Reward toast.** `Services/RewardToastService.cs` is a singleton (registered alongside `PlayerStateService` in `Program.cs`) that queues unlock notifications:

```csharp
public enum RewardToastKind { Achievement, Title, Cosmetic, Challenge, SeasonalEvent, DailyReward }

public record RewardToastItem(
    RewardToastKind Kind,
    string Headline,          // "Achievement Unlocked!"
    string Detail,            // "Winner — Win your first event."
    decimal? CurrencyAwarded, // shown as "+150 cr"
    string? TitleDisplayText, // "Champion" — null if no title reward
    string? CosmeticName);    // "First Place Ribbon" — null if no cosmetic reward

public class RewardToastService
{
    public IReadOnlyList<RewardToastItem> Active => _items;
    public event Action? OnChange;
    public void Enqueue(RewardToastItem item);
    public void Dismiss(RewardToastItem item);
}
```

`Components/RewardToastHost.razor` is rendered once in `Layout/MainLayout.razor`'s top row (alongside the currency badge), as a fixed-position stack (top-right) of `alert alert-success alert-dismissible py-2` banners — the same dismissible-alert markup `Home.razor` uses for `AutoFillNotifications` (`btn-close` dismiss button, no auto-timer; manual dismiss only, consistent with existing conventions). Each banner renders `Headline` bold, `Detail` muted-small, and any of `+{CurrencyAwarded:N0} cr` (green, bold), `New title: {TitleDisplayText}`, `New cosmetic: {CosmeticName}` as applicable.

Any code path that receives an inline unlock (achievement, challenge, seasonal challenge, daily reward claim, cosmetic grant) or a currency change calls `RewardToastService.Enqueue(...)` and, if currency changed, `PlayerStateService.SetCurrency(newBalance)` in the same breath — the top-bar badge and the toast update together, never one without the other.

**`ProgressStateService`** (new singleton, same pattern as `PlayerStateService`):

```csharp
public class ProgressStateService
{
    public bool HasPendingAchievements { get; private set; }
    public bool HasPendingChallenges { get; private set; }
    public bool HasPendingSeasonalCompletion { get; private set; }
    public bool DailyRewardAvailable { get; private set; }
    public bool HasAnyPending => HasPendingAchievements || HasPendingChallenges
                                  || HasPendingSeasonalCompletion || DailyRewardAvailable;
    public event Action? OnChange;
    public void ApplyHomeSnapshot(HomeResponse home); // sets flags from home.Challenges/.SeasonalEvent/.DailyReward/.NewAchievements
    public void ClearAchievements();  // called after GET /api/achievements drains
    public void ClearChallenges();    // called after GET /api/challenges drains
    public void ClearSeasonal();      // called after GET /api/seasonal-event drains
    public void ClearDailyReward();   // called after POST /api/daily-reward/claim
}
```

`MainLayout.razor` calls `ApplyHomeSnapshot` whenever `Home.razor` loads (via the existing `PlayerState`-style event, or `Home.razor` calling it directly after `LoadAsync`). See §5 (Open Questions) for the one gap in this pattern specific to achievements.

**Currency-gain display.** Currency deltas render as `+{amount:N0} cr` in `text-success fw-bold`. This matches the existing `bg-dark` balance badge style already used in `MainLayout.razor` and `Home.razor`.

**Terminology.** "Unlock" (achievements, titles, cosmetics earned passively), "Claim" (daily reward), "Complete" (challenges, seasonal challenges), "Equip"/"Apply" (titles, cosmetics), "Buy" (shop cosmetics — mirrors `Shop.razor`'s "Buy" button label).

---

## 3. Pages & Components

### `Pages/ProgressAchievements.razor` — route `/progress/achievements`

- Breadcrumb: `Progress / Achievements`, matching `ShopCategory.razor`'s breadcrumb pattern.
- Header summary: `"{summary.Completed} / {summary.Total} unlocked"` as a `badge bg-dark fs-6`, next to the page title.
- One `card` per category (`Progression`, `Competition`, `Breeding`, `Care`, `Collection`, plus the extended catalogue's categories), card header = category name.
- Each achievement is a row inside the card body:
  - Name (bold) + description (muted, small)
  - If `completedAt` is non-null: a green checkmark badge + completion date (`toLocalTime` short date)
  - If not completed and `threshold > 1`: a Bootstrap `progress` bar (`progress / threshold`), same 8px-height style as the cage food/water bars in `Home.razor`
  - If not completed and `threshold == 1` (Once-type): a plain "Not yet unlocked" muted label, no bar
  - Reward preview, always visible (transparency-first, per backend design principle): `+{currency} cr` and/or `Title: {name}` and/or `Cosmetic: {name}` as small badges. Title/cosmetic names are resolved client-side from the reward's `titleId`/`cosmeticId` against the Task 18b/18f catalogues fetched separately (see §4) — fall back to the raw id if the lookup catalogue hasn't loaded yet.

No buy/claim actions on this page — achievements are entirely passive.

---

## 4. API Integration

### `GET /api/achievements`

`GlassWingApiClient.GetAchievementsAsync()`:

```csharp
public async Task<(AchievementsResponse? Result, string? Error)> GetAchievementsAsync()
```

New `ApiModels.cs` records:

```csharp
public record AchievementsResponse(AchievementCategoryGroup[] Categories, AchievementSummary Summary);
public record AchievementCategoryGroup(string Category, AchievementEntry[] Achievements);
public record AchievementEntry(
    string Id, string Name, string Description,
    DateTime? CompletedAt, int Progress, int Threshold,
    AchievementRewardInfo Reward);
public record AchievementRewardInfo(decimal? Currency, string? TitleId, string? CosmeticId);
public record AchievementSummary(int Total, int Completed, string[] PendingUnlocks);
```

Calling `GetAchievementsAsync()` drains `PendingAchievementUnlocks` server-side. The client should diff `summary.PendingUnlocks` against a session-local `HashSet<string> shownAchievementIds` and enqueue a `RewardToast` for any id not already shown (covers unlocks that happened via a trigger with no inline surfacing point — see §7).

### `GET /api/home` — inline unlocks

Add to `HomeResponse`:

```csharp
public record HomeResponse(
    ...,
    NewAchievementNotice[]? NewAchievements = null);

public record NewAchievementNotice(string Id, string Name, AchievementRewardInfo Reward);
```

`Home.razor.LoadAsync()` — after a successful load, for each entry in `home.NewAchievements`: enqueue a `RewardToastKind.Achievement` toast, add the id to `shownAchievementIds`, and if `Reward.Currency` is set, call `PlayerState.SetCurrency` with the player's updated balance (requires the home/profile call to also return the fresh balance — already true via the existing `PlayerProfileResponse`/currency badge refresh path).

### Event scoring endpoint — resolved 2026-07-06, differently than originally proposed

`GET /api/events/{lobbyId}/results` still returns a bare `LobbyResultEntryResponse[]` (no top-level wrapper) — but each entry itself now carries the unlocks that scoring pass triggered, rather than a single top-level `NewAchievements`/`NewChallengeCompletions` list for the whole lobby:

```csharp
public record LobbyResultEntryResponse(
    string? PlayerId, string? RatId, string EntrantLabel, string? OwnerUsername, bool IsNpc,
    int Score, int Placement, int CurrencyAwarded, DateTime ScoredAt,
    NewAchievementNotice[] NewAchievements,
    string[] NewChallengeCompletions);
```

This is arguably better than the envelope this doc originally proposed — each entry's unlocks are tied to the specific player/rat that triggered them, which matters once NPC/other-player rows are mixed into the same array. `EventResults.razor`/wherever this response is consumed: after rendering results, walk `entries.Where(e => e.PlayerId == myPlayerId)` and toast any `NewAchievements`/`NewChallengeCompletions` found there, same toast/dedup logic as the Home-load path (§6).

---

## 5. UX Flows

1. **Passive unlock via Home load.** Player opens Home; `GET /api/home` returns `NewAchievements: [{ id: "first-event-win", ... }]` because `RatHealthEvaluated` or a prior action already completed it server-side. `Home.razor` enqueues a toast immediately on load. No navigation required.
2. **Browsing the catalogue.** Player clicks "Progress" → "Achievements". Page calls `GET /api/achievements`, renders all categories. Any `summary.PendingUnlocks` not already shown this session trigger toasts on page load (covers the leaderboard-triggered edge case in §7).
3. **Locked achievement discovery.** Player scrolls the Competition category, sees `event-wins-50` at "3 / 50" progress — the transparent-catalogue design principle means this is visible even far from completion.

---

## 6. Client-Side Validation & Guards

- No mutating actions exist on this page — no validation needed.
- Guard against duplicate toasts: track shown achievement ids per session (`HashSet<string>`, cleared on logout) so the same unlock isn't toasted twice if both an inline `NewAchievements` entry and a later `GET /api/achievements` fetch both reference it.
- If `AchievementRewardInfo.TitleId`/`CosmeticId` can't be resolved against the Title/Cosmetic catalogues (not yet loaded, or catalogue fetch failed), render the raw id rather than throwing — this page must never hard-fail on a partial catalogue load.

---

## 7. Open Questions / Deferred

- **Home-summary field for achievement backlog — partially resolved, still worth reading carefully.** `HomeResponse.Achievements.HasPendingUnlocks` (`bool`) now exists, but it's **not full parity with Challenges/Seasonal Events (18d/18e)** — the backend team deliberately shipped the minimal option here (see `IssuesResolutionDesigns.md` C7 in the API repo): `GetHomeAsync` still drains `PendingAchievementUnlocks` on every call the same way it always did, and `HasPendingUnlocks` is peeked *before* that same call's drain. So it tells you "was there something to report on **this** call" — it does not stay `true` across multiple Home loads the way `challenges.hasCompletions`/`seasonalEvent.hasPendingCompletions` do. Practically: still useful for the leaderboard-triggered edge case (an achievement unlocking from another player's action right before *your* next Home load), but don't rely on it as a durable "you have an unseen achievement" badge that survives a missed render — a client crash or dropped render between the peek and the toast still loses the notification permanently, same risk as before. The 18d/18e-parity option (stop draining in `GetHomeAsync`, drain only in `GET /api/achievements`) is still on the table as a future backend change if this proves insufficient.
- ~~**Event-scoring envelope gap**~~ — **Resolved**, differently than proposed (per-entry fields, not a top-level wrapper) — see §4.
- **Title/Cosmetic name resolution** — this page resolves reward `titleId`/`cosmeticId` to display names client-side against separately-fetched catalogues (Task18b_Client_Design.md, Task18f_Client_Design.md). A future backend enhancement could inline `titleDisplayText`/`cosmeticName` directly into `AchievementRewardInfo` to avoid the extra fetch/join.
- **Hidden achievements** are explicitly out of scope for v1 per the backend doc — no client design needed until backend adds them.
