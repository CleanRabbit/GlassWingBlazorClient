# Task 17 (Client) — Competition & Events Leaderboards: Design Document

## Backend Dependency

> **Refreshed 2026-07-06: the backend has since shipped.** Task 17 was implemented 2026-07-03 (959 tests passing at the time) — `GET /api/events/leaderboard`, `GET /api/rats/{id}/events`, and the enriched `GET /api/events/{lobbyId}/results` (entry snapshots, rat/owner names) are all live. Re-verify field names against the live API/`openapi.json` before wiring up — not re-checked field-by-field as part of this refresh pass, beyond the ownership-boundary fix documented below.

## Overview

Task 17 adds three player-facing capabilities on top of the existing event/lobby flow (`Pages/Events.razor`, `Pages/EventResults.razor`):

1. **Leaderboards** — computed rankings per event type (`Sprint`/`AgilityCourse`/`Endurance`), metric (`BestScore`/`WinCount`/`AverageScore`), and time window (`Daily`/`Seasonal`).
2. **Per-rat event history** — a paginated list of a rat's past results, publicly viewable for any rat.
3. **Richer lobby results** — the existing results table gains locked-in entry-time stats (`EntrySnapshot`) and stable rat/owner identity, making a single lobby's result view self-contained.

This is mostly independent of `Task16_Client_Design.md` — the two share no endpoints or components. The only overlap is stylistic (badge/table conventions).

> **Refreshed 2026-07-06.** This doc's central open question — *"is it safe to link into `RatDetail.razor` for a rat you don't own?"* — is now resolved: `GET /api/rats/{id}/public` exists (`BackendAuditFindings.md` C2 in the API repo), and `Task13_Client_Design.md` §4 now specifies the fallback `RatDetail.razor` needs (try the owner endpoint, fall back to the public one on 404, hide owner-only actions when showing the public projection). **Recommendation below: switch to direct `/rats/{ratId}` links wherever this doc currently uses inline expansion specifically to avoid the ownership question** (§1.1, §1.3) — the workaround is no longer necessary, and a real page is a richer destination than an inline snippet. Inline expansion for the leaderboard row (§1.1) can still be kept as a *secondary*, faster-glance affordance if desired; that's a UX preference now, not a technical requirement.

---

## 1. Pages & Components

### 1.1 New page: `/leaderboards`

New `Pages/Leaderboards.razor`, added to `Layout/NavMenu.razor` between "Events" and "Shop".

- **Event type tabs**: Sprint / Agility / Endurance (reuse `TypeBadgeClass`/`TypeLabel` conventions already duplicated across `Events.razor` and `EventResults.razor` — worth promoting to a shared static helper while touching this code, though not required).
- **Metric selector**: dropdown, `BestScore` / `WinCount` / `AverageScore`, scoped to the active event type tab.
- **Window toggle**: segmented control, `Daily` / `Seasonal`. When `Seasonal` is active, show the season number and date range (from the computed fields on `GET /api/game/settings`, §2.1).
- **Table**: rank, rat name, owner username, score, entry count. Reserve a badge slot next to `OwnerUsername` for the future `ownerTitle` (Task 18b — not built; see §4).
- Changing any selector re-fetches; no manual refresh button needed since the server already caches per `LeaderboardCacheTtlSeconds` (~5 min) — show the returned `cachedAt` as a small "as of HH:mm" caption instead, so staleness is transparent without the client managing its own cache.
- **Row interaction**: click the rat name to navigate to `/rats/{ratId}` (see §1.4 — `RatDetail.razor` now handles non-owned rats gracefully via the public-projection fallback). Clicking elsewhere on the row can still expand inline history via `GET /api/rats/{id}/events` as a faster-glance option without leaving the leaderboard — both are fine to offer; navigation is no longer blocked the way it was when this doc was first written.

### 1.2 Events.razor

Add a small "View Leaderboards →" link/button near the page header (next to the `<h1>Events</h1>`), linking to `/leaderboards`. No other structural change.

### 1.3 EventResults.razor — richer result rows

`Pages/EventResults.razor`'s table (lines 37-63) currently renders `PlayerId`/`EntrantLabel`/`IsNpc`/`Score`/`Placement`/`CurrencyAwarded` with no per-row navigation at all — even the viewer's own row is plain text. With the enhanced endpoint (`Task17_Design.md` §8) each entry additionally carries `ratId`, `ratName` (snapshot), `ownerPlayerId`, `ownerUsername` (snapshot), and the full `snapshot` (`EntrySnapshot`).

- Link the rat name to `/rats/{ratId}` for **every** row, not just the viewer's own — `RatDetail.razor` now handles non-owned rats via the public-projection fallback (see §1.4). Keep `ownerUsername` as plain text either way (there's no per-player page to link to).
- Add a small expandable "i" affordance per row showing the locked `EntrySnapshot` (sprint/agility/endurance ability, diet quality, health state, vitality at entry) — no extra fetch needed, this data is already in the enhanced response.

### 1.4 RatDetail.razor — Event History section

New card on `Pages/RatDetail.razor`, placed after the existing Training section and before/alongside Marketplace (around line 179), titled "Event History":

- Optional event-type filter dropdown (All / Sprint / Agility / Endurance).
- Paginated list (reuse the existing small-button pager idiom already used elsewhere, e.g. `Prev`/`Next`, default `pageSize=20`) via `GET /api/rats/{id}/events`.
- Each row: event type badge, placement (reuse `Ordinal()` helper already defined in `EventResults.razor`/`Events.razor` — another good shared-helper candidate), score, `scoredAt` (relative or short date), entry count, and an expandable snapshot detail identical in shape to §1.3's.

This section renders for **any** rat detail page, since `GET /api/rats/{id}/events` is explicitly public (`Task17_Design.md` §7: "Any authenticated player can view any rat's history"). `RatDetail.razor` itself now needs to be ownership-adaptive to support this (see `Task13_Client_Design.md` §4, which specifies the fallback in full): try `GetRatAsync(ratId)` first, fall back to `GetPublicRatAsync(ratId)` on 404, and hide owner-only controls (Rename, Train, List on Marketplace, Retire, cosmetics) plus any field the public projection doesn't carry when showing the fallback view. The Event History card itself renders identically either way, since it's always public regardless of which rat-detail fetch succeeded.

---

## 2. API Integration

### 2.1 Endpoints consumed

| Method & path | Purpose |
|---|---|
| `GET /api/events/leaderboard?eventType=&metric=&window=` | Computed leaderboard; `400` if metric not configured for event type |
| `GET /api/rats/{id}/events?page=&pageSize=&eventType=` | Per-rat paginated result history (public) |
| `GET /api/events/{lobbyId}` / `GET /api/events/{lobbyId}/results` | Existing calls, enhanced with snapshot + stable rat/owner identity |
| `GET /api/game/settings` | Existing call, gains `LeaderboardSeasonDurationDays`, `LeaderboardAverageMinEntries`, and computed current-season start/end/number |

### 2.2 `ApiModels.cs` additions

```csharp
// --- Leaderboards (Task 17) ---

public record EntrySnapshotResponse(
    double SprintAbility,
    double AgilityAbility,
    double EnduranceAbility,
    string DietQuality,
    string HealthState,
    double VitalityScore);

public record LeaderboardEntryResponse(
    int Rank,
    string RatId,
    string RatName,
    string OwnerPlayerId,
    string OwnerUsername,
    double Score,
    int EntryCount);
    // OwnerTitle placeholder — see §4. Not added yet; Task 18b not built.

public record LeaderboardResponse(
    string EventType,
    string Metric,
    string Window,
    DateTime WindowStart,
    DateTime WindowEnd,
    int? SeasonNumber,
    LeaderboardEntryResponse[] Entries,
    int TotalEntries,
    DateTime CachedAt);

public record RatEventHistoryEntry(
    string LobbyId,
    string EventType,
    int Rank,
    double Score,
    int EntryCount,
    DateTime ScoredAt,
    EntrySnapshotResponse Snapshot);

public record RatEventHistoryResponse(
    string RatId,
    string RatName,
    int TotalCount,
    int Page,
    int PageSize,
    RatEventHistoryEntry[] Results);
```

Extend existing records (trailing optional params, matching the existing pattern e.g. `HomeCarryCaseInfo`):

```csharp
public record LobbyResultEntryResponse(
    string? PlayerId, string EntrantLabel, bool IsNpc, int Score, int Placement, int CurrencyAwarded,
    string? RatId = null,
    string? RatName = null,
    string? OwnerUsername = null,
    EntrySnapshotResponse? Snapshot = null);

public record GameSettingsResponse(
    /* ...existing... */
    int? LeaderboardSeasonDurationDays = null,
    int? LeaderboardAverageMinEntries = null,
    int? CurrentSeasonNumber = null,
    DateTime? CurrentSeasonStart = null,
    DateTime? CurrentSeasonEnd = null);
```

### 2.3 `GlassWingApiClient.cs` additions

```csharp
// --- Leaderboards ---

public async Task<(LeaderboardResponse? Result, string? Error)> GetLeaderboardAsync(
    string eventType, string metric, string window)
{
    var resp = await http.GetAsync(
        $"/api/events/leaderboard?eventType={eventType}&metric={metric}&window={window}");
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<LeaderboardResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode == 400
        ? "This metric isn't tracked for this event type."
        : string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}

public async Task<RatEventHistoryResponse?> GetRatEventHistoryAsync(
    string ratId, int page = 1, int pageSize = 20, string? eventType = null)
{
    var url = $"/api/rats/{ratId}/events?page={page}&pageSize={pageSize}";
    if (eventType is not null) url += $"&eventType={eventType}";
    var resp = await http.GetAsync(url);
    return resp.IsSuccessStatusCode
        ? await resp.Content.ReadFromJsonAsync<RatEventHistoryResponse>(JsonOpts)
        : null;
}
```

`GetLobbyResultsAsync` and `GetLobbyAsync` need no signature change — they deserialize into the same (extended) records.

---

## 3. UX Flows

### 3.1 Leaderboard browsing

Default view on `/leaderboards`: Sprint / BestScore / Daily. Every selector change triggers a fresh `GetLeaderboardAsync` call (loading spinner over the table, not a full-page reload). On `400` (metric not configured for the selected event type), show an inline "This metric isn't tracked for this event type" message instead of an empty table — this is expected to be rare given the documented default of all 3 metrics × all 3 types (`Task17_Design.md` §2), but must degrade gracefully rather than assume it never happens (see Open Questions §1).

Empty leaderboard (`TotalEntries == 0`) renders "No results yet for this window." `AverageScore` additionally shows a footnote referencing `LeaderboardAverageMinEntries` ("Rats need at least N entries this window to qualify") when entries are sparse.

### 3.2 Per-rat history

On `RatDetail.razor`, the Event History card loads page 1 (all event types) on tab/page mount, independent of the rat's other sections. Changing the event-type filter or page resets to a fresh fetch — no client-side caching, each page is a cheap targeted query.

From `/leaderboards`, the rat name link navigates straight to `/rats/{ratId}` (§1.1). Inline expansion via the same `GetRatEventHistoryAsync` call (page 1, small `pageSize`, e.g. 5) is still worth offering as a quicker, stay-on-page option — no longer needed as an ownership workaround (§1.4 no longer applies), just a nice-to-have for a fast glance without navigating away.

### 3.3 Lobby results enhancement

No new navigation — `EventResults.razor`'s existing table gains an expand affordance per row (§1.3). Behaviourally identical page, richer per-row detail.

### 3.4 `ownerTitle` — shipped on `LeaderboardEntryResponse`, not on rat event history

**Updated 2026-07-06:** Task 18b has since shipped (see `Task18b_Client_Design.md`), and `LeaderboardEntryResponse.OwnerTitle` (`string?`, resolved fresh at query time from the owner's *current* active title — not snapshotted) is live exactly as this section anticipated. Render it as a small badge immediately after `OwnerUsername` on every leaderboard row. **`RatEventHistoryEntry` does not carry an owner title field** (confirmed against the real response shape) — that's a rat's own history page, where repeating the owner's title on every row wasn't judged worth the field; only the leaderboard table needs this slot.

---

## 4. Client-Side Validation & Guards

- `page`/`pageSize` clamped client-side before calling `GetRatEventHistoryAsync` (`page >= 1`, `pageSize` bounded to a sane range, e.g. 5–50) even though the backend presumably defaults/validates too — avoids obviously-wrong requests.
- Leaderboard selector combinations are not pre-validated client-side against a metrics catalogue (none exists yet — see Open Questions §1); rely on the `400` handling in §2.3/§3.1.
- `EntrySnapshot` fields are display-only; no client-side interpretation or recomputation (e.g. no re-deriving a score) — the whole point of the snapshot is that it's the authoritative, locked-in record.
- Guard against `SeasonNumber == null` when `window == "Daily"` (expected per `Task17_Design.md` §7 example) — don't render season chrome for the Daily tab.

---

## 5. State Management

No client-side leaderboard caching — the server already caches computed leaderboards in Redis for `LeaderboardCacheTtlSeconds` (default 300s per `Task17_Design.md` §4), and the response's `cachedAt` field gives the client a truthful "as of" timestamp to display instead of guessing freshness itself. Every selector change or page revisit is a fresh network call; this keeps the client simple and avoids a second, potentially-stale cache layer on top of the server's.

Per-rat event history is likewise fetched fresh per page/filter change — small, cheap, paginated queries with no need for caching.

`GameSettingsResponse` (season dates, min-entries threshold) is fetched once per page load on `/leaderboards`, consistent with the existing per-page-fetch pattern already used independently in `Home.razor` and `RatDetail.razor` (no shared settings cache exists today; not introducing one here keeps this change isolated).

---

## 6. Open Questions / Deferred

1. **No endpoint exposes which `LeaderboardMetrics` are configured per event type.** `Task17_Design.md` §2 states a sensible default (all 3 metrics × all 3 event types) but doesn't commit to it being exposed to clients, and doesn't rule out per-type variation later. The client currently assumes the default and handles `400` defensively (§3.1). A metadata endpoint (or exposing `EventType.LeaderboardMetrics` via some existing/future event-types listing) would remove the guesswork and let the UI hide invalid combinations up front instead of erroring after the fact.
2. ~~**Whether non-owned rats can be safely viewed via `RatDetail.razor` is unconfirmed.**~~ **Resolved** — `GET /api/rats/{id}/public` exists and `RatDetail.razor`'s fallback behavior is specified (`Task13_Client_Design.md` §4). §1.1/§1.3/§1.4 above now link directly instead of avoiding `RatDetail`.
3. ~~**`ownerTitle` (Task 18b) is a placeholder only**~~ — **Resolved**, confirmed shipped on `LeaderboardEntryResponse` (not `RatEventHistoryEntry`) — see §3.4. Styling (badge placement/weight) is still a real open call for whoever implements this.
4. **No real-time updates.** Leaderboards and lobby results only refresh on navigation/selector change; there's no push mechanism (e.g. SignalR) when a lobby completes or a new result lands. Given the server's own 5-minute cache TTL, near-real-time freshness isn't achievable without a bigger architecture change anyway — out of scope here.
5. **Shared helpers not yet extracted.** `TypeBadgeClass`/`TypeLabel`/`Ordinal` are currently duplicated across `Events.razor` and `EventResults.razor`; this task adds a third and fourth consumer (`Leaderboards.razor`, `RatDetail.razor`'s history section). Worth promoting to a shared static helper class during implementation, though it's a refactor, not a requirement.
