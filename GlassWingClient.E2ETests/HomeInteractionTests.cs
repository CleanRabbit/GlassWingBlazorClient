using Microsoft.Playwright;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class HomeInteractionTests : PageTest
{
    const string BaseUrl = "http://localhost:5001";
    readonly List<string> consoleErrors = new();

    [SetUp]
    public void SubscribeConsole()
    {
        consoleErrors.Clear();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") consoleErrors.Add(msg.Text);
        };
        Page.Response += (_, resp) =>
        {
            if (resp.Status >= 400) consoleErrors.Add($"{resp.Status} {resp.Url}");
        };
    }

    [Test]
    public async Task RefillWaterUpdatesLevelWithNoErrors()
    {
        await Page.GotoAsync(BaseUrl);
        var refillButton = Page.GetByRole(AriaRole.Button, new() { Name = "Refill Water" }).First;
        await Expect(refillButton).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await refillButton.ClickAsync();
        await Expect(refillButton).ToBeEnabledAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/home-after-refill-water.png" });

        await Expect(Page.Locator(".alert-danger")).Not.ToBeVisibleAsync();
        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }
}
