using Microsoft.Playwright;
using static GlassWingClient.E2ETests.TestEnvironment;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class ShopTests : PageTest
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

    // A fresh home has zero storage drawers, and bowls/bottles/accessories all require
    // existing drawer space (MarketplaceService.BuyAccessoryAsync etc.). Re-running this
    // suite against the same persistent dev account means drawers may already exist from a
    // prior run, so this tries the accessory purchase first and only buys drawers on demand
    // — matching how a real player would recover from the same "no drawers" error.
    [Test]
    public async Task BuyingAnAccessoryOrDrawersFirstSucceedsWithNoErrors()
    {
        await Page.GotoAsync($"{BaseUrl}/shop/accessories");
        var buyAccessoryButton = Page.GetByRole(AriaRole.Button, new() { Name = "Buy → Inventory" }).First;
        await Expect(buyAccessoryButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await buyAccessoryButton.ClickAsync();
        await Page.WaitForSelectorAsync(".alert-success, .alert-danger", new() { Timeout = 30_000 });

        if (await Page.Locator(".alert-danger").IsVisibleAsync())
        {
            await Page.GotoAsync($"{BaseUrl}/shop/storage-drawers");
            var buyDrawersButton = Page.GetByRole(AriaRole.Button, new() { Name = "Buy", Exact = true }).First;
            await Expect(buyDrawersButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await buyDrawersButton.ClickAsync();

            var slotButton = Page.Locator(".btn-outline-primary").First;
            await Expect(slotButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
            await slotButton.ClickAsync();

            var confirmButton = Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" });
            await Expect(confirmButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
            await confirmButton.ClickAsync();
            await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync(new() { Timeout = 30_000 });

            await Page.GotoAsync($"{BaseUrl}/shop/accessories");
            buyAccessoryButton = Page.GetByRole(AriaRole.Button, new() { Name = "Buy → Inventory" }).First;
            await Expect(buyAccessoryButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await buyAccessoryButton.ClickAsync();
        }

        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/shop-accessories-after-buy.png" });

        await Expect(Page.Locator(".alert-danger")).Not.ToBeVisibleAsync();
        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }
}
