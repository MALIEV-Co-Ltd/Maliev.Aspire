using Maliev.Aspire.Tests.Infrastructure;
using Microsoft.Playwright;
using System.Net.Http.Json;
using System.Text.Json;
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
    /// Verifies the public Web content routes used for trust, policy, support, and conversion research render without broken navigation.
    /// Covers the route-level executable portions of WEB-001, WEB-010, WEB-011, WEB-012, and WEB-013.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "WEB-001,WEB-010,WEB-011,WEB-012,WEB-013")]
    public async Task Web_PublicContentRoutes_RenderTrustPolicyAndSupportSurfaces()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var webBase = GetEndpoint("WebBff");

        var routes = new[]
        {
            "/",
            "/about",
            "/services",
            "/materials",
            "/industries",
            "/case-studies",
            "/blog",
            "/faq",
            "/contact",
            "/shipping-returns",
            "/privacy",
            "/terms",
            "/cookie-policy",
            "/refund-policy",
            "/warranty-policy",
            "/quote"
        };

        foreach (var route in routes)
        {
            var response = await page.GotoAsync(new Uri(webBase, route).ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            Assert.True(response?.Ok, $"Web route {route} did not return success. Status: {response?.Status}");
            await Expect(page.Locator("body")).Not.ToBeEmptyAsync(new() { Timeout = 15_000 });
            await Expect(page.Locator("body")).Not.ToContainTextAsync("Sorry, there is nothing at this address", new() { Timeout = 5_000 });
        }
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
    /// Verifies a visitor can submit a public website contact inquiry through the Web BFF to ContactService.
    /// Covers the executable submission portion of WEB-002 and WEB-012.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "WEB-002,WEB-012")]
    public async Task Web_ContactInquiry_SubmitsThroughContactBoundary()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var webBase = GetEndpoint("WebBff");

        await page.GotoAsync(new Uri(webBase, "/contact").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var form = page.Locator("form.contact-form");
        await Expect(form).ToBeVisibleAsync();

        await form.GetByLabel(new Regex("Full name|ชื่อ", RegexOptions.IgnoreCase)).FillAsync("E2E Website Customer");
        await form.GetByLabel(new Regex("Email|อีเมล", RegexOptions.IgnoreCase)).FillAsync($"e2e-contact-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}@example.com");
        await form.GetByLabel(new Regex("Phone|โทรศัพท์", RegexOptions.IgnoreCase)).FillAsync("+66 2 000 0000");
        await form.GetByLabel(new Regex("Company|บริษัท", RegexOptions.IgnoreCase)).FillAsync("E2E Manufacturing Co.");
        await form.GetByLabel(new Regex("Subject|หัวข้อ", RegexOptions.IgnoreCase)).FillAsync("Manufacturing quote support");
        await form.GetByLabel(new Regex("Message|ข้อความ", RegexOptions.IgnoreCase)).FillAsync("Please confirm MALIEV received this E2E support inquiry.");

        await form.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Send message|ส่งข้อความ", RegexOptions.IgnoreCase) }).ClickAsync();
        await Expect(form.Locator(".form-status")).ToContainTextAsync(new Regex("Message received|ได้รับข้อความแล้ว", RegexOptions.IgnoreCase), new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies a customer can self-register with email/password, land in the protected account area, browse account pages, and sign out.
    /// Covers the executable portions of WEB-003, WEB-005, and WEB-009.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "WEB-003,WEB-005,WEB-009")]
    public async Task Web_CustomerEmailRegistration_CreatesAccountSessionAndSignsOut()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var browserDiagnostics = new List<string>();
        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/auth/", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("/web/", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("/customer/", StringComparison.OrdinalIgnoreCase))
            {
                browserDiagnostics.Add($"request: {request.Method} {request.Url}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            browserDiagnostics.Add($"request-failed: {request.Method} {request.Url} {request.Failure}");
        };
        page.Response += (_, response) =>
        {
            if (response.Url.Contains("/auth/", StringComparison.OrdinalIgnoreCase) ||
                response.Url.Contains("/web/", StringComparison.OrdinalIgnoreCase) ||
                response.Url.Contains("/customer/", StringComparison.OrdinalIgnoreCase) ||
                response.Status >= 400)
            {
                browserDiagnostics.Add($"response: {response.Status} {response.Url}");
            }
        };
        var webBase = GetEndpoint("WebBff");
        var unique = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var email = $"e2e-customer-{unique}@example.com";
        const string password = "E2e-Customer-12345!";

        await page.GotoAsync(new Uri(webBase, "/auth/sign-up?returnUrl=%2Faccount").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("details.auth-email-panel summary").ClickAsync();
        var signUpForm = page.Locator("form.auth-form[action='/auth/sign-up/email']");
        await Expect(signUpForm).ToBeVisibleAsync();
        await signUpForm.Locator("input[name='FirstName']").FillAsync("E2E");
        await signUpForm.Locator("input[name='LastName']").FillAsync("Customer");
        await signUpForm.Locator("input[name='Email']").FillAsync(email);
        await signUpForm.Locator("input[name='Password']").FillAsync(password);
        await signUpForm.EvaluateAsync("form => form.requestSubmit()");

        try
        {
            await page.WaitForURLAsync(
                url => url.Contains("/account", StringComparison.OrdinalIgnoreCase),
                new()
                {
                    Timeout = 45_000,
                    WaitUntil = WaitUntilState.Commit
                });
        }
        catch (TimeoutException ex)
        {
            var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            throw new TimeoutException(
                $"Customer registration did not reach /account. Url: {page.Url}. Body: {body[..Math.Min(body.Length, 1_500)]}. Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, browserDiagnostics)}",
                ex);
        }
        try
        {
            await Expect(page.GetByText(new Regex("Signed in|เข้าสู่ระบบแล้ว", RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        catch (Exception ex)
        {
            var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            var session = await page.EvaluateAsync<string>(
                "async () => { const r = await fetch('/web/v1/account/session', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
            throw new InvalidOperationException(
                $"Customer account page did not show signed-in summary. Url: {page.Url}. Body: {body[..Math.Min(body.Length, 1_500)]}. Session: {session}. Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, browserDiagnostics)}",
                ex);
        }
        await Expect(page.GetByText(email, new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.Locator(".account-status-row").Filter(new() { HasTextRegex = new Regex("Customer ID|รหัสลูกค้า", RegexOptions.IgnoreCase) })).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(webBase, "/account/profile").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("Customer details|ข้อมูลลูกค้า", RegexOptions.IgnoreCase) })).ToBeVisibleAsync();
        await Expect(page.Locator("input[type='email']").First).ToHaveValueAsync(email);

        await page.GotoAsync(new Uri(webBase, "/account/addresses").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("Billing and shipping addresses|ที่อยู่ออกบิลและจัดส่ง", RegexOptions.IgnoreCase) })).ToBeVisibleAsync();
        var addressForm = page.Locator("form.account-form").Last;
        await addressForm.GetByLabel(new Regex("^(Recipient|ผู้รับ)$", RegexOptions.IgnoreCase)).FillAsync("E2E Receiver");
        await addressForm.GetByLabel(new Regex("Recipient phone|เบอร์ผู้รับ", RegexOptions.IgnoreCase)).FillAsync("+66 81 000 0000");
        await addressForm.GetByLabel(new Regex("Address line 1|ที่อยู่บรรทัดที่ 1", RegexOptions.IgnoreCase)).FillAsync("99 E2E Road");
        await addressForm.GetByLabel(new Regex("Address line 2|ที่อยู่บรรทัดที่ 2", RegexOptions.IgnoreCase)).FillAsync("Unit 1");
        await addressForm.GetByLabel(new Regex("District|เขต", RegexOptions.IgnoreCase)).FillAsync("Bang Rak");
        await addressForm.GetByLabel(new Regex("City|เมือง", RegexOptions.IgnoreCase)).FillAsync("Bangkok");
        await addressForm.GetByLabel(new Regex("Province|จังหวัด", RegexOptions.IgnoreCase)).FillAsync("Bangkok");
        await addressForm.GetByLabel(new Regex("Postal code|รหัสไปรษณีย์", RegexOptions.IgnoreCase)).FillAsync("10500");
        await addressForm.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add address|เพิ่มที่อยู่", RegexOptions.IgnoreCase) }).ClickAsync();
        await Expect(page.GetByText(new Regex("Address added|เพิ่มที่อยู่แล้ว", RegexOptions.IgnoreCase))).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GotoAsync(new Uri(webBase, "/account").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign out|ออกจากระบบ", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForURLAsync(
            url => new Uri(url).AbsolutePath == "/",
            new()
            {
                Timeout = 30_000,
                WaitUntil = WaitUntilState.Commit
            });

        await page.GotoAsync(new Uri(webBase, "/account").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForURLAsync(
            url => url.Contains("/auth/sign-in", StringComparison.OrdinalIgnoreCase),
            new()
            {
                Timeout = 30_000,
                WaitUntil = WaitUntilState.Commit
            });
    }

    /// <summary>
    /// Verifies customer-owned account data cannot be mutated by another customer session and expired sessions preserve return URLs.
    /// Covers executable Web account portions of SEC-001 and SEC-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "SEC-001,SEC-003,WEB-009")]
    public async Task Web_CustomerAccountSecurity_BlocksCrossCustomerAddressMutationAndPreservesReturnUrl()
    {
        var webBase = GetEndpoint("WebBff");

        await using var customerAContext = await NewContextAsync();
        var customerAPage = await customerAContext.NewPageAsync();
        var customerAEmail = await RegisterWebCustomerAsync(customerAPage, webBase, "/account");
        await Expect(customerAPage.Locator("body")).ToContainTextAsync(customerAEmail, new() { Timeout = 30_000 });

        var createAddressResult = await customerAPage.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/web/v1/account/addresses', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        type: 'Shipping',
                        isDefault: true,
                        addressLine1: '101 Owner Road',
                        city: 'Bangkok',
                        stateProvince: 'Bangkok',
                        postalCode: '10500',
                        recipientName: 'Customer A Receiver',
                        recipientPhone: '+66810000001'
                    })
                });
                return `${r.status} ${await r.text()}`;
            }");
        Assert.StartsWith("200", createAddressResult, StringComparison.Ordinal);
        using var addressDocument = JsonDocument.Parse(createAddressResult[4..]);
        var addressId = addressDocument.RootElement.GetProperty("id").GetGuid();

        await using var customerBContext = await NewContextAsync();
        var customerBPage = await customerBContext.NewPageAsync();
        var customerBEmail = await RegisterWebCustomerAsync(customerBPage, webBase, "/account");
        await Expect(customerBPage.Locator("body")).ToContainTextAsync(customerBEmail, new() { Timeout = 30_000 });

        var crossCustomerUpdate = await customerBPage.EvaluateAsync<string>(
            @"async addressId => {
                const r = await fetch(`/web/v1/account/addresses/${addressId}`, {
                    method: 'PATCH',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        type: 'Shipping',
                        isDefault: true,
                        addressLine1: '999 Attacker Road',
                        city: 'Bangkok',
                        stateProvince: 'Bangkok',
                        postalCode: '10500',
                        recipientName: 'Customer B Receiver',
                        recipientPhone: '+66810000002',
                        version: 0
                    })
                });
                return `${r.status} ${await r.text()}`;
            }",
            addressId);
        Assert.StartsWith("404", crossCustomerUpdate, StringComparison.Ordinal);

        await customerBContext.ClearCookiesAsync();
        await customerBPage.GotoAsync(new Uri(webBase, "/account/addresses").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await customerBPage.WaitForURLAsync(
            url => url.Contains("/auth/sign-in", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions
            {
                Timeout = 30_000,
                WaitUntil = WaitUntilState.Commit
            });

        var signInUrl = new Uri(customerBPage.Url);
        Assert.Contains("returnUrl", signInUrl.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/account/addresses", Uri.UnescapeDataString(signInUrl.Query), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies an employee can publish a Commerce product and the Web storefront exposes only the published listing.
    /// Covers executable portions of WEB-008, COM-001, COM-002, COM-003, and COM-004.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "WEB-008,COM-001,COM-002,COM-003,COM-004")]
    public async Task Commerce_EmployeePublishesProduct_WebCustomerCanBrowseCartAndArchivedProductIsHidden()
    {
        await using var intranetContext = await NewContextAsync();
        var intranetPage = await intranetContext.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(intranetPage, intranetBase, "/commerce/catalog");
        var product = await CreateCommerceProductAsync(intranetPage);

        await intranetPage.GotoAsync(new Uri(intranetBase, "/commerce/catalog").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(intranetPage.Locator("body")).ToContainTextAsync(product.Title, new() { Timeout = 30_000 });
        await Expect(intranetPage.Locator("body")).ToContainTextAsync("Draft", new() { Timeout = 15_000 });

        await using var webContext = await NewContextAsync();
        var webPage = await webContext.NewPageAsync();
        var webDiagnostics = new List<string>();
        webPage.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                webDiagnostics.Add($"{message.Type}: {message.Text}");
            }
        };
        webPage.PageError += (_, exception) => webDiagnostics.Add($"pageerror: {exception}");
        webPage.Request += (_, request) =>
        {
            if (request.Url.Contains("/web/v1/checkout", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("/_blazor", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("/_framework/blazor", StringComparison.OrdinalIgnoreCase))
            {
                webDiagnostics.Add($"request: {request.Method} {request.Url}");
            }
        };
        webPage.Response += (_, response) =>
        {
            if (response.Status >= 400 ||
                response.Url.Contains("/web/v1/checkout", StringComparison.OrdinalIgnoreCase) ||
                response.Url.Contains("/_blazor", StringComparison.OrdinalIgnoreCase) ||
                response.Url.Contains("/_framework/blazor", StringComparison.OrdinalIgnoreCase))
            {
                webDiagnostics.Add($"response: {response.Status} {response.Url}");
            }
        };
        var webBase = GetEndpoint("WebBff");

        await webPage.GotoAsync(new Uri(webBase, "/shop").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await webPage.Locator(".shop-toolbar input[type='search']").FillAsync(product.Title);
        await Expect(webPage.Locator("body")).Not.ToContainTextAsync(product.Title, new() { Timeout = 5_000 });

        product = await UpdateCommerceProductStatusAsync(intranetPage, product, "Published");
        await webPage.GotoAsync(new Uri(webBase, "/shop").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(webPage.Locator("body")).ToContainTextAsync(product.Title, new() { Timeout = 30_000 });
        await webPage.GetByRole(AriaRole.Link, new() { NameString = product.Title }).First.ClickAsync();
        await webPage.WaitForURLAsync(url => url.Contains($"/shop/{product.Handle}", StringComparison.OrdinalIgnoreCase), new() { Timeout = 30_000 });
        await Expect(webPage.GetByRole(AriaRole.Heading, new() { NameString = product.Title })).ToBeVisibleAsync();
        await webPage.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add to cart|เพิ่มลงตะกร้า", RegexOptions.IgnoreCase) }).ClickAsync();
        try
        {
            await webPage.WaitForFunctionAsync(
                "handle => window.localStorage.getItem('maliev.cart.v1')?.includes(handle)",
                product.Handle,
                new() { Timeout = 15_000 });
        }
        catch (Exception ex)
        {
            var cartJson = await webPage.EvaluateAsync<string?>("() => window.localStorage.getItem('maliev.cart.v1')");
            var body = await webPage.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            throw new InvalidOperationException(
                $"Add-to-cart did not persist the product before navigation. Cart JSON: {cartJson ?? "<null>"}. Url: {webPage.Url}. Body: {body[..Math.Min(body.Length, 1_000)]}. Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, webDiagnostics)}",
                ex);
        }

        await webPage.GotoAsync(new Uri(webBase, "/cart").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        try
        {
            await Expect(webPage.Locator("body")).ToContainTextAsync(product.Title, new() { Timeout = 15_000 });
        }
        catch (Exception ex)
        {
            var cartJson = await webPage.EvaluateAsync<string?>("() => window.localStorage.getItem('maliev.cart.v1')");
            var body = await webPage.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            throw new InvalidOperationException(
                $"Cart page did not show the persisted product. Cart JSON: {cartJson ?? "<null>"}. Url: {webPage.Url}. Body: {body[..Math.Min(body.Length, 1_000)]}. Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, webDiagnostics)}",
                ex);
        }
        var quantityInput = webPage.GetByLabel(new Regex("Quantity|จำนวน", RegexOptions.IgnoreCase)).First;
        await quantityInput.FillAsync("2");
        await quantityInput.PressAsync("Tab");
        await Expect(webPage.Locator("body")).ToContainTextAsync("2,980.00", new() { Timeout = 15_000 });
        await webPage.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Continue to checkout|ดำเนินการชำระเงิน", RegexOptions.IgnoreCase) }).ClickAsync();
        await webPage.WaitForURLAsync(url => url.Contains("/auth/sign-in", StringComparison.OrdinalIgnoreCase), new() { Timeout = 30_000 });
        Assert.Contains("returnUrl", webPage.Url, StringComparison.OrdinalIgnoreCase);

        var checkoutCustomerEmail = await RegisterWebCustomerAsync(webPage, webBase, "/cart");
        await Expect(webPage.Locator("body")).ToContainTextAsync(product.Title, new() { Timeout = 30_000 });
        var accountSession = await webPage.EvaluateAsync<string>(
            "async () => { const r = await fetch('/web/v1/account/session', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200", accountSession, StringComparison.Ordinal);
        Assert.Contains(checkoutCustomerEmail, accountSession, StringComparison.OrdinalIgnoreCase);
        await webPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var storedCartJsonBeforeCheckout = await webPage.EvaluateAsync<string?>("() => window.localStorage.getItem('maliev.cart.v1')");
        var hiddenCartJsonBeforeCheckout = await webPage.Locator("input[name='ItemsJson']").InputValueAsync();
        var checkoutFormSyncAvailable = await webPage.EvaluateAsync<bool>("() => typeof window.malievCart?.syncCheckoutForm === 'function'");
        Assert.Contains(product.Handle, storedCartJsonBeforeCheckout ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(checkoutFormSyncAvailable, "Cart checkout form localStorage sync script was not loaded.");
        var checkoutButton = webPage.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Continue to checkout|ดำเนินการชำระเงิน", RegexOptions.IgnoreCase) });
        await Expect(checkoutButton).ToBeEnabledAsync(new() { Timeout = 30_000 });
        var checkoutNavigationTask = webPage.WaitForURLAsync(
            url => url.Contains("checkout=ready", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions
            {
                Timeout = 45_000,
                WaitUntil = WaitUntilState.Commit
            });
        await checkoutButton.ClickAsync();
        try
        {
            await checkoutNavigationTask;
        }
        catch (TimeoutException ex)
        {
            var buttonMarkup = await checkoutButton.EvaluateAsync<string>("button => button.outerHTML");
            var blazorState = await webPage.EvaluateAsync<string>(
                "() => JSON.stringify({ hasBlazor: !!window.Blazor, scripts: Array.from(document.scripts).map(s => s.src).filter(src => src.includes('blazor')) })");
            var checkoutApiDiagnostic = await webPage.EvaluateAsync<string>(
                @"async cartJson => {
                    const items = JSON.parse(cartJson || '[]');
                    const r = await fetch('/web/v1/checkout/draft', {
                        method: 'POST',
                        credentials: 'include',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ culture: document.documentElement.dataset.culture || 'en-US', items })
                    });
                    return `${r.status} ${await r.text()}`;
                }",
                storedCartJsonBeforeCheckout);
            var body = await webPage.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            throw new TimeoutException(
                $"Checkout button click did not complete the checkout form flow. Button: {buttonMarkup}. Blazor: {blazorState}. Stored cart JSON before checkout: {storedCartJsonBeforeCheckout ?? "<null>"}. Hidden cart JSON before checkout: {hiddenCartJsonBeforeCheckout}. Checkout API diagnostic: {checkoutApiDiagnostic}. Url: {webPage.Url}. Body: {body[..Math.Min(body.Length, 1_000)]}. Browser diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, webDiagnostics)}",
                ex);
        }
        await Expect(webPage.Locator("body")).ToContainTextAsync(new Regex("Checkout is ready|พร้อมดำเนินการชำระเงินแล้ว", RegexOptions.IgnoreCase), new() { Timeout = 45_000 });

        await ArchiveCommerceProductAsync(intranetPage, product);
        await webPage.GotoAsync(new Uri(webBase, $"/shop/{product.Handle}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(webPage.Locator("body")).ToContainTextAsync("Product not found", new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies the QuoteEngine anonymous demo remains non-mutating and usable.
    /// Covers the demo-backed executable portions of QUOTE-002, QUOTE-003, QUOTE-004, QUOTE-018, QUOTE-019, QUOTE-020, and QUOTE-024.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "QUOTE-002,QUOTE-003,QUOTE-004,QUOTE-018,QUOTE-019,QUOTE-020,QUOTE-024")]
    public async Task QuoteEngine_AnonymousDemo_EstimatesWithoutFormalArtifacts()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var quoteBase = GetEndpoint("QuoteEngineBff");

        await page.GotoAsync(new Uri(quoteBase, "/demo").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByText("Demo only", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByText("maliev-sample-bracket.step", new() { Exact = false }).First).ToBeVisibleAsync();
        await Expect(page.GetByText("Prototype viewer", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByText("DFM checks", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByText("No customer data is created", new() { Exact = false })).ToBeVisibleAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "CNC Machining", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { NameString = "FDM 3D Printing", Exact = true }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { NameString = "Estimate" }).ClickAsync();
        await Expect(page.GetByText(new Regex("2,596\\.00\\s+THB")).First).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { NameString = "Express" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameString = "Estimate" }).ClickAsync();
        await Expect(page.GetByText(new Regex("3,504\\.60\\s+THB")).First).ToBeVisibleAsync();

        await page.GetByLabel("Quantity").FillAsync("3");
        await page.GetByRole(AriaRole.Button, new() { NameString = "Estimate" }).ClickAsync();
        await Expect(page.GetByText(new Regex("5,256\\.90\\s+THB")).First).ToBeVisibleAsync();

        var pdfButton = page.GetByRole(AriaRole.Button, new() { NameString = "PDF" }).First;
        await Expect(pdfButton).ToBeDisabledAsync();
    }

    /// <summary>
    /// Verifies signed customer project mode is gated before customer-owned uploads.
    /// Covers the authentication-gated entry portions of QUOTE-001, QUOTE-005, and QUOTE-017.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "QUOTE-001,QUOTE-005,QUOTE-017")]
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
    /// Verifies the prototype-backed signed customer path can upload, estimate, create a quote, create an order,
    /// and expose the resulting history through account APIs. This is intentionally partial until QuoteEngine is
    /// service-backed by ProjectService, UploadService, GeometryService, PricingService, QuotationService, and OrderService.
    /// Covers executable prototype portions of QUOTE-001, QUOTE-002, QUOTE-003, QUOTE-005, QUOTE-006, QUOTE-007,
    /// QUOTE-009, QUOTE-011, QUOTE-012, QUOTE-015, QUOTE-017, QUOTE-020, QUOTE-022, and QUOTE-025.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "QUOTE-001,QUOTE-002,QUOTE-003,QUOTE-005,QUOTE-006,QUOTE-007,QUOTE-009,QUOTE-011,QUOTE-012,QUOTE-015,QUOTE-017,QUOTE-020,QUOTE-022,QUOTE-025")]
    public async Task QuoteEngine_PrototypeSignedCustomer_UploadsEstimatesQuotesOrdersAndRecordsHistory()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var quoteBase = GetEndpoint("QuoteEngineBff");
        var unique = Guid.NewGuid().ToString("N")[..8];
        var fileName = $"quote-engine-e2e-{unique}.step";

        await SignInToQuoteEngineAsync(page, quoteBase, $"quote.customer.{unique}@example.com");
        await Expect(page.GetByText("Signed-in customer project boundary", new() { Exact = false })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.Locator("#quote-cad-files").SetInputFilesAsync(new FilePayload
        {
            Name = fileName,
            MimeType = "application/step",
            Buffer = System.Text.Encoding.UTF8.GetBytes(
                """
                ISO-10303-21;
                HEADER;
                FILE_DESCRIPTION(('MALIEV QuoteEngine prototype E2E'),'2;1');
                FILE_NAME('quote-engine-e2e.step','2026-05-16T00:00:00',('MALIEV'),('MALIEV'),'Aspire E2E','MALIEV','');
                ENDSEC;
                DATA;
                ENDSEC;
                END-ISO-10303-21;
                """)
        });

        await Expect(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(Regex.Escape(fileName), RegexOptions.IgnoreCase) }).First).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByText("ANALYZED", new() { Exact = false })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByText("Threaded features should be confirmed", new() { Exact = false })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.Locator("[role='status']")).ToContainTextAsync($"Analysis complete for {fileName}", new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "CNC Machining", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Aluminum 6061", RegexOptions.IgnoreCase) }).First).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.GetByLabel("DFM reviewed").CheckAsync();
        await page.GetByLabel("Quantity").FillAsync("2");
        await page.GetByRole(AriaRole.Button, new() { NameString = "Estimate" }).ClickAsync();
        await Expect(page.Locator(".qe-qsb-total")).ToContainTextAsync(new Regex(@"[0-9,.]+\s+THB", RegexOptions.IgnoreCase), new() { Timeout = 30_000 });
        var quoteButton = page.GetByRole(AriaRole.Button, new() { NameString = "Quote", Exact = true });
        await Expect(quoteButton).ToBeEnabledAsync(new() { Timeout = 30_000 });

        await quoteButton.ClickAsync();
        await Expect(page.GetByText(new Regex(@"MQ-\d{8}-\d{4}"))).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "Create order" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Create order" }).ClickAsync();
        await Expect(page.GetByText(new Regex(@"MO-\d{8}-\d{4}"))).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByText("Order received", new() { Exact = false })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var accountState = await page.EvaluateAsync<string>(
            @"async () => {
                const [profile, quotes, orders] = await Promise.all([
                    fetch('/quote/v1/account/profile', { credentials: 'include' }),
                    fetch('/quote/v1/account/quotes', { credentials: 'include' }),
                    fetch('/quote/v1/account/orders', { credentials: 'include' })
                ]);
                return JSON.stringify({
                    profileStatus: profile.status,
                    profile: await profile.json(),
                    quotesStatus: quotes.status,
                    quotes: await quotes.json(),
                    ordersStatus: orders.status,
                    orders: await orders.json()
                });
            }");
        using var accountDocument = JsonDocument.Parse(accountState);
        var root = accountDocument.RootElement;
        Assert.Equal(200, GetJsonInt(root, "profileStatus"));
        Assert.Equal(200, GetJsonInt(root, "quotesStatus"));
        Assert.Equal(200, GetJsonInt(root, "ordersStatus"));
        Assert.True(TryGetJsonProperty(root, out var quotes, "quotes"));
        Assert.Contains(quotes.EnumerateArray(), quote => GetJsonString(quote, "quotationNumber", "quoteNumber", "QuoteNumber").StartsWith("MQ-", StringComparison.Ordinal));
        Assert.True(TryGetJsonProperty(root, out var orders, "orders"));
        Assert.Contains(orders.EnumerateArray(), order => GetJsonString(order, "orderNumber", "OrderNumber").StartsWith("MO-", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies QuoteEngine customer quote/order history is scoped to the signed-in customer boundary.
    /// Covers the QuoteEngine-owned executable portion of SEC-001 for quote and order records.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "SEC-001,QUOTE-009,QUOTE-011,QUOTE-012")]
    public async Task QuoteEngine_CustomerIsolation_BlocksCrossCustomerQuoteOrderHistoryAccess()
    {
        var quoteBase = GetEndpoint("QuoteEngineBff");
        var unique = Guid.NewGuid().ToString("N")[..8];

        await using var customerAContext = await NewContextAsync();
        var customerAPage = await customerAContext.NewPageAsync();
        await SignInToQuoteEngineAsync(customerAPage, quoteBase, $"quote.owner.a.{unique}@example.com");
        var customerAState = await customerAPage.EvaluateAsync<string>(
            @"async () => {
                const quoteResponse = await fetch('/quote/v1/quotes/formal', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        projectId: crypto.randomUUID(),
                        quoteSessionId: `isolation-${crypto.randomUUID()}`,
                        parts: [],
                        notes: 'Customer A quote.'
                    })
                });
                const quote = await quoteResponse.json();
                const orderResponse = await fetch('/quote/v1/orders', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        quoteId: quote.quoteId,
                        customerPoNumber: '',
                        notes: 'Customer A accepted.'
                    })
                });
                const order = await orderResponse.json();
                const quotesResponse = await fetch('/quote/v1/account/quotes', { credentials: 'include' });
                const ordersResponse = await fetch('/quote/v1/account/orders', { credentials: 'include' });
                return JSON.stringify({
                    quoteStatus: quoteResponse.status,
                    orderStatus: orderResponse.status,
                    quote,
                    order,
                    quotesStatus: quotesResponse.status,
                    quotes: await quotesResponse.json(),
                    ordersStatus: ordersResponse.status,
                    orders: await ordersResponse.json()
                });
            }");
        using var customerADocument = JsonDocument.Parse(customerAState);
        var customerARoot = customerADocument.RootElement;
        Assert.Equal(200, GetJsonInt(customerARoot, "quoteStatus"));
        Assert.Equal(200, GetJsonInt(customerARoot, "orderStatus"));
        Assert.True(TryGetJsonProperty(customerARoot, out var customerAQuote, "quote"));
        Assert.True(TryGetJsonProperty(customerARoot, out var customerAOrder, "order"));
        var customerAQuoteId = GetJsonString(customerAQuote, "quoteId", "QuoteId");
        var customerAOrderId = GetJsonString(customerAOrder, "orderId", "OrderId");
        Assert.StartsWith("MQ-", GetJsonString(customerAQuote, "quoteNumber", "QuoteNumber"), StringComparison.Ordinal);
        Assert.StartsWith("MO-", GetJsonString(customerAOrder, "orderNumber", "OrderNumber"), StringComparison.Ordinal);
        Assert.True(TryGetJsonProperty(customerARoot, out var customerAQuotes, "quotes"));
        Assert.Contains(customerAQuotes.EnumerateArray(), quote => string.Equals(customerAQuoteId, GetJsonString(quote, "quoteId", "QuoteId"), StringComparison.OrdinalIgnoreCase));
        Assert.True(TryGetJsonProperty(customerARoot, out var customerAOrders, "orders"));
        Assert.Contains(customerAOrders.EnumerateArray(), order => string.Equals(customerAOrderId, GetJsonString(order, "orderId", "OrderId"), StringComparison.OrdinalIgnoreCase));

        await using var customerBContext = await NewContextAsync();
        var customerBPage = await customerBContext.NewPageAsync();
        await SignInToQuoteEngineAsync(customerBPage, quoteBase, $"quote.owner.b.{unique}@example.com");
        var customerBState = await customerBPage.EvaluateAsync<string>(
            @"async quoteId => {
                const profileResponse = await fetch('/quote/v1/account/profile', { credentials: 'include' });
                const quotesResponse = await fetch('/quote/v1/account/quotes', { credentials: 'include' });
                const ordersResponse = await fetch('/quote/v1/account/orders', { credentials: 'include' });
                const crossOrderResponse = await fetch('/quote/v1/orders', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        quoteId,
                        customerPoNumber: '',
                        notes: 'Cross-customer order attempt.'
                    })
                });
                return JSON.stringify({
                    profileStatus: profileResponse.status,
                    profile: await profileResponse.json(),
                    quotesStatus: quotesResponse.status,
                    quotes: await quotesResponse.json(),
                    ordersStatus: ordersResponse.status,
                    orders: await ordersResponse.json(),
                    crossOrderStatus: crossOrderResponse.status
                });
            }",
            customerAQuoteId);
        using var customerBDocument = JsonDocument.Parse(customerBState);
        var customerBRoot = customerBDocument.RootElement;
        Assert.Equal(200, GetJsonInt(customerBRoot, "profileStatus"));
        Assert.Equal(200, GetJsonInt(customerBRoot, "quotesStatus"));
        Assert.Equal(200, GetJsonInt(customerBRoot, "ordersStatus"));
        Assert.Equal(404, GetJsonInt(customerBRoot, "crossOrderStatus"));
        Assert.True(TryGetJsonProperty(customerBRoot, out var customerBProfile, "profile"));
        Assert.Equal($"quote.owner.b.{unique}@example.com", GetJsonString(customerBProfile, "email", "Email"));
        Assert.True(TryGetJsonProperty(customerBRoot, out var customerBQuotes, "quotes"));
        Assert.DoesNotContain(customerBQuotes.EnumerateArray(), quote => string.Equals(customerAQuoteId, GetJsonString(quote, "quoteId", "QuoteId"), StringComparison.OrdinalIgnoreCase));
        Assert.True(TryGetJsonProperty(customerBRoot, out var customerBOrders, "orders"));
        Assert.DoesNotContain(customerBOrders.EnumerateArray(), order => string.Equals(customerAOrderId, GetJsonString(order, "orderId", "OrderId"), StringComparison.OrdinalIgnoreCase));
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

        await page.GotoAsync(new Uri(intranetBase, "/sales/projects/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForURLAsync(url => url.Contains("/login", StringComparison.OrdinalIgnoreCase));
        await Expect(page.GetByText("Sign in with Google", new() { Exact = false })).ToBeVisibleAsync();
        Assert.Contains("returnUrl", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies an employee with only profile permissions cannot access restricted Intranet modules by API or direct URL.
    /// Covers the authenticated permission-boundary portion of SEC-002.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "SEC-002,INT-001")]
    public async Task Intranet_LimitedEmployee_CannotAccessRestrictedModuleApis()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(
            page,
            intranetBase,
            "/hr/profile",
            _fixture.AspireTestLimitedEmployeeEmail,
            _fixture.AspireTestLimitedEmployeePassword,
            permissionState =>
                !permissionState.HasWildcard &&
                permissionState.HasPermission("auth.sessions.read") &&
                permissionState.HasPermission("employee.profiles.read") &&
                permissionState.HasPermission("employee.profiles.update"),
            "limited employee profile permissions");

        var permissionState = await GetIntranetPermissionStateAsync(page);
        Assert.True(permissionState.IsAuthenticated, permissionState.Diagnostic);
        Assert.False(permissionState.HasWildcard, permissionState.Diagnostic);
        Assert.DoesNotContain("iam.roles.list", permissionState.Permissions);
        Assert.DoesNotContain("employee.employees.write", permissionState.Permissions);

        var profileResponse = await page.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/api/v1/employees/me/profile', { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }");
        Assert.StartsWith("200", profileResponse, StringComparison.Ordinal);
        Assert.Contains(_fixture.AspireTestLimitedEmployeeEmail, profileResponse, StringComparison.OrdinalIgnoreCase);

        await page.GotoAsync(new Uri(intranetBase, "/iam").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.DoesNotContain("/login", page.Url, StringComparison.OrdinalIgnoreCase);

        var restrictedResponses = await page.EvaluateAsync<string[]>(
            @"async () => {
                const endpoints = [
                    '/api/v1/iam/users',
                    '/api/v1/iam/roles',
                    '/api/v1/employees',
                    '/api/v1/search?query=customer&limit=5'
                ];
                const results = [];
                for (const endpoint of endpoints) {
                    const r = await fetch(endpoint, { credentials: 'include' });
                    const body = await r.text();
                    results.push(`${r.status} ${endpoint} ${body.slice(0, 120)}`);
                }
                return results;
            }");

        Assert.All(restrictedResponses, response => Assert.StartsWith("403", response, StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies a limited employee can update their own profile without receiving broad employee-management access.
    /// Covers the self-service employee profile portion of HR-001 and the permission-shaped behavior of INT-001.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "HR-001,INT-001,SEC-002")]
    public async Task Intranet_LimitedEmployee_CanUpdateOwnProfileOnly()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(
            page,
            intranetBase,
            "/hr/profile",
            _fixture.AspireTestLimitedEmployeeEmail,
            _fixture.AspireTestLimitedEmployeePassword,
            permissionState =>
                !permissionState.HasWildcard &&
                permissionState.HasPermission("employee.profiles.read") &&
                permissionState.HasPermission("employee.profiles.update"),
            "limited employee profile read/update permissions");

        await Expect(page.Locator("body")).ToContainTextAsync("My Profile", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(_fixture.AspireTestLimitedEmployeeEmail, new() { Timeout = 30_000 });

        var unique = Guid.NewGuid().ToString("N")[..8];
        var preferredName = $"Limited {unique}";
        var personalEmail = $"limited.{unique}@example.com";
        var mobilePhone = $"+6681{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10000000:0000000}";

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Edit profile", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.Locator(".profile-field").Filter(new() { HasText = "Preferred name" }).Locator("input").FillAsync(preferredName);
        await page.Locator(".profile-field").Filter(new() { HasText = "Personal email" }).Locator("input").FillAsync(personalEmail);
        await page.Locator(".profile-field").Filter(new() { HasText = "Mobile phone" }).Locator("input").FillAsync(mobilePhone);
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save changes", RegexOptions.IgnoreCase) }).ClickAsync();

        await Expect(page.Locator("body")).ToContainTextAsync(preferredName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(personalEmail, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(mobilePhone, new() { Timeout = 30_000 });

        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(preferredName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(personalEmail, new() { Timeout = 30_000 });

        var employeeListResponse = await page.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/api/v1/employees', { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }");
        Assert.StartsWith("403", employeeListResponse, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a limited employee can submit a leave request and the seeded manager can approve it.
    /// Covers the currently executable employee/manager leave journey in HR-002.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "HR-002")]
    public async Task Intranet_LeaveRequest_LimitedEmployeeSubmitsAndManagerApproves()
    {
        await AspireTestData.EnsureAnnualLeavePolicyAsync(_fixture);

        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..10];
        var reason = $"E2E planned leave {unique}";
        var startDate = DateTime.UtcNow.Date.AddDays(14 + Random.Shared.Next(1, 30));
        var endDate = startDate.AddDays(1);

        await SignInToIntranetAsync(
            page,
            intranetBase,
            "/hr/leave",
            _fixture.AspireTestLimitedEmployeeEmail,
            _fixture.AspireTestLimitedEmployeePassword,
            permissionState =>
                permissionState.HasPermission("leave.balances.read") &&
                permissionState.HasPermission("leave.requests.read") &&
                permissionState.HasPermission("leave.requests.create"),
            "limited employee leave read/create permissions");

        await Expect(page.Locator("body")).ToContainTextAsync("Leave Management", new() { Timeout = 30_000 });
        await page.GetByLabel("Leave type").SelectOptionAsync("Annual");
        await page.GetByLabel("Start date").FillAsync(startDate.ToString("yyyy-MM-dd"));
        await page.GetByLabel("End date").FillAsync(endDate.ToString("yyyy-MM-dd"));
        await page.GetByLabel("Reason").FillAsync(reason);

        var submitResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/timeoff/requests", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Submit leave request" }).ClickAsync();
        var submitResponse = await submitResponseTask;
        var submitBody = await ReadResponseTextOrEmptyAsync(submitResponse);
        Assert.True(submitResponse.Ok, $"Leave request submission failed with HTTP {submitResponse.Status}: {submitBody}");
        using var submitDocument = JsonDocument.Parse(submitBody);
        var leaveRequestId = submitDocument.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, leaveRequestId);
        await Expect(page.Locator("body")).ToContainTextAsync("Pending", new() { Timeout = 30_000 });

        await page.GotoAsync(new Uri(intranetBase, "/api/v1/auth/logout").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Commit });

        await SignInToIntranetAsync(page, intranetBase, "/hr/leave");
        await Expect(page.Locator("body")).ToContainTextAsync("Leave Management", new() { Timeout = 30_000 });
        await Expect(page.Locator(".mlv-list-row").Filter(new() { HasText = reason })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var decisionResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/timeoff/requests/{leaveRequestId}/decision", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.Locator(".mlv-list-row")
            .Filter(new() { HasText = reason })
            .GetByRole(AriaRole.Button, new() { NameString = "Approve request" })
            .ClickAsync();
        var decisionResponse = await decisionResponseTask;
        var decisionBody = await ReadResponseTextOrEmptyAsync(decisionResponse);
        Assert.True(decisionResponse.Ok, $"Leave approval failed with HTTP {decisionResponse.Status}: {decisionBody}");
        await Expect(page.Locator(".mlv-list-row").Filter(new() { HasText = reason })).ToBeHiddenAsync(new() { Timeout = 30_000 });

        var managerApprovalsResult = await page.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/api/v1/timeoff/approvals', { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }");
        Assert.StartsWith("200 ", managerApprovalsResult, StringComparison.Ordinal);
        Assert.DoesNotContain(leaveRequestId.ToString(), managerApprovalsResult, StringComparison.OrdinalIgnoreCase);

        await page.GotoAsync(new Uri(intranetBase, "/api/v1/auth/logout").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
        await SignInToIntranetAsync(
            page,
            intranetBase,
            "/hr/leave",
            _fixture.AspireTestLimitedEmployeeEmail,
            _fixture.AspireTestLimitedEmployeePassword,
            permissionState =>
                permissionState.HasPermission("leave.balances.read") &&
                permissionState.HasPermission("leave.requests.read") &&
                permissionState.HasPermission("leave.requests.create"),
            "limited employee leave read/create permissions");

        var employeeRequestsResult = await page.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/api/v1/timeoff/requests', { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }");
        Assert.StartsWith("200 ", employeeRequestsResult, StringComparison.Ordinal);
        using var employeeRequestsDocument = JsonDocument.Parse(employeeRequestsResult[4..]);
        Assert.Contains(employeeRequestsDocument.RootElement.EnumerateArray(), request =>
            string.Equals(leaveRequestId.ToString(), GetJsonString(request, "id", "Id"), StringComparison.OrdinalIgnoreCase) &&
            string.Equals("Approved", GetJsonString(request, "status", "Status"), StringComparison.Ordinal));

        await Expect(page.Locator("body")).ToContainTextAsync("Approved", new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies the manager dashboard detects pending work, shows source-service business widgets,
    /// and navigates from the action item into the approval workflow.
    /// Covers the executable dashboard/business overview portion of INT-014.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-014,HR-002")]
    public async Task Intranet_DashboardBusinessOverview_SurfacesPendingLeaveApproval()
    {
        await AspireTestData.EnsureAnnualLeavePolicyAsync(_fixture);

        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..10];
        var reason = $"E2E dashboard leave action {unique}";
        var startDate = DateTime.UtcNow.Date.AddDays(45 + Random.Shared.Next(1, 30));
        var endDate = startDate.AddDays(1);

        await SignInToIntranetAsync(
            page,
            intranetBase,
            "/hr/leave",
            _fixture.AspireTestLimitedEmployeeEmail,
            _fixture.AspireTestLimitedEmployeePassword,
            permissionState =>
                permissionState.HasPermission("leave.balances.read") &&
                permissionState.HasPermission("leave.requests.read") &&
                permissionState.HasPermission("leave.requests.create"),
            "limited employee leave read/create permissions");

        await page.GetByLabel("Leave type").SelectOptionAsync("Annual");
        await page.GetByLabel("Start date").FillAsync(startDate.ToString("yyyy-MM-dd"));
        await page.GetByLabel("End date").FillAsync(endDate.ToString("yyyy-MM-dd"));
        await page.GetByLabel("Reason").FillAsync(reason);

        var submitResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/timeoff/requests", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Submit leave request" }).ClickAsync();
        var submitResponse = await submitResponseTask;
        var submitBody = await ReadResponseTextOrEmptyAsync(submitResponse);
        Assert.True(submitResponse.Ok, $"Dashboard fixture leave request failed with HTTP {submitResponse.Status}: {submitBody}");
        using var submitDocument = JsonDocument.Parse(submitBody);
        var leaveRequestId = submitDocument.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, leaveRequestId);

        await page.GotoAsync(new Uri(intranetBase, "/api/v1/auth/logout").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Commit });

        await SignInToIntranetAsync(page, intranetBase, "/");
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Dashboard" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var dashboardResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/dashboard', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", dashboardResult, StringComparison.Ordinal);
        using (var dashboardDocument = JsonDocument.Parse(dashboardResult[4..]))
        {
            var dashboardRoot = dashboardDocument.RootElement;
            Assert.True(TryGetJsonProperty(dashboardRoot, out var widgets, "widgets", "Widgets"));
            var sourceServices = widgets
                .EnumerateArray()
                .Select(widget => GetJsonString(widget, "sourceService", "SourceService"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            Assert.Contains("PaymentService", sourceServices);
            Assert.Contains("OrderService", sourceServices);
            Assert.Contains("QuotationService", sourceServices);
            Assert.Contains("EmployeeService", sourceServices);
        }

        await WaitForIntranetApiTextContainsAsync(page, "/api/v1/dashboard/action-items", "leave request");
        var actionItemsResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/dashboard/action-items', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", actionItemsResult, StringComparison.Ordinal);
        using (var actionItemsDocument = JsonDocument.Parse(actionItemsResult[4..]))
        {
            var actionItemsRoot = actionItemsDocument.RootElement;
            Assert.True(TryGetJsonProperty(actionItemsRoot, out var categories, "categories", "Categories"));
            Assert.Contains(categories.EnumerateArray(), category =>
                GetJsonString(category, "label", "Label").Contains("leave request", StringComparison.OrdinalIgnoreCase) &&
                GetJsonDouble(category, "count", "Count") >= 1 &&
                GetJsonString(category, "navigateTo", "NavigateTo").Contains("/hr/leave", StringComparison.OrdinalIgnoreCase));
        }

        var leaveAction = page.Locator(".mlv-list-row").Filter(new() { HasText = "leave request" }).First;
        await Expect(leaveAction).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await leaveAction.ClickAsync();
        await page.WaitForURLAsync(
            url => new Uri(url).AbsolutePath.Equals("/hr/leave", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000, WaitUntil = WaitUntilState.NetworkIdle });

        var approvalRow = page.Locator(".mlv-list-row").Filter(new() { HasText = reason });
        await Expect(approvalRow).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var decisionResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/timeoff/requests/{leaveRequestId}/decision", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await approvalRow.GetByRole(AriaRole.Button, new() { NameString = "Approve request" }).ClickAsync();
        var decisionResponse = await decisionResponseTask;
        var decisionBody = await ReadResponseTextOrEmptyAsync(decisionResponse);
        Assert.True(decisionResponse.Ok, $"Dashboard cleanup leave approval failed with HTTP {decisionResponse.Status}: {decisionBody}");
        await Expect(approvalRow).ToBeHiddenAsync(new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies the authenticated Intranet AI assistant opens from the employee shell, reaches ChatbotService,
    /// executes a quotation operation prompt, and keeps suggested-action context for a reminder follow-up.
    /// Covers the executable assistant/tool-callback portion of INT-015.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-015")]
    public async Task Intranet_AiAssistant_ExecutesQuotationOperationAndSuggestedAction()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var diagnostics = new List<string>();

        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                diagnostics.Add($"{message.Type}: {message.Text}");
            }
        };
        page.PageError += (_, exception) => diagnostics.Add($"pageerror: {exception}");
        page.RequestFailed += (_, request) =>
            diagnostics.Add($"request-failed: {request.Method} {request.Url} {request.Failure}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400 &&
                (response.Url.Contains("/api/v1/chat", StringComparison.OrdinalIgnoreCase) ||
                 response.Url.Contains("/api/v1/aiprocessing", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add($"response: {response.Status} {response.Url}");
            }
        };

        await SignInToIntranetAsync(page, intranetBase, "/");

        var aiHealth = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/aiprocessing/health', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", aiHealth, StringComparison.Ordinal);
        Assert.Contains("canInitiateSession", aiHealth, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("true", aiHealth, StringComparison.OrdinalIgnoreCase);

        await page.Locator(".topbar-chat-toggle").ClickAsync();
        var assistant = page.GetByLabel("AI assistant conversation");
        await Expect(assistant).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByText("How can I help?", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 60_000 });

        var sessionResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/chat/session", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });
        var quoteResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/chat/message", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.Locator(".sidekick-composer-input").FillAsync("Check quotation Q-2026-000001 and suggest next action");
        await page.GetByLabel("Send message").ClickAsync();

        var sessionResponse = await sessionResponseTask;
        var sessionBody = await ReadResponseTextOrEmptyAsync(sessionResponse);
        Assert.True(sessionResponse.Ok, $"Chat session initiation failed with HTTP {sessionResponse.Status}: {sessionBody}. Diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}");

        var quoteResponse = await quoteResponseTask;
        var quoteBody = await ReadResponseTextOrEmptyAsync(quoteResponse);
        Assert.True(quoteResponse.Ok, $"Chat quotation operation failed with HTTP {quoteResponse.Status}: {quoteBody}. Diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}");
        Assert.Contains("Q-2026-000001", quoteBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Send Reminder", quoteBody, StringComparison.OrdinalIgnoreCase);

        await Expect(page.Locator("body")).ToContainTextAsync("Quotation Q-2026-000001", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("ABC Manufacturing", new() { Timeout = 30_000 });
        await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "Send Reminder" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var reminderResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/chat/message", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Send Reminder" }).ClickAsync();
        var reminderResponse = await reminderResponseTask;
        var reminderBody = await ReadResponseTextOrEmptyAsync(reminderResponse);
        Assert.True(reminderResponse.Ok, $"Chat suggested reminder failed with HTTP {reminderResponse.Status}: {reminderBody}. Diagnostics:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}");
        Assert.Contains("Reminder sent successfully", reminderBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Q-2026-000001", reminderBody, StringComparison.OrdinalIgnoreCase);
        await Expect(page.Locator("body")).ToContainTextAsync("Reminder sent successfully for quotation Q-2026-000001", new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies the Aspire-local automation employee can sign into Intranet and reach protected ERP surfaces.
    /// Covers the authenticated entry portions of INT-001, INT-014, OPS-001, OPS-002, and SEC-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-001,INT-014,OPS-001,OPS-002,SEC-003")]
    public async Task Intranet_AutomationEmployee_SignsInAndReachesProtectedSurfaces()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var browserDiagnostics = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                browserDiagnostics.Add($"{message.Type}: {message.Text}");
            }
        };
        page.PageError += (_, exception) => browserDiagnostics.Add($"pageerror: {exception}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                browserDiagnostics.Add($"response: {response.Status} {response.Url}");
            }
        };
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(page, intranetBase, "/");
        try
        {
            await Expect(page.Locator("body")).ToContainTextAsync("Dashboard", new() { Timeout = 15_000 });
            await Expect(page.Locator("body")).ToContainTextAsync("Codex Admin", new() { Timeout = 15_000 });
        }
        catch (Exception ex)
        {
            var diagnostics = string.Join(Environment.NewLine, browserDiagnostics);
            throw new InvalidOperationException($"Intranet did not start after sign-in. Browser diagnostics:{Environment.NewLine}{diagnostics}", ex);
        }

        await page.GotoAsync(new Uri(intranetBase, "/admin/system-health").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(new Regex("health|service|system", RegexOptions.IgnoreCase), new() { Timeout = 15_000 });

        await page.GotoAsync(new Uri(intranetBase, "/sales/projects/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.DoesNotContain("/login", page.Url, StringComparison.OrdinalIgnoreCase);
        await Expect(page.Locator("body")).ToContainTextAsync(new Regex("project|quote|customer", RegexOptions.IgnoreCase), new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Verifies the operational health page reports critical IAM and Geometry service readiness through the browser session.
    /// Covers the authenticated operational monitoring portion of OPS-002.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "OPS-002,INT-014")]
    public async Task Intranet_SystemHealth_ShowsIamAndGeometryReadiness()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(page, intranetBase, "/admin/system-health");
        await page.GotoAsync(new Uri(intranetBase, "/admin/system-health").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "System Health" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Auto-refresh every", new() { Timeout = 15_000 });

        var healthResult = await WaitForSystemHealthAsync(page, "AuthService", "IAMService", "GeometryService");
        using var healthDocument = JsonDocument.Parse(healthResult.Body);
        var healthRoot = healthDocument.RootElement;

        Assert.NotEqual("Unhealthy", GetJsonString(healthRoot, "overallStatus", "OverallStatus"));
        Assert.True(TryGetJsonProperty(healthRoot, out var services, "services", "Services"));
        Assert.True(services.GetArrayLength() >= 30, $"Expected the health page to include the MALIEV service ecosystem, but found {services.GetArrayLength()} services.");

        AssertCriticalHealthService(services, "AuthService", "/auth/liveness", "/auth/readiness");
        AssertCriticalHealthService(services, "IAMService", "/iam/liveness", "/iam/readiness");
        AssertCriticalHealthService(services, "GeometryService", "/geometry/liveness", "/geometry/readiness");

        await Expect(page.Locator("body")).ToContainTextAsync("IAMService", new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("GeometryService", new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("/geometry/liveness", new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("/geometry/readiness", new() { Timeout = 15_000 });

        var historyResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/system-health/history?days=7', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", historyResult, StringComparison.Ordinal);

        using var historyDocument = JsonDocument.Parse(historyResult[4..]);
        var historyRoot = historyDocument.RootElement;
        Assert.True(TryGetJsonProperty(historyRoot, out var historyServices, "services", "Services"));
        Assert.Contains(historyServices.EnumerateArray(), service =>
            string.Equals("IAMService", GetJsonString(service, "serviceName", "ServiceName"), StringComparison.Ordinal));
        Assert.Contains(historyServices.EnumerateArray(), service =>
            string.Equals("GeometryService", GetJsonString(service, "serviceName", "ServiceName"), StringComparison.Ordinal));

        await page.GetByRole(AriaRole.Button, new() { NameString = "Refresh" }).ClickAsync();
        await Expect(page.Locator(".system-health-probe-grid").First).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies an Intranet admin can browse paginated reference data and manage RegistryService Thai address records.
    /// Covers the executable reference-data maintenance portion of OPS-001 and INT-014.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "OPS-001,INT-014")]
    public async Task Intranet_ReferenceDataWorkbench_ManagesRegistryLocationsAndLoadsReferencePages()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..8];
        var postalCode = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 90_000 + 10_000:D5}";
        var subdistrictTh = $"ตำบลทดสอบ {unique}";
        var districtTh = $"อำเภอทดสอบ {unique}";
        var provinceTh = $"จังหวัดทดสอบ {unique}";
        var subdistrictEn = $"E2E Subdistrict {unique}";
        var districtEn = $"E2E District {unique}";
        var provinceEn = $"E2E Province {unique}";
        var updatedDistrictEn = $"E2E Updated District {unique}";

        await SignInToIntranetAsync(page, intranetBase, "/admin/reference-data?section=registry");
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Reference Data" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByRole(AriaRole.Tab, new() { NameString = "Countries" })).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(page.GetByRole(AriaRole.Tab, new() { NameString = "Currencies" })).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(page.GetByRole(AriaRole.Tab, new() { NameString = "Registry locations" })).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("RegistryService Thai Locations", new() { Timeout = 30_000 });

        var countryPageResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/ReferenceData/countries/page?pageNumber=1&pageSize=10&includeInactive=true', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", countryPageResult, StringComparison.Ordinal);
        using (var countryDocument = JsonDocument.Parse(countryPageResult[4..]))
        {
            Assert.True(GetJsonInt(countryDocument.RootElement, "totalCount", "TotalCount") > 0, "CountryService should return paginated country reference data.");
            Assert.True(TryGetJsonProperty(countryDocument.RootElement, out var countries, "items", "Items"));
            Assert.True(countries.GetArrayLength() > 0, "CountryService page should include country items.");
        }

        var currencyPageResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/ReferenceData/currencies/page?pageNumber=1&pageSize=10', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", currencyPageResult, StringComparison.Ordinal);
        using (var currencyDocument = JsonDocument.Parse(currencyPageResult[4..]))
        {
            Assert.True(GetJsonInt(currencyDocument.RootElement, "totalCount", "TotalCount") > 0, "CurrencyService should return paginated currency reference data.");
            Assert.True(TryGetJsonProperty(currencyDocument.RootElement, out var currencies, "items", "Items"));
            Assert.True(currencies.GetArrayLength() > 0, "CurrencyService page should include currency items.");
        }

        var locationPageResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/ReferenceData/locations?pageNumber=1&pageSize=10', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", locationPageResult, StringComparison.Ordinal);
        using (var locationDocument = JsonDocument.Parse(locationPageResult[4..]))
        {
            Assert.True(GetJsonInt(locationDocument.RootElement, "totalCount", "TotalCount") > 0, "RegistryService should return paginated Thai address reference data.");
            Assert.True(TryGetJsonProperty(locationDocument.RootElement, out var locations, "items", "Items"));
            Assert.True(locations.GetArrayLength() > 0, "RegistryService page should include location items.");
        }

        await page.GetByRole(AriaRole.Button, new() { NameString = "New location" }).ClickAsync();
        var editor = page.Locator("[aria-label='Registry location editor']").First;
        await Expect(editor).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await editor.GetByLabel("Postal code").FillAsync(postalCode);
        await editor.GetByLabel("Province TH").FillAsync(provinceTh);
        await editor.GetByLabel("District TH").FillAsync(districtTh);
        await editor.GetByLabel("Subdistrict TH").FillAsync(subdistrictTh);
        await editor.GetByLabel("Province EN").FillAsync(provinceEn);
        await editor.GetByLabel("District EN").FillAsync(districtEn);
        await editor.GetByLabel("Subdistrict EN").FillAsync(subdistrictEn);

        var createResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/ReferenceData/locations", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await editor.GetByRole(AriaRole.Button, new() { NameString = "Save location" }).ClickAsync();
        var createResponse = await createResponseTask;
        var createBody = await ReadResponseTextOrEmptyAsync(createResponse);
        Assert.True(createResponse.Ok, $"Registry location create failed with HTTP {createResponse.Status}: {createBody}");

        using var createDocument = JsonDocument.Parse(createBody);
        var createdLocationId = createDocument.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, createdLocationId);
        Assert.Equal(postalCode, GetJsonString(createDocument.RootElement, "postalCode", "PostalCode"));
        await Expect(page.Locator("body")).ToContainTextAsync("Registry location created.", new() { Timeout = 30_000 });

        var searchInput = page.GetByLabel("Search locations");
        await searchInput.FillAsync(unique);
        await page.GetByRole(AriaRole.Button, new() { NameString = "Search" }).ClickAsync();
        var createdRow = page.Locator("tbody tr").Filter(new() { HasText = unique }).First;
        await Expect(createdRow).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(createdRow).ToContainTextAsync(postalCode);

        await createdRow.GetByRole(AriaRole.Button, new() { NameString = "Edit" }).ClickAsync();
        await Expect(editor).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await editor.GetByLabel("District EN").FillAsync(updatedDistrictEn);

        var updateResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/ReferenceData/locations/{createdLocationId}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await editor.GetByRole(AriaRole.Button, new() { NameString = "Save location" }).ClickAsync();
        var updateResponse = await updateResponseTask;
        var updateBody = await ReadResponseTextOrEmptyAsync(updateResponse);
        Assert.True(updateResponse.Ok, $"Registry location update failed with HTTP {updateResponse.Status}: {updateBody}");
        using (var updateDocument = JsonDocument.Parse(updateBody))
        {
            Assert.Equal(updatedDistrictEn, GetJsonString(updateDocument.RootElement, "districtEn", "DistrictEn"));
        }

        await Expect(page.Locator("body")).ToContainTextAsync("Registry location updated.", new() { Timeout = 30_000 });
        var updatedRow = page.Locator("tbody tr").Filter(new() { HasText = unique }).First;
        await Expect(updatedRow).ToContainTextAsync(updatedDistrictEn, new() { Timeout = 30_000 });

        var deleteResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/ReferenceData/locations/{createdLocationId}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "DELETE", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await updatedRow.GetByRole(AriaRole.Button, new() { NameString = "Delete" }).ClickAsync();
        var deleteResponse = await deleteResponseTask;
        var deleteBody = await ReadResponseTextOrEmptyAsync(deleteResponse);
        Assert.True(deleteResponse.Ok, $"Registry location delete failed with HTTP {deleteResponse.Status}: {deleteBody}");
        await Expect(page.Locator("body")).ToContainTextAsync("Registry location deleted.", new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies an Intranet admin can create an IAM user, assign an initial role, and inspect role permissions.
    /// Covers the executable IAM administration portions of INT-010 and INT-011.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-010,INT-011,SEC-002")]
    public async Task Intranet_IamAdmin_CreatesUserAssignsRoleAndViewsPermissionMatrix()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..10];
        var displayName = $"E2E IAM User {unique}";
        var email = $"e2e.iam.{unique}@maliev.local";
        const string roleId = "roles.aspire.limited";

        await SignInToIntranetAsync(page, intranetBase, "/iam");
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "IAM" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var rolesResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/iam/roles', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        Assert.StartsWith("200 ", rolesResult, StringComparison.Ordinal);

        using var rolesDocument = JsonDocument.Parse(rolesResult[4..]);
        var limitedRole = rolesDocument.RootElement.EnumerateArray().SingleOrDefault(role =>
            string.Equals(roleId, GetJsonString(role, "roleId", "RoleId"), StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(JsonValueKind.Undefined, limitedRole.ValueKind);
        var roleName = GetJsonString(limitedRole, "name", "Name");
        var rolePermissions = GetJsonStringArray(limitedRole, "permissionIds", "PermissionIds", "permissions", "Permissions");
        Assert.Contains("employee.profiles.read", rolePermissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("employee.profiles.update", rolePermissions, StringComparer.OrdinalIgnoreCase);

        await page.GetByRole(AriaRole.Button, new() { NameString = "Roles" }).ClickAsync();
        await Expect(page.Locator("body")).ToContainTextAsync(string.IsNullOrWhiteSpace(roleName) ? roleId : roleName, new() { Timeout = 30_000 });

        await page.GotoAsync(new Uri(intranetBase, $"/iam/roles/{Uri.EscapeDataString(roleId)}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(roleId, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("employee.profiles.read", new() { Timeout = 30_000 });

        var matrixResult = await page.EvaluateAsync<string>(
            "async roleId => { const r = await fetch(`/api/v1/iam/roles/${encodeURIComponent(roleId)}/permissions-matrix`, { credentials: 'include' }); return `${r.status} ${await r.text()}`; }",
            roleId);
        Assert.StartsWith("200 ", matrixResult, StringComparison.Ordinal);

        using var matrixDocument = JsonDocument.Parse(matrixResult[4..]);
        var matrixRoot = matrixDocument.RootElement;
        Assert.Equal(roleId, GetJsonString(matrixRoot, "roleId", "RoleId"));
        Assert.True(TryGetJsonProperty(matrixRoot, out var matrixPermissions, "permissions", "Permissions"));
        Assert.Contains(matrixPermissions.EnumerateArray(), permission =>
            string.Equals("employee.profiles.read", GetJsonString(permission, "permissionId", "PermissionId"), StringComparison.OrdinalIgnoreCase) &&
            GetJsonBool(permission, "granted", "Granted"));

        await page.GotoAsync(new Uri(intranetBase, "/iam/users/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Create IAM User" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.GetByLabel("Name").FillAsync(displayName);
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Role").SelectOptionAsync(roleId);
        await page.GetByRole(AriaRole.Button, new() { NameString = "Create User" }).ClickAsync();

        await page.WaitForURLAsync(
            url => new Uri(url).AbsolutePath.Equals("/iam", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions
            {
                Timeout = 60_000,
                WaitUntil = WaitUntilState.NetworkIdle
            });

        var usersResult = await page.EvaluateAsync<string>(
            @"async email => {
                const r = await fetch(`/api/v1/iam/users?search=${encodeURIComponent(email)}&pageSize=10`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            email);
        Assert.StartsWith("200 ", usersResult, StringComparison.Ordinal);

        using var usersDocument = JsonDocument.Parse(usersResult[4..]);
        Assert.True(TryGetJsonProperty(usersDocument.RootElement, out var userData, "data", "Data"));
        var createdUser = userData.EnumerateArray().SingleOrDefault(user =>
            string.Equals(email, GetJsonString(user, "email", "Email"), StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(JsonValueKind.Undefined, createdUser.ValueKind);
        Assert.Equal(displayName, GetJsonString(createdUser, "displayName", "DisplayName"));
        var principalId = createdUser.GetProperty("principalId").GetGuid();

        await page.GotoAsync(new Uri(intranetBase, $"/iam/users/{principalId}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(displayName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(email, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(roleName, new() { Timeout = 30_000 });

        var userRolesResult = await page.EvaluateAsync<string>(
            @"async principalId => {
                const r = await fetch(`/api/v1/iam/users/${principalId}/roles`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            principalId);
        Assert.StartsWith("200 ", userRolesResult, StringComparison.Ordinal);

        using var userRolesDocument = JsonDocument.Parse(userRolesResult[4..]);
        Assert.Contains(userRolesDocument.RootElement.EnumerateArray(), binding =>
            string.Equals(roleId, GetJsonString(binding, "roleId", "RoleId"), StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(GetJsonString(binding, "bindingId", "BindingId")));
    }

    /// <summary>
    /// Verifies authenticated Intranet module routes render for the production-gate employee story groups.
    /// Covers route-level executable portions of INT-002, INT-003, INT-010, INT-011, INT-012, INT-013, INT-014,
    /// COM-001, FIN-001, FIN-002, PROC-002, PROC-003, MFG-001, MFG-003, MFG-004, HR-001, HR-002, OPS-001, and SEC-002.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-002,INT-003,INT-010,INT-011,INT-012,INT-013,INT-014,COM-001,FIN-001,FIN-002,PROC-002,PROC-003,MFG-001,MFG-003,MFG-004,HR-001,HR-002,OPS-001,SEC-002")]
    public async Task Intranet_AutomationEmployee_ModuleRoutesRenderWithoutAuthOrStartupFailures()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(page, intranetBase, "/");

        await AssertIntranetRouteAsync(page, intranetBase, "/", new Regex("Dashboard|Good afternoon", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/search", new Regex("Search|result", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/admin", new Regex("Admin|System|IAM", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/iam", new Regex("IAM|user|role", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/iam/users/new", new Regex("user|role|permission", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/sales/customers", new Regex("customer|company", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/sales/customers/new", new Regex("customer|company|address", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/sales/projects", new Regex("project|quote", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/commerce/catalog", new Regex("catalog|product|commerce", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/accounting", new Regex("invoice|accounting|finance", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/accounting/new", new Regex("invoice|customer|billing", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/purchasing", new Regex("purchase|supplier|procurement|PO", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/purchasing/new", new Regex("purchase|supplier|procurement|PO", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/mfg/materials", new Regex("material|inventory|manufacturing", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/mfg/equipment", new Regex("equipment|facility|machine", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/mfg/production-schedule", new Regex("production|schedule|job", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/hr/profile", new Regex("profile|employee|leave|HR", RegexOptions.IgnoreCase));
        await AssertIntranetRouteAsync(page, intranetBase, "/hr/leave", new Regex("leave|request|approval", RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Verifies an employee can create material master data, inspect the material detail, edit quote-critical fields,
    /// and read the persisted state back through the BFF boundary.
    /// Covers the executable material master-data portion of INT-012.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-012")]
    public async Task Intranet_MaterialMasterData_CreatesAndEditsMaterial()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var materialName = $"E2E PEEK {unique}";
        var materialCode = $"E2E-MAT-{unique % 1_000_000:D6}";
        var description = $"Production-gate material created by E2E {unique}.";

        await SignInToIntranetAsync(page, intranetBase, "/mfg/materials");
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Materials" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Add Material" }).ClickAsync();
        var createPanel = page.Locator(".mlv-panel-card").Filter(new() { HasText = "Create material" }).First;
        await Expect(createPanel).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await createPanel.GetByLabel("Name").FillAsync(materialName);
        await createPanel.GetByLabel("Code").FillAsync(materialCode);
        await createPanel.GetByLabel("Unit price").FillAsync("125.75");
        await createPanel.GetByLabel("Stock").FillAsync("42");
        await createPanel.GetByLabel("Unit", new() { Exact = true }).FillAsync("pcs");
        await createPanel.GetByLabel("Description").FillAsync(description);

        var createResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/materials", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await createPanel.GetByRole(AriaRole.Button, new() { NameString = "Create Material" }).ClickAsync();
        var createResponse = await createResponseTask;
        var createBody = await ReadResponseTextOrEmptyAsync(createResponse);
        Assert.True(createResponse.Ok, $"Material create failed with HTTP {createResponse.Status}: {createBody}");

        using var createDocument = JsonDocument.Parse(createBody);
        var created = createDocument.RootElement;
        var materialId = created.GetProperty("id").GetGuid();
        Assert.Equal(materialName, GetJsonString(created, "name", "Name"));
        Assert.Equal(materialCode, GetJsonString(created, "sku", "SKU", "code", "Code"));
        Assert.Equal(42, (int)GetJsonDouble(created, "quantityOnHand", "QuantityOnHand", "stockLevel", "StockLevel"));

        await page.WaitForURLAsync(
            url => url.Contains($"/mfg/materials/{materialId}", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000, WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(materialName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(materialCode, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(description, new() { Timeout = 30_000 });

        var updatedName = $"{materialName} Updated";
        var updatedCode = $"{materialCode}-U";
        var updatedDescription = $"Updated quote-critical material data {unique}.";
        await page.GetByRole(AriaRole.Button, new() { NameString = "Edit" }).ClickAsync();
        await page.GetByLabel("Name").FillAsync(updatedName);
        await page.GetByLabel("Code").FillAsync(updatedCode);
        await page.GetByLabel("Unit price").FillAsync("139.25");
        await page.GetByLabel("Stock").FillAsync("84");
        await page.GetByLabel("Description").FillAsync(updatedDescription);

        var updateResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/materials/{materialId}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Save" }).ClickAsync();
        var updateResponse = await updateResponseTask;
        var updateBody = await ReadResponseTextOrEmptyAsync(updateResponse);
        Assert.True(updateResponse.Ok, $"Material update failed with HTTP {updateResponse.Status}: {updateBody}");

        using var updateDocument = JsonDocument.Parse(updateBody);
        var updated = updateDocument.RootElement;
        Assert.Equal(updatedName, GetJsonString(updated, "name", "Name"));
        Assert.Equal(updatedCode, GetJsonString(updated, "sku", "SKU", "code", "Code"));
        Assert.Equal(139.25, GetJsonDouble(updated, "unitPrice", "UnitPrice", "pricePerUnit", "PricePerUnit"), precision: 2);
        Assert.Equal(84, (int)GetJsonDouble(updated, "quantityOnHand", "QuantityOnHand", "stockLevel", "StockLevel"));

        await Expect(page.Locator("body")).ToContainTextAsync(updatedName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(updatedCode, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(updatedDescription, new() { Timeout = 30_000 });

        var persistedResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/materials/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            materialId);

        Assert.StartsWith("200 ", persistedResult, StringComparison.Ordinal);
        using var persistedDocument = JsonDocument.Parse(persistedResult[4..]);
        var persisted = persistedDocument.RootElement;
        Assert.Equal(updatedName, GetJsonString(persisted, "name", "Name"));
        Assert.Equal(updatedCode, GetJsonString(persisted, "sku", "SKU", "code", "Code"));
        Assert.Equal(updatedDescription, GetJsonString(persisted, "description", "Description"));
        Assert.Equal(84, (int)GetJsonDouble(persisted, "quantityOnHand", "QuantityOnHand", "stockLevel", "StockLevel"));
    }

    /// <summary>
    /// Verifies an employee can register equipment, inspect the generated asset record, append an operating note,
    /// and append a maintenance log through the FacilityService boundary.
    /// Covers the executable equipment/facility master-data portion of INT-013.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-013")]
    public async Task Intranet_EquipmentMasterData_RegistersNotesAndMaintenance()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var equipmentName = $"E2E CNC Mill {unique}";
        var serial = $"E2E-SN-{unique % 1_000_000:D6}";
        var noteText = $"E2E spindle alignment note {unique}";
        var maintenanceDescription = $"E2E calibration maintenance record {unique}";

        await SignInToIntranetAsync(page, intranetBase, "/mfg/equipment");
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Equipment" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Add Equipment" }).ClickAsync();
        var createPanel = page.Locator(".mlv-panel-card").Filter(new() { HasText = "Register equipment" }).First;
        await Expect(createPanel).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await createPanel.GetByLabel("Name").FillAsync(equipmentName);
        await createPanel
            .Locator("xpath=.//label[contains(concat(' ', normalize-space(@class), ' '), ' mlv-form-field ')][span[normalize-space(.) = 'Category']]//select")
            .SelectOptionAsync("CncMachine");
        await createPanel.GetByLabel("Brand").FillAsync("Haas");
        await createPanel.GetByLabel("Model").FillAsync("VF-2SS");
        await createPanel.GetByLabel("Serial").FillAsync(serial);
        await createPanel.GetByLabel("Sub-category").FillAsync("3-axis machining center");
        await createPanel.GetByLabel("Price THB").FillAsync("750000");

        var createResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/equipments", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await createPanel.GetByRole(AriaRole.Button, new() { NameString = "Register" }).ClickAsync();
        var createResponse = await createResponseTask;
        var createBody = await ReadResponseTextOrEmptyAsync(createResponse);
        Assert.True(createResponse.Ok, $"Equipment registration failed with HTTP {createResponse.Status}: {createBody}");

        using var createDocument = JsonDocument.Parse(createBody);
        var created = createDocument.RootElement;
        var equipmentId = created.GetProperty("id").GetGuid();
        var assetCode = GetJsonString(created, "assetCode", "AssetCode");
        Assert.Equal(equipmentName, GetJsonString(created, "name", "Name"));
        Assert.Equal("CncMachine", GetJsonString(created, "category", "Category"));
        Assert.Equal("Active", GetJsonString(created, "status", "Status"));
        Assert.False(string.IsNullOrWhiteSpace(assetCode));

        await page.GotoAsync(new Uri(intranetBase, $"/mfg/equipment/{equipmentId}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(equipmentName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(assetCode, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Haas", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("VF-2SS", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(serial, new() { Timeout = 30_000 });

        var noteResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/equipments/{equipmentId}/notes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.Locator("textarea[placeholder^='Maintenance observation']").FillAsync(noteText);
        await page.GetByRole(AriaRole.Button, new() { NameString = "Add Note" }).ClickAsync();
        var noteResponse = await noteResponseTask;
        var noteBody = await ReadResponseTextOrEmptyAsync(noteResponse);
        Assert.True(noteResponse.Ok, $"Equipment note create failed with HTTP {noteResponse.Status}: {noteBody}");
        using (var noteDocument = JsonDocument.Parse(noteBody))
        {
            var note = noteDocument.RootElement;
            Assert.Equal(equipmentId, note.GetProperty("equipmentId").GetGuid());
            Assert.Equal(noteText, GetJsonString(note, "content", "Content"));
        }

        await Expect(page.Locator("body")).ToContainTextAsync(noteText, new() { Timeout = 30_000 });

        await page.GetByLabel("Type").FillAsync("Calibration");
        await page.GetByLabel("Vendor").FillAsync("E2E Calibration Lab");
        await page.GetByLabel("Cost THB").FillAsync("3500");
        await page.GetByLabel("Description").FillAsync(maintenanceDescription);

        var maintenanceResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/equipments/{equipmentId}/maintenance", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Add Maintenance" }).ClickAsync();
        var maintenanceResponse = await maintenanceResponseTask;
        var maintenanceBody = await ReadResponseTextOrEmptyAsync(maintenanceResponse);
        Assert.True(maintenanceResponse.Ok, $"Equipment maintenance create failed with HTTP {maintenanceResponse.Status}: {maintenanceBody}");
        using (var maintenanceDocument = JsonDocument.Parse(maintenanceBody))
        {
            var maintenance = maintenanceDocument.RootElement;
            Assert.Equal(equipmentId, maintenance.GetProperty("equipmentId").GetGuid());
            Assert.Equal("Calibration", GetJsonString(maintenance, "type", "Type"));
            Assert.Equal(maintenanceDescription, GetJsonString(maintenance, "description", "Description"));
            Assert.Equal("E2E Calibration Lab", GetJsonString(maintenance, "vendorName", "VendorName"));
            Assert.Equal(3500, (int)GetJsonDouble(maintenance, "costTHB", "costThb", "CostTHB"));
        }

        await Expect(page.Locator("body")).ToContainTextAsync("Calibration", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(maintenanceDescription, new() { Timeout = 30_000 });

        var persistedResult = await page.EvaluateAsync<string>(
            @"async id => {
                const [detail, notes, maintenance] = await Promise.all([
                    fetch(`/api/v1/equipments/${id}`, { credentials: 'include' }),
                    fetch(`/api/v1/equipments/${id}/notes`, { credentials: 'include' }),
                    fetch(`/api/v1/equipments/${id}/maintenance`, { credentials: 'include' })
                ]);
                return JSON.stringify({
                    detailStatus: detail.status,
                    detail: await detail.json(),
                    notesStatus: notes.status,
                    notes: await notes.json(),
                    maintenanceStatus: maintenance.status,
                    maintenance: await maintenance.json()
                });
            }",
            equipmentId);

        using var persistedDocument = JsonDocument.Parse(persistedResult);
        var persisted = persistedDocument.RootElement;
        Assert.Equal(200, persisted.GetProperty("detailStatus").GetInt32());
        Assert.Equal(200, persisted.GetProperty("notesStatus").GetInt32());
        Assert.Equal(200, persisted.GetProperty("maintenanceStatus").GetInt32());
        Assert.Equal(equipmentName, GetJsonString(persisted.GetProperty("detail"), "name", "Name"));
        Assert.Contains(persisted.GetProperty("notes").EnumerateArray(), note =>
            string.Equals(noteText, GetJsonString(note, "content", "Content"), StringComparison.Ordinal));
        Assert.Contains(persisted.GetProperty("maintenance").EnumerateArray(), maintenance =>
            string.Equals(maintenanceDescription, GetJsonString(maintenance, "description", "Description"), StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies an employee can create a customer-backed invoice, attach PO evidence, finalize it, record payment, and create a receipt.
    /// Covers the executable invoice creation, attachment, credit-term, payment-status, and receipt portions of FIN-001, FIN-002, and INT-008.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "FIN-001,FIN-002,INT-008")]
    public async Task Intranet_FinanceInvoice_CreatesAttachesAndFinalizesCustomerInvoice()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var lineDescription = $"E2E machining services {Guid.NewGuid():N}"[..34];
        var poNumber = $"PO-E2E-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var paymentReference = $"BANK-E2E-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        var poFilePath = Path.Combine(Path.GetTempPath(), $"maliev-e2e-po-{Guid.NewGuid():N}.txt");
        var poFileName = Path.GetFileName(poFilePath);

        await File.WriteAllTextAsync(poFilePath, $"Purchase order evidence for {poNumber}");

        try
        {
            await SignInToIntranetAsync(page, intranetBase, "/accounting/new");
            var customer = await CreateIntranetCorporateCustomerAsync(page);

            await page.GotoAsync(new Uri(intranetBase, "/accounting/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Expect(page.Locator("body")).ToContainTextAsync("New Invoice", new() { Timeout = 30_000 });
            var currenciesResult = await page.EvaluateAsync<string>(
                "async () => { const r = await fetch('/api/v1/referenceData/currencies', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
            Assert.StartsWith("200 ", currenciesResult, StringComparison.Ordinal);
            Assert.Contains("THB", currenciesResult, StringComparison.OrdinalIgnoreCase);

            var customerPickerTrigger = page.Locator(".customer-picker-trigger").First;
            try
            {
                await Expect(customerPickerTrigger).ToBeVisibleAsync(new() { Timeout = 30_000 });
            }
            catch (TimeoutException ex)
            {
                var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
                throw new TimeoutException(
                    $"Invoice page did not render the customer picker. Url: {page.Url}. Body: {body[..Math.Min(body.Length, 1_500)]}",
                    ex);
            }

            await SelectCustomerInPickerAsync(page, customer.Email, customer.FullName);

            await Expect(page.Locator("body")).ToContainTextAsync(customer.CompanyName, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(customer.TaxId, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(customer.BillingAddressLine1, new() { Timeout = 30_000 });

            await page.GetByLabel("Currency").SelectOptionAsync("THB");
            await page.GetByLabel("Customer PO number").FillAsync(poNumber);
            await page.Locator("input[type=file]").SetInputFilesAsync(poFilePath);
            await Expect(page.Locator(".mlv-form-help").Filter(new() { HasText = poFileName })).ToBeVisibleAsync(new() { Timeout = 15_000 });
            await page.GetByLabel("Description").FillAsync(lineDescription);
            await page.GetByLabel("Quantity").FillAsync("2");
            await page.GetByLabel("Unit price").FillAsync("1250");
            await page.GetByLabel("Tax rate").FillAsync("7");
            await Expect(page.Locator("body")).ToContainTextAsync(customer.BillingAddressLine1, new() { Timeout = 15_000 });

            await page.GetByRole(AriaRole.Button, new() { NameString = "Create Invoice" }).ClickAsync();
            try
            {
                await page.WaitForURLAsync(
                    url => Regex.IsMatch(new Uri(url).AbsolutePath, "^/accounting/[0-9a-fA-F-]{36}$"),
                    new PageWaitForURLOptions
                    {
                        Timeout = 60_000,
                        WaitUntil = WaitUntilState.NetworkIdle
                    });
            }
            catch (TimeoutException ex)
            {
                var pageError = await page.Locator(".mlv-error").First.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 }).ContinueWith(task => task.IsCompletedSuccessfully ? task.Result : string.Empty);
                var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
                throw new TimeoutException(
                    $"Invoice creation did not navigate after submit. Url: {page.Url}. Error: {pageError}. Body: {body[..Math.Min(body.Length, 2_000)]}",
                    ex);
            }

            var invoiceIdText = Regex.Match(new Uri(page.Url).AbsolutePath, @"/accounting/(?<id>[0-9a-fA-F-]{36})").Groups["id"].Value;
            Assert.True(Guid.TryParse(invoiceIdText, out var invoiceId), $"Invoice detail route did not contain a GUID: {page.Url}");

            await Expect(page.Locator("body")).ToContainTextAsync(customer.CompanyName, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(customer.TaxId, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(poNumber, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(lineDescription, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync("2.00", new() { Timeout = 15_000 });
            await Expect(page.Locator("body")).ToContainTextAsync("1,250.00", new() { Timeout = 15_000 });

            var invoiceResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/invoices/${id}`, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                invoiceId);

            Assert.StartsWith("200 ", invoiceResult, StringComparison.Ordinal);
            using (var invoiceDocument = JsonDocument.Parse(invoiceResult[4..]))
            {
                var invoice = invoiceDocument.RootElement;
                Assert.Equal(customer.CustomerId, invoice.GetProperty("customerId").GetGuid());
                Assert.Equal(customer.CompanyName, GetJsonString(invoice, "customerName", "CustomerName"));
                Assert.Equal(customer.TaxId, GetJsonString(invoice, "customerTaxId", "CustomerTaxId"));
                Assert.Equal(poNumber, GetJsonString(invoice, "poNumber", "PoNumber"));
                Assert.Equal("THB", GetJsonString(invoice, "currency", "Currency"));
                Assert.Equal("Draft", GetJsonString(invoice, "status", "Status"));
                Assert.Equal(2500.00, GetJsonDouble(invoice, "subTotal", "SubTotal", "subtotal"), precision: 2);
                Assert.Equal(175.00, GetJsonDouble(invoice, "taxAmount", "TaxAmount"), precision: 2);
                Assert.Equal(2675.00, GetJsonDouble(invoice, "total", "Total", "grandTotal"), precision: 2);
                Assert.True(TryGetJsonProperty(invoice, out var items, "items", "Items"));
                Assert.Contains(items.EnumerateArray(), item =>
                    string.Equals(lineDescription, GetJsonString(item, "description", "Description"), StringComparison.Ordinal) &&
                    Math.Abs(GetJsonDouble(item, "quantity", "Quantity") - 2) < 0.01);
            }

            var attachmentResult = await page.EvaluateAsync<string>(
                @"async args => {
                    const form = new FormData();
                    form.append('file', new Blob([args.content], { type: 'text/plain' }), args.fileName);
                    const r = await fetch(`/api/v1/invoices/${args.invoiceId}/files?customerId=${args.customerId}&fileType=CustomerPO`, {
                        method: 'POST',
                        credentials: 'include',
                        body: form
                    });
                    return `${r.status} ${await r.text()}`;
                }",
                new
                {
                    invoiceId,
                    customerId = customer.CustomerId,
                    fileName = $"verified-{poFileName}",
                    content = $"Verified PO attachment for {poNumber}"
                });

            Assert.StartsWith("200 ", attachmentResult, StringComparison.Ordinal);
            using (var attachmentDocument = JsonDocument.Parse(attachmentResult[4..]))
            {
                var attachment = attachmentDocument.RootElement;
                Assert.Equal(invoiceId, attachment.GetProperty("invoiceId").GetGuid());
                Assert.Equal("CustomerPO", GetJsonString(attachment, "fileType", "FileType"));
                Assert.Contains(customer.CustomerId.ToString(), GetJsonString(attachment, "fileUrl", "FileUrl"), StringComparison.OrdinalIgnoreCase);
            }

            var finalizeResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/invoices/${id}/finalize`, {
                        method: 'POST',
                        credentials: 'include'
                    });
                    return `${r.status} ${await r.text()}`;
                }",
                invoiceId);

            Assert.StartsWith("200", finalizeResult, StringComparison.Ordinal);
            var finalizedResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/invoices/${id}`, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                invoiceId);

            Assert.StartsWith("200 ", finalizedResult, StringComparison.Ordinal);
            using (var finalizedDocument = JsonDocument.Parse(finalizedResult[4..]))
            {
                var finalized = finalizedDocument.RootElement;
                Assert.Equal("Finalized", GetJsonString(finalized, "status", "Status"));
                Assert.False(string.IsNullOrWhiteSpace(GetJsonString(finalized, "invoiceNumber", "InvoiceNumber")));
                Assert.False(string.IsNullOrWhiteSpace(GetJsonString(finalized, "finalizedBy", "FinalizedBy")));
            }

            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Expect(page.Locator("body")).ToContainTextAsync("Finalized", new() { Timeout = 30_000 });

            await page.GetByLabel("Reference").FillAsync(paymentReference);
            await page.GetByLabel("Notes").FillAsync("Verified bank transfer evidence during Aspire E2E.");
            await page.GetByRole(AriaRole.Button, new() { NameString = "Record Payment" }).ClickAsync();
            await Expect(page.Locator("body")).ToContainTextAsync("Fully Paid", new() { Timeout = 60_000 });
            await Expect(page.Locator("body")).ToContainTextAsync("2,675.00", new() { Timeout = 15_000 });

            var paidResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/invoices/${id}`, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                invoiceId);

            Assert.StartsWith("200 ", paidResult, StringComparison.Ordinal);
            using (var paidDocument = JsonDocument.Parse(paidResult[4..]))
            {
                var paid = paidDocument.RootElement;
                Assert.Equal("FullyPaid", GetJsonString(paid, "status", "Status"));
                Assert.Equal(2675.00, GetJsonDouble(paid, "paidAmount", "PaidAmount"), precision: 2);
                Assert.Equal(0.00, GetJsonDouble(paid, "balance", "Balance", "outstandingBalance", "OutstandingBalance"), precision: 2);
            }

            await page.GetByRole(AriaRole.Button, new() { NameString = "Create Receipt" }).ClickAsync();
            await Expect(page.Locator("body")).ToContainTextAsync("Pending Pdf", new() { Timeout = 60_000 });

            var receiptsResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch('/api/v1/receipts?page=1&pageSize=50', { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                invoiceId);

            Assert.StartsWith("200 ", receiptsResult, StringComparison.Ordinal);
            using (var receiptsDocument = JsonDocument.Parse(receiptsResult[4..]))
            {
                var receipt = receiptsDocument.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .FirstOrDefault(item => item.TryGetProperty("invoiceId", out var invoiceIdProperty) && invoiceIdProperty.GetGuid() == invoiceId);

                Assert.NotEqual(JsonValueKind.Undefined, receipt.ValueKind);
                Assert.False(string.IsNullOrWhiteSpace(GetJsonString(receipt, "receiptNumber", "ReceiptNumber")));
                Assert.Equal("PendingPdf", GetJsonString(receipt, "status", "Status"));
                Assert.Equal("Bank Transfer", GetJsonString(receipt, "paymentMethod", "PaymentMethod"));
                Assert.Equal(2675.00, GetJsonDouble(receipt, "totalAmount", "TotalAmount", "amount", "Amount"), precision: 2);
            }
        }
        finally
        {
            if (File.Exists(poFilePath))
            {
                File.Delete(poFilePath);
            }
        }
    }

    /// <summary>
    /// Verifies an employee can create and maintain a supplier profile through the Intranet supplier UI.
    /// Covers the executable supplier profile create/edit/detail portions of PROC-002.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "PROC-002")]
    public async Task Intranet_SupplierProfile_CreatesAndEditsSupplier()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..12];
        var supplierName = $"E2E Supplier Profile {unique}";
        var taxId = $"SUP{DateTimeOffset.UtcNow:HHmmssff}";
        var email = $"supplier.profile.{unique}@maliev.local";
        var phone = $"+662{Random.Shared.Next(1000000, 9999999)}";
        var contact = $"Procurement Contact {unique}";
        var address = $"88 Supplier Road {unique}";
        var city = "Bangkok";
        var postalCode = "10310";
        var capabilities = "CNC, Sheet metal";

        await SignInToIntranetAsync(page, intranetBase, "/purchasing/suppliers");
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "Supplier Profiles" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "New Supplier" }).ClickAsync();
        var createPanel = page.Locator(".mlv-panel-card").Filter(new() { HasText = "Create supplier" }).First;
        await Expect(createPanel).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await createPanel.GetByLabel("Supplier name").FillAsync(supplierName);
        await createPanel.GetByLabel("Tax ID").FillAsync(taxId);
        await createPanel.GetByLabel("Email").FillAsync(email);
        await createPanel.GetByLabel("Phone").FillAsync(phone);
        await createPanel.GetByLabel("Contact person").FillAsync(contact);
        await createPanel.GetByLabel("Country").FillAsync("Thailand");
        await createPanel.GetByLabel("City").FillAsync(city);
        await createPanel.GetByLabel("Postal code").FillAsync(postalCode);
        await createPanel.GetByLabel("Address").FillAsync(address);
        await createPanel.GetByLabel("Capabilities").FillAsync(capabilities);

        var createResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/suppliers", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await createPanel.GetByRole(AriaRole.Button, new() { NameString = "Create Supplier" }).ClickAsync();
        var createResponse = await createResponseTask;
        var createBody = await ReadResponseTextOrEmptyAsync(createResponse);
        Assert.True(createResponse.Ok, $"Supplier create failed with HTTP {createResponse.Status}: {createBody}");

        using var createDocument = JsonDocument.Parse(createBody);
        var supplierId = createDocument.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(supplierName, GetJsonString(createDocument.RootElement, "companyName", "CompanyName", "name", "Name"));
        Assert.Equal(taxId, GetJsonString(createDocument.RootElement, "taxId", "TaxId"));
        Assert.False(string.IsNullOrWhiteSpace(GetJsonString(createDocument.RootElement, "rowVersion", "RowVersion")));

        await page.WaitForURLAsync(
            url => url.Contains($"/purchasing/suppliers/{supplierId}", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000, WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(supplierName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(taxId, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(address, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(contact, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("CNC", new() { Timeout = 30_000 });

        var updatedName = $"{supplierName} Revised";
        var updatedAddress = $"99 Revised Supplier Road {unique}";
        var updatedCity = "Samut Prakan";
        var updatedPostalCode = "10270";
        var updatedCapabilities = "CNC, Anodizing, Aluminum stock";

        await page.GetByRole(AriaRole.Button, new() { NameString = "Edit" }).ClickAsync();
        await page.GetByLabel("Supplier name").FillAsync(updatedName);
        await page.GetByLabel("Address").FillAsync(updatedAddress);
        await page.GetByLabel("City").FillAsync(updatedCity);
        await page.GetByLabel("Postal code").FillAsync(updatedPostalCode);
        await page.GetByRole(AriaRole.Textbox, new() { NameString = "Capabilities" }).FillAsync(updatedCapabilities);

        var updateResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/suppliers/{supplierId}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Save" }).ClickAsync();
        var updateResponse = await updateResponseTask;
        var updateBody = await ReadResponseTextOrEmptyAsync(updateResponse);
        Assert.True(updateResponse.Ok, $"Supplier update failed with HTTP {updateResponse.Status}: {updateBody}");

        await Expect(page.Locator("body")).ToContainTextAsync(updatedName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(updatedAddress, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(updatedCity, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(updatedPostalCode, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Anodizing", new() { Timeout = 30_000 });

        var persistedResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/suppliers/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            supplierId);

        Assert.StartsWith("200 ", persistedResult, StringComparison.Ordinal);
        using (var persistedDocument = JsonDocument.Parse(persistedResult[4..]))
        {
            var supplier = persistedDocument.RootElement;
            Assert.Equal(updatedName, GetJsonString(supplier, "name", "Name"));
            Assert.Equal(taxId, GetJsonString(supplier, "taxId", "TaxId"));
            Assert.Equal(updatedAddress, GetJsonString(supplier, "address", "Address"));
            Assert.Equal(updatedCity, GetJsonString(supplier, "city", "City"));
            Assert.Equal(updatedPostalCode, GetJsonString(supplier, "postalCode", "PostalCode"));
            Assert.False(string.IsNullOrWhiteSpace(GetJsonString(supplier, "rowVersion", "RowVersion")));
            Assert.True(TryGetJsonProperty(supplier, out var persistedCapabilities, "capabilities", "Capabilities"));
            Assert.Contains(persistedCapabilities.EnumerateArray(), capability =>
                string.Equals("Anodizing", capability.GetString(), StringComparison.Ordinal));
        }

        await page.GotoAsync(new Uri(intranetBase, "/purchasing/suppliers").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(updatedName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(taxId, new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies an employee can create a supplier-backed purchase order, attach evidence, cancel with a reason, and see persisted state.
    /// Covers the executable supplier profile, purchase order, attachment, and cancellation portions of PROC-002 and PROC-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "PROC-002,PROC-003")]
    public async Task Intranet_ProcurementPurchaseOrder_CreatesAttachesAndCancelsSupplierPo()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var procurementDiagnostics = new List<string>();
        page.Console += (_, message) =>
        {
            var text = message.Text;
            if (text.Contains("procurement", StringComparison.OrdinalIgnoreCase)
                || text.Contains("purchase order", StringComparison.OrdinalIgnoreCase)
                || text.Contains("HTTP", StringComparison.OrdinalIgnoreCase))
            {
                procurementDiagnostics.Add($"console:{message.Type}:{text}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            if (request.Url.Contains("/api/v1/procurement", StringComparison.OrdinalIgnoreCase))
            {
                procurementDiagnostics.Add($"request-failed:{request.Method} {request.Url} {request.Failure}");
            }
        };
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..12];
        var customerPo = $"CPO-E2E-{unique}".ToUpperInvariant();
        var attachmentPath = Path.Combine(Path.GetTempPath(), $"maliev-e2e-purchase-order-{unique}.txt");
        var attachmentName = Path.GetFileName(attachmentPath);
        var attachmentDescription = $"Supplier PO evidence {unique}";
        var cancelReason = $"E2E duplicate supplier PO {unique}";

        await File.WriteAllTextAsync(attachmentPath, $"Supplier purchase order evidence for {customerPo}");

        try
        {
            using var purchaseOrderClient = _fixture.CreateAuthenticatedClient("PurchaseOrderService");

            await SignInToIntranetAsync(page, intranetBase, "/purchasing/new");
            var customer = await CreateIntranetCorporateCustomerAsync(page);
            var order = await AspireTestData.CreateOrderAsync(
                _fixture,
                customer.CustomerId,
                $"Procurement source order {unique}");
            var sourceOrderId = GetJsonString(order, "orderId", "OrderId");
            Assert.False(string.IsNullOrWhiteSpace(sourceOrderId), $"OrderService did not return an order id: {order}");

            var supplier = await CreateIntranetSupplierAsync(page);
            await WaitForIntranetApiTextContainsAsync(page, "/api/v1/suppliers?page=1&pageSize=100", supplier.Name);
            await WaitForIntranetApiTextContainsAsync(page, "/api/v1/orders?page=1&pageSize=100", sourceOrderId);

            await page.GotoAsync(new Uri(intranetBase, "/purchasing/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Expect(page.Locator("body")).ToContainTextAsync("New Purchase Order", new() { Timeout = 30_000 });
            await Expect(page.GetByLabel("Supplier")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(supplier.Name, new() { Timeout = 30_000 });

            await page.GetByLabel("Order type").SelectOptionAsync("1");
            await page.GetByLabel("Supplier").SelectOptionAsync([new SelectOptionValue { Label = supplier.Name }]);
            await page.GetByLabel("Source order").SelectOptionAsync(sourceOrderId);
            await Expect(page.GetByLabel("Order item")).ToBeEnabledAsync(new() { Timeout = 30_000 });
            await page.GetByLabel("Order item").SelectOptionAsync("primary");
            await page.GetByLabel("Currency").SelectOptionAsync("THB");
            await page.GetByLabel("Customer PO").FillAsync(customerPo);
            await page.GetByLabel("Quantity").FillAsync("1");
            await page.GetByLabel("Notes").FillAsync($"Created by Aspire browser E2E procurement gate {unique}");

            var createResponseTask = page.WaitForResponseAsync(response =>
                response.Url.Contains("/api/v1/procurement", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
                new PageWaitForResponseOptions { Timeout = 90_000 });

            await page.GetByRole(AriaRole.Button, new() { NameString = "Create PO" }).ClickAsync();
            IResponse createResponse;
            try
            {
                createResponse = await createResponseTask;
            }
            catch (TimeoutException ex)
            {
                var pageError = await page.Locator(".mlv-error").First.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 }).ContinueWith(task => task.IsCompletedSuccessfully ? task.Result : string.Empty);
                var body = await ReadBodyPreviewAsync(page, 2_000);
                throw new TimeoutException(
                    $"Purchase order form did not receive a create response. Url: {page.Url}. Error: {pageError}. Diagnostics: {string.Join(" || ", procurementDiagnostics)}. Body: {body}",
                    ex);
            }

            var createResponseBody = await ReadResponseTextOrEmptyAsync(createResponse);
            var createPageError = createResponse.Ok
                ? string.Empty
                : await page.Locator(".mlv-error").First.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 }).ContinueWith(task => task.IsCompletedSuccessfully ? task.Result : string.Empty);
            var createBody = createResponse.Ok
                ? string.Empty
                : await ReadBodyPreviewAsync(page, 2_000);
            Assert.True(
                createResponse.Ok,
                $"Purchase order create POST failed with HTTP {createResponse.Status}: {createResponseBody}. Error: {createPageError}. Body: {createBody}. Diagnostics: {string.Join(" || ", procurementDiagnostics)}");

            try
            {
                await page.WaitForURLAsync(
                    url => Regex.IsMatch(new Uri(url).AbsolutePath, "^/purchasing/[0-9]+$"),
                    new PageWaitForURLOptions
                    {
                        Timeout = 60_000,
                        WaitUntil = WaitUntilState.NetworkIdle
                    });
            }
            catch (TimeoutException ex)
            {
                var pageError = await page.Locator(".mlv-error").First.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 }).ContinueWith(task => task.IsCompletedSuccessfully ? task.Result : string.Empty);
                var body = await ReadBodyPreviewAsync(page, 2_000);
                throw new TimeoutException(
                    $"Purchase order creation did not navigate after submit. Url: {page.Url}. Create response: HTTP {createResponse.Status} {createResponseBody}. Error: {pageError}. Diagnostics: {string.Join(" || ", procurementDiagnostics)}. Body: {body}",
                    ex);
            }

            var poIdText = Regex.Match(new Uri(page.Url).AbsolutePath, @"/purchasing/(?<id>[0-9]+)").Groups["id"].Value;
            Assert.True(int.TryParse(poIdText, out var purchaseOrderId), $"Purchase order detail route did not contain an integer id: {page.Url}");

            await Expect(page.Locator("body")).ToContainTextAsync(supplier.Name, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(sourceOrderId, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync(customerPo, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync("THB", new() { Timeout = 15_000 });

            var createdResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/procurement/${id}`, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                purchaseOrderId);
            Assert.StartsWith("200 ", createdResult, StringComparison.Ordinal);
            using (var createdDocument = JsonDocument.Parse(createdResult[4..]))
            {
                var purchaseOrder = createdDocument.RootElement;
                Assert.Equal(sourceOrderId, GetJsonString(purchaseOrder, "sourceOrderId", "SourceOrderId"));
                Assert.Equal(customerPo, GetJsonString(purchaseOrder, "customerPo", "CustomerPo"));
                Assert.Equal("THB", GetJsonString(purchaseOrder, "currencyCode", "CurrencyCode"));
                Assert.False(string.IsNullOrWhiteSpace(GetJsonString(purchaseOrder, "poNumber", "PoNumber")));
                var status = GetJsonString(purchaseOrder, "status", "Status");
                Assert.True(
                    new[] { "Pending", "Approved", "Draft" }.Contains(status, StringComparer.Ordinal),
                    $"Unexpected purchase order status after creation: {status}");
            }

            await page.Locator("input[type=file]").SetInputFilesAsync(attachmentPath);
            await Expect(page.Locator("body")).ToContainTextAsync(attachmentName, new() { Timeout = 15_000 });
            await page.Locator("select.mlv-form-input").Last.SelectOptionAsync("CustomerPO");
            await page.Locator("input[placeholder='Description']").FillAsync(attachmentDescription);
            await page.GetByRole(AriaRole.Button, new() { NameString = "Attach document" }).ClickAsync();
            await Expect(page.Locator("body")).ToContainTextAsync(attachmentName, new() { Timeout = 30_000 });
            await Expect(page.Locator("body")).ToContainTextAsync("CustomerPO", new() { Timeout = 30_000 });

            var fileResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/procurement/${id}`, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                purchaseOrderId);
            Assert.StartsWith("200 ", fileResult, StringComparison.Ordinal);
            using (var fileDocument = JsonDocument.Parse(fileResult[4..]))
            {
                var purchaseOrder = fileDocument.RootElement;
                Assert.True(TryGetJsonProperty(purchaseOrder, out var files, "files", "Files"));
                Assert.Contains(files.EnumerateArray(), file =>
                    string.Equals(attachmentName, GetJsonString(file, "fileName", "FileName"), StringComparison.Ordinal) &&
                    string.Equals("CustomerPO", GetJsonString(file, "documentType", "DocumentType"), StringComparison.Ordinal));
            }

            await page.GetByRole(AriaRole.Button, new() { NameString = "Cancel PO" }).ClickAsync();
            await page.Locator("textarea[placeholder='Cancellation reason']").FillAsync(cancelReason);
            await page.GetByRole(AriaRole.Button, new() { NameString = "Confirm cancel" }).ClickAsync();
            await Expect(page.Locator("body")).ToContainTextAsync("Cancelled", new() { Timeout = 30_000 });

            var cancelledResult = await page.EvaluateAsync<string>(
                @"async id => {
                    const r = await fetch(`/api/v1/procurement/${id}`, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                purchaseOrderId);
            Assert.StartsWith("200 ", cancelledResult, StringComparison.Ordinal);
            using (var cancelledDocument = JsonDocument.Parse(cancelledResult[4..]))
            {
                var purchaseOrder = cancelledDocument.RootElement;
                Assert.Equal("Cancelled", GetJsonString(purchaseOrder, "status", "Status"));
                Assert.True(TryGetJsonProperty(purchaseOrder, out var files, "files", "Files"));
                Assert.Contains(files.EnumerateArray(), file =>
                    string.Equals(attachmentName, GetJsonString(file, "fileName", "FileName"), StringComparison.Ordinal));
            }
        }
        finally
        {
            if (File.Exists(attachmentPath))
            {
                File.Delete(attachmentPath);
            }
        }
    }

    /// <summary>
    /// Verifies an employee can receive a supplier purchase order through the Intranet lifecycle UI.
    /// Covers the currently executable receiving/status/event portion of PROC-004.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "PROC-004")]
    public async Task Intranet_ProcurementReceiving_ApprovesSendsAndReceivesSupplierPo()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..12];
        var customerPo = $"CPO-RECV-{unique}".ToUpperInvariant();

        await SignInToIntranetAsync(page, intranetBase, "/purchasing");
        var customer = await CreateIntranetCorporateCustomerAsync(page);
        var order = await AspireTestData.CreateOrderAsync(
            _fixture,
            customer.CustomerId,
            $"Procurement receiving source order {unique}");
        var sourceOrderId = GetJsonString(order, "orderId", "OrderId");
        Assert.False(string.IsNullOrWhiteSpace(sourceOrderId), $"OrderService did not return an order id: {order}");

        var supplier = await CreateIntranetSupplierAsync(page);
        await WaitForIntranetApiTextContainsAsync(page, "/api/v1/suppliers?page=1&pageSize=100", supplier.Name);
        await WaitForIntranetApiTextContainsAsync(page, "/api/v1/orders?page=1&pageSize=100", sourceOrderId);

        var createResult = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch('/api/v1/procurement', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        orderType: 1,
                        supplierServiceId: args.supplierId,
                        sourceOrderId: args.sourceOrderId,
                        customerPo: args.customerPo,
                        currencyCode: 'THB',
                        expectedDeliveryDate: args.expectedDeliveryDate,
                        notes: args.notes,
                        items: [{ externalOrderItemId: 0, sourceOrderItemId: 'primary', quantity: 1 }]
                    })
                });
                return `${r.status} ${await r.text()}`;
            }",
            new
            {
                supplierId = supplier.Id,
                sourceOrderId,
                customerPo,
                expectedDeliveryDate = DateTime.UtcNow.Date.AddDays(14).ToString("O"),
                notes = $"Created by Aspire browser E2E receiving gate {unique}"
            });

        Assert.StartsWith("201 ", createResult, StringComparison.Ordinal);
        using var createdDocument = JsonDocument.Parse(createResult[4..]);
        var created = createdDocument.RootElement;
        var purchaseOrderId = created.GetProperty("id").GetInt32();
        var poNumber = GetJsonString(created, "poNumber", "PoNumber");
        Assert.Equal("Pending", GetJsonString(created, "status", "Status"));
        Assert.False(string.IsNullOrWhiteSpace(poNumber));

        await page.GotoAsync(new Uri(intranetBase, $"/purchasing/{purchaseOrderId}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(poNumber, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(supplier.Name, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(customerPo, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Pending", new() { Timeout = 30_000 });

        var approved = await RunPurchaseOrderLifecycleActionAsync(page, purchaseOrderId, "Approve", "approve", "Approved");
        Assert.Equal(poNumber, GetJsonString(approved, "poNumber", "PoNumber"));
        Assert.False(string.IsNullOrWhiteSpace(GetJsonString(approved, "approvedBy", "ApprovedBy")));
        Assert.False(string.IsNullOrWhiteSpace(GetJsonString(approved, "approvedAt", "ApprovedAt")));

        var ordered = await RunPurchaseOrderLifecycleActionAsync(page, purchaseOrderId, "Send", "send-to-supplier", "Ordered");
        Assert.Equal(poNumber, GetJsonString(ordered, "poNumber", "PoNumber"));

        var delivered = await RunPurchaseOrderLifecycleActionAsync(page, purchaseOrderId, "Receive", "receive", "Delivered");
        Assert.Equal(poNumber, GetJsonString(delivered, "poNumber", "PoNumber"));
        Assert.Equal(sourceOrderId, GetJsonString(delivered, "sourceOrderId", "SourceOrderId"));
        Assert.Equal(customerPo, GetJsonString(delivered, "customerPo", "CustomerPo"));
        Assert.False(string.IsNullOrWhiteSpace(GetJsonString(delivered, "lastModifiedBy", "LastModifiedBy")));

        var persistedResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/procurement/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            purchaseOrderId);
        Assert.StartsWith("200 ", persistedResult, StringComparison.Ordinal);
        using (var persistedDocument = JsonDocument.Parse(persistedResult[4..]))
        {
            var purchaseOrder = persistedDocument.RootElement;
            Assert.Equal("Delivered", GetJsonString(purchaseOrder, "status", "Status"));
            Assert.Equal(poNumber, GetJsonString(purchaseOrder, "poNumber", "PoNumber"));
            Assert.Equal("THB", GetJsonString(purchaseOrder, "currencyCode", "CurrencyCode"));
            Assert.True(TryGetJsonProperty(purchaseOrder, out var items, "items", "Items"));
            Assert.Contains(items.EnumerateArray(), item =>
                string.Equals("primary", GetJsonString(item, "sourceOrderItemId", "SourceOrderItemId"), StringComparison.Ordinal));
        }

        await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "Receive" })).ToBeDisabledAsync(new() { Timeout = 15_000 });
        await Expect(page.GetByRole(AriaRole.Button, new() { NameString = "Cancel PO" })).ToBeDisabledAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Verifies an employee can create a delivery note, progress shipment status, request the delivery PDF, and verify proof-of-delivery state.
    /// Covers the executable logistics portion of INT-009.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-009")]
    public async Task Intranet_DeliveryNotes_CreatesTracksPdfAndDeliveredStatus()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var trackingNumber = $"TH-E2E-{unique}";
        var receiverName = $"E2E Receiver {unique[..6]}";
        var productCode = $"MLV-{unique[..6]}";
        var productName = $"E2E Delivery Bracket {unique[..6]}";

        await SignInToIntranetAsync(page, intranetBase, "/finance/delivery-notes/new");
        var customer = await CreateIntranetCorporateCustomerAsync(page);
        var order = await AspireTestData.CreateOrderAsync(
            _fixture,
            customer.CustomerId,
            $"Delivery note source order {unique}");
        var sourceOrderId = GetJsonString(order, "orderId", "OrderId");
        Assert.False(string.IsNullOrWhiteSpace(sourceOrderId), $"OrderService did not return an order id: {order}");

        await page.GotoAsync(new Uri(intranetBase, "/finance/delivery-notes/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.GetByRole(AriaRole.Heading, new() { NameString = "New Delivery Note" })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GetByLabel("Source order ID").FillAsync(sourceOrderId);
        await page.GetByLabel("Customer ID").FillAsync(customer.CustomerId.ToString());
        await page.GetByLabel("Customer name").FillAsync(customer.CompanyName);
        await page.GetByLabel("Carrier").FillAsync("Flash Express");
        await page.GetByLabel("Tracking number").FillAsync(trackingNumber);
        await page.GetByLabel("Shipping cost").FillAsync("180.50");
        await page.GetByLabel("Shipping currency").FillAsync("THB");
        await page.GetByLabel("Address line 1").FillAsync(customer.BillingAddressLine1);
        await page.GetByLabel("Address line 2").FillAsync("Logistics dock");
        await page.GetByLabel("City").FillAsync("Bangkok");
        await page.GetByLabel("Province").FillAsync("Bangkok");
        await page.GetByLabel("Postal code").FillAsync("10500");
        await page.GetByLabel("Country").FillAsync("Thailand");
        await page.GetByLabel("Contact name").FillAsync(receiverName);
        await page.GetByLabel("Contact phone").FillAsync("+66810000999");
        await page.GetByLabel("Contact email").FillAsync(customer.Email);
        await page.GetByLabel("Delivery instructions").FillAsync("Call before arrival and capture receiver signature.");
        await page.GetByLabel("Product code").FillAsync(productCode);
        await page.GetByLabel("Product name").FillAsync(productName);
        await page.GetByLabel("Description").FillAsync("Machined aluminium verification part");
        await page.GetByLabel("Ordered quantity").FillAsync("2");
        await page.GetByLabel("Manufactured quantity").FillAsync("2");
        await page.GetByLabel("Delivered quantity").FillAsync("2");
        await page.GetByLabel("Unit").FillAsync("pcs");
        await page.GetByLabel("Item notes").FillAsync("Packed in one shipment.");

        var createResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/deliverynotes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Create Delivery Note" }).ClickAsync();
        var createResponse = await createResponseTask;
        var createBody = await ReadResponseTextOrEmptyAsync(createResponse);
        Assert.True(createResponse.Ok, $"Delivery note create failed with HTTP {createResponse.Status}: {createBody}");

        using var createDocument = JsonDocument.Parse(createBody);
        var created = createDocument.RootElement;
        var deliveryNoteId = GetJsonString(created, "deliveryNoteId", "DeliveryNoteId", "id", "Id");
        Assert.Matches(@"^DN-\d{4}-\d{6}$", deliveryNoteId);

        await page.WaitForURLAsync(
            url => new Uri(url).AbsolutePath.Contains($"/finance/delivery-notes/{deliveryNoteId}", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 60_000, WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(deliveryNoteId, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(customer.CompanyName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(trackingNumber, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(productCode, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Pending", new() { Timeout = 30_000 });

        var inTransitResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/deliverynotes/{deliveryNoteId}/status", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PATCH", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Mark In Transit" }).ClickAsync();
        var inTransitResponse = await inTransitResponseTask;
        Assert.True(inTransitResponse.Ok, $"Delivery in-transit update failed with HTTP {inTransitResponse.Status}: {await ReadResponseTextOrEmptyAsync(inTransitResponse)}");
        await Expect(page.Locator("body")).ToContainTextAsync("In Transit", new() { Timeout = 30_000 });

        await page.GetByLabel("Received by").FillAsync(receiverName);
        await page.GetByLabel("Actual delivery time").FillAsync(DateTime.Now.ToString("yyyy-MM-ddTHH:mm"));

        var deliveredResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/deliverynotes/{deliveryNoteId}/status", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PATCH", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Mark Delivered" }).ClickAsync();
        var deliveredResponse = await deliveredResponseTask;
        var deliveredBody = await ReadResponseTextOrEmptyAsync(deliveredResponse);
        Assert.True(deliveredResponse.Ok, $"Delivery delivered update failed with HTTP {deliveredResponse.Status}: {deliveredBody}");
        await Expect(page.Locator("body")).ToContainTextAsync("Delivered", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(receiverName, new() { Timeout = 30_000 });

        var pdfResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/deliverynotes/{deliveryNoteId}/pdf", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 60_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Request PDF" }).ClickAsync();
        var pdfResponse = await pdfResponseTask;
        var pdfBody = await ReadResponseTextOrEmptyAsync(pdfResponse);
        Assert.True(pdfResponse.Ok, $"Delivery PDF request failed with HTTP {pdfResponse.Status}: {pdfBody}");
        await Expect(page.Locator("body")).ToContainTextAsync("Requested", new() { Timeout = 30_000 });

        var detailResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/deliverynotes/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            deliveryNoteId);
        Assert.StartsWith("200 ", detailResult, StringComparison.Ordinal);
        using (var detailDocument = JsonDocument.Parse(detailResult[4..]))
        {
            var detail = detailDocument.RootElement;
            Assert.Equal(deliveryNoteId, GetJsonString(detail, "deliveryNoteId", "DeliveryNoteId"));
            Assert.Equal("Delivered", GetJsonString(detail, "status", "Status"));
            Assert.Equal(receiverName, GetJsonString(detail, "receivedByName", "ReceivedByName"));
            Assert.Equal(trackingNumber, GetJsonString(detail, "trackingNumber", "TrackingNumber"));
            Assert.True(TryGetJsonProperty(detail, out var items, "items", "Items"));
            Assert.Contains(items.EnumerateArray(), item =>
                string.Equals(productCode, GetJsonString(item, "productCode", "ProductCode"), StringComparison.Ordinal) &&
                Math.Abs(GetJsonDouble(item, "quantityDelivered", "QuantityDelivered") - 2) < 0.01);
        }

        await page.GotoAsync(new Uri(intranetBase, "/finance/delivery-notes").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(deliveryNoteId, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Delivered", new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies employee-created customer records are searchable, detail pages render, and the employee project quote workspace can select a customer.
    /// Covers executable customer/project-workspace portions of INT-003 and INT-004.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-003,INT-004")]
    public async Task Intranet_EmployeeCreatedCustomer_CanBeOpenedAndSelectedInProjectWorkspace()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(page, intranetBase, "/customers");
        var customer = await CreateIntranetCustomerAsync(page);

        await page.GotoAsync(new Uri(intranetBase, $"/customers?search={Uri.EscapeDataString(customer.Email)}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(customer.Email, new() { Timeout = 30_000 });
        await page.GetByText(customer.Email).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/customers/", StringComparison.OrdinalIgnoreCase), new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(customer.FullName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(new Regex("Contact information|Addresses|Projects", RegexOptions.IgnoreCase), new() { Timeout = 15_000 });

        await page.GotoAsync(new Uri(intranetBase, "/sales/projects/new").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync("Drop CAD files to quote", new() { Timeout = 30_000 });
        await Expect(page.GetByText("Bill To", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { NameString = "Select customer..." }).ClickAsync();
        var customerSearch = page.Locator(".customer-picker-search-input");
        await customerSearch.FillAsync(customer.Email);
        var customerOption = page.Locator(".customer-picker-option").Filter(new() { HasText = customer.FullName }).First;
        await Expect(customerOption).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await customerOption.ClickAsync();
        await Expect(page.Locator(".ccc-root")).ToContainTextAsync(customer.FullName, new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Drop files here or click to upload", new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Quote Total", new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Verifies an employee project can move from uploaded/configured part through DFM acknowledgement,
    /// immutable quotation versions, quote PDF attachment, acceptance, and reorder duplication.
    /// Covers executable project quote lifecycle portions of INT-005, INT-006, INT-007, INT-016, INT-017,
    /// INT-018, INT-019, INT-021, INT-022, INT-023, INT-024, INT-025, INT-027, and INT-028.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-005,INT-006,INT-007,INT-016,INT-017,INT-018,INT-019,INT-021,INT-022,INT-023,INT-024,INT-025,INT-027,INT-028")]
    public async Task Intranet_ProjectQuoteLifecycle_GeneratesVersionsPdfAcceptanceAndDuplicate()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..10];
        var projectTitle = $"E2E revision workspace {unique}";
        var secondChangeSummary = $"Updated quantity and commercial terms {unique}";

        await SignInToIntranetAsync(page, intranetBase, "/");
        var customer = await CreateIntranetCorporateCustomerAsync(page);
        var project = await CreateIntranetProjectAsync(page, customer.CustomerId, customer.FullName, projectTitle);
        var modelUpload = await UploadProjectFileAsync(page, project.ProjectId, customer.CustomerId, $"e2e-bracket-{unique}.step", "application/step");
        var part = await AddConfiguredProjectPartAsync(page, project.ProjectId, customer.CustomerId, modelUpload, quantity: 2, dfmAcknowledged: false);

        var drawing = await UploadProjectAttachmentAsync(page, project.ProjectId, customer.CustomerId, part.PartId, "Drawing", $"drawing-{unique}.pdf", "application/pdf");
        var supportDocument = await UploadProjectAttachmentAsync(page, project.ProjectId, customer.CustomerId, part.PartId, "Supplementary", $"setup-photo-{unique}.jpg", "image/jpeg");
        part = await UpdateProjectPartConfigurationAsync(
            page,
            project.ProjectId,
            part.PartId,
            modelUpload,
            quantity: 2,
            dfmAcknowledged: false,
            drawing,
            supportDocument);

        await page.GotoAsync(new Uri(intranetBase, $"/sales/projects/{project.ProjectId}?tab=parts").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(projectTitle, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(modelUpload.FileName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("DFM warnings", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(drawing.FileName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(supportDocument.FileName, new() { Timeout = 30_000 });

        var acknowledgeResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/projects/{project.ProjectId}/parts/{part.PartId}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Acknowledge", RegexOptions.IgnoreCase) }).ClickAsync();
        var acknowledgeResponse = await acknowledgeResponseTask;
        var acknowledgeBody = await ReadResponseTextOrEmptyAsync(acknowledgeResponse);
        Assert.True(acknowledgeResponse.Ok, $"DFM acknowledgement failed with HTTP {acknowledgeResponse.Status}: {acknowledgeBody}");
        await Expect(page.Locator("body")).ToContainTextAsync("DFM acknowledged", new() { Timeout = 30_000 });

        var acknowledgedProject = await GetIntranetProjectAsync(page, project.ProjectId);
        var acknowledgedPart = FindProjectPart(acknowledgedProject, part.PartId);
        Assert.True(GetJsonBool(acknowledgedPart, "dfmAcknowledged", "DfmAcknowledged"));
        Assert.True(GetJsonBool(acknowledgedPart, "hasDfmWarnings", "HasDfmWarnings"));

        await ConfirmProjectPartPriceAsync(page, project.ProjectId, part.PartId, 1250m);
        var firstQuote = await GenerateProjectQuotationAsync(
            page,
            project.ProjectId,
            "Initial employee quote version from Aspire E2E.",
            manualDiscountAmount: 100m,
            shippingCost: 250m);
        var quotationId = GetJsonGuid(firstQuote, "quotationId", "QuotationId");
        var quotationNumber = GetJsonString(firstQuote, "quotationNumber", "QuotationNumber");
        Assert.NotEqual(Guid.Empty, quotationId);
        Assert.False(string.IsNullOrWhiteSpace(quotationNumber));
        Assert.Equal(1, GetJsonInt(firstQuote, "currentQuotationVersionNumber", "CurrentQuotationVersionNumber"));

        part = await UpdateProjectPartConfigurationAsync(
            page,
            project.ProjectId,
            part.PartId,
            modelUpload,
            quantity: 4,
            dfmAcknowledged: true,
            drawing,
            supportDocument);
        await ConfirmProjectPartPriceAsync(page, project.ProjectId, part.PartId, 1175m);
        var secondQuote = await GenerateProjectQuotationAsync(
            page,
            project.ProjectId,
            secondChangeSummary,
            manualDiscountAmount: 175m,
            shippingCost: 300m);
        Assert.Equal(quotationId, GetJsonGuid(secondQuote, "quotationId", "QuotationId"));
        Assert.Equal(2, GetJsonInt(secondQuote, "currentQuotationVersionNumber", "CurrentQuotationVersionNumber"));

        var quotation = await GetIntranetQuotationAsync(page, quotationId);
        Assert.Equal(2, GetJsonInt(quotation, "currentVersionNumber", "CurrentVersionNumber"));
        Assert.True(TryGetJsonProperty(quotation, out var versions, "versions", "Versions"));
        Assert.True(versions.GetArrayLength() >= 2);
        Assert.Contains(versions.EnumerateArray(), version =>
            GetJsonInt(version, "versionNumber", "VersionNumber") == 2 &&
            GetJsonString(version, "changeSummary", "ChangeSummary").Contains(secondChangeSummary, StringComparison.Ordinal));
        Assert.All(versions.EnumerateArray(), version =>
        {
            Assert.False(string.IsNullOrWhiteSpace(GetJsonString(version, "projectSnapshotJson", "ProjectSnapshotJson")));
            Assert.False(string.IsNullOrWhiteSpace(GetJsonString(version, "projectSnapshotHash", "ProjectSnapshotHash")));
        });

        var latestPdf = await GetLatestQuotationPdfAsync(page, quotationId);
        Assert.False(string.IsNullOrWhiteSpace(latestPdf));

        await page.GotoAsync(new Uri(intranetBase, $"/sales/projects/{project.ProjectId}?tab=quote").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync("Quote document", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Quote revision history", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Version 2", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Version 1", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Current", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(secondChangeSummary, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Open version PDF", new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Accept quote" }).First.ClickAsync();
        var acceptDialog = page.Locator(".project-accept-dialog");
        await Expect(acceptDialog).ToBeVisibleAsync(new() { Timeout = 15_000 });
        var acceptResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/projects/{project.ProjectId}/accept-quotation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });
        await acceptDialog.GetByRole(AriaRole.Button, new() { NameString = "Accept quote" }).ClickAsync();
        var acceptResponse = await acceptResponseTask;
        var acceptBody = await ReadResponseTextOrEmptyAsync(acceptResponse);
        Assert.True(acceptResponse.Ok, $"Quote acceptance failed with HTTP {acceptResponse.Status}: {acceptBody}");
        await Expect(page.Locator("body")).ToContainTextAsync("Accepted", new() { Timeout = 30_000 });

        var acceptedProject = await GetIntranetProjectAsync(page, project.ProjectId);
        Assert.Equal("QuotationAccepted", GetJsonString(acceptedProject, "status", "Status"));

        var duplicate = await DuplicateIntranetProjectAsync(page, project.ProjectId, $"{projectTitle} reorder");
        Assert.NotEqual(project.ProjectId, GetJsonGuid(duplicate, "id", "Id"));
        Assert.Equal(project.ProjectId, GetJsonGuid(duplicate, "sourceProjectId", "SourceProjectId"));
        Assert.Equal(GetJsonString(acceptedProject, "projectNumber", "ProjectNumber"), GetJsonString(duplicate, "sourceProjectNumber", "SourceProjectNumber"));
        Assert.True(TryGetJsonProperty(duplicate, out var duplicatedParts, "parts", "Parts"));
        Assert.Contains(duplicatedParts.EnumerateArray(), duplicatePart =>
            string.Equals(modelUpload.FileName, GetJsonString(duplicatePart, "fileName", "FileName"), StringComparison.Ordinal) &&
            GetJsonInt(duplicatePart, "quantity", "Quantity") == 4);

        var duplicateId = GetJsonGuid(duplicate, "id", "Id");
        await page.GotoAsync(new Uri(intranetBase, $"/sales/projects/{duplicateId}?tab=overview").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync($"{projectTitle} reorder", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Duplicated from", new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(modelUpload.FileName, new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Verifies an employee can create a customer from the Intranet onboarding UI with company, address, and note data.
    /// Covers the manual employee onboarding portions of INT-002 and the customer-detail propagation portions of INT-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-002,INT-003")]
    public async Task Intranet_CustomerOnboardingUi_CreatesCompanyAddressesAndInternalNote()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..10];
        var firstName = "E2E";
        var lastName = $"Onboard {unique}";
        var fullName = $"{firstName} {lastName}";
        var email = $"e2e.ui.onboard.{unique}@maliev.local";
        var phone = "+66810000000";
        var companyName = $"E2E UI Components {unique}";
        var taxId = $"01055{DateTimeOffset.UtcNow:HHmmssff}";
        var internalNote = $"Created through the Intranet onboarding UI E2E gate {unique}.";

        await SignInToIntranetAsync(page, intranetBase, "/sales/customers/new");

        var countriesResult = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/ReferenceData/countries', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
        if (!countriesResult.StartsWith("200 ", StringComparison.Ordinal) ||
            !countriesResult.Contains("Thailand", StringComparison.OrdinalIgnoreCase) ||
            !countriesResult.Contains("\"iso2\":\"TH\"", StringComparison.OrdinalIgnoreCase))
        {
            using var countryClient = _fixture.CreateAuthenticatedClient("CountryService");
            using var directCountryResponse = await countryClient.GetAsync("/country/v1/countries?pageSize=1000");
            var directCountryBody = await directCountryResponse.Content.ReadAsStringAsync();
            Assert.Fail(
                $"Intranet BFF did not return Thailand country reference data. " +
                $"BFF result: {countriesResult}. " +
                $"Direct CountryService result: {(int)directCountryResponse.StatusCode} {directCountryResponse.StatusCode} " +
                $"{directCountryBody[..Math.Min(directCountryBody.Length, 2_000)]}");
        }

        await Expect(page.Locator(".customer-profile-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.GetByLabel("First name").FillAsync(firstName);
        await page.GetByLabel("Last name").FillAsync(lastName);
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Phone number").FillAsync(phone);
        await Expect(page.Locator(".customer-create-side")).ToContainTextAsync(fullName);

        await OpenCustomerCreateTabAsync(page, "Company");
        await page.Locator(".company-name-field input").FillAsync(companyName);
        await page.Locator(".company-tax-field input").FillAsync(taxId);
        await Expect(page.Locator(".customer-create-side")).ToContainTextAsync(companyName);
        await Expect(page.Locator(".customer-create-side")).ToContainTextAsync("Head office", new() { IgnoreCase = true });

        var companyBilling = page.Locator(".company-address-block");
        await FillAddressFieldsAsync(companyBilling, "99 Demo Industrial Road", "Si Lom", "Bang Rak", "Bangkok", "10500");

        await OpenCustomerCreateTabAsync(page, "Addresses");
        var customerBilling = page.Locator(".address-panel").Filter(new() { HasText = "Customer billing address" });
        await FillAddressFieldsAsync(customerBilling, "88 Customer Billing Road", "Khlong Toei Nuea", "Watthana", "Bangkok", "10110");

        var customerShipping = page.Locator(".address-panel").Filter(new() { HasText = "Customer shipping address" });
        await FillAddressFieldsAsync(customerShipping, "77 Shipping Warehouse Lane", "Bang Kapi", "Huai Khwang", "Bangkok", "10310");

        await OpenCustomerCreateTabAsync(page, "Notes");
        await page.Locator(".notes-input").FillAsync(internalNote);
        await page.WaitForTimeoutAsync(750);
        await Expect(page.Locator(".customer-create-side")).ToContainTextAsync("10500");
        await Expect(page.Locator(".customer-create-side")).ToContainTextAsync("10110");
        await Expect(page.Locator(".customer-create-side")).ToContainTextAsync("10310");

        await page.GetByRole(AriaRole.Button, new() { NameString = "Create Customer" }).ClickAsync();
        try
        {
            await page.WaitForURLAsync(
                url => Regex.IsMatch(new Uri(url).AbsolutePath, "^/customers/[0-9a-fA-F-]{36}$"),
                new PageWaitForURLOptions
                {
                    Timeout = 60_000,
                    WaitUntil = WaitUntilState.NetworkIdle
                });
        }
        catch (TimeoutException ex)
        {
            var pageError = await page.Locator(".mlv-error").First.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 }).ContinueWith(task => task.IsCompletedSuccessfully ? task.Result : string.Empty);
            var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            throw new TimeoutException(
                $"Customer onboarding did not navigate after submit. Url: {page.Url}. Error: {pageError}. Body: {body[..Math.Min(body.Length, 1_500)]}",
                ex);
        }

        var customerId = new Uri(page.Url).Segments.Last().TrimEnd('/');
        await Expect(page.Locator("body")).ToContainTextAsync(fullName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(companyName, new() { Timeout = 30_000 });
        await Expect(page.Locator(".customer-field").Filter(new() { HasText = "Email" }).Locator("input").First).ToHaveValueAsync(email, new() { Timeout = 15_000 });

        var detailResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/customers/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            customerId);

        Assert.StartsWith("200", detailResult, StringComparison.Ordinal);
        using var detail = JsonDocument.Parse(detailResult[4..]);
        var root = detail.RootElement;
        Assert.Equal(fullName, GetJsonString(root, "name", "Name"));
        Assert.Equal(email, GetJsonString(root, "email", "Email"));
        Assert.Equal(companyName, GetJsonString(root, "companyName", "CompanyName"));
        Assert.Equal(taxId, GetJsonString(root, "companyVatNumber", "CompanyVatNumber"));

        Assert.True(TryGetJsonProperty(root, out var addresses, "addresses", "Addresses"));
        Assert.Contains(addresses.EnumerateArray(), address =>
            string.Equals("Billing", GetJsonString(address, "type", "Type"), StringComparison.OrdinalIgnoreCase)
            && string.Equals("88 Customer Billing Road", GetJsonString(address, "addressLine1", "AddressLine1"), StringComparison.Ordinal)
            && string.Equals("10110", GetJsonString(address, "postalCode", "PostalCode"), StringComparison.Ordinal));
        Assert.Contains(addresses.EnumerateArray(), address =>
            string.Equals("Shipping", GetJsonString(address, "type", "Type"), StringComparison.OrdinalIgnoreCase)
            && string.Equals("77 Shipping Warehouse Lane", GetJsonString(address, "addressLine1", "AddressLine1"), StringComparison.Ordinal)
            && string.Equals("10310", GetJsonString(address, "postalCode", "PostalCode"), StringComparison.Ordinal));

        Assert.True(TryGetJsonProperty(root, out var companyBillingAddress, "companyBillingAddress", "CompanyBillingAddress"));
        Assert.Equal("99 Demo Industrial Road", GetJsonString(companyBillingAddress, "addressLine1", "AddressLine1"));
        Assert.Equal("10500", GetJsonString(companyBillingAddress, "postalCode", "PostalCode"));

        Assert.True(TryGetJsonProperty(root, out var notes, "notes", "Notes"));
        Assert.Contains(notes.EnumerateArray(), note =>
            GetJsonString(note, "noteText", "NoteText").Contains(internalNote, StringComparison.Ordinal)
            && GetJsonString(note, "noteText", "NoteText").Contains("Head office", StringComparison.OrdinalIgnoreCase));

        await page.Locator(".customer-record-tabs button[data-tab='addresses']").ClickAsync();
        await Expect(page.Locator(".customer-tab-panel[data-section='addresses']")).ToContainTextAsync("88 Customer Billing Road");
        await Expect(page.Locator(".customer-tab-panel[data-section='addresses']")).ToContainTextAsync("77 Shipping Warehouse Lane");

        await page.Locator(".customer-record-tabs button[data-tab='notes']").ClickAsync();
        await Expect(page.Locator(".customer-notes-layout[data-section='notes']")).ToContainTextAsync(internalNote);
    }

    /// <summary>
    /// Verifies customer detail maintenance persists profile, payment-term, status, and address changes.
    /// Covers the executable customer-maintenance portion of INT-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "INT-003")]
    public async Task Intranet_CustomerDetail_EditsProfilePaymentTermsAndAddress()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");
        var unique = Guid.NewGuid().ToString("N")[..10];
        var editedFullName = $"E2E Edited Customer {unique}";
        var editedPhone = $"+6681{Random.Shared.Next(1000000, 9999999)}";
        var editedRecipient = $"Receiving Team {unique}";
        var editedStreet = $"55 Revised Warehouse Road {unique}";
        const string editedDistrict = "Bang Na Nuea";
        const string editedCity = "Bang Na";
        const string editedProvince = "Bangkok";
        const string editedPostalCode = "10260";

        await SignInToIntranetAsync(page, intranetBase, "/customers");
        var customer = await CreateIntranetCorporateCustomerAsync(page);

        await page.GotoAsync(new Uri(intranetBase, $"/customers/{customer.CustomerId}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(customer.FullName, new() { Timeout = 30_000 });

        await page.GetByLabel("Full name").FillAsync(editedFullName);
        await page.GetByLabel("Phone").FillAsync(editedPhone);
        await page.GetByLabel("Status").SelectOptionAsync("Lead");
        await page.Locator(".customer-payment-term-trigger").ClickAsync();
        await page.Locator(".customer-payment-term-option").Filter(new() { HasText = "Due on receipt" }).First.ClickAsync();

        var updateResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/customers/{customer.CustomerId}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PATCH", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = "Save" }).ClickAsync();
        var updateResponse = await updateResponseTask;
        var updateBody = await ReadResponseTextOrEmptyAsync(updateResponse);
        Assert.True(updateResponse.Ok, $"Customer detail profile update failed with HTTP {updateResponse.Status}: {updateBody}");
        await Expect(page.GetByLabel("Full name")).ToHaveValueAsync(editedFullName, new() { Timeout = 15_000 });

        var updatedDetail = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/customers/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            customer.CustomerId);

        Assert.StartsWith("200 ", updatedDetail, StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(updatedDetail[4..]))
        {
            var root = document.RootElement;
            Assert.Equal(editedFullName, GetJsonString(root, "name", "Name"));
            Assert.Equal(editedPhone, GetJsonString(root, "mobile", "Mobile"));
            Assert.Equal("Lead", GetJsonString(root, "status", "Status"));
            Assert.Equal("Due on receipt", GetJsonString(root, "paymentTerms", "PaymentTerms"));
        }

        await page.Locator(".customer-record-tabs button[data-tab='addresses']").ClickAsync();
        var shippingAddressCard = page.Locator(".customer-address-card").Filter(new() { HasText = "88 Finance Warehouse Lane" }).First;
        await Expect(shippingAddressCard).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await shippingAddressCard.GetByLabel("Edit address").ClickAsync();

        var addressModal = page.Locator(".customer-modal-address").First;
        await Expect(addressModal).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await addressModal.Locator(".customer-field").Filter(new() { HasText = "Recipient name" }).Locator("input").FillAsync(editedRecipient);
        await addressModal.Locator(".customer-field").Filter(new() { HasText = "Street" }).First.Locator("input").FillAsync(editedStreet);
        await addressModal.Locator(".customer-field").Filter(new() { HasText = "District / sub-district" }).Locator("input").FillAsync(editedDistrict);
        await addressModal.Locator(".customer-field").Filter(new() { HasText = "City" }).Locator("input").FillAsync(editedCity);
        await addressModal.Locator(".customer-field").Filter(new() { HasText = "State / province" }).Locator("input").FillAsync(editedProvince);
        await addressModal.Locator(".customer-field").Filter(new() { HasText = "Postal" }).Locator("input").FillAsync(editedPostalCode);

        var addressResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/v1/customers/addresses/", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "PATCH", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await addressModal.GetByRole(AriaRole.Button, new() { NameString = "Save" }).ClickAsync();
        var addressResponse = await addressResponseTask;
        var addressBody = await ReadResponseTextOrEmptyAsync(addressResponse);
        Assert.True(addressResponse.Ok, $"Customer address update failed with HTTP {addressResponse.Status}: {addressBody}");
        await Expect(page.Locator(".customer-tab-panel[data-section='addresses']")).ToContainTextAsync(editedStreet, new() { Timeout = 30_000 });

        var addressDetail = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/customers/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            customer.CustomerId);

        Assert.StartsWith("200 ", addressDetail, StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(addressDetail[4..]))
        {
            var root = document.RootElement;
            Assert.True(TryGetJsonProperty(root, out var addresses, "addresses", "Addresses"));
            Assert.Contains(addresses.EnumerateArray(), address =>
                string.Equals("Shipping", GetJsonString(address, "type", "Type"), StringComparison.OrdinalIgnoreCase)
                && string.Equals(editedRecipient, GetJsonString(address, "recipientName", "RecipientName"), StringComparison.Ordinal)
                && string.Equals(editedStreet, GetJsonString(address, "addressLine1", "AddressLine1"), StringComparison.Ordinal)
                && string.Equals(editedDistrict, GetJsonString(address, "district", "District"), StringComparison.Ordinal)
                && string.Equals(editedCity, GetJsonString(address, "city", "City"), StringComparison.Ordinal)
                && string.Equals(editedProvince, GetJsonString(address, "stateProvince", "StateProvince"), StringComparison.Ordinal)
                && string.Equals(editedPostalCode, GetJsonString(address, "postalCode", "PostalCode"), StringComparison.Ordinal));
        }

        await page.Locator(".customer-record-tabs button[data-tab='activity']").ClickAsync();
        await Expect(page.Locator(".customer-activity-panel[data-section='activity']")).ToContainTextAsync("Updated customer profile", new() { Timeout = 30_000 });
        await Expect(page.Locator(".customer-activity-panel[data-section='activity']")).ToContainTextAsync("Changed status from 'Active' to 'Lead'", new() { Timeout = 30_000 });

        await page.GotoAsync(new Uri(intranetBase, $"/customers?search={Uri.EscapeDataString(editedFullName)}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator("body")).ToContainTextAsync(editedFullName, new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Lead", new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Verifies employee-triggered customer notifications flow through NotificationService delivery logs and
    /// customer notification preferences affect later delivery decisions. Covers the executable portion of OPS-003.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "OPS-003")]
    public async Task Intranet_CustomerNotification_QueuesDeliveryAndRespectsOptOutPreference()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(page, intranetBase, "/");
        var customer = await CreateIntranetCorporateCustomerAsync(page);
        var detailResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/customers/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            customer.CustomerId);

        Assert.StartsWith("200 ", detailResult, StringComparison.Ordinal);
        using var detailDocument = JsonDocument.Parse(detailResult[4..]);
        var customerDetail = detailDocument.RootElement;
        var principalId = GetJsonString(customerDetail, "principalId", "PrincipalId");
        Assert.False(string.IsNullOrWhiteSpace(principalId));

        await WaitForIntranetApiTextContainsAsync(page, $"/api/v1/notifications/preferences/{principalId}", "email");

        await page.GotoAsync(new Uri(intranetBase, $"/customers/{customer.CustomerId}").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(page.Locator(".customer-field").Filter(new() { HasText = "Email" }).Locator("input").First).ToHaveValueAsync(customer.Email, new() { Timeout = 30_000 });

        var unique = Guid.NewGuid().ToString("N")[..8];
        var subject = $"OPS003 shipment update {unique}";
        var body = $"Your MALIEV order notification is being verified by Aspire E2E {unique}.";
        var deliveredMessageId = await SendCustomerEmailFromDetailAsync(page, customer.CustomerId, subject, body);

        await WaitForIntranetApiTextContainsAsync(page, "/api/v1/notifications/delivery-logs?page=1&pageSize=50", deliveredMessageId);
        var deliveredLog = await GetNotificationDeliveryLogAsync(page, deliveredMessageId);
        Assert.Equal(principalId, GetJsonString(deliveredLog, "userId", "UserId"));
        Assert.Equal("email", GetJsonString(deliveredLog, "channelType", "ChannelType"));
        Assert.Equal("delivered", GetJsonString(deliveredLog, "status", "Status"));
        Assert.False(string.IsNullOrWhiteSpace(GetJsonString(deliveredLog, "providerMessageId", "ProviderMessageId")));

        var optOutSubject = $"OPS003 opt out {unique}";
        var preferenceUpdate = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/notifications/preferences/${args.principalId}`, {
                    method: 'PUT',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        primaryChannelType: 'email',
                        fallbackChannelTypes: ['sms'],
                        optOutCategories: [args.optOutSubject]
                    })
                });
                return `${r.status} ${await r.text()}`;
            }",
            new { principalId, optOutSubject });

        Assert.StartsWith("200 ", preferenceUpdate, StringComparison.Ordinal);
        Assert.Contains(optOutSubject, preferenceUpdate, StringComparison.OrdinalIgnoreCase);

        await page.Locator(".mlv-modal-actions").GetByRole(AriaRole.Button, new() { NameString = "Cancel" }).ClickAsync();

        var skippedMessageId = await SendCustomerEmailFromDetailAsync(
            page,
            customer.CustomerId,
            optOutSubject,
            $"This notification category should be skipped by preference {unique}.");

        await WaitForIntranetApiTextContainsAsync(page, "/api/v1/notifications/delivery-logs?page=1&pageSize=50", skippedMessageId);
        var skippedLog = await GetNotificationDeliveryLogAsync(page, skippedMessageId);
        Assert.Equal(principalId, GetJsonString(skippedLog, "userId", "UserId"));
        Assert.Equal("failed", GetJsonString(skippedLog, "status", "Status"));
        var skipReason = GetJsonString(skippedLog, "error", "Error", "subject", "Subject", "messageContent", "MessageContent");
        Assert.Contains("Skipped", skipReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opted out", skipReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies global search returns newly created customer records and navigates to the customer workflow.
    /// Covers the indexed, permission-scoped search portion of OPS-001.
    /// </summary>
    [Fact]
    [Trait("Tier", "E2E")]
    [Trait("Stories", "OPS-001,INT-003")]
    public async Task Intranet_GlobalSearch_ReturnsEmployeeCreatedCustomerAndNavigatesToRecord()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var intranetBase = GetEndpoint("IntranetBff");

        await SignInToIntranetAsync(page, intranetBase, "/");
        var customer = await CreateIntranetCustomerAsync(page);

        var indexedResult = await WaitForGlobalSearchResultAsync(page, customer.Email, customer.FullName);
        Assert.True(
            string.Equals("customer", indexedResult.ResourceType, StringComparison.OrdinalIgnoreCase),
            $"Expected a customer search result but got resource type '{indexedResult.ResourceType}'.");
        Assert.Contains("/customers", indexedResult.Href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(customer.FullName, indexedResult.Title, StringComparison.OrdinalIgnoreCase);

        await page.GotoAsync(new Uri(intranetBase, "/").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var globalSearch = page.Locator(".topbar-global-search .global-search-input").First;
        await globalSearch.FillAsync(customer.Email);
        var searchResult = page.Locator(".topbar-global-search .global-search-result").Filter(new() { HasText = customer.FullName }).First;
        await Expect(searchResult).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.Locator(".global-search-backdrop").First.ClickAsync();
        await Expect(page.Locator(".topbar-global-search .global-search-panel")).ToBeHiddenAsync(new() { Timeout = 10_000 });

        await globalSearch.FillAsync(string.Empty);
        await globalSearch.FillAsync(customer.Email);
        searchResult = page.Locator(".topbar-global-search .global-search-result").Filter(new() { HasText = customer.FullName }).First;
        await Expect(searchResult).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await searchResult.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/customers", StringComparison.OrdinalIgnoreCase), new() { Timeout = 30_000 });
        await Expect(page.Locator("body")).ToContainTextAsync(customer.Email, new() { Timeout = 30_000 });
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
            return _fixture.AppFactory!.GetEndpoint(resourceName, "https");
        }
        catch
        {
            return _fixture.AppFactory!.GetEndpoint(resourceName, "http");
        }
    }

    private static async Task SignInToQuoteEngineAsync(IPage page, Uri quoteBase, string email, string returnUrl = "/projects/new")
    {
        await page.GotoAsync(
            new Uri(quoteBase, $"/auth/sign-in?returnUrl={Uri.EscapeDataString(returnUrl)}").ToString(),
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync("PrototypeOnly123!");
        await page.GetByRole(AriaRole.Button, new() { NameString = "Sign in with email" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains(returnUrl, StringComparison.OrdinalIgnoreCase), new() { Timeout = 30_000 });
    }

    private async Task SignInToIntranetAsync(IPage page, Uri intranetBase, string returnUrl)
    {
        await SignInToIntranetAsync(
            page,
            intranetBase,
            returnUrl,
            _fixture.AspireTestAdminEmail,
            _fixture.AspireTestAdminPassword,
            permissionState => permissionState.HasWildcard,
            "wildcard automation permissions");
    }

    private async Task SignInToIntranetAsync(
        IPage page,
        Uri intranetBase,
        string returnUrl,
        string email,
        string password,
        Func<IntranetPermissionState, bool> isReady,
        string readinessDescription)
    {
        var loginUrl = new Uri(intranetBase, $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}").ToString();
        var deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        string? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var tokenState = await GetAuthLoginPermissionStateAsync(email, password);
                if (!isReady(tokenState))
                {
                    lastError = $"AuthService login for {email} succeeded, but {readinessDescription} are not ready. Auth token: {tokenState.Diagnostic}";
                    await page.WaitForTimeoutAsync(2_000);
                    continue;
                }

                await page.GotoAsync(loginUrl, new PageGotoOptions
                {
                    Timeout = 15_000,
                    WaitUntil = WaitUntilState.Commit
                });

                await page.Locator("#login-email, #Username").First.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 15_000
                });
            }
            catch (TimeoutException ex)
            {
                if (!page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
                {
                    var permissionState = await GetIntranetPermissionStateAsync(page);
                    if (isReady(permissionState))
                    {
                        return;
                    }

                    if (permissionState.IsAuthenticated)
                    {
                        lastError = $"Intranet redirected away from login for {email}, but {readinessDescription} are not ready. Auth user: {permissionState.Diagnostic}";
                        await page.GotoAsync(new Uri(intranetBase, "/api/v1/auth/logout").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
                        await page.WaitForTimeoutAsync(2_000);
                        continue;
                    }
                }

                var body = await ReadBodyPreviewAsync(page);
                var content = await ReadHtmlPreviewAsync(page, 2_000);
                var inputs = await ReadInputPreviewAsync(page);
                lastError = $"Intranet login form did not render. {ex.Message}. Url: {page.Url}. Body: {body}. Inputs: {string.Join(" | ", inputs)}. Html: {content[..Math.Min(content.Length, 2_000)]}";
                await page.WaitForTimeoutAsync(2_000);
                continue;
            }

            var emailInput = page.Locator("#login-email, #Username").First;
            var passwordInput = page.Locator("#login-password, #Password").First;
            await emailInput.FillAsync(email, new LocatorFillOptions { Force = true });
            await passwordInput.FillAsync(password, new LocatorFillOptions { Force = true });
            var serverLoginForm = page.Locator("form[action='/api/v1/auth/login-form']").First;
            if (await serverLoginForm.CountAsync() > 0)
            {
                await serverLoginForm.EvaluateAsync("form => form.requestSubmit()");
            }
            else
            {
                await page.Locator("button[type='submit']").First.ClickAsync();
            }

            try
            {
                await page.WaitForURLAsync(
                    url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase),
                    new PageWaitForURLOptions
                    {
                        Timeout = 10_000,
                        WaitUntil = WaitUntilState.Commit
                    });

                var permissionState = await GetIntranetPermissionStateAsync(page);
                if (isReady(permissionState))
                {
                    return;
                }

                lastError = $"Signed in as {email}, but {readinessDescription} are not ready. Auth user: {permissionState.Diagnostic}";
                await page.GotoAsync(new Uri(intranetBase, "/api/v1/auth/logout").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
                await page.WaitForTimeoutAsync(2_000);
            }
            catch (TimeoutException ex)
            {
                var body = await ReadBodyPreviewAsync(page);
                lastError = $"{ex.Message}. Url: {page.Url}. Body: {body}";
                await page.WaitForTimeoutAsync(2_000);
            }
        }

        throw new TimeoutException($"Intranet employee {email} could not sign in before timeout. Last error: {lastError}");
    }

    private async Task<IntranetPermissionState> GetAuthLoginPermissionStateAsync(string email, string password)
    {
        try
        {
            using var authClient = _fixture.CreateClient("AuthService");
            using var response = await authClient.PostAsJsonAsync(
                "/auth/v1/login",
                new
                {
                    username = email,
                    password,
                    user_type = "employee"
                });

            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return new IntranetPermissionState(false, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), $"{(int)response.StatusCode} {responseText}");
            }

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            var accessToken = GetJsonString(root, "access_token", "accessToken");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new IntranetPermissionState(true, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), responseText);
            }

            return GetPermissionStateFromJwt(accessToken, responseText);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        {
            return new IntranetPermissionState(false, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ex.Message);
        }
    }

    private static async Task<string> ReadBodyPreviewAsync(IPage page, int maxLength = 1_000)
    {
        try
        {
            var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            return body[..Math.Min(body.Length, maxLength)];
        }
        catch (PlaywrightException)
        {
            return string.Empty;
        }
        catch (TimeoutException)
        {
            return string.Empty;
        }
    }

    private static async Task<string> ReadResponseTextOrEmptyAsync(IResponse response)
    {
        try
        {
            return await response.TextAsync();
        }
        catch (PlaywrightException)
        {
            return string.Empty;
        }
    }

    private static async Task<JsonElement> RunPurchaseOrderLifecycleActionAsync(
        IPage page,
        int purchaseOrderId,
        string buttonName,
        string pathSuffix,
        string expectedStatus)
    {
        var responseTask = page.WaitForResponseAsync(
            response =>
                response.Url.Contains($"/api/v1/procurement/{purchaseOrderId}/{pathSuffix}", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.GetByRole(AriaRole.Button, new() { NameString = buttonName }).ClickAsync();
        var response = await responseTask;
        var body = await ReadResponseTextOrEmptyAsync(response);
        Assert.True(
            response.Ok,
            $"Purchase order lifecycle action {buttonName} failed with HTTP {response.Status}: {body}");
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement.Clone();
        Assert.Equal(expectedStatus, GetJsonString(root, "status", "Status"));
        await Expect(page.Locator("body")).ToContainTextAsync(expectedStatus, new() { Timeout = 30_000 });
        return root;
    }

    private static async Task<string> ReadHtmlPreviewAsync(IPage page, int maxLength)
    {
        try
        {
            var content = await page.ContentAsync();
            return content[..Math.Min(content.Length, maxLength)];
        }
        catch (PlaywrightException)
        {
            return string.Empty;
        }
    }

    private static async Task<string[]> ReadInputPreviewAsync(IPage page)
    {
        try
        {
            return await page.Locator("input").EvaluateAllAsync<string[]>(
                "els => els.map(e => e.outerHTML)");
        }
        catch (PlaywrightException)
        {
            return [];
        }
    }

    private static async Task<SystemHealthE2EResult> WaitForSystemHealthAsync(IPage page, params string[] requiredHealthyServiceNames)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        var required = requiredHealthyServiceNames.ToHashSet(StringComparer.Ordinal);
        var lastDiagnostic = "No health result was returned.";

        while (DateTimeOffset.UtcNow < deadline)
        {
            string result;
            try
            {
                result = await page.EvaluateAsync<string>(
                    "async () => { const r = await fetch('/api/v1/system-health', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
            }
            catch (PlaywrightException ex)
            {
                lastDiagnostic = $"System health fetch failed: {ex.Message}";
                await page.WaitForTimeoutAsync(3_000);
                continue;
            }

            if (!result.StartsWith("200 ", StringComparison.Ordinal))
            {
                lastDiagnostic = result;
                await page.WaitForTimeoutAsync(3_000);
                continue;
            }

            var body = result[4..];
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (!TryGetJsonProperty(root, out var services, "services", "Services"))
                {
                    lastDiagnostic = $"Health payload did not include services. Body: {body[..Math.Min(body.Length, 2_000)]}";
                    await page.WaitForTimeoutAsync(3_000);
                    continue;
                }

                var statusByService = services.EnumerateArray()
                    .ToDictionary(
                        service => GetJsonString(service, "serviceName", "ServiceName"),
                        service => GetJsonString(service, "status", "Status"),
                        StringComparer.Ordinal);

                var missing = required.Where(service => !statusByService.ContainsKey(service)).ToList();
                var unhealthy = required
                    .Where(service => statusByService.TryGetValue(service, out var status) && !string.Equals("Healthy", status, StringComparison.Ordinal))
                    .Select(service => $"{service}={statusByService[service]}")
                    .ToList();
                var overallStatus = GetJsonString(root, "overallStatus", "OverallStatus");
                var requiredDiagnostics = services.EnumerateArray()
                    .Where(service => required.Contains(GetJsonString(service, "serviceName", "ServiceName")))
                    .Select(service =>
                    {
                        var serviceName = GetJsonString(service, "serviceName", "ServiceName");
                        var status = GetJsonString(service, "status", "Status");
                        var error = GetJsonString(service, "errorMessage", "ErrorMessage");
                        var errorBody = GetJsonString(service, "errorBody", "ErrorBody");
                        var responseTime = GetJsonDouble(service, "responseTimeMs", "ResponseTimeMs");
                        var readinessTime = GetJsonDouble(service, "readinessResponseTimeMs", "ReadinessResponseTimeMs");
                        return $"{serviceName}: status={status}; responseMs={responseTime:N0}; readinessMs={readinessTime:N0}; error={error}; body={errorBody}";
                    })
                    .ToArray();

                if (missing.Count == 0 && unhealthy.Count == 0 && !string.Equals("Unhealthy", overallStatus, StringComparison.Ordinal))
                {
                    return new SystemHealthE2EResult(body, overallStatus, services.GetArrayLength());
                }

                lastDiagnostic = $"Overall={overallStatus}; missing=[{string.Join(", ", missing)}]; unhealthy=[{string.Join(", ", unhealthy)}]; required=[{string.Join(" | ", requiredDiagnostics)}]; body={body[..Math.Min(body.Length, 2_000)]}";
            }
            catch (JsonException ex)
            {
                lastDiagnostic = $"System health returned invalid JSON: {ex.Message}. Raw result: {result[..Math.Min(result.Length, 2_000)]}";
            }

            await page.WaitForTimeoutAsync(3_000);
        }

        Assert.Fail($"System health did not report required services as healthy before timeout. {lastDiagnostic}");
        return default;
    }

    private static void AssertCriticalHealthService(JsonElement services, string serviceName, string expectedLivenessPath, string expectedReadinessPath)
    {
        var service = services.EnumerateArray().SingleOrDefault(element =>
            string.Equals(serviceName, GetJsonString(element, "serviceName", "ServiceName"), StringComparison.Ordinal));

        Assert.NotEqual(default, service.ValueKind);
        Assert.Equal("Healthy", GetJsonString(service, "status", "Status"));
        Assert.True(GetJsonBool(service, "isCritical", "IsCritical"), $"{serviceName} should be marked critical.");
        Assert.Equal(expectedLivenessPath, GetJsonString(service, "livenessPath", "LivenessPath"));
        Assert.Equal(expectedReadinessPath, GetJsonString(service, "readinessPath", "ReadinessPath"));
        Assert.True(GetJsonDouble(service, "livenessResponseTimeMs", "LivenessResponseTimeMs") >= 0);
        Assert.True(GetJsonDouble(service, "readinessResponseTimeMs", "ReadinessResponseTimeMs") >= 0);
    }

    private static async Task<IntranetPermissionState> GetIntranetPermissionStateAsync(IPage page)
    {
        string authUser = string.Empty;
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            try
            {
                authUser = await page.EvaluateAsync<string>(
                    "async () => { const r = await fetch('/api/v1/auth/user', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
                break;
            }
            catch (PlaywrightException ex) when (
                attempt < 12 &&
                ex.Message.Contains("Execution context was destroyed", StringComparison.OrdinalIgnoreCase))
            {
                await page.WaitForTimeoutAsync(500);
            }
            catch (PlaywrightException ex)
            {
                authUser = $"fetch failed: {ex.Message}";
                if (attempt < 12)
                {
                    await page.WaitForTimeoutAsync(500);
                }
            }
        }

        if (!authUser.StartsWith("200 ", StringComparison.Ordinal))
        {
            return new IntranetPermissionState(false, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), authUser);
        }

        using var document = JsonDocument.Parse(authUser[4..]);
        if (!document.RootElement.TryGetProperty("permissions", out var permissions) ||
            permissions.ValueKind != JsonValueKind.Array)
        {
            return new IntranetPermissionState(true, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), authUser);
        }

        var permissionSet = permissions
            .EnumerateArray()
            .Select(permission => permission.GetString() ?? string.Empty)
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasWildcard = permissionSet.Contains("*");
        return new IntranetPermissionState(true, hasWildcard, permissionSet, authUser);
    }

    private static IntranetPermissionState GetPermissionStateFromJwt(string accessToken, string diagnostic)
    {
        var segments = accessToken.Split('.');
        if (segments.Length < 2)
        {
            return new IntranetPermissionState(true, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), diagnostic);
        }

        var payloadBytes = Base64UrlDecode(segments[1]);
        using var document = JsonDocument.Parse(payloadBytes);
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddJwtClaimValues(document.RootElement, permissions, "permissions", "permission");

        return new IntranetPermissionState(true, permissions.Contains("*"), permissions, diagnostic);
    }

    private static void AddJwtClaimValues(JsonElement root, HashSet<string> values, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }
            else if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record IntranetPermissionState(
        bool IsAuthenticated,
        bool HasWildcard,
        IReadOnlySet<string> Permissions,
        string Diagnostic)
    {
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }

    private static async Task<string> RegisterWebCustomerAsync(IPage page, Uri webBase, string returnUrl)
    {
        var unique = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var email = $"e2e-checkout-{unique}@example.com";
        const string password = "E2e-Checkout-12345!";

        await page.GotoAsync(
            new Uri(webBase, $"/auth/sign-up?returnUrl={Uri.EscapeDataString(returnUrl)}").ToString(),
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var signUpForm = page.Locator("form.auth-form[action='/auth/sign-up/email']");
        if (!await signUpForm.IsVisibleAsync())
        {
            await page.Locator("details.auth-email-panel summary").ClickAsync();
        }

        await Expect(signUpForm).ToBeVisibleAsync();
        await signUpForm.Locator("input[name='FirstName']").FillAsync("E2E");
        await signUpForm.Locator("input[name='LastName']").FillAsync("Checkout");
        await signUpForm.Locator("input[name='Email']").FillAsync(email);
        await signUpForm.Locator("input[name='Password']").FillAsync(password);
        await signUpForm.EvaluateAsync("form => form.requestSubmit()");

        await page.WaitForURLAsync(
            url =>
            {
                var current = new Uri(url);
                return current.AbsolutePath.Equals(returnUrl, StringComparison.OrdinalIgnoreCase);
            },
            new PageWaitForURLOptions
            {
                Timeout = 45_000,
                WaitUntil = WaitUntilState.Commit
            });

        return email;
    }

    private static async Task<CreatedIntranetCustomer> CreateIntranetCustomerAsync(IPage page)
    {
        var unique = Guid.NewGuid().ToString("N")[..12];
        var email = $"e2e.intranet.customer.{unique}@maliev.local";
        const string firstName = "E2E";
        var lastName = $"Project Customer {unique[..6]}";
        var fullName = $"{firstName} {lastName}";

        var payload = new
        {
            customer = new
            {
                firstName,
                lastName,
                email,
                mobile = "+66810000000",
                segment = "Retail",
                tier = "Bronze",
                preferredLanguage = "en",
                timezone = "Asia/Bangkok",
                usesCompanyBillingAddress = true,
                paymentTerms = "Due on receipt",
                communicationPreferences = new Dictionary<string, bool>
                {
                    ["email_opt_in"] = true,
                    ["sms_opt_in"] = false,
                    ["marketing_opt_in"] = false
                }
            },
            addresses = Array.Empty<object>(),
            documents = Array.Empty<object>(),
            internalNote = "Created by Aspire browser E2E for customer/project workspace verification."
        };

        var createResult = await page.EvaluateAsync<string>(
            @"async payload => {
                const r = await fetch('/api/v1/customers/create-basic', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            payload);

        Assert.StartsWith("200", createResult, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(createResult[4..]);
        Assert.Equal(email, document.RootElement.GetProperty("email").GetString());

        return new CreatedIntranetCustomer(email, fullName, createResult);
    }

    private sealed record CreatedIntranetCustomer(string Email, string FullName, string RawCreateResponse);

    private static async Task<CreatedCorporateIntranetCustomer> CreateIntranetCorporateCustomerAsync(IPage page)
    {
        var countryResult = await page.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/api/v1/ReferenceData/countries', { credentials: 'include' });
                const text = await r.text();
                if (r.status !== 200) {
                    return `${r.status} ${text}`;
                }

                const body = JSON.parse(text);
                const countries = Array.isArray(body) ? body : (body.data ?? body.Data ?? body.items ?? body.Items ?? []);
                const thailand = countries.find(country => {
                    const code = country.iso2 ?? country.code ?? country.Code ?? country.Iso2 ?? '';
                    const name = country.name ?? country.Name ?? '';
                    return code.toLowerCase() === 'th' || name.toLowerCase().includes('thailand');
                });

                return thailand ? `200 ${JSON.stringify(thailand)}` : `404 ${text}`;
            }");

        Assert.StartsWith("200 ", countryResult, StringComparison.Ordinal);
        using var countryDocument = JsonDocument.Parse(countryResult[4..]);
        var countryId = countryDocument.RootElement.GetProperty("id").GetGuid();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var email = $"e2e.finance.customer.{unique}@maliev.local";
        const string firstName = "E2E";
        var lastName = $"Finance {unique}";
        var fullName = $"{firstName} {lastName}";
        var companyName = $"E2E Finance Components {unique}";
        var taxId = $"01055{DateTimeOffset.UtcNow:HHmmssff}";
        const string billingAddressLine1 = "99 Invoice Verification Road";

        var payload = new
        {
            customer = new
            {
                firstName,
                lastName,
                email,
                mobile = "+66810000000",
                segment = "Manufacturing",
                tier = "Gold",
                preferredLanguage = "en",
                timezone = "Asia/Bangkok",
                usesCompanyBillingAddress = true,
                paymentTerms = "Net 30",
                communicationPreferences = new Dictionary<string, bool>
                {
                    ["email_opt_in"] = true,
                    ["sms_opt_in"] = false,
                    ["marketing_opt_in"] = false
                }
            },
            newCompany = new
            {
                name = companyName,
                vatNumber = taxId,
                registrationNumber = taxId,
                contactEmail = email,
                contactPhone = "+6620000000",
                segment = "Manufacturing",
                tier = "Gold",
                fullNameTh = companyName,
                isVerifiedFromBdex = false
            },
            companyBillingAddress = new
            {
                type = "Billing",
                isDefault = true,
                addressLine1 = billingAddressLine1,
                addressLine2 = "Unit E2E",
                district = "Bang Rak",
                city = "Bangkok",
                stateProvince = "Bangkok",
                postalCode = "10500",
                countryId,
                recipientName = companyName,
                recipientPhone = "+6620000000"
            },
            addresses = new[]
            {
                new
                {
                    type = "Shipping",
                    isDefault = true,
                    addressLine1 = "88 Finance Warehouse Lane",
                    district = "Khlong Toei",
                    city = "Bangkok",
                    stateProvince = "Bangkok",
                    postalCode = "10110",
                    countryId,
                    recipientName = fullName,
                    recipientPhone = "+66810000000"
                }
            },
            documents = Array.Empty<object>(),
            internalNote = "Created by Aspire browser E2E for finance invoice verification."
        };

        var createResult = await page.EvaluateAsync<string>(
            @"async payload => {
                const r = await fetch('/api/v1/customers/create-basic', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            payload);

        Assert.StartsWith("200", createResult, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(createResult[4..]);
        var root = document.RootElement;
        var customerId = root.GetProperty("id").GetGuid();
        Assert.Equal(email, GetJsonString(root, "email", "Email"));

        var detailResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/customers/${id}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            customerId);

        Assert.StartsWith("200 ", detailResult, StringComparison.Ordinal);
        using var detailDocument = JsonDocument.Parse(detailResult[4..]);
        var detail = detailDocument.RootElement;
        Assert.Equal(companyName, GetJsonString(detail, "companyName", "CompanyName"));
        Assert.Equal(taxId, GetJsonString(detail, "companyVatNumber", "CompanyVatNumber"));
        Assert.True(TryGetJsonProperty(detail, out var companyBillingAddress, "companyBillingAddress", "CompanyBillingAddress"));
        Assert.Equal(billingAddressLine1, GetJsonString(companyBillingAddress, "addressLine1", "AddressLine1"));

        return new CreatedCorporateIntranetCustomer(
            customerId,
            email,
            fullName,
            companyName,
            taxId,
            billingAddressLine1,
            createResult);
    }

    private sealed record CreatedCorporateIntranetCustomer(
        Guid CustomerId,
        string Email,
        string FullName,
        string CompanyName,
        string TaxId,
        string BillingAddressLine1,
        string RawCreateResponse);

    private static async Task<CreatedIntranetProject> CreateIntranetProjectAsync(
        IPage page,
        Guid customerId,
        string customerName,
        string title)
    {
        var createResult = await page.EvaluateAsync<string>(
            @"async payload => {
                const r = await fetch('/api/v1/projects', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            new
            {
                customerId,
                customerName,
                title,
                description = "Created by Aspire browser E2E for project quotation lifecycle validation.",
                currency = "THB"
            });

        Assert.True(
            createResult.StartsWith("201 ", StringComparison.Ordinal) ||
            createResult.StartsWith("200 ", StringComparison.Ordinal),
            $"Project create failed. Result: {createResult}");
        using var document = JsonDocument.Parse(createResult[4..]);
        var root = document.RootElement;
        var projectId = GetJsonGuid(root, "id", "Id");
        Assert.NotEqual(Guid.Empty, projectId);
        Assert.Equal(title, GetJsonString(root, "title", "Title"));
        return new CreatedIntranetProject(projectId, GetJsonString(root, "projectNumber", "ProjectNumber"), title);
    }

    private static async Task<UploadedProjectFile> UploadProjectFileAsync(
        IPage page,
        Guid projectId,
        Guid customerId,
        string fileName,
        string contentType)
    {
        var uploadResult = await page.EvaluateAsync<string>(
            @"async args => {
                const step = `ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('MALIEV Aspire E2E demo bracket'),'2;1');
FILE_NAME('${args.fileName}','2026-05-16T00:00:00',('MALIEV'),('MALIEV'),'Aspire E2E','MALIEV','');
ENDSEC;
DATA;
ENDSEC;
END-ISO-10303-21;`;
                const bytes = new TextEncoder().encode(step);
                const initiate = await fetch('/api/v1/uploads/resumable', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        projectId: args.projectId,
                        customerId: args.customerId,
                        fileName: args.fileName,
                        contentType: args.contentType,
                        fileSize: bytes.byteLength
                    })
                });
                const initiateText = await initiate.text();
                if (!initiate.ok) {
                    return `initiate ${initiate.status} ${initiateText}`;
                }

                const session = JSON.parse(initiateText);
                const uploadId = session.uploadId ?? session.UploadId;
                const storagePath = session.storagePath ?? session.StoragePath;
                const put = await fetch(`/api/v1/uploads/resumable/${encodeURIComponent(uploadId)}`, {
                    method: 'PUT',
                    credentials: 'include',
                    headers: {
                        'Content-Type': args.contentType,
                        'Content-Range': `bytes 0-${bytes.byteLength - 1}/${bytes.byteLength}`
                    },
                    body: bytes
                });
                const putText = await put.text();
                if (!put.ok) {
                    return `resume ${put.status} ${putText}`;
                }

                const complete = await fetch(`/api/v1/uploads/resumable/${encodeURIComponent(uploadId)}/complete`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: '{}'
                });
                const completeText = await complete.text();
                if (!complete.ok) {
                    return `complete ${complete.status} ${completeText}`;
                }

                const completed = JSON.parse(completeText);
                completed.uploadId ??= uploadId;
                completed.storagePath ??= storagePath;
                completed.fileName ??= args.fileName;
                return `${complete.status} ${JSON.stringify(completed)}`;
            }",
            new
            {
                projectId,
                customerId,
                fileName,
                contentType
            });

        if (!uploadResult.StartsWith("200 ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Project model upload failed. Result: {uploadResult}");
        }
        using var document = JsonDocument.Parse(uploadResult[4..]);
        var root = document.RootElement;
        var uploadIdText = GetJsonString(root, "uploadId", "UploadId");
        Assert.True(Guid.TryParse(uploadIdText, out var uploadId), $"Upload id was not a GUID. Result: {uploadResult}");
        var storagePath = GetJsonString(root, "storagePath", "StoragePath", "fileReference", "FileReference");
        Assert.False(string.IsNullOrWhiteSpace(storagePath));
        return new UploadedProjectFile(uploadId, fileName, storagePath, contentType);
    }

    private static async Task<UploadedProjectAttachment> UploadProjectAttachmentAsync(
        IPage page,
        Guid projectId,
        Guid customerId,
        Guid partId,
        string kind,
        string fileName,
        string contentType)
    {
        var uploadResult = await page.EvaluateAsync<string>(
            @"async args => {
                const file = new File([`MALIEV ${args.kind} attachment ${args.fileName}`], args.fileName, { type: args.contentType });
                const form = new FormData();
                form.append('files', file);
                const url = `/api/v1/uploads/attachments?projectId=${encodeURIComponent(args.projectId)}&customerId=${encodeURIComponent(args.customerId)}&partId=${encodeURIComponent(args.partId)}&kind=${encodeURIComponent(args.kind)}`;
                const r = await fetch(url, { method: 'POST', credentials: 'include', body: form });
                return `${r.status} ${await r.text()}`;
            }",
            new
            {
                projectId,
                customerId,
                partId,
                kind,
                fileName,
                contentType
            });

        Assert.StartsWith("200 ", uploadResult, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(uploadResult[4..]);
        Assert.True(document.RootElement.ValueKind == JsonValueKind.Array);
        var attachment = Assert.Single(document.RootElement.EnumerateArray());
        var fileId = GetJsonGuid(attachment, "fileId", "FileId");
        var storagePath = GetJsonString(attachment, "storagePath", "StoragePath");
        Assert.NotEqual(Guid.Empty, fileId);
        Assert.False(string.IsNullOrWhiteSpace(storagePath));
        return new UploadedProjectAttachment(
            fileId,
            FirstNonEmpty(GetJsonString(attachment, "fileName", "FileName"), GetJsonString(attachment, "name", "Name"), fileName) ?? fileName,
            storagePath,
            FirstNonEmpty(GetJsonString(attachment, "contentType", "ContentType"), GetJsonString(attachment, "fileType", "FileType"), contentType) ?? contentType,
            GetJsonInt(attachment, "sizeBytes", "SizeBytes", "fileSizeBytes", "FileSizeBytes"));
    }

    private static async Task<CreatedProjectPart> AddConfiguredProjectPartAsync(
        IPage page,
        Guid projectId,
        Guid customerId,
        UploadedProjectFile upload,
        int quantity,
        bool dfmAcknowledged)
    {
        var addResult = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/projects/${args.projectId}/parts`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(args.payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            new
            {
                projectId,
                payload = BuildProjectPartPayload(customerId, upload, quantity, dfmAcknowledged, null, null)
            });

        Assert.True(
            addResult.StartsWith("200 ", StringComparison.Ordinal) ||
            addResult.StartsWith("201 ", StringComparison.Ordinal),
            $"Project part add failed. Result: {addResult}");
        using var document = JsonDocument.Parse(addResult[4..]);
        var root = document.RootElement;
        var partId = GetJsonGuid(root, "id", "Id");
        Assert.NotEqual(Guid.Empty, partId);
        return new CreatedProjectPart(partId, GetJsonString(root, "fileName", "FileName"));
    }

    private static async Task<CreatedProjectPart> UpdateProjectPartConfigurationAsync(
        IPage page,
        Guid projectId,
        Guid partId,
        UploadedProjectFile upload,
        int quantity,
        bool dfmAcknowledged,
        UploadedProjectAttachment drawing,
        UploadedProjectAttachment supportDocument)
    {
        var updateResult = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/projects/${args.projectId}/parts/${args.partId}`, {
                    method: 'PUT',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(args.payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            new
            {
                projectId,
                partId,
                payload = BuildProjectPartPayload(Guid.Empty, upload, quantity, dfmAcknowledged, drawing, supportDocument)
            });

        Assert.StartsWith("204 ", updateResult, StringComparison.Ordinal);
        return new CreatedProjectPart(partId, upload.FileName);
    }

    private static object BuildProjectPartPayload(
        Guid customerId,
        UploadedProjectFile upload,
        int quantity,
        bool dfmAcknowledged,
        UploadedProjectAttachment? drawing,
        UploadedProjectAttachment? supportDocument)
    {
        object[] drawingFiles = drawing is null ? [] : [BuildAttachmentPayload(drawing)];
        object[] supplementaryFiles = supportDocument is null ? [] : [BuildAttachmentPayload(supportDocument)];

        return new
        {
            fileId = upload.UploadId,
            fileReference = upload.StoragePath,
            fileName = upload.FileName,
            processType = "CNC_MILL",
            materialId = customerId == Guid.Empty ? Guid.Parse("11111111-1111-1111-1111-111111111111") : Guid.NewGuid(),
            materialName = "Aluminium 6061-T6",
            materialCode = "AL6061-T6",
            quantity,
            finish = "Anodized",
            color = "Black",
            tolerance = "ISO 2768-m",
            partNotes = "Aspire E2E configured CNC project part.",
            roughnessCode = "Ra1.6",
            dfmAcknowledged,
            hasDfmWarnings = true,
            hasThreadedHoles = true,
            threadedHoleSpec = "M6 x 1.0",
            threadedHoleCount = 4,
            hasInserts = false,
            insertCount = 0,
            bagAndTag = true,
            certificates = new[] { "MaterialCert", "CoC" },
            drawingFiles,
            supplementaryFiles,
            processConfig = new Dictionary<string, string>
            {
                ["anodizeColor"] = "Black",
                ["fixtureSide"] = "A"
            },
            bodyCount = 1,
            bodiesJson = """[{"index":0,"name":"E2E Body"}]""",
            selectedBodyIndex = 0,
            volumeCm3 = 15.25m,
            supportVolumeCm3 = 0m,
            surfaceAreaCm2 = 84.4m,
            boundingBoxX = 120m,
            boundingBoxY = 80m,
            boundingBoxZ = 35m,
            isManifold = true
        };
    }

    private static object BuildAttachmentPayload(UploadedProjectAttachment attachment) => new
    {
        fileId = attachment.FileId,
        fileName = attachment.FileName,
        storagePath = attachment.StoragePath,
        contentType = attachment.ContentType,
        sizeBytes = attachment.SizeBytes,
        uploadedAt = DateTime.UtcNow
    };

    private static async Task ConfirmProjectPartPriceAsync(IPage page, Guid projectId, Guid partId, decimal unitPrice)
    {
        var result = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/projects/${args.projectId}/parts/${args.partId}/confirm-price`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ confirmedUnitPrice: args.unitPrice })
                });
                return `${r.status} ${await r.text()}`;
            }",
            new { projectId, partId, unitPrice });

        Assert.True(
            result.StartsWith("204 ", StringComparison.Ordinal) ||
            result.StartsWith("200 ", StringComparison.Ordinal),
            $"Project part price confirmation failed. Result: {result}");
    }

    private static async Task<JsonElement> GenerateProjectQuotationAsync(
        IPage page,
        Guid projectId,
        string changeSummary,
        decimal manualDiscountAmount,
        decimal shippingCost)
    {
        var result = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/projects/${args.projectId}/generate-quotation`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        validityDays: 30,
                        deliveryExpectations: 'Standard production lead time, verified by Aspire E2E.',
                        bulkDiscountAmount: 0,
                        manualDiscountAmount: args.manualDiscountAmount,
                        shippingCost: args.shippingCost,
                        taxAmount: 0,
                        quotationTerms: 'E2E commercial terms for immutable quotation snapshot verification.',
                        changeSummary: args.changeSummary,
                        idempotencyKey: `${args.projectId}:${Date.now()}`
                    })
                });
                const body = await r.text();
                if (!r.ok) {
                    const projectResponse = await fetch(`/api/v1/projects/${args.projectId}`, { credentials: 'include' });
                    const projectBody = await projectResponse.text();
                    let projectDiagnostic = `${projectResponse.status}`;
                    let quotationDiagnostic = '<not available>';
                    let latestPdfDiagnostic = '<not available>';

                    try {
                        const projectJson = JSON.parse(projectBody);
                        const quotationId = projectJson.quotationId ?? projectJson.QuotationId;
                        const quotationNumber = projectJson.quotationNumber ?? projectJson.QuotationNumber;
                        const currentVersion = projectJson.currentQuotationVersionNumber ?? projectJson.CurrentQuotationVersionNumber;
                        const projectStatus = projectJson.status ?? projectJson.Status;
                        projectDiagnostic = `${projectResponse.status} quotationId=${quotationId ?? '<null>'} quotationNumber=${quotationNumber ?? '<null>'} currentVersion=${currentVersion ?? '<null>'} status=${projectStatus ?? '<null>'}`;
                        if (quotationId) {
                            const quotationResponse = await fetch(`/api/v1/quotations/${quotationId}`, { credentials: 'include' });
                            const quotationText = await quotationResponse.text();
                            quotationDiagnostic = `${quotationResponse.status} ${quotationText.substring(0, 1200)}`;
                            const latestPdfResponse = await fetch(`/api/v1/quotations/${quotationId}/pdf/latest`, { credentials: 'include' });
                            const latestPdfText = await latestPdfResponse.text();
                            latestPdfDiagnostic = `${latestPdfResponse.status} ${latestPdfText.substring(0, 1200)}`;
                        }
                    } catch (error) {
                        quotationDiagnostic = `diagnostic error: ${error}`;
                    }

                    return `${r.status} ${body} | project=${projectDiagnostic} | quotation=${quotationDiagnostic} | latestPdf=${latestPdfDiagnostic}`;
                }

                const projectResponse = await fetch(`/api/v1/projects/${args.projectId}`, { credentials: 'include' });
                return `${projectResponse.status} ${await projectResponse.text()}`;
            }",
            new
            {
                projectId,
                changeSummary,
                manualDiscountAmount,
                shippingCost
            });

        Assert.True(
            result.StartsWith("200 ", StringComparison.Ordinal),
            $"Project quotation generation failed. Result: {result}");
        using var document = JsonDocument.Parse(result[4..]);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GetIntranetProjectAsync(IPage page, Guid projectId)
    {
        var result = await page.EvaluateAsync<string>(
            @"async projectId => {
                const r = await fetch(`/api/v1/projects/${projectId}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            projectId);

        Assert.StartsWith("200 ", result, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result[4..]);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GetIntranetQuotationAsync(IPage page, Guid quotationId)
    {
        var result = await page.EvaluateAsync<string>(
            @"async quotationId => {
                const r = await fetch(`/api/v1/quotations/${quotationId}`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            quotationId);

        Assert.StartsWith("200 ", result, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result[4..]);
        return document.RootElement.Clone();
    }

    private static async Task<string> GetLatestQuotationPdfAsync(IPage page, Guid quotationId)
    {
        var result = await page.EvaluateAsync<string>(
            @"async quotationId => {
                const r = await fetch(`/api/v1/quotations/${quotationId}/pdf/latest`, { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }",
            quotationId);

        Assert.StartsWith("200 ", result, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result[4..]);
        return GetJsonString(document.RootElement, "storageUrl", "StorageUrl", "url", "Url");
    }

    private static async Task<JsonElement> DuplicateIntranetProjectAsync(IPage page, Guid projectId, string title)
    {
        var result = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/projects/${args.projectId}/duplicate`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ title: args.title })
                });
                return `${r.status} ${await r.text()}`;
            }",
            new { projectId, title });

        Assert.StartsWith("200 ", result, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result[4..]);
        return document.RootElement.Clone();
    }

    private static JsonElement FindProjectPart(JsonElement project, Guid partId)
    {
        Assert.True(TryGetJsonProperty(project, out var parts, "parts", "Parts"));
        foreach (var part in parts.EnumerateArray())
        {
            if (GetJsonGuid(part, "id", "Id") == partId)
            {
                return part.Clone();
            }
        }

        Assert.Fail($"Project part {partId} was not found.");
        return default;
    }

    private sealed record CreatedIntranetProject(Guid ProjectId, string ProjectNumber, string Title);

    private sealed record UploadedProjectFile(Guid UploadId, string FileName, string StoragePath, string ContentType);

    private sealed record UploadedProjectAttachment(Guid FileId, string FileName, string StoragePath, string ContentType, int SizeBytes);

    private sealed record CreatedProjectPart(Guid PartId, string FileName);

    private static async Task<CreatedIntranetSupplier> CreateIntranetSupplierAsync(IPage page)
    {
        var unique = Guid.NewGuid().ToString("N")[..12];
        var name = $"E2E Supplier Metals {unique}";
        var taxId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var email = $"supplier.{unique}@maliev.local";

        var createResult = await page.EvaluateAsync<string>(
            @"async payload => {
                const r = await fetch('/api/v1/suppliers', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            new
            {
                name,
                taxId,
                email,
                phone = "+6625550123",
                country = "Thailand",
                address = "18 Procurement Verification Road",
                city = "Bangkok",
                postalCode = "10310",
                contactPerson = "E2E Supplier Contact",
                website = "https://supplier.e2e.maliev.local",
                capabilities = new[] { "CNC", "Aluminium" }
            });

        Assert.True(
            createResult.StartsWith("201", StringComparison.Ordinal),
            $"Supplier create failed. Result: {createResult}");
        using var document = JsonDocument.Parse(createResult[4..]);
        var root = document.RootElement;
        var supplierId = root.GetProperty("id").GetGuid();
        Assert.Equal(name, GetJsonString(root, "companyName", "CompanyName"));
        Assert.Equal(taxId, GetJsonString(root, "taxId", "TaxId"));

        return new CreatedIntranetSupplier(supplierId, name, taxId, email);
    }

    private sealed record CreatedIntranetSupplier(Guid Id, string Name, string TaxId, string Email);

    private static async Task<string> SendCustomerEmailFromDetailAsync(IPage page, Guid customerId, string subject, string body)
    {
        var sendResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/customers/{customerId}/email", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
            new PageWaitForResponseOptions { Timeout = 90_000 });

        await page.Locator("button.customer-email-open").ClickAsync();
        await page.Locator(".customer-email-subject").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await page.Locator(".customer-email-subject").FillAsync(subject);
        await page.Locator(".customer-email-body").FillAsync(body);
        await page.Locator("button.customer-email-send").ClickAsync();

        var sendResponse = await sendResponseTask;
        var sendBody = await ReadResponseTextOrEmptyAsync(sendResponse);
        Assert.True(sendResponse.Ok, $"Customer notification request failed with HTTP {sendResponse.Status}: {sendBody}");
        using var document = JsonDocument.Parse(sendBody);
        var messageId = GetJsonString(document.RootElement, "messageId", "MessageId");
        Assert.False(string.IsNullOrWhiteSpace(messageId));
        await page.Locator(".customer-email-state.success").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        return messageId;
    }

    private static async Task<JsonElement> GetNotificationDeliveryLogAsync(IPage page, string eventId)
    {
        var logsResult = await page.EvaluateAsync<string>(
            @"async () => {
                const r = await fetch('/api/v1/notifications/delivery-logs?page=1&pageSize=50', { credentials: 'include' });
                return `${r.status} ${await r.text()}`;
            }");

        Assert.StartsWith("200 ", logsResult, StringComparison.Ordinal);
        using var logsDocument = JsonDocument.Parse(logsResult[4..]);
        Assert.True(TryGetJsonProperty(logsDocument.RootElement, out var logs, "data", "Data", "items", "Items"));

        foreach (var log in logs.EnumerateArray())
        {
            if (string.Equals(eventId, GetJsonString(log, "eventId", "EventId"), StringComparison.OrdinalIgnoreCase))
            {
                return log.Clone();
            }
        }

        Assert.Fail($"Notification delivery log for event {eventId} was not found. Last result: {logsResult[..Math.Min(logsResult.Length, 2_000)]}");
        return default;
    }

    private static async Task WaitForIntranetApiTextContainsAsync(IPage page, string path, string expectedText)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var lastResult = string.Empty;

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastResult = await page.EvaluateAsync<string>(
                @"async path => {
                    const r = await fetch(path, { credentials: 'include' });
                    return `${r.status} ${await r.text()}`;
                }",
                path);

            if (lastResult.StartsWith("200 ", StringComparison.Ordinal) &&
                lastResult.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await page.WaitForTimeoutAsync(1_000);
        }

        Assert.Fail($"Expected {path} to contain {expectedText} before timeout. Last result: {lastResult[..Math.Min(lastResult.Length, 2_000)]}");
    }

    private static async Task SelectCustomerInPickerAsync(IPage page, string query, string expectedName)
    {
        var customerPickerTrigger = page.Locator(".customer-picker-trigger").First;
        await Expect(customerPickerTrigger).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await customerPickerTrigger.ClickAsync();
        var customerSearch = page.Locator(".customer-picker-search-input");
        await customerSearch.FillAsync(query);
        var customerOption = page.Locator(".customer-picker-option").Filter(new() { HasText = expectedName });
        await Expect(customerOption).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await customerOption.ClickAsync();
    }

    private static async Task OpenCustomerCreateTabAsync(IPage page, string tabName)
    {
        await page.Locator(".customer-create-tabs button").Filter(new() { HasText = tabName }).ClickAsync();
        await Expect(page.Locator(".customer-create-tab-panel")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    private static async Task FillAddressFieldsAsync(
        ILocator scope,
        string line1,
        string subdistrict,
        string district,
        string province,
        string postalCode)
    {
        await FillScopedTextboxAsync(scope, "Address line 1", line1);
        await FillScopedTextboxAsync(scope, "Subdistrict", subdistrict);
        await FillScopedTextboxAsync(scope, "District", district);
        await FillScopedTextboxAsync(scope, "State / Province", province);
        await FillScopedTextboxAsync(scope, "Postal code", postalCode);
    }

    private static async Task FillScopedTextboxAsync(ILocator scope, string label, string value)
    {
        var labelLiteral = ToXPathLiteral(label);
        await scope
            .Locator($"xpath=.//label[contains(concat(' ', normalize-space(@class), ' '), ' mlv-form-field ')][span[normalize-space(.) = {labelLiteral}]]//input")
            .FillAsync(value);
    }

    private static string ToXPathLiteral(string value)
    {
        if (!value.Contains('\'', StringComparison.Ordinal))
        {
            return $"'{value}'";
        }

        var parts = value.Split('\'').Select(part => $"'{part}'");
        return $"concat({string.Join(", \"'\", ", parts)})";
    }

    private static async Task<GlobalSearchE2EResult> WaitForGlobalSearchResultAsync(IPage page, string query, string expectedTitle)
    {
        var searchResult = await page.EvaluateAsync<string>(
            @"async args => {
                const deadline = Date.now() + 60000;
                let last = '';
                while (Date.now() < deadline) {
                    const r = await fetch(`/api/v1/search?query=${encodeURIComponent(args.query)}&limit=10`, { credentials: 'include' });
                    const text = await r.text();
                    last = `${r.status} ${text}`;
                    if (r.status === 200) {
                        const body = JSON.parse(text);
                        const results = body.results ?? body.Results ?? [];
                        const match = results.find(result => {
                            const title = result.title ?? result.Title ?? '';
                            const subtitle = result.subtitle ?? result.Subtitle ?? '';
                            return title.toLowerCase().includes(args.expectedTitle.toLowerCase()) ||
                                subtitle.toLowerCase().includes(args.query.toLowerCase());
                        });
                        if (match) {
                            return `200 ${JSON.stringify(match)}`;
                        }
                    }
                    await new Promise(resolve => setTimeout(resolve, 1000));
                }

                return last;
            }",
            new { query, expectedTitle });

        Assert.StartsWith("200", searchResult, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(searchResult[4..]);
        var root = document.RootElement;
        return new GlobalSearchE2EResult(
            GetJsonString(root, "title", "Title"),
            GetJsonString(root, "resourceType", "ResourceType"),
            GetJsonString(root, "href", "Href"));
    }

    private static string GetJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static Guid GetJsonGuid(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return Guid.Empty;
    }

    private static int GetJsonInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static bool GetJsonBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                return value.ValueKind == JsonValueKind.True;
            }
        }

        return false;
    }

    private static double GetJsonDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.TryGetDouble(out var number))
            {
                return number;
            }
        }

        return 0;
    }

    private static List<string> GetJsonStringArray(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return value
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        return [];
    }

    private static bool TryGetJsonProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        property = default;
        return false;
    }

    private sealed record GlobalSearchE2EResult(string Title, string ResourceType, string Href);

    private sealed record SystemHealthE2EResult(string Body, string OverallStatus, int ServiceCount);

    private static async Task<CommerceE2EProduct> CreateCommerceProductAsync(IPage page)
    {
        var unique = Guid.NewGuid().ToString("N")[..12];
        var handle = $"e2e-storefront-product-{unique}";
        var title = $"E2E Storefront Product {unique}";
        var payload = new
        {
            handle,
            title,
            brand = "MALIEV",
            summary = "Browser E2E published catalog product.",
            description = "Created by the Aspire browser E2E gate to verify employee catalog management and Web storefront exposure.",
            productType = "Printed Product",
            status = "Draft",
            variants = new[]
            {
                new
                {
                    sku = $"E2E-{unique}",
                    title = "Default",
                    priceAmount = 1490.00m,
                    currency = "THB",
                    inventoryQuantity = 7,
                    optionValuesJson = "{\"Lead time\":\"3 business days\"}"
                }
            },
            media = Array.Empty<object>(),
            collectionHandles = Array.Empty<string>()
        };

        var createResult = await page.EvaluateAsync<string>(
            @"async payload => {
                const r = await fetch('/api/v1/commerce/products', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return `${r.status} ${await r.text()}`;
            }",
            payload);

        Assert.StartsWith("201", createResult, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(createResult[4..]);
        return new CommerceE2EProduct(
            document.RootElement.GetProperty("id").GetGuid(),
            document.RootElement.GetProperty("handle").GetString() ?? handle,
            document.RootElement.GetProperty("title").GetString() ?? title,
            document.RootElement.GetProperty("status").GetString() ?? "Draft");
    }

    private static async Task<CommerceE2EProduct> UpdateCommerceProductStatusAsync(IPage page, CommerceE2EProduct product, string status)
    {
        var updateResult = await page.EvaluateAsync<string>(
            @"async args => {
                const r = await fetch(`/api/v1/commerce/products/${args.id}/status`, {
                    method: 'PATCH',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ status: args.status })
                });
                return `${r.status} ${await r.text()}`;
            }",
            new { id = product.Id, status });

        Assert.StartsWith("200", updateResult, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(updateResult[4..]);
        return product with { Status = document.RootElement.GetProperty("status").GetString() ?? status };
    }

    private static async Task ArchiveCommerceProductAsync(IPage page, CommerceE2EProduct product)
    {
        var archiveResult = await page.EvaluateAsync<string>(
            @"async id => {
                const r = await fetch(`/api/v1/commerce/products/${id}`, {
                    method: 'DELETE',
                    credentials: 'include'
                });
                return `${r.status} ${await r.text()}`;
            }",
            product.Id);

        Assert.StartsWith("204", archiveResult, StringComparison.Ordinal);
    }

    private sealed record CommerceE2EProduct(Guid Id, string Handle, string Title, string Status);

    private static async Task AssertIntranetRouteAsync(IPage page, Uri intranetBase, string path, Regex expectedText)
    {
        await page.GotoAsync(new Uri(intranetBase, path).ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.DoesNotContain("/login", page.Url, StringComparison.OrdinalIgnoreCase);
        await Expect(page.Locator("body")).Not.ToBeEmptyAsync(new() { Timeout = 15_000 });
        try
        {
            await Expect(page.Locator("body")).ToContainTextAsync(expectedText, new() { Timeout = 15_000 });
        }
        catch (Exception ex)
        {
            var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
            var authUser = await page.EvaluateAsync<string>(
                "async () => { const r = await fetch('/api/v1/auth/user', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");
            throw new InvalidOperationException(
                $"Intranet route {path} did not render expected text. Url: {page.Url}. Body: {body[..Math.Min(body.Length, 1_000)]}. Auth user: {authUser}",
                ex);
        }
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
