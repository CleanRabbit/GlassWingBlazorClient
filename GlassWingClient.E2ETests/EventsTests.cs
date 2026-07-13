using Microsoft.Playwright;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class EventsTests : PageTest
{
    const string BaseUrl = "http://localhost:5001";
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
}
