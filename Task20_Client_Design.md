# Task 20 (Client) — Human-Rat Interaction (Play Sessions): Client Design Document

## Backend Dependency Note

**Refreshed 2026-07-06: the backend has since shipped.** Task 20 was implemented 2026-07-04 (1268 tests passing at the time, commit `0f6b973`) — `POST /api/rats/{ratId}/play-session`, the `playSession` block on `GET /api/home`, and `Solitude` stressor wiring are all live. **Also since shipped (2026-07-06):** `PlaySessionSummaryResponse` — the same `playSession` block this doc designs for `GET /api/home` — is now *also* on `GET /api/rats/{id}` directly (`RatResponse.PlaySession`), closing what would otherwise be this doc's own gap requiring a redundant Home fetch just to read one rat's session state on `RatDetail.razor`. Re-verify other shapes against the live API/`openapi.json` before wiring up.

This doc assumes `Task19_Client_Design.md` has already shipped the trick catalogue view and the trick-training assignment control on `RatDetail.razor` — see that document for the full trick UI. This doc only adds the **active play-session** layer on top.

## 1. Overview

Task 20 makes the passive trick-training system from Task 19 optional-but-slow by giving the player a direct, high-value action: play with a specific rat for a reported duration, which contributes far more trick progress and bonding than waiting passively. It also introduces `Solitude` stress, a gentle welfare signal for rats who haven't been played with recently.

Client surfaces:

1. A **play-session control** on `RatDetail.razor`, gated on the rat having a trick assigned (Task 19's control).
2. A **daily-progress-remaining indicator** so the player knows when further play today stops helping trick progress.
3. A **solitude warning** on both the rat detail page and the Home cage cards.

---

## 2. Pages & Components

### 2.1 `RatDetail.razor` — Play Session block

Added directly below the trick-training assignment control from `Task19_Client_Design.md` §2.2, inside the same Tricks section (play sessions are meaningless without an assigned trick, so keeping them visually adjacent reinforces the dependency):

- **If `CurrentTrickTraining == null`**: render a muted prompt — `Assign a trick above to start a play session.` — with the Play button disabled/absent. This mirrors the `RatNotInCage`/gating-message style already used elsewhere (e.g. Marketplace section only rendering when `Vitality == "Healthy"`).
- **If a trick is assigned**: render the timer control (see §4.1) plus:
  - Progress bar for the trick itself (`Progress` / 100) — same bar already shown by Task 19's control; no duplication needed, just co-located.
  - **Daily-progress-remaining indicator**: a second, smaller bar or text readout: `Today's active progress: 12.5 / 45.0 used` (from `dailyProgressRemaining`, see §3). When `dailyProgressRemaining <= 0`, replace the Play button's default label with a de-emphasized state (see §4.3) — the button stays enabled (a session still banks bonding + clears solitude even at zero trick progress) but the copy changes.
  - **Solitude badge**: if `solitudeActive == true`, a small `bg-warning text-dark` badge reading `Lonely` next to the rat's name/header, with `title="Hasn't been played with recently"`. Clears automatically on next data refresh after a successful session.

### 2.2 `Home.razor` — cage card indicators

The occupancy strip (lines 142–157) currently renders each rat as a plain `badge bg-secondary` link. Task 20 extends this per-rat, using the `playSession` block that `GET /api/home` gains per rat:

- Append a small dot/icon suffix to a rat's badge when `solitudeActive == true` (e.g. a `●` in warning colour, or an outline treatment — full icon-font/SVG treatment is a polish detail, not specified here). Keep it subtle: the cage card is already dense (see `CageResponse` — occupancy, resources, contents, training all compete for space already).
- No play-session action button is added directly to the Home cage card in this design — playing is a focused, single-rat interaction and belongs on `RatDetail.razor` where the trick-training context lives. The Home badge is a **notice**, not an action surface; clicking the rat badge navigates to `/rats/{id}` as it already does today, where the player can act.

This keeps `Home.razor`'s existing per-cage information density from growing further while still surfacing the welfare signal where the player already scans daily.

---

## 3. API Integration

### 3.1 Endpoint

| Endpoint | Method | Purpose |
|---|---|---|
| `POST /api/rats/{ratId}/play-session` | new `SubmitPlaySessionAsync(ratId, trickId, durationSeconds)` | Report a completed session |
| `GET /api/home` | existing `GetHomeAsync()` | Each rat entry gains a `playSession` block |

Per Task19_Client_Design.md §3.2, `RatResponse` already gains `CurrentTrickTraining`/`Bonding`. This doc proposes **also mirroring the `playSession` block onto `GET /api/rats/{id}`** (`RatResponse`), not just `GET /api/home` — the source design only specifies the `/api/home` addition, but `RatDetail.razor` is the primary play-session surface and does not otherwise have `dailyProgressRemaining`/`solitudeActive`/`lastPlaySessionAt` available without a redundant home fetch. Flagging this as a proposed extension to the backend contract, not something already specified in `Task20_Design.md`.

### 3.2 `ApiModels.cs` additions

```csharp
// --- Play Sessions ---

public record PlaySessionResponse(
    string TrickId,
    double ProgressBefore,
    double ProgressAfter,
    double ProgressGained,
    bool CappedByDailyLimit,
    bool TrickLearned,
    double BondingBefore,
    double BondingAfter,
    string[]? NewAchievements);

public record PlaySessionInfo(
    string? CurrentTrickId,
    double? TrickProgress,
    double DailyProgressRemaining,
    DateTime? LastPlaySessionAt,
    bool SolitudeActive);

// RatSummary (Home occupancy) and RatResponse (rat detail) both gain:
//   PlaySession   PlaySessionInfo?
```

`RatSummary` (currently `record RatSummary(string Id, string Name)`, used in `CageResponse.Rats`) needs a `PlaySession` field added for the Home badge (§2.2) — this is a breaking-ish change to a positional record used only for display, so it's additive as a trailing optional param: `RatSummary(string Id, string Name, PlaySessionInfo? PlaySession = null)`.

`GameSettingsResponse` gains `int? MaxPlaySessionSeconds = null` (needed client-side to know the timer cap — see §4.1), following the same optional-trailing-param convention as Task 19's additions.

### 3.3 `GlassWingApiClient.cs` addition

```csharp
public async Task<(PlaySessionResponse? Result, string? Error)> SubmitPlaySessionAsync(
    string ratId, string trickId, int durationSeconds)
{
    var resp = await http.PostAsJsonAsync($"/api/rats/{ratId}/play-session",
        new { trickId, durationSeconds }, JsonOpts);
    if (resp.IsSuccessStatusCode)
        return (await resp.Content.ReadFromJsonAsync<PlaySessionResponse>(JsonOpts), null);
    var body = await resp.Content.ReadAsStringAsync();
    return (null, (int)resp.StatusCode switch
    {
        409 when body.Contains("NotInTraining") => "Assign a trick before starting a play session.",
        400 when body.Contains("TrickMismatch") => "The rat's trick changed — refresh and try again.",
        _ => string.IsNullOrWhiteSpace(body) ? $"Error {(int)resp.StatusCode}" : body
    });
}
```

---

## 4. UX Flows

### 4.1 Starting and completing a play session — timer design

The API receives only `durationSeconds` — an opaque integer the server clamps to `MaxPlaySessionSeconds` (600s) and otherwise trusts. Two options were considered:

| Option | Description | Verdict |
|---|---|---|
| Free-text/numeric duration input | Player types a number of seconds/minutes and submits | **Rejected.** Since the server only clamps and doesn't otherwise verify authenticity, a numeric field is a standing invitation to always type the max (600) — it actively teaches the "optimal" move is to lie, which cuts against the design's stated intent ("time-based, not skill-based... players who engage are meaningfully rewarded"). It also has no relationship to actually spending time with the rat. |
| **In-app stopwatch (Start/End buttons)** | Player presses `Start Play Session`; a live `mm:ss` counter runs; player presses `End Session` when done; elapsed whole seconds becomes `durationSeconds` | **Adopted.** Matches the design's own framing of play sessions as a real-time investment. Not tamper-proof (a determined player can start the timer and walk away, or reload with dev tools to fake elapsed time) — but it is not designed as an anti-cheat mechanism, only as a UI that nudges genuine engagement and gives the action appropriate narrative weight. This matches the source doc's framing that the mini-game/interaction content itself is out of scope — the stopwatch is the minimal viable "something has to decide this" answer, not a game. |

**Implementation sketch** (Blazor, no JS interop required): a `System.Threading.Timer` (or `PeriodicTimer` in an async loop) ticking every second, updating a `TimeSpan elapsed` field and calling `InvokeAsync(StateHasChanged)`. `Start Play Session` begins the timer and swaps the button to `End Session (mm:ss)`. `End Session` stops the timer and immediately calls `SubmitPlaySessionAsync` with the accumulated whole-second count.

- **Cap behaviour**: once `elapsed >= MaxPlaySessionSeconds` (from `GameSettingsResponse.MaxPlaySessionSeconds`, §3.2), stop incrementing the displayed counter and show a `Max session length reached` badge next to it — the timer keeps running server-relevant-wise (i.e., `End Session` still submits `MaxPlaySessionSeconds` exactly) but there is no reason to let the displayed number climb past what counts. This avoids a confusing "I played 20 minutes but only got 10 minutes of credit" moment.
- **Navigation-away / refresh mid-session**: there is no server-side "session started" state — the endpoint is a single atomic POST on completion, not a start/stop pair. If the player navigates away or reloads while the timer is running, the in-progress session is silently lost (no partial credit, no error). This is a real UX gap worth flagging (see §6) but is consistent with the backend contract as specified — introducing a persisted "session in progress" concept would require a backend change out of scope for this doc.
- **No pause button**: keeps the control surface minimal; if the player needs to step away mid-session they can End early (banking whatever elapsed so far) and Start a new one later, or simply let the timer keep running (idle time still counts, same as an in-person distraction would).

### 4.2 Daily cap feedback (`cappedByDailyLimit`)

- Before submitting, the client already shows the running "today's active progress used" indicator (§2.1) computed from `dailyProgressRemaining`, so a diligent player can anticipate diminishing returns.
- After a session where the response has `cappedByDailyLimit: true`, the success message swaps from the default (`+26.5 progress, +2.0 bonding`) to an explicit **"Come back tomorrow for full training benefit — today's active limit is reached, but bonding still grew."** This distinction (from Task20_Design.md §1: `cappedByDailyLimit: true` → "come back tomorrow" vs "keep playing") is surfaced verbatim as the differentiator between the two response states, not inferred client-side from `progressGained == 0` alone (a session can be *partially* capped, per the `remaining`/`actualGain` clamp in the source design).
- The daily-progress-remaining bar refreshes to `0 / 45.0` (or whatever the true remaining value is) immediately from the response-adjacent `GetRatAsync`/`GetHomeAsync` refetch, same pattern as every other mutate-then-refetch flow in the client (e.g. `RefillFoodAsync`, `SetRegimeAsync`).

### 4.3 Solitude indicator and how playing clears it

- `solitudeActive: true` renders the `Lonely` badge (§2.1) on `RatDetail.razor` and the subtle dot on `Home.razor` (§2.2).
- Playing (any duration, per Task20_Design.md §5 "regardless of duration") clears solitude server-side. The client does not attempt to predict this — it simply refetches (`GetRatAsync`/`GetHomeAsync`) after a successful `SubmitPlaySessionAsync` call, same as every other mutation in this client, and the badge disappears once the refreshed `solitudeActive` comes back `false`.
- No proactive "your rat is lonely" push/notification is designed here — the badge is passive, discovered on the pages the player already visits (Home, rat detail). Matches the source design's "gentle and recoverable" framing; no urgency chrome (no red, no animation).

---

## 5. Client-Side Validation & Guards

| Guard | Where | Rationale |
|---|---|---|
| Hide/disable Play control entirely when `CurrentTrickTraining == null` | `RatDetail.razor` | Pre-empt `409 NotInTraining` |
| Submit `trickId` from the rat's *current* `CurrentTrickTraining.TrickId`, never player-chosen | Play session submit | Pre-empt `400 TrickMismatch` — there is no reason to expose trick choice at play-session time since it's implicitly the rat's assigned trick |
| Disable `End Session` if `elapsed == 0` | Timer control | Pre-empt `400` (`durationSeconds > 0` required) — a session ended in the same tick it started (effectively impossible with a 1s-resolution timer, but guarded anyway) |
| Disable `Start`/`End` buttons while the submit request is in flight | Timer control | Existing busy-flag convention (`training`, `regimeBusy`, etc.) — prevents double-submit on slow connections |
| Re-fetch rat/home state after every session, success or capped | Play session submit | Keeps trick progress, bonding, daily-remaining, and solitude badges consistent with server truth (client never computes these locally) |

Server-side clamping of `durationSeconds` to `MaxPlaySessionSeconds` is intentionally **not** duplicated as a hard client validation error — the client stops the visible counter at the cap (§4.1) but still submits accurately-capped values, so there's nothing to reject.

---

## 6. Open Questions / Deferred

- **Lost session on navigation-away**: flagged in §4.1 as a real gap — a player who starts a timer and closes the tab loses that elapsed time entirely, with no warning. A `beforeunload`-style confirmation ("You have an active play session — leaving will lose your progress") is a plausible mitigation but adds JS interop the rest of this client doesn't currently use anywhere; deferred pending a decision on whether that pattern is worth introducing.
- **Play session history**: per Task20_Design.md §12, the API keeps no session log ("cannot show 'you played with Flash 3 times this week'"). The client therefore cannot build a history view or streak indicator beyond what's inferable from achievement/challenge progress (`play-sessions-10`, `play-session-7days` — visible only via the existing achievements UI, out of scope here). No client-side workaround (e.g. local-storage session log) is proposed — it would be per-device and misleading.
- **Multiple rats needing a play session at once**: the Home badge (§2.2) surfaces solitude per-rat but there's no aggregate "3 rats need attention" summary. Given "no punitive absence mechanics" is a stated design principle, this doc intentionally avoids adding urgency-driving aggregate counters; revisit only if playtesting shows players miss the per-cage badges.
- **`playSession` on `GET /api/rats/{id}`**: as noted in §3.1, this is a proposed extension beyond what `Task20_Design.md` specifies (which only adds the block to `GET /api/home`). If the backend team prefers keeping `RatResponse` unchanged, the alternative is for `RatDetail.razor` to additionally call `GetHomeAsync()` and cross-reference the rat's cage entry — workable, but wasteful (fetches the whole home graph to read one rat's block) and is not this doc's preferred option.
- **Group/inter-rat play, rat-initiated "wants attention" flags**: explicitly deferred server-side (Task20_Design.md §12); no client UI is designed for either.
