# Task 18d — Challenges: Client Design Document

## 1. Overview

Challenges are a shared weekly set of 4 goals (2 easy, 1 medium, 1 hard) that auto-complete and auto-reward — there is no claim step, unlike daily rewards. The client's job is purely to display progress and surface completion moments; there is no player-initiated action on this page at all.

**Backend Dependency — Refreshed 2026-07-06: shipped.** Task 18d was implemented 2026-07-03 (1111 tests passing at the time, commit `693801e`). The challenge pool, weekly selection, and `GET /api/challenges` are live. Re-verify shapes against the live API/`openapi.json` before wiring up — not re-checked field-by-field as part of this refresh pass, except where noted below (see the per-entry achievement/challenge-completion note carried over from Task 18a's equivalent fix).

---

## 2. Shared Conventions

See Task18a_Client_Design.md §2 for the canonical "Progress" nav entry, Progress hub page, `RewardToastService`/`RewardToastHost`, `ProgressStateService`, and terminology conventions. Challenge completions use `RewardToastKind.Challenge` — currency-only reward, so the toast body is always just `+{amount:N0} cr` plus the challenge name.

---

## 3. Pages & Components

### `Pages/ProgressChallenges.razor` — route `/progress/challenges`

- Breadcrumb: `Progress / Challenges`.
- Header: week range (`weekStart`–`weekEnd`, formatted as local dates) and a summary badge `"{summary.Completed} / {summary.Total} complete"` plus `"{summary.TotalRewardAvailable} cr available"`.
- Exactly 4 challenge cards, one per difficulty slot, in a `row g-3` (not grouped by category — difficulty is the primary axis players care about here, unlike achievements which group by category).
- Each card shows:
  - Difficulty badge (`Easy` = `bg-secondary`, `Medium` = `bg-info text-dark`, `Hard` = `bg-danger`) — reuses the existing tier-badge color convention already established for cage/food tiers in `Home.razor`/`ShopCategory.razor` rather than inventing new colors
  - Name + description
  - Progress bar (`progress / threshold`), same 8px style as elsewhere, unless `threshold == 1` in which case a simple "Not yet complete" / "Complete" state (no bar) — same Once-vs-Count treatment as 18a
  - Reward: `+{currency} cr` badge
  - Completed cards get a green checkmark + are visually de-emphasized (muted background) rather than removed from the grid — players should still see what they achieved this week without the completed cards competing visually with in-progress ones

No actions/buttons on this page at all — it is pure progress display, since completion and reward application are both fully server-side and automatic.

### Home page summary

Add a small non-blocking indicator to `Home.razor`, e.g. a line under the currency badge or a compact card: `"Challenges: {completed}/{total} this week"`, linking to `/progress/challenges`. This is optional polish (not a banner/alert — challenges don't need a claim prompt like daily rewards do) but gives players a reason to check in without requiring a dedicated notification.

---

## 4. API Integration

### `GET /api/challenges`

```csharp
public async Task<ChallengesResponse?> GetChallengesAsync()
```

```csharp
public record ChallengesResponse(
    int WeekNumber, DateTime WeekStart, DateTime WeekEnd,
    ChallengeEntry[] Challenges, string[] PendingCompletions, ChallengeSummary Summary);

public record ChallengeEntry(
    string Id, string Name, string Description, string Difficulty, string Category,
    int Progress, int Threshold, DateTime? CompletedAt, ChallengeRewardInfo Reward);

public record ChallengeRewardInfo(decimal Currency);
public record ChallengeSummary(int Completed, int Total, decimal TotalRewardAvailable);
```

Calling this drains `PendingChallengeCompletions` server-side, same drain-on-fetch pattern as achievements (Task18a_Client_Design.md §4). The client diffs `pendingCompletions` against a session-local `HashSet<string> shownChallengeIds` and toasts any not already shown.

### `GET /api/home` — challenges summary block

Add to `HomeResponse`:

```csharp
public record HomeResponse(
    ...,
    ChallengesSummaryInfo? Challenges = null);

public record ChallengesSummaryInfo(DateTime WeekEnd, int Completed, int Total, bool HasCompletions);
```

`Home.razor.LoadAsync()` passes this straight to `ProgressStateService.ApplyHomeSnapshot(home)`, which sets `HasPendingChallenges = home.Challenges?.HasCompletions ?? false`. Unlike achievements, this field reliably drives the Progress nav badge because every trigger point that can complete a challenge (`EventResultScored`, `BreedingDelivered`, `RatAcquired`, `RatHealthEvaluated`) is either a client-initiated call or immediately reflected on the next Home load — there's no cross-player-triggered gap here as there is for `LeaderboardRankAchieved`-based achievements (see Task18a_Client_Design.md §7), except for the one challenge below.

---

## 5. UX Flows

1. **Silent completion.** Player enters and wins an event. The event-scoring call itself doesn't (yet — see Task18a_Client_Design.md §4's envelope gap) carry inline challenge completions. The next Home load sets `home.Challenges.HasCompletions = true`, lighting up the Progress nav badge. Player clicks through, `/progress/challenges` loads, diffs `pendingCompletions`, and toasts "Challenge Complete: Winning Streak — +150 cr".
2. **Checking progress mid-week.** Player visits `/progress/challenges` purely to see how close they are to the hard challenge — no action needed, page is a pure read.
3. **Week rollover.** Player who was mid-progress on last week's challenges opens `/progress/challenges` after Monday's rollover. The 4 challenges shown are simply different (server has already reset `ChallengeProgress` and selected the new week's set) — no "your progress was reset" messaging, consistent with the backend's "no penalty" principle. The client does not need to detect or announce the rollover itself.

---

## 6. Client-Side Validation & Guards

- No mutating actions — nothing to validate.
- Guard against duplicate toasts via the same `shownChallengeIds` session-local set pattern as `shownAchievementIds` (Task18a_Client_Design.md §6) — keep them as two separate sets since achievement and challenge ids are drawn from different catalogues and could theoretically collide as strings.
- `leaderboard-top10` (hard challenge) depends on `LeaderboardRankAchieved` firing from a leaderboard computation that may not always be initiated by this player — same category of gap as the achievement leaderboard case. The client cannot guarantee `home.Challenges.HasCompletions` reflects this specific challenge's completion the instant it happens; treat `/progress/challenges`'s own fetch as the source of truth and don't assume the nav badge is exhaustive for this one challenge type.

---

## 7. Open Questions / Deferred

- **Event-scoring envelope gap** — same issue as Task18a_Client_Design.md §7. Once resolved, challenge completions from `EventResultScored` can toast immediately rather than waiting for the next Home load.
- **Challenge history** — backend explicitly defers "what did I complete last week"; no client design needed (nothing to show).
- **Category badges** — `ChallengeEntry.Category` (Competition/Breeding/Care/Collection) is returned but not surfaced prominently in this design, since difficulty is the primary sort axis for the 4-card layout. Could add a small muted category label under the name if playtesting shows players want it; not required for v1.
