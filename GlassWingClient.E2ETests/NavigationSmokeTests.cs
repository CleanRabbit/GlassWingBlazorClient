using Microsoft.Playwright;
using static GlassWingClient.E2ETests.TestEnvironment;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class NavigationSmokeTests : PageTest
{
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

    [TestCase("rats", "My Rats")]
    [TestCase("tricks", "Tricks")]
    [TestCase("events", "Events")]
    [TestCase("leaderboards", "Leaderboards")]
    [TestCase("shop", "Shop")]
    [TestCase("inventory", "Inventory")]
    [TestCase("marketplace", "Marketplace")]
    [TestCase("adoption", "Adoption Agency")]
    [TestCase("progress", "Progress")]
    [TestCase("profile", "Profile")]
    public async Task NavPageLoadsWithHeadingAndNoErrors(string route, string expectedHeading)
    {
        await Page.GotoAsync($"{BaseUrl}/{route}");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = expectedHeading, Exact = false }).First)
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page.Locator(".alert-danger")).Not.ToBeVisibleAsync();

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = $"screenshots/{route}.png" });

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors on /{route}: {string.Join("; ", consoleErrors)}");
    }
}
