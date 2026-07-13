using static GlassWingClient.E2ETests.TestEnvironment;

namespace GlassWingClient.E2ETests;

// Not parallelizable: every fixture in this suite drives the same single shared
// dev-bypass account, and concurrent mutations against it caused real MongoDB
// write-conflict 500s (see api.log during the first parallel run).
[NonParallelizable]
[TestFixture]
public class HomeSmokeTests : PageTest
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
    }

    [Test]
    public async Task RootLoadsBootstrappedHomeWithNoErrors()
    {
        await Page.GotoAsync(BaseUrl);
        await Expect(Page.GetByText("Server config")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page.Locator(".alert-danger")).Not.ToBeVisibleAsync();

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/home.png" });

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }
}
