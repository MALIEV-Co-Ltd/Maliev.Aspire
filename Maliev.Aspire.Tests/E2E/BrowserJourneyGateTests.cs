using Maliev.Aspire.Tests.Infrastructure;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace Maliev.Aspire.Tests.E2E;

/// <summary>
/// Browser-level production-gate checks for the currently executable E2E catalog surface.
/// These tests intentionally verify user-visible journeys through Aspire-hosted frontends.
/// </summary>
[Collection("AspireDomainTests")]
public sealed class BrowserJourneyGateTests : IAsyncLifetime
{
    private readonly AspireTestFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserJourneyGateTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Aspire application fixture.</param>
    public BrowserJourneyGateTests(AspireTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    /// <summary>
    /// Verifies public Web trust, conversion, quote handoff, and cookie-consent journeys.
    /// Covers WEB-001, WEB-010, WEB-011, and WEB-013.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "WEB-001,WEB-010,WEB-011,WEB-013")]
    public async Task Web_PublicTrustAndQuoteHandoff_RoutesToLocalQuoteEngine()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var webBase = GetEndpoint("WebBff");
        var quoteBase = GetEndpoint("QuoteEngineBff");

        var homeResponse = await page.GotoAsync(webBase.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.True(homeResponse?.Ok, $"Web home did not return success. Status: {homeResponse?.Status}");

        var cookieSettings = page.GetByText("Cookie settings", new() { Exact = true });
        if (await cookieSettings.IsVisibleAsync())
        {
            await cookieSettings.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { NameString = "Accept optional" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "Accept optional" })).ToBeHiddenAsync();
        }

        var servicesResponse = await page.GotoAsync(new Uri(webBase, "/services").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.True(servicesResponse?.Ok, $"Web services page did not return success. Status: {servicesResponse?.Status}");

        var shopResponse = await page.GotoAsync(new Uri(webBase, "/shop").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.True(shopResponse?.Ok, $"Web shop page did not return success. Status: {shopResponse?.Status}");

        await page.GotoAsync(new Uri(webBase, "/quote").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var demoLink = page.GetByRole(AriaRole.Link, new() { NameString = "Try demo" }).First;
        await Expect(demoLink).ToBeVisibleAsync();

        await demoLink.ClickAsync();
        await page.WaitForURLAsync(url => url.StartsWith(new Uri(quoteBase, "/demo").ToString(), StringComparison.OrdinalIgnoreCase));
        await Expect(page.GetByText("Demo only", new() { Exact = false })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies public Web contact, policy, support, auth, account, shop, and cart entry points render.
    /// Covers the currently executable portions of WEB-002, WEB-003, WEB-005, WEB-006, WEB-007, WEB-008, WEB-009, WEB-012, and COM-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "WEB-002,WEB-003,WEB-005,WEB-006,WEB-007,WEB-008,WEB-009,WEB-012,COM-003")]
    public async Task Web_PublicAccountSupportAndCommerceEntryPoints_Render()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var webBase = GetEndpoint("WebBff");

        await page.GotoAsync(new Uri(webBase, "/contact").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".contact-form")).ToBeVisibleAsync();
        await Expect(page.Locator(".contact-form").GetByRole(AriaRole.Button)).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/warranty-policy").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".page-hero h1")).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/auth/sign-up").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".auth-google")).ToBeVisibleAsync();
        await page.Locator("details.auth-email-panel summary").ClickAsync();
        await Expect(page.Locator("form.auth-form[action='/auth/sign-up/email']")).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/auth/sign-in").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".auth-google")).ToBeVisibleAsync();
        await page.Locator("details.auth-email-panel summary").ClickAsync();
        await Expect(page.Locator("form.auth-form[action='/auth/sign-in/email']")).ToBeVisibleAsync();
        await Expect(page.Locator("a[href='/auth/forgot-password']")).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/auth/forgot-password").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("form.auth-form[action='/auth/forgot-password/request']")).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/account").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForURLAsync(url => url.Contains("/auth/sign-in", StringComparison.OrdinalIgnoreCase));

        await page.GotoAsync(new Uri(webBase, "/shop").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".shop-toolbar input[type='search']")).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/cart").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".cart-layout")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies the QuoteEngine anonymous demo remains non-mutating and usable.
    /// Covers QUOTE-003 and QUOTE-024.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "QUOTE-003,QUOTE-024")]
    public async Task QuoteEngine_AnonymousDemo_EstimatesWithoutFormalArtifacts()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var quoteBase = GetEndpoint("QuoteEngineBff");

        await page.GotoAsync(new Uri(quoteBase, "/demo").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByText("Demo only", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByText("maliev-sample-bracket.step", new() { Exact = false }).First).ToBeVisibleAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "CNC Machining", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { NameString = "FDM 3D Printing", Exact = true }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { NameString = "Estimate" }).ClickAsync();
        await Expect(page.GetByText(new Regex("2,596\\.00\\s+THB")).First).ToBeVisibleAsync();

        var pdfButton = page.GetByRole(AriaRole.Button, new() { NameString = "PDF" }).First;
        await Expect(pdfButton).ToBeDisabledAsync();
    }

    /// <summary>
    /// Verifies signed customer project mode is gated before customer-owned uploads.
    /// Covers QUOTE-005.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "QUOTE-005")]
    public async Task QuoteEngine_RealProjectMode_BlocksCustomerUploadUntilSignIn()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var quoteBase = GetEndpoint("QuoteEngineBff");

        await page.GotoAsync(new Uri(quoteBase, "/projects/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByText("Sign in to upload your own files", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByText("Google sign-in is the primary path", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { NameString = "Sign in" }).First).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies QuoteEngine-local portal routes render against the prototype-backed store.
    /// Covers the currently executable portions of QUOTE-008, QUOTE-009, QUOTE-010, QUOTE-011, QUOTE-012, QUOTE-013, and QUOTE-014.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "QUOTE-008,QUOTE-009,QUOTE-010,QUOTE-011,QUOTE-012,QUOTE-013,QUOTE-014")]
    public async Task QuoteEngine_LocalPortalRoutes_RenderPrototypeBackedSurfaces()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var quoteBase = GetEndpoint("QuoteEngineBff");

        await page.GotoAsync(new Uri(quoteBase, "/profile").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Profile" })).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(quoteBase, "/orders").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Track custom production" })).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(quoteBase, "/ndas").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "NDAs" })).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(quoteBase, "/documents").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Documents" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies Intranet protected routes enforce the employee authentication boundary.
    /// Covers INT-001, SEC-002, and SEC-003 anonymous portions.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-001,SEC-002,SEC-003")]
    public async Task Intranet_ProtectedRoutes_RedirectAnonymousUserToLogin()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await page.GotoAsync(new Uri(intranetBase, "/projects/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForURLAsync(url => url.Contains("/login", StringComparison.OrdinalIgnoreCase));
        await Expect(page.GetByText("Sign in with Google", new() { Exact = false })).ToBeVisibleAsync();
        Assert.Contains("returnUrl", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IBrowserContext> NewContextAsync()
    {
        return await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });
    }

    private Uri GetEndpoint(string resourceName)
    {
        try
        {
            return _fixture.AppFactory!.GetEndpoint(resourceName, "http");
        }
        catch
        {
            return _fixture.AppFactory!.GetEndpoint(resourceName, "https");
        }
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
