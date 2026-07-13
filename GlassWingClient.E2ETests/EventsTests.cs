using System.Net.Http.Json;
using Microsoft.Playwright;
using static GlassWingClient.E2ETests.TestEnvironment;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class EventsTests : PageTest
{
    readonly List<string> consoleErrors = new();

    [SetUp]
    public void SubscribeConsole()
    {
        consoleErrors.Clear();
        // The tutorial event is genuinely one-time per player (TutorialEventService.cs:44) —
        // re-running this suite against the same persistent dev account always gets a 400
        // "already completed" on the 2nd+ run. That's correct backend behavior, not a failure
        // to flag, so both the browser's own "failed to load resource" console log and the
        // matching 400 response are excluded here rather than counted as console errors.
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error" && !msg.Text.Contains("status of 400")) consoleErrors.Add(msg.Text);
        };
        Page.Response += (_, resp) =>
        {
            if (resp.Status >= 400 && !resp.Url.EndsWith("/api/events/tutorial"))
                consoleErrors.Add($"{resp.Status} {resp.Url}");
        };
    }

    [Test]
    public async Task RunTutorialEventCompletesOrCorrectlyReportsAlreadyCompleted()
    {
        await Page.GotoAsync($"{BaseUrl}/events");
        var runTutorialButton = Page.GetByRole(AriaRole.Button, new() { Name = "Run Tutorial" });
        await Expect(runTutorialButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await runTutorialButton.ClickAsync();

        var ratPicker = Page.Locator("select.form-select-sm").First;
        await Expect(ratPicker).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await ratPicker.SelectOptionAsync(new SelectOptionValue { Index = 1 });

        var runButton = Page.GetByRole(AriaRole.Button, new() { Name = "Run", Exact = true });
        await Expect(runButton).ToBeEnabledAsync(new() { Timeout = 10_000 });
        await runButton.ClickAsync();

        var outcome = Page.Locator(".alert-success, .alert-danger");
        await Expect(outcome.First).ToBeVisibleAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/events-tutorial-result.png" });

        var dangerText = await Page.Locator(".alert-danger").CountAsync() > 0
            ? await Page.Locator(".alert-danger").First.InnerTextAsync()
            : null;
        if (dangerText is not null)
            Assert.That(dangerText, Does.Contain("already completed"),
                $"Unexpected tutorial failure: {dangerText}");

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }

    // Regression check for a bug where every open lobby was persisted with no Status field at
    // all (LobbyStatus.Open was the enum's default value, silently omitted by a global
    // IgnoreIfDefaultConvention on BSON serialization), so GET /api/events — which filters on
    // Status == Open — could never find any lobby a player had just created. Creating "worked"
    // (200 OK, correctly scheduled) but the lobby was permanently invisible to every browse/enter
    // flow. Drives the real create flow through the UI, then confirms via a direct API call that
    // the created lobby actually shows up in the open-lobbies list, not just that the click didn't error.
    [Test]
    public async Task CreatingAnOpenLobbyMakesItVisibleInTheOpenLobbiesList()
    {
        await Page.GotoAsync($"{BaseUrl}/events");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Create Event" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // Scoped to the "Create Event" column specifically. Note the client renders this
        // definition's label as just "Open Sprint" here — distinct from the backend's real name
        // "Open Sprint Lobby", which is what shows in the "Live Lobbies" browse column instead.
        var lobbyCard = Page.Locator("div.col-lg-5 .card", new() { HasTextString = "Open Sprint" });
        await Expect(lobbyCard).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await lobbyCard.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();

        var ratPicker = lobbyCard.Locator("select");
        await Expect(ratPicker).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await ratPicker.SelectOptionAsync(new SelectOptionValue { Index = 1 });

        var createButton = lobbyCard.GetByRole(AriaRole.Button, new() { Name = "Create & Enter" });
        await Expect(createButton).ToBeEnabledAsync(new() { Timeout = 10_000 });
        await createButton.ClickAsync();

        await Expect(Page.Locator(".alert-danger")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5123") };
        var lobbies = await http.GetFromJsonAsync<OpenLobbiesPage>("/api/events?page=1&pageSize=200");
        Assert.That(lobbies, Is.Not.Null);
        Assert.That(lobbies!.Items, Has.Some.Matches<LobbyItem>(
            l => l.EventDefinitionId == "open-sprint" && l.Status == "Open" && l.EntrantsCount >= 1),
            "The just-created open-sprint lobby should be visible via GET /api/events");

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }

    record OpenLobbiesPage(LobbyItem[] Items, long TotalCount);
    record LobbyItem(string Id, string EventDefinitionId, string Status, int EntrantsCount);
}
