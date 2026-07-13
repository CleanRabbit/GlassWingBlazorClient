using Microsoft.Playwright;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class RatDetailTests : PageTest
{
    const string BaseUrl = "http://localhost:5001";
    readonly List<string> consoleErrors = new();

    [SetUp]
    public void SubscribeConsole()
    {
        consoleErrors.Clear();
        Page.Console += (_, msg) =>
        {
            // Training/breeding can legitimately 422 on repeat runs (cooldowns, already-pregnant,
            // litter limits) — those are correct backend behavior, not bugs, and are asserted on
            // via the on-page message instead of being treated as console errors here.
            if (msg.Type == "error" && !msg.Text.Contains("status of 422")) consoleErrors.Add(msg.Text);
        };
        Page.Response += (_, resp) =>
        {
            if (resp.Status >= 400 && resp.Status != 422) consoleErrors.Add($"{resp.Status} {resp.Url}");
        };
    }

    async Task OpenRatByNameAsync(string name)
    {
        await Page.GotoAsync($"{BaseUrl}/rats");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Rats" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        // Not Exact: true — a retired/pregnant rat's row concatenates a badge into the same
        // <td>'s text content ("Cider Retired"), so an exact match on just the name stops
        // matching once that badge appears.
        await Page.GetByText(name).First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    [Test]
    public async Task TrainingSprintSucceedsOrGracefullyReportsCooldown()
    {
        await OpenRatByNameAsync("Spudder");
        var sprintButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sprint", Exact = true });
        await Expect(sprintButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await sprintButton.ClickAsync();

        var outcome = Page.Locator(".alert-success, .alert-warning");
        await Expect(outcome.First).ToBeVisibleAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/rat-detail-train.png" });

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }

    [Test]
    public async Task BreedingDandelionWithRobinSucceedsOrGracefullyReportsBlock()
    {
        await OpenRatByNameAsync("Dandelion");

        if (await Page.GetByText("Pregnant").IsVisibleAsync())
        {
            // Already pregnant from a prior run against this shared account — the breeding UI
            // correctly hides the mate picker in this state, nothing further to exercise.
            Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
            return;
        }

        // Not just "select.form-select-sm" — the Appearance section's cosmetic-accessory
        // dropdown matches that same class and sits earlier in the DOM.
        var matePicker = Page.Locator("select:has(option:text-is('— pick a mate —'))");
        await Expect(matePicker).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await matePicker.SelectOptionAsync(new SelectOptionValue { Label = "Robin" });

        var breedButton = Page.GetByRole(AriaRole.Button, new() { Name = "Breed", Exact = true });
        await Expect(breedButton).ToBeEnabledAsync(new() { Timeout = 10_000 });
        await breedButton.ClickAsync();

        // Success re-renders the page with "Pregnant"; failure shows an inline alert-danger
        // with a specific, already-mapped reason (cooldown, litter limit, etc.) — both are
        // acceptable outcomes on a repeatedly-run shared account.
        var outcome = Page.GetByText("Pregnant").Or(Page.Locator(".alert-danger"));
        await Expect(outcome.First).ToBeVisibleAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/rat-detail-breed.png" });

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }

    // Cider has an undiagnosed illness seeded directly in Mongo for this test (no in-app path
    // reliably induces one on demand). Runs the full vet flow: diagnose -> treat -> retire,
    // tolerant of already having been diagnosed/treated/retired by an earlier run.
    [Test]
    public async Task VetCareThenRetireCiderSucceeds()
    {
        await OpenRatByNameAsync("Cider");

        if (await Page.GetByText("Retired", new() { Exact = true }).IsVisibleAsync())
        {
            // Already retired by a prior run — nothing left to exercise.
            Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
            return;
        }

        var vetButton = Page.GetByRole(AriaRole.Button, new() { Name = "Take to vet", Exact = false });
        if (await vetButton.IsVisibleAsync())
        {
            await vetButton.ClickAsync();
            await Expect(Page.GetByText("Diagnosed")).ToBeVisibleAsync(new() { Timeout = 30_000 });

            var treatButton = Page.GetByRole(AriaRole.Button, new() { Name = "Treat", Exact = true });
            if (await treatButton.IsVisibleAsync())
            {
                await treatButton.ClickAsync();
                await Expect(Page.GetByText("Dose 0 /")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            }
        }

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/rat-detail-vetcare.png" });

        var retireButton = Page.GetByRole(AriaRole.Button, new() { Name = "Retire", Exact = true });
        await Expect(retireButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await retireButton.ClickAsync();

        var confirmRetireButton = Page.GetByRole(AriaRole.Button, new() { Name = "Retire Rat" });
        await Expect(confirmRetireButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await confirmRetireButton.ClickAsync();

        await Expect(Page.GetByText("Retired", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/rat-detail-retired.png" });

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }
}
