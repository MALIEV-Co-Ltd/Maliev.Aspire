using Maliev.Aspire.Tests.Infrastructure;
using Microsoft.Playwright;
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
        await Expect(page.Locator(".customer-picker-option").Filter(new() { HasText = customer.FullName })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.Locator(".customer-picker-option").Filter(new() { HasText = customer.FullName }).ClickAsync();
        await Expect(page.Locator(".ccc-root")).ToContainTextAsync(customer.FullName, new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Drop files here or click to upload", new() { Timeout = 15_000 });
        await Expect(page.Locator("body")).ToContainTextAsync("Quote Total", new() { Timeout = 15_000 });
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
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        string? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            await page.GotoAsync(loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
            try
            {
                await page.Locator("#login-email, #Username").First.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 15_000
                });
            }
            catch (TimeoutException ex)
            {
                var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
                var content = await page.ContentAsync();
                var inputs = await page.Locator("input").EvaluateAllAsync<string[]>(
                    "els => els.map(e => e.outerHTML)");
                throw new TimeoutException(
                    $"Intranet login form did not render. Url: {page.Url}. Body: {body[..Math.Min(body.Length, 1_000)]}. Inputs: {string.Join(" | ", inputs)}. Html: {content[..Math.Min(content.Length, 2_000)]}",
                    ex);
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
                var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2_000 });
                lastError = $"{ex.Message}. Url: {page.Url}. Body: {body[..Math.Min(body.Length, 1_000)]}";
                await page.WaitForTimeoutAsync(2_000);
            }
        }

        throw new TimeoutException($"Intranet employee {email} could not sign in before timeout. Last error: {lastError}");
    }

    private static async Task<IntranetPermissionState> GetIntranetPermissionStateAsync(IPage page)
    {
        var authUser = await page.EvaluateAsync<string>(
            "async () => { const r = await fetch('/api/v1/auth/user', { credentials: 'include' }); return `${r.status} ${await r.text()}`; }");

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
        const string lastName = "Project Customer";
        const string fullName = $"{firstName} {lastName}";

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

    private sealed record GlobalSearchE2EResult(string Title, string ResourceType, string Href);

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
