# Task 18c — Daily Rewards: Client Design Document

## 1. Overview

Daily rewards give a small benefit for checking in each day, surfaced through the player's normal Home visit rather than a separate ritual. The client needs: (1) a lightweight claim prompt on Home itself, matching the existing `AutoFillNotifications` banner style, and (2) a full 28-day calendar view for players who want to see the whole schedule.

**Backend Dependency — Refreshed 2026-07-06: shipped.** Task 18c was implemented 2026-07-03 (1063 tests passing at the time, commit `91b50b4`). The calendar and both endpoints are live. Re-verify shapes against the live API/`openapi.json` before wiring up — not re-checked field-by-field as part of this refresh pass.

---

## 2. Shared Conventions

See Task18a_Client_Design.md §2 for the canonical "Progress" nav entry, Progress hub page, `RewardToastService`/`RewardToastHost`, `ProgressStateService`, and terminology conventions. Daily reward claims use `RewardToastKind.DailyReward`. Because the daily reward is currency/waiver only (cosmetics are a Task 18f backfill — see §7), the toast typically shows only the `+{amount:N0} cr` line, with no title/cosmetic line, except on the two calendar days that grant fee waivers.

---

## 3. Pages & Components

### Home page prompt — `Pages/Home.razor`

Add a claim banner directly above the existing `AutoFills` notification block (same alert styling family, so the two read as one system):

```razor
@if (home!.DailyReward is { Available: true } dr)
{
    <div class="alert alert-success d-flex justify-content-between align-items-center mb-2 py-2">
        <span>
            Daily reward ready — <strong>@FormatReward(dr.Reward)</strong> (Day @dr.Day)
        </span>
        <button class="btn btn-sm btn-success" @onclick="ClaimDailyRewardAsync" disabled="@dailyRewardBusy">
            @(dailyRewardBusy ? "..." : "Claim")
        </button>
    </div>
}
```

When `Available == false`, no banner is shown (no nagging, per the backend's "no push notifications" principle) — the player can still check progress via `/progress/daily` if curious. This mirrors `AutoFillNotifications`' pattern of only showing something when there's something to show.

### `Pages/ProgressDaily.razor` — route `/progress/daily`

- Breadcrumb: `Progress / Daily Reward`.
- 28-day calendar grid (7 columns × 4 rows, one `<div>` cell per day), each cell showing:
  - Day number
  - Reward icon/text (`+30 cr`, `Fee Waiver`, etc.)
  - Visual state: `claimed` (past days, muted/checked), `current` (highlighted border, matches `currentDay`), `future` (default)
  - Every 7th day (week milestones) and day 28 get a slightly larger/accented cell to visually call out the milestone, per the backend's "milestone every 7 days" design.
- A "Claim today's reward" button at the top of the page, mirroring the Home banner's button — this page is reachable even without visiting Home first (e.g. from the Progress hub), so it needs its own claim action, not just a read view.
- No streak indicator, no "missed days" warning — consistent with the backend's explicit "no punishment for absence" principle.

---

## 4. API Integration

### `GET /api/home` — daily reward block

Add to `HomeResponse`:

```csharp
public record HomeResponse(
    ...,
    DailyRewardInfo? DailyReward = null);

public record DailyRewardInfo(bool Available, int Day, DateTime? NextAvailableAt, DailyRewardEntry Reward);
public record DailyRewardEntry(string Type, decimal? Amount); // Type: "Currency" | "AdoptionFeeWaiver" | "EventEntryWaiver" | "Cosmetic" (18f backfill)
```

### `POST /api/daily-reward/claim`

```csharp
public async Task<(DailyRewardClaimResponse? Result, string? Error)> ClaimDailyRewardAsync()
{
    var resp = await http.PostAsync("/api/daily-reward/claim", null);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<DailyRewardClaimResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode == 409 ? "Already claimed today." : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}
```

```csharp
public record DailyRewardClaimResponse(int Day, DailyRewardEntry Reward, int NextDay, DailyRewardEntry NextReward, bool CalendarWrapped);
```

On success: enqueue a `RewardToastKind.DailyReward` toast (`+{amount} cr`, or "Adoption fee waiver granted" / "Event entry waiver granted" for waiver days), call `PlayerState.SetCurrency` if the claimed home/profile refresh returns a new balance, call `ProgressStateService.ClearDailyReward()`, and refresh `home` (re-fetch `GET /api/home`, same pattern `Home.razor` already uses after every mutating action). If `CalendarWrapped == true`, append a distinct "You completed a full calendar cycle!" line to the toast detail.

### `GET /api/daily-reward/calendar`

```csharp
public async Task<DailyRewardCalendarResponse?> GetDailyRewardCalendarAsync()
```

```csharp
public record DailyRewardCalendarResponse(int CalendarLength, int CurrentDay, bool ClaimedToday, DailyRewardCalendarEntry[] Entries);
public record DailyRewardCalendarEntry(int Day, DailyRewardEntry Reward, bool Claimed);
```

---

## 5. UX Flows

1. **Passive check-in.** Player opens Home as they normally would. If a reward is available, the claim banner appears above the auto-fill notifications. One click claims it; the banner disappears, a toast confirms the reward, and the currency badge updates in place.
2. **Browsing the schedule.** Player navigates Progress → Daily Reward to see what's coming (e.g. checking how close they are to the Day 21 or Day 28 milestone). This is read-only unless today's reward hasn't been claimed yet, in which case the same claim button is available here too.
3. **Missed a day.** Player returns after a 3-day absence. `CanClaimDailyReward` is still true (last claim was `> 1` calendar day ago) — the claim banner appears exactly as normal, at the same calendar day they left off on. No "you missed N days" messaging is shown; the client does not track or display absence.
4. **Waiver claimed.** Player claims Day 11 (`AdoptionFeeWaiver`). Toast reads "Adoption fee waiver granted — free on your next adoption." No home-page indicator persists beyond the toast; the waiver's presence is implicitly confirmed the next time the player adopts (Task 14's adoption flow deducts nothing and the buy confirmation should say "Fee waived" — cross-reference: adoption UI is out of scope for this doc but should special-case a `0`/waived fee display when it exists).

---

## 6. Client-Side Validation & Guards

- The claim button is disabled (`dailyRewardBusy`/local busy flag) for the duration of the request, same pattern as every other mutating button in `Home.razor` and `Shop.razor`, to prevent double-claim races from a double-click.
- A `409 AlreadyClaimed` response (e.g. two browser tabs both open to Home) is treated as a soft no-op: hide the claim banner and silently refresh home state, rather than showing a red error alert — this is not a real failure from the player's perspective.
- No client-side date/timezone computation is needed — `Available`/`Day`/`NextAvailableAt` are always server-computed (UTC midnight boundary per the backend doc); the client only ever displays what the server returns.

---

## 7. Open Questions / Deferred

- **Cosmetic calendar days (Task 18f backfill).** Task18c_Design.md §9 notes days 11/25 may later become cosmetic grants instead of waivers once Task 18f ships. The `DailyRewardEntry.Type` union already includes `"Cosmetic"` for forward compatibility; the calendar grid's `FormatReward` helper should render a cosmetic name/icon when that type appears, but no cosmetic-specific art/preview is designed here — see Task18f_Client_Design.md.
- **Waiver visibility elsewhere.** A granted-but-unconsumed waiver (`PendingAdoptionFeeWaiver`/`PendingEventEntryWaiver`) has no persistent UI indicator outside the initial toast (e.g. no badge on the Adoption or Events pages saying "you have a free entry queued"). Recommend a small badge on those pages once they're touched for other reasons, but not required for v1 — the waiver still applies silently and correctly server-side even without a client indicator.
- **Streak/catch-up UI** — deliberately absent per backend design principles; no client work needed, but flagging so a future contributor doesn't "helpfully" add one.
