# Task 18b — Player Titles: Client Design Document

## 1. Overview

Player titles are a small catalogue of display strings the player can equip. The client needs two things: a full browsing page (locked + unlocked, transparent per backend design) and a compact equip control near the player's identity (username). Title unlocks are always a side effect of achievement completion (Task 18a) or seasonal event completion (Task 18e) — this doc covers only the browse/equip surface, not the unlock trigger.

**Backend Dependency — Refreshed 2026-07-06: shipped.** Task 18b was implemented 2026-07-03 (1024 tests passing at the time). The title catalogue and endpoints are live — **note the real route is `PUT /api/players/me/title`, not `/api/player/title`** as this doc (following the design doc) assumed; the shipped API used its established `/me` convention instead. Re-verify other endpoint shapes against the live API/`openapi.json` before wiring up.

---

## 2. Shared Conventions

See Task18a_Client_Design.md §2 for the canonical definitions of the "Progress" nav entry, the Progress hub page, `RewardToastService`/`RewardToastHost`, `ProgressStateService`, and currency-display/terminology conventions — all reused unchanged here. Title unlocks are not currency events, but when an achievement or seasonal event unlocks a title, the resulting toast (enqueued by 18a/18e's code paths, not this page) uses `RewardToastKind.Title` with `TitleDisplayText` set and `CurrencyAwarded` set only if the same reward also included currency.

---

## 3. Pages & Components

### `Pages/ProgressTitles.razor` — route `/progress/titles`

- Breadcrumb: `Progress / Titles`.
- List of all titles (unlocked and locked, per the backend's transparency principle — no hidden titles).
- Each row: `DisplayText` (bold), `Description` (muted, small), `UnlockSource` (small badge, e.g. "Achievement: Winner").
- Unlocked rows get an "Equip" button (disabled/replaced with an "Active" badge if it's the currently active title) plus a "Clear" link shown only next to whichever title is currently active.
- Locked rows are visually muted (`text-muted`, greyed badge) with no action — same "show the goal, don't hide it" treatment as locked achievements in 18a.
- A "No title" option is always the first row, selectable at any time (equivalent to `PUT /api/player/title` with `titleId: null`).

### Profile page integration — `Pages/Profile.razor`

Task 18b's own doc flags Profile as the likely home for a quick picker. Add a compact inline control to the existing "Account" card, directly under the `Username` row:

```razor
<dt class="col-sm-4 text-muted fw-normal">Title</dt>
<dd class="col-sm-8">
    <select class="form-select form-select-sm d-inline-block w-auto" @bind="selectedTitleId">
        <option value="">— none —</option>
        @foreach (var t in unlockedTitles) { <option value="@t.Id">@t.DisplayText</option> }
    </select>
    <button class="btn btn-sm btn-outline-primary ms-1" @onclick="SaveTitleAsync" disabled="@titleBusy">Save</button>
    <a class="ms-2 small" href="/progress/titles">Browse all titles →</a>
</dd>
```

This mirrors the existing `SaveAsync`/`busy` pattern already used for Country/State/Weather in `Profile.razor` — same card, same button style, no new UI pattern introduced. The dropdown is populated from `GET /api/titles`, filtered to `unlocked: true`. Full browsing (locked titles, unlock sources) stays on `/progress/titles` to avoid cluttering Profile.

---

## 4. API Integration

### `GET /api/titles`

```csharp
public async Task<TitlesResponse?> GetTitlesAsync()
```

```csharp
public record TitlesResponse(string? ActiveTitleId, TitleEntry[] Titles);
public record TitleEntry(string Id, string DisplayText, string Description, string UnlockSource, bool Unlocked);
```

### `PUT /api/player/title`

```csharp
public async Task<(bool Success, string? Error)> SetActiveTitleAsync(string? titleId)
{
    var resp = await http.PutAsJsonAsync("/api/player/title", new { titleId }, JsonOpts);
    if (resp.IsSuccessStatusCode) return (true, null);
    var body = await resp.Content.ReadAsStringAsync();
    return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body);
}
```

`403` → "You haven't unlocked this title yet." `400` → "Unknown title." Both are unreachable through normal UI flow (the dropdown is always built from `unlocked: true` entries) but are defensive-guarded (see §6) in case of a stale client cache.

### `PlayerProfileResponse` extension

Per Task18b_Design.md §5, add to the existing player profile shape so the username display can eventually show the active title:

```csharp
public record PlayerProfileResponse(
    ..., // existing fields unchanged
    string? ActiveTitleId = null,
    string? ActiveTitleText = null);
```

---

## 5. UX Flows

1. **Quick equip from Profile.** Player opens Profile, sees the Title dropdown pre-populated with their unlocked titles and the current active one selected. They pick a new one, click Save — same success/error alert pattern already on the page.
2. **Browse and equip from the hub.** Player navigates Progress → Titles, sees the full catalogue including locked entries with their unlock source (e.g. "Achievement: Veteran Racer"), building a personal goal list. Clicking "Equip" on an unlocked row calls `PUT /api/player/title` directly and updates the row state without a full page reload.
3. **Clear active title.** Player clicks "Clear" next to their currently active title — same endpoint, `titleId: null`, no confirmation needed (equipping/clearing a title is free and instantly reversible).
4. **Unlock elsewhere.** Player wins their first event; the Task 18a inline-unlock flow shows an "Achievement Unlocked!" toast whose reward included the title `champion`. The toast text includes "New title: Champion" but does **not** auto-equip it — the player must actively choose to equip via Profile or `/progress/titles`. This is a deliberate UX choice (see §7).

---

## 6. Client-Side Validation & Guards

- The equip dropdown and the `/progress/titles` "Equip" buttons are only ever built from `unlocked: true` entries — a `403 Forbidden` from the server should never occur through normal navigation. Still handle it defensively: show the returned error text in the existing alert pattern, and refetch `GET /api/titles` to resync client state (covers the case where the catalogue was cached from before an unlock/desync).
- No client-side character-limit or format validation needed — the player never types a title, only selects from a server-provided list.
- If `GET /api/titles` fails, Profile's title dropdown should degrade gracefully to a disabled "Unable to load titles" state rather than blocking the rest of the Profile page (Country/State/Weather sections still need to work).

---

## 7. Open Questions / Deferred

- **Should a new title auto-equip on unlock?** Backend doc is silent; this design keeps it manual (player must opt in) to avoid surprising a player who has deliberately chosen a different title to display. Revisit if playtesting shows players miss/forget to equip earned titles.
- **Leaderboard / event-lobby display of `ownerTitle`** (Task18b_Design.md §5) has no client surface yet — the client currently has no Leaderboard page and lobby participant lists don't show usernames with titles. Deferred until Task 17's client work exists; this doc only covers the equip/browse UI, not third-party title display.
- **Seasonal titles** (Task 18e) share this exact catalogue and page — no additional client work needed when 18e ships; `/progress/titles` will simply show more rows.
