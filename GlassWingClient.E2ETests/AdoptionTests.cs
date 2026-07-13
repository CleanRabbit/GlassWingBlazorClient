using Microsoft.Playwright;

namespace GlassWingClient.E2ETests;

[NonParallelizable]
[TestFixture]
public class AdoptionTests : PageTest
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

    // Adopting requires a free carry case (MarketplaceService/AdoptionService), and a fresh
    // home has none — buys one first if needed, same recovery pattern as the shop's
    // drawers-then-accessory flow.
    [Test]
    public async Task AdoptingARatSucceedsWithNoErrors()
    {
        await Page.GotoAsync($"{BaseUrl}/adoption");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Adoption Agency" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        if (await Page.GetByText("No rats available").IsVisibleAsync())
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Generate rats" }).ClickAsync();
        }

        var adoptButton = Page.GetByRole(AriaRole.Button, new() { Name = "Adopt", Exact = true }).First;
        await Expect(adoptButton).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // Adopt is disabled outright (not just blocked in the confirm modal) when there's no
        // free carry case — a fresh home has none, so buy one first in that case.
        if (await adoptButton.IsDisabledAsync())
        {
            await Page.GotoAsync($"{BaseUrl}/shop/carry-cases");
            var buyCaseButton = Page.GetByRole(AriaRole.Button, new() { Name = "Buy", Exact = true }).First;
            await Expect(buyCaseButton).ToBeVisibleAsync(new() { Timeout = 30_000 });

            // The starter Apartment home has only 4 total accessory anchor slots and 1 cage
            // slot — adopted rats have nowhere to go but a carry case (no free cage), so each
            // run against this shared account permanently consumes one slot. Once all 4 fill
            // up, buying another carry case is correctly disabled — that's bounded game
            // capacity, not a bug, so stop gracefully instead of failing.
            if (await buyCaseButton.IsDisabledAsync())
            {
                Assert.Inconclusive("No free home accessory slots left on this shared dev account " +
                    "to buy another carry case — adoption capacity exhausted from repeated runs.");
            }
            await buyCaseButton.ClickAsync();

            var slotButton = Page.Locator(".btn-outline-primary").First;
            await Expect(slotButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
            await slotButton.ClickAsync();
            var confirmSlotButton = Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" });
            await Expect(confirmSlotButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
            await confirmSlotButton.ClickAsync();
            await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync(new() { Timeout = 30_000 });

            await Page.GotoAsync($"{BaseUrl}/adoption");
            if (await Page.GetByText("No rats available").IsVisibleAsync())
                await Page.GetByRole(AriaRole.Button, new() { Name = "Generate rats" }).ClickAsync();

            adoptButton = Page.GetByRole(AriaRole.Button, new() { Name = "Adopt", Exact = true }).First;
            await Expect(adoptButton).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Expect(adoptButton).ToBeEnabledAsync(new() { Timeout = 10_000 });
            await adoptButton.ClickAsync();
        }
        else
        {
            await adoptButton.ClickAsync();
        }

        var confirmAdoptButton = Page.GetByRole(AriaRole.Button, new() { Name = "Confirm Adoption" });
        await Expect(confirmAdoptButton).ToBeEnabledAsync(new() { Timeout = 10_000 });
        await confirmAdoptButton.ClickAsync();

        await Expect(Page.GetByText("Adopted!")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new() { Path = "screenshots/adoption-after-adopt.png" });

        Assert.That(consoleErrors, Is.Empty, () => $"Console errors: {string.Join("; ", consoleErrors)}");
    }
}
