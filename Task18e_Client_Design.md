# Task 18e — Seasonal Events: Client Design Document

## 1. Overview

Seasonal events are time-limited themed periods (4/year, 6–10 weeks each) layered on top of the regular weekly challenge system. The client needs a prominent-but-not-nagging banner while an event is active, a dedicated page showing the event's 3 challenges and completion reward, and a schedule view of upcoming events for players who like to plan ahead.

**Backend Dependency — Refreshed 2026-07-06: shipped.** Task 18e was implemented 2026-07-03 (1154 tests passing at the time, commit `0409dc8`). The seasonal event catalogue and `GET /api/seasonal-event` are live. Re-verify shapes against the live API/`openapi.json` before wiring up — not re-checked field-by-field as part of this refresh pass.

---

## 2. Shared Conventions

See Task18a_Client_Design.md §2 for the canonical "Progress" nav entry, Progress hub page, `RewardToastService`/`RewardToastHost`, `ProgressStateService`, and terminology conventions. Seasonal challenge completions use `RewardToastKind.Challenge` (identical treatment to weekly challenges — same per-challenge currency-only reward shape). The event's **completion** reward (currency + cosmetics + title, all at once) uses `RewardToastKind.SeasonalEvent` with `CurrencyAwarded`, `CosmeticName` (first of `cosmeticIds`, or "N cosmetics" if multiple — see §6), and `TitleDisplayText` all populated together, since Task18e_Design.md §5 applies all three atomically on final-challenge completion.

---

## 3. Pages & Components

### Home page banner — `Pages/Home.razor`

When `home.SeasonalEvent?.Active == true`, show a themed banner above the cage grid (below the auto-fill notifications, above the cage row) — distinct from the daily-reward claim banner (`alert-success`) and from auto-fill notices (`alert-info`) so the three don't visually blur together. Use `alert-primary` for the seasonal banner:

```razor
@if (home!.SeasonalEvent is { Active: true } se)
{
    <div class="alert alert-primary d-flex justify-content-between align-items-center mb-2 py-2">
        <span>
            <strong>@se.Name</strong> is on — @se.ChallengesCompleted / @se.ChallengesTotal challenges complete
            · @se.DaysRemaining day@(se.DaysRemaining != 1 ? "s" : "") left
        </span>
        <a class="btn btn-sm btn-outline-primary" href="/progress/seasonal">View</a>
    </div>
}
```

No dismiss button — unlike auto-fill notices, this is a standing status (not a one-off event) that should persist for the whole event window, similarly to how the daily-reward banner persists until claimed. It naturally disappears when `home.SeasonalEvent` is null (event ended or hasn't started).

### `Pages/ProgressSeasonal.razor` — route `/progress/seasonal`

- Breadcrumb: `Progress / Seasonal Event`.
- **Active event section** (only rendered if `active` is non-null):
  - Theme banner: event name, theme text, countdown (`daysRemaining`), styled as a larger/hero card at the top of the page (this is the one place in the whole Progress hub that gets special visual treatment, reflecting that seasonal events are the "special occasion" system)
  - 3 challenge cards, same layout/progress-bar treatment as `/progress/challenges` (Task18d_Client_Design.md §3) — difficulty badge, progress bar, reward
  - Completion reward summary card: currency + cosmetic thumbnails (name-only text badges until Task 18f defines actual art) + title, shown as "locked" (muted) until `allChallengesCompleted == true`, then shown as "claimed" — even though the reward auto-applies server-side with no client action, showing the locked/claimed state gives the player a clear finish line to see coming
- **Upcoming events section** (always rendered, even with no active event): a simple list/table of `upcoming` events with name + start/end date range. No challenge details for upcoming events — those aren't revealed until the event goes live, keeping with "transparent schedule" (dates only) rather than "transparent everything" (backend design principle explicitly separates "publish dates" from "no other spoilers").
- **No active event state**: the active-event section is replaced with a simple "No seasonal event running right now" message plus the upcoming list, no visual dead-space/error styling — this is a normal, expected state, not a failure.

---

## 4. API Integration

### `GET /api/seasonal-event`

```csharp
public async Task<SeasonalEventResponse?> GetSeasonalEventAsync()
```

```csharp
public record SeasonalEventResponse(ActiveSeasonalEvent? Active, UpcomingSeasonalEvent[] Upcoming);

public record ActiveSeasonalEvent(
    string Id, string Name, string Theme, DateTime StartDate, DateTime EndDate, int DaysRemaining,
    SeasonalCompletionRewardInfo CompletionReward, string TitleId, string TitleDisplayText,
    ChallengeEntry[] Challenges, // reuses Task18d_Client_Design.md's ChallengeEntry shape — same fields
    bool AllChallengesCompleted, string[] PendingCompletions);

public record SeasonalCompletionRewardInfo(decimal Currency, string[] CosmeticIds);
public record UpcomingSeasonalEvent(string Id, string Name, DateTime StartDate, DateTime EndDate);
```

`ChallengeEntry` is reused verbatim from Task18d_Client_Design.md (`Id, Name, Description, Difficulty, Category, Progress, Threshold, CompletedAt, Reward`) — seasonal challenges use the exact same criteria/reward shape as weekly challenges per the backend doc, so the client should not define a second near-identical record.

Fetching this drains `PendingSeasonalCompletions`. Same diff-against-session-set pattern as achievements/challenges (`shownSeasonalChallengeIds` — kept distinct from `shownChallengeIds` since seasonal and weekly challenge ids are separate namespaces per the backend catalogue).

### `GET /api/home` — seasonal event summary block

Add to `HomeResponse`:

```csharp
public record HomeResponse(
    ...,
    SeasonalEventSummaryInfo? SeasonalEvent = null);

public record SeasonalEventSummaryInfo(
    bool Active, string Name, DateTime EndsAt, int DaysRemaining,
    int ChallengesCompleted, int ChallengesTotal, bool HasPendingCompletions);
```

`Home.razor.LoadAsync()` passes this to `ProgressStateService.ApplyHomeSnapshot(home)`, setting `HasPendingSeasonalCompletion = home.SeasonalEvent?.HasPendingCompletions ?? false`.

---

## 5. UX Flows

1. **Event start.** Player opens Home on the first day of Summer Sprint. The `alert-primary` banner appears for the first time. No toast/interruption — banners are ambient, toasts are for discrete unlock moments (per §2's shared convention split).
2. **Per-challenge completion.** Player wins their 3rd Sprint event, completing "Heat Wave" (easy seasonal challenge). Next Home load sets `HasPendingCompletions = true`; visiting `/progress/seasonal` toasts "Challenge Complete: Heat Wave — +100 cr" (`RewardToastKind.Challenge`, identical treatment to a weekly challenge completion).
3. **Full event completion.** Player finishes all 3 seasonal challenges. The completion reward (currency + cosmetic(s) + title) applies atomically server-side. Visiting `/progress/seasonal` (or the next Home load surfacing `HasPendingCompletions`) toasts once with `RewardToastKind.SeasonalEvent`: "Seasonal Event Complete: Summer Sprint! +300 cr, new cosmetic: Summer Sunshine, new title: Summer Champion 2026." This is the single richest toast in the whole system — deliberately, since it's the rarest and most celebratory unlock (4×/year, not weekly).
4. **Event ends before completion.** Player has 2/3 seasonal challenges done when the window closes. `home.SeasonalEvent` becomes null on the next Home load — the banner and challenge cards simply disappear. No "you missed the reward" messaging; consistent with "missing this year means waiting, not losing" — the challenges reappear (freshly reset) when the event recurs next year, which is simply a new event `Id` (e.g. `summer-sprint-2027`) in the catalogue.
5. **Planning ahead.** Player checks `/progress/seasonal` mid-year with no active event, sees "Harvest Festival — Sept 22 to Oct 31" in the upcoming list, and knows to keep an eye out.

---

## 6. Client-Side Validation & Guards

- No mutating actions on this page — pure display, same as Task 18d's challenges page.
- `SeasonalCompletionRewardInfo.CosmeticIds` may reference cosmetics that don't resolve in the Task 18f catalogue yet (per Task18e_Design.md §2, `CosmeticIds` "may be empty until 18f is implemented"). The client should render a generic "Cosmetic reward" placeholder rather than crash or blank the field if the id doesn't resolve — same null-safe posture the backend takes server-side.
- Guard against showing a stale banner: `Home.razor` should treat `home.SeasonalEvent` as fully authoritative each load (don't cache/carry over a previous session's active event across a reload) since events can end between visits.

---

## 7. Open Questions / Deferred

- **Cosmetic display fidelity** — until Task 18f's catalogue and asset keys are real, this page can only show cosmetic reward names as text badges, not previews/icons. Revisit once Task18f_Client_Design.md's cosmetic rendering approach is implemented.
- **`RatSurrendered` trigger UX** — the Harvest Festival "Community Spirit" challenge requires a surrender action. The client doesn't yet have a dedicated "surrender rat" UI flow surfaced outside the welfare-block-resolution path; this doc assumes that flow exists by the time 18e ships and simply reflects its resulting challenge progress. No new client screen is designed here for surrendering itself.
- **Multi-cosmetic completion reward display** — when `CosmeticIds` has more than one entry (not currently the case in the starter catalogue, which grants exactly one per event, but the shape allows more), the toast/summary card should list all names rather than truncating to "N cosmetics" if there are only 2–3; use a hard cutoff (e.g. show up to 3 by name, then "+N more") if the catalogue ever grows larger.
- **Countdown urgency styling** — should the banner change color/urgency as `daysRemaining` approaches 0 (e.g. last week of a 6-week event)? The backend design principle explicitly rejects "punishing urgency" — this doc intentionally keeps the banner static/neutral (`alert-primary`) throughout the event rather than introducing a red "hurry up" state near the end.
