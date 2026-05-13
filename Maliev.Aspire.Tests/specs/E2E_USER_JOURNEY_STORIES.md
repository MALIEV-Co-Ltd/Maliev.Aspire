# MALIEV Aspire E2E User Journey Stories

> Production-gate user journey catalog for browser-driven E2E tests against the Aspire integrated environment.
>
> Last updated: 2026-05-14

## Purpose

This document defines the user stories that must be verified before MALIEV is promoted to production. The stories are intentionally higher level than unit, service integration, or Aspire API workflow tests. They verify complete user journeys through the real user interfaces, BFFs, service discovery, databases, messaging, generated artifacts, and operational health surfaces.

Use this document as the source of truth when creating future Playwright E2E suites. A future automated test does not need to assert every internal field from every service, but it must prove the visible journey, the core downstream records, and the cross-service side effects listed here.

## Status Legend

| Status | Meaning |
| --- | --- |
| Ready to automate | The route and service path exist and can be turned into an E2E test once test data and credentials are available. |
| Partial | The main path exists, but part of the expected production journey is prototype-backed, manual, or missing a downstream connection. |
| Required gap | The story describes a production-gate requirement that is not yet implemented end to end. |

## Shared E2E Rules

- Run against the full `Maliev.Aspire.AppHost`, not isolated BFF or service test hosts.
- Use browser-visible assertions first: route, page state, validation messages, generated links, downloadable files, and status updates.
- Use service-level verification only for journey outcomes: customer/account/order/project/payment/PDF/document records, emitted events, and generated artifacts.
- Do not duplicate endpoint CRUD coverage from per-service tests.
- Do not assert private implementation details unless they are the only reliable way to prove the cross-service side effect.
- Keep the current Intranet ProjectNew quote/project lifecycle untouched until the product owner revisits the separate QuoteEngine/ProjectNew lifecycle discussion.
- Use unique test data per run: customer email, company name, project reference, quote number, product handle, and order number.
- Use the Aspire dashboard, service health endpoints, logs, and traces as supporting verification for failures and event-driven waits.
- Never rely on fixed sleeps. Future automated tests must poll visible UI state or downstream records until the expected state appears or a meaningful timeout expires.

## Aspire Integrated Surfaces

| Surface | Role in E2E gate | Current use |
| --- | --- | --- |
| `Maliev.Web` | Customer-facing website, marketing, storefront, cart, customer account, contact, and quote entry | Active customer surface backed by `Maliev.Web.Bff`. |
| `Maliev.QuoteEngine` | Dedicated customer quoting platform for CAD upload, part configuration, DFM, pricing, quote approval, and order handoff | Must be wired into Aspire. Current BFF has prototype-backed quote/session behavior that must be marked partial until real service-backed workflows replace it. |
| `Maliev.Intranet` | Employee ERP/CRM for customer management, ProjectNew, quotations, commerce catalog, orders, payments, procurement, HR, and operations | Active employee surface backed by `Maliev.Intranet.Bff`. |
| Backend services | Domain owners for auth, identity, customer, upload, geometry, pricing, quotation, PDF, order, payment, delivery, commerce, search, notification, procurement, HR, and operations | Verified by Aspire system tests today; E2E must verify them through real user journeys. |

## Known Production-Gate Gaps

- Customer email sign-up currently creates customer/account records and allows authentication, but a full email verification token and email-link confirmation journey was not found in the current Web/Auth/Customer/Notification flow.
- Customer password reset token creation exists, but the full "receive reset email, click link, return to site" browser journey must be verified or implemented with the notification/email provider.
- `Maliev.QuoteEngine` currently contains prototype-backed session and quote behavior. E2E stories must mark these flows partial until the BFF uses the real Upload, Geometry, Pricing, Project, Quotation, PDF, Order, Payment, and Delivery services.
- ProjectNew and QuoteEngine lifecycle unification is explicitly deferred. QuoteEngine-local portal stories may be planned, but no story in this pass requires ProjectNew to share or replace its current quote/project workflow.
- Browser E2E automation is not yet implemented in this repo. This document is the production-gate story catalog for that future suite.

---

# Customer Website And Account Stories

## WEB-001: Visitor discovers services and starts quote path

**Persona:** Prospective customer evaluating MALIEV manufacturing services.

**Entry point:** `Maliev.Web` root page, then `/services`, `/services/{slug}`, and quote entry.

**Business value:** Confirms that the public website clearly moves a visitor from service discovery into a quote journey without employee assistance.

**Prerequisites:**
- Aspire is running with `WebBff`, `CommerceService`, `MaterialService`, `PricingService`, `UploadService`, and dependencies healthy.
- Public service/catalog content is seeded or available.

**User path:**
1. Open the Web home page.
2. Verify the page identifies MALIEV and exposes manufacturing services.
3. Navigate to the services listing.
4. Open a service detail page for a manufacturing capability.
5. Start the quote path from the service page.
6. Confirm the browser reaches the quote entry surface without a broken route or missing state.

**Features covered:**
- Web shell routing.
- Service marketing pages.
- Quote handoff from marketing to quote.
- Public BFF service discovery.

**Services involved:**
- `Maliev.Web.Bff`
- `CommerceService` if service/product data is catalog-backed
- `MaterialService` and `PricingService` if quote entry preloads options
- `UploadService` if quote entry supports immediate file upload

**Data created or mutated:**
- None required for the discovery portion.
- Optional anonymous quote draft/session if the quote entry creates one.

**Verification checklist:**
- The home page loads with no browser console errors that block rendering.
- `/services` renders service cards or service rows.
- `/services/{slug}` renders the selected service detail.
- The quote CTA navigates to the correct quote entry route.
- The quote route preserves service context where the UI supports it.
- All referenced assets and media load without 404s.

**Observability checks:**
- `WebBff` is healthy at `/web/aspire-liveness`.
- Any downstream service used by the quote entry is healthy in Aspire.
- Logs show no service-discovery fallback to hardcoded hostnames.

**Current implementation status:** Ready to automate for Web discovery and handoff. Quote destination must be aligned with the final Web-to-QuoteEngine product decision.

**Known product gaps:** The final destination for a serious manufacturing quote should become `Maliev.QuoteEngine` once the dedicated quote platform is production-backed.

## WEB-002: Visitor submits contact inquiry

**Persona:** Prospective customer who wants a human follow-up before quoting.

**Entry point:** `Maliev.Web` contact form or contact CTA.

**Business value:** Confirms that a customer inquiry is captured and routed to the business instead of being lost in a static form.

**Prerequisites:**
- Aspire is running with `WebBff`, `ContactService`, and any notification/search dependencies healthy.
- Test inquiry email address and phone number are unique.

**User path:**
1. Open the Web contact surface.
2. Enter name, company, email, phone, subject, and message.
3. Submit the inquiry.
4. Verify the form shows a success state and prevents duplicate accidental submission.
5. Open the employee-facing follow-up surface when available.

**Features covered:**
- Public contact form validation.
- Web BFF forwarding.
- ContactService persistence.
- Optional employee visibility and notification.

**Services involved:**
- `Maliev.Web.Bff`
- `ContactService`
- `NotificationService` if inquiry notifications are configured
- `SearchService` if inquiry/customer search indexing is expected

**Data created or mutated:**
- Contact inquiry/message record.
- Optional notification delivery record.

**Verification checklist:**
- Required fields show clear validation before submission.
- Submit button enters a busy state and does not create duplicates on repeated clicks.
- Success state includes enough detail for the customer to know the inquiry was received.
- ContactService contains exactly one inquiry for the unique test data.
- If employee UI exists, the inquiry is visible to authorized employees only.

**Observability checks:**
- `WebBff` logs show a successful call to ContactService.
- ContactService health remains green.
- Notification logs show delivery or queued status if notification is part of the workflow.

**Current implementation status:** Ready to automate for Web BFF to ContactService. Employee follow-up visibility depends on the current Intranet contact UI.

**Known product gaps:** If no employee inquiry queue exists, the story should fail as a product gap after ContactService persistence is verified.

## WEB-003: Customer signs up with email and password

**Persona:** Customer creating their own MALIEV account.

**Entry point:** `Maliev.Web` `/auth/sign-up`.

**Business value:** Confirms that a customer can self-onboard without employee data entry.

**Prerequisites:**
- Aspire is running with `WebBff`, `CustomerService`, `AuthService`, `IAMService`, `CountryService`, and `NotificationService` healthy.
- Unique email address is available for the run.
- Email capture/sandbox is configured if the verification flow exists.

**User path:**
1. Open `/auth/sign-up`.
2. Enter first name, last name, company details where required, email, and password.
3. Submit the registration form.
4. Verify customer and account creation succeeds.
5. Attempt sign-in with the new credentials.
6. If email verification is required, verify the app blocks privileged account actions until verification is complete.

**Features covered:**
- Customer self-registration.
- CustomerService account creation.
- IAM principal creation or reuse.
- AuthService customer login.
- Customer session establishment in Web BFF.

**Services involved:**
- `Maliev.Web.Bff`
- `CustomerService`
- `AuthService`
- `IAMService`
- `CountryService` if profile/address fields are required
- `NotificationService` if welcome/verification notifications are produced
- `SearchService` if customer indexing is expected

**Data created or mutated:**
- Customer record.
- Customer account record.
- IAM principal/identity.
- Optional notification settings and search index record.

**Verification checklist:**
- Form validates password, email format, and required profile fields.
- CustomerService creates exactly one customer for the email.
- IAMService contains or resolves the customer principal.
- AuthService accepts login for `user_type=customer`.
- Web shows authenticated account navigation after successful sign-in.
- Duplicate registration with the same email is handled with a clear message and no duplicate customer.

**Observability checks:**
- Customer creation emits the expected customer-created side effects.
- Auth logs show successful customer login and no employee-user confusion.
- Aspire shows WebBff, CustomerService, AuthService, and IAMService healthy.

**Current implementation status:** Partial. Registration and authentication paths exist, but full email verification was not found.

**Known product gaps:** The production gate requires email verification token issuance, email delivery, link handling, token validation, and verified account state.

## WEB-004: Customer verifies email from received link

**Persona:** Newly registered customer proving ownership of their email address.

**Entry point:** Email inbox link leading back to `Maliev.Web`.

**Business value:** Prevents unverified accounts from becoming trusted customer identities.

**Prerequisites:**
- A customer account was created by `WEB-003`.
- Email verification token generation is implemented.
- Email sandbox/provider is accessible to the test runner.

**User path:**
1. Complete customer registration.
2. Verify the platform issues an email verification token.
3. Open the verification email.
4. Verify the email contains clear instructions and a link back to MALIEV.
5. Click the verification link.
6. Confirm the link redirects to the Web app with token context.
7. Confirm the app validates the token and marks the account email verified.
8. Sign in and access verified-account functionality.

**Features covered:**
- Verification token creation.
- Notification/email delivery.
- Verification link routing.
- Token validation.
- Account status transition.

**Services involved:**
- `Maliev.Web.Bff`
- `CustomerService`
- `AuthService`
- `NotificationService`
- `IAMService`

**Data created or mutated:**
- Email verification token.
- Account `EmailVerified` or equivalent verified state.
- Notification/email delivery record.

**Verification checklist:**
- Token is single use.
- Expired or invalid token shows a safe error and does not verify the account.
- Verification link preserves the token and email/account context.
- Successful verification changes the customer account state.
- Customer can sign in after verification.
- Verified status is visible in account or employee customer detail where applicable.

**Observability checks:**
- NotificationService records email send attempt and result.
- CustomerService or AuthService logs token validation without exposing token values.
- Aspire health remains green across Web/Auth/Customer/Notification.

**Current implementation status:** Required gap.

**Known product gaps:** Implement the full token and email-link flow before this story can pass production gate.

## WEB-005: Customer signs in, refreshes session, opens account, and signs out

**Persona:** Returning customer managing their account.

**Entry point:** `Maliev.Web` `/auth/sign-in`.

**Business value:** Confirms that customer authentication and BFF session handling work across normal account navigation.

**Prerequisites:**
- Existing customer account with known credentials.
- Aspire is running with Web/Auth/Customer/IAM services healthy.

**User path:**
1. Open `/auth/sign-in`.
2. Enter customer email and password.
3. Submit sign-in.
4. Navigate to account profile, addresses, preferences, and orders.
5. Refresh the browser and confirm the session is preserved or refreshed.
6. Sign out.
7. Confirm protected account routes redirect to sign-in.

**Features covered:**
- Customer login.
- BFF cookie/session handling.
- Token refresh.
- Protected account routes.
- Logout/revoke behavior.

**Services involved:**
- `Maliev.Web.Bff`
- `AuthService`
- `CustomerService`
- `IAMService`
- `OrderService` for account orders
- `DeliveryService` for address/order delivery data

**Data created or mutated:**
- Auth session/token records if persisted.
- Optional refresh token rotation/revocation.

**Verification checklist:**
- Invalid credentials show a safe error and do not establish a session.
- Valid credentials route to account or previous return URL.
- Account pages show the signed-in customer, not a generic profile.
- Browser refresh does not lose session unexpectedly.
- Logout clears customer session and protected pages require sign-in again.

**Observability checks:**
- AuthService logs customer login with `user_type=customer`.
- No employee Auth route is used for customer login.
- WebBff health remains green.

**Current implementation status:** Ready to automate.

**Known product gaps:** Verified-account restrictions should be added once email verification exists.

## WEB-006: Customer signs in or registers with Google

**Persona:** Customer using Google identity instead of a password.

**Entry point:** `Maliev.Web` Google sign-in action.

**Business value:** Reduces account friction while preserving customer identity ownership.

**Prerequisites:**
- Google OAuth test credential or mocked browser identity flow is available in the E2E environment.
- Aspire is running with Web/Auth/Customer/IAM healthy.

**User path:**
1. Open sign-in or sign-up page.
2. Choose Google authentication.
3. Complete Google account selection or test provider flow.
4. Return to Web.
5. Confirm existing customer account is linked or a new customer account is created.
6. Open account profile.

**Features covered:**
- Google auth exchange.
- Customer account link-or-register behavior.
- AuthService customer Google exchange.
- BFF session creation.

**Services involved:**
- `Maliev.Web.Bff`
- `AuthService`
- `CustomerService`
- `IAMService`

**Data created or mutated:**
- Customer account Google subject/link.
- IAM principal if new.
- Customer record if new registration.

**Verification checklist:**
- Google callback returns to the Web app without losing state.
- Existing email links to the existing customer instead of creating duplicates.
- New Google customer has a valid customer profile.
- Signed-in user can open account pages.
- Employee Google exchange is not used for customer login.

**Observability checks:**
- AuthService logs customer Google exchange path.
- CustomerService logs link-or-register result.
- No duplicate customer for same Google subject/email.

**Current implementation status:** Ready to automate once a reliable test OAuth strategy is chosen.

**Known product gaps:** E2E environment needs a deterministic Google test identity approach.

## WEB-007: Customer resets password from email link

**Persona:** Customer who forgot their password.

**Entry point:** `Maliev.Web` `/auth/forgot-password` and reset link.

**Business value:** Prevents account lockout and support burden.

**Prerequisites:**
- Existing customer account with email/password login.
- Email sandbox/provider is available if reset email delivery is implemented.

**User path:**
1. Open `/auth/forgot-password`.
2. Enter the customer email.
3. Submit reset request.
4. Verify the app shows a non-enumerating success message.
5. Open reset email.
6. Click reset link or paste token into reset page.
7. Set a new password.
8. Sign in with the new password.
9. Confirm old password no longer works.

**Features covered:**
- Password reset request.
- Reset token creation.
- Reset notification/email.
- Password update.
- AuthService login with new credentials.

**Services involved:**
- `Maliev.Web.Bff`
- `AuthService`
- `CustomerService`
- `NotificationService` if email delivery is implemented

**Data created or mutated:**
- Password reset token hash and expiry.
- Customer account password hash.
- Optional email delivery record.

**Verification checklist:**
- Unknown email produces the same visible response as known email.
- Reset token is time-limited and single use.
- New password must pass password rules.
- Old password fails after reset.
- New password succeeds.

**Observability checks:**
- Reset token value is not logged.
- Auth/Customer logs show request and confirmation without leaking sensitive fields.
- NotificationService delivery is recorded when email is part of the flow.

**Current implementation status:** Partial. Reset request/confirm paths exist, but full email delivery and link-click journey must be verified or implemented.

**Known product gaps:** Production gate requires real reset email handling, not only manual token entry.

## WEB-008: Customer browses shop, cart, and checkout draft

**Persona:** Customer buying a catalog product.

**Entry point:** `Maliev.Web` `/shop`, `/shop/{handle}`, `/cart`.

**Business value:** Confirms that the e-commerce storefront can convert published catalog data into a customer checkout draft.

**Prerequisites:**
- At least one published product exists in CommerceService.
- Customer can browse anonymously and sign in when checkout requires identity.

**User path:**
1. Open `/shop`.
2. Browse published products.
3. Open a product detail page.
4. Select variant/options where available.
5. Add product to cart.
6. Edit quantity in cart.
7. Start checkout draft.
8. Sign in if required.
9. Confirm draft contains correct product, quantity, price, and customer context.

**Features covered:**
- Public product listing.
- Product detail mapping.
- Cart state.
- Checkout draft.
- Customer session requirement.

**Services involved:**
- `Maliev.Web.Bff`
- `CommerceService`
- `CustomerService`
- `OrderService`
- `PaymentService`
- `DeliveryService`

**Data created or mutated:**
- Browser cart state.
- Checkout draft/order draft.
- Optional customer delivery selection.

**Verification checklist:**
- Draft and archived products are not visible in public shop.
- Published product detail matches employee-managed catalog fields.
- Cart quantity and totals update correctly.
- Checkout draft uses the signed-in customer.
- Draft lines match CommerceService product/variant identifiers.

**Observability checks:**
- WebBff resolves CommerceService through Aspire service discovery.
- Order/Payment/Delivery calls are visible if checkout draft starts those workflows.

**Current implementation status:** Ready to automate for storefront and draft creation, assuming published catalog seed data.

**Known product gaps:** Final payment provider behavior may require sandbox-specific assertions.

## WEB-009: Customer maintains profile, addresses, preferences, and order history

**Persona:** Signed-in customer managing their own account.

**Entry point:** `Maliev.Web` `/account`, `/account/profile`, `/account/addresses`, `/account/preferences`, `/account/orders`.

**Business value:** Allows customers to keep account data current and track work without contacting employees.

**Prerequisites:**
- Signed-in customer account.
- At least one order exists for order-history checks.
- Country/address reference data is seeded.

**User path:**
1. Sign in as customer.
2. Open account profile and update profile/company fields.
3. Add or edit address.
4. Update preferences.
5. Open orders list.
6. Open order detail.
7. Confirm order and delivery status visibility.

**Features covered:**
- Account route authorization.
- Customer profile update.
- Address management.
- Preferences.
- Order history and detail.

**Services involved:**
- `Maliev.Web.Bff`
- `CustomerService`
- `CountryService`
- `OrderService`
- `DeliveryService`

**Data created or mutated:**
- Customer profile fields.
- Customer address records.
- Preference records.

**Verification checklist:**
- Anonymous users cannot access account pages.
- Profile changes persist after browser refresh.
- Address validation catches missing required fields.
- Country/address values map to backend records.
- Order list shows only the signed-in customer's orders.
- Order detail does not leak another customer's data.

**Observability checks:**
- CustomerService records update calls.
- OrderService returns customer-scoped results.
- IAM/Auth context is customer-scoped.

**Current implementation status:** Ready to automate where current account pages expose these operations.

**Known product gaps:** Any missing account editing UI should be logged as product work, not hidden by direct API setup.

## WEB-010: Customer researches trust content before conversion

**Persona:** Prospective customer validating MALIEV capability before contacting sales or starting a quote.

**Entry point:** `Maliev.Web` `/materials`, `/industries`, `/case-studies`, `/case-studies/{slug}`, `/blog`, `/blog/{slug}`, and `/faq`.

**Business value:** Confirms the marketing site can educate and build trust before a customer commits to a quote or contact request.

**Prerequisites:**
- Aspire is running with `WebBff` healthy.
- Static/content routes are published and reachable.
- At least one case study and blog/detail route is present.

**User path:**
1. Open `/materials` and verify manufacturing material guidance is visible.
2. Open `/industries` and verify industry fit is understandable.
3. Open `/case-studies`, then a case-study detail.
4. Open `/blog`, then a blog detail.
5. Open `/faq`.
6. Navigate from content to quote or contact without losing language/culture preference.

**Features covered:**
- Static content routing.
- SEO/content discovery.
- Content-to-conversion navigation.
- Culture/preference persistence where exposed.

**Services involved:**
- `Maliev.Web.Bff`
- `Maliev.Web.Client`
- `ContactService` only when the journey ends in contact submission.

**Data created or mutated:**
- None for browsing.
- Contact inquiry if the journey converts through contact.

**Verification checklist:**
- Every listed route renders with HTTP success and customer-readable content.
- Detail routes render the expected title and do not fall back to a generic not-found page.
- Quote/contact CTAs are visible and route to the current Web-owned quote/contact surfaces.
- Browser title/meta content is present enough for SEO smoke coverage.
- Culture/preference state is preserved if the user changes language before conversion.

**Observability checks:**
- WebBff health remains green.
- Browser console has no fatal route/render errors.
- ContactService receives data only if the user submits contact.

**Current implementation status:** Ready to automate for existing Web content routes.

**Known product gaps:** Content-to-quote conversion should be reviewed once the final Web-to-QuoteEngine handoff is revisited.

**Product direction implied by story:** Web should remain the customer education and trust surface even while quote ownership is discussed separately.

## WEB-011: Customer manages cookie and privacy consent

**Persona:** Public website visitor deciding whether to accept optional tracking or preference cookies.

**Entry point:** `Maliev.Web` first-page visit and `/cookie-policy`, `/privacy`, `/terms`.

**Business value:** Confirms compliance-facing website behavior is clear, reversible, and does not block core conversion.

**Prerequisites:**
- Browser profile starts without MALIEV consent state.
- Web static policy pages are reachable.

**User path:**
1. Open Web home page in a fresh browser context.
2. Observe consent banner or panel.
3. Reject optional cookies.
4. Refresh and verify the decision persists.
5. Reset or change consent where UI supports it.
6. Accept cookies and verify banner no longer blocks navigation.
7. Open cookie, privacy, and terms pages from the consent/policy links.

**Features covered:**
- Consent banner.
- Consent persistence.
- Policy navigation.
- Non-blocking website access.

**Services involved:**
- `Maliev.Web.Client`
- `Maliev.Web.Bff` if preferences are server-backed.

**Data created or mutated:**
- Browser consent/preference state.
- Optional server preference record if signed in and implemented.

**Verification checklist:**
- Consent UI appears only when consent state is missing.
- Reject and accept states persist across refresh.
- Banner does not cover required navigation or quote/contact actions after a choice.
- Policy links navigate to the correct pages.
- Consent state does not leak across isolated browser contexts.

**Observability checks:**
- Browser console has no consent-script errors.
- WebBff receives no unexpected PII for anonymous consent-only browsing.

**Current implementation status:** Ready to automate where consent UI is currently implemented.

**Known product gaps:** If consent cannot be changed after the first decision, add a visible preference-management affordance.

**Product direction implied by story:** Compliance controls must protect trust without reducing conversion usability.

## WEB-012: Customer uses post-sale support information and submits support/contact request

**Persona:** Customer or buyer looking for warranty, refund, shipping, or post-sale help.

**Entry point:** `Maliev.Web` `/warranty-policy`, `/refund-policy`, `/shipping-returns`, `/contact`.

**Business value:** Ensures customers can self-serve policy answers and escalate to MALIEV with usable context.

**Prerequisites:**
- Policy pages are reachable.
- ContactService is healthy.
- Unique support inquiry data is available.

**User path:**
1. Open shipping/returns, refund, and warranty policy pages.
2. Confirm each page explains next steps and scope.
3. Navigate to contact.
4. Submit a support inquiry referencing an order, product, or quote where possible.
5. Verify the customer receives confirmation.
6. Verify ContactService stores the inquiry for employee follow-up.

**Features covered:**
- Policy pages.
- Support escalation.
- Contact form validation.
- ContactService persistence.

**Services involved:**
- `Maliev.Web.Bff`
- `ContactService`
- `NotificationService` if support notifications are configured.

**Data created or mutated:**
- Contact/support message.
- Optional notification delivery record.

**Verification checklist:**
- Policy pages are reachable and not hidden behind auth.
- Contact form captures support category/context where UI exposes it.
- Required fields validate.
- Success state is visible and duplicate submissions are prevented.
- Employee follow-up queue or ContactService contains the support request.

**Observability checks:**
- WebBff logs a successful ContactService call.
- NotificationService logs support notification if configured.

**Current implementation status:** Ready to automate for policy routes and contact persistence; category-specific support fields may be a product gap.

**Known product gaps:** The contact form should distinguish sales inquiry from post-sale support if it does not already.

**Product direction implied by story:** Web should support the complete customer relationship, not only acquisition.

## WEB-013: Customer submits current Web quote request without assuming QuoteEngine ownership

**Persona:** Customer starting a lightweight quote from the public website.

**Entry point:** `Maliev.Web` `/quote`.

**Business value:** Preserves the current Web quote-entry behavior while the dedicated QuoteEngine lifecycle decision remains deferred.

**Prerequisites:**
- Web quote page is reachable.
- WebBff quote endpoints and any downstream ManufacturingCatalog, Upload, Material, Pricing, or Contact dependencies are healthy.

**User path:**
1. Open `/quote`.
2. Enter manufacturing intent, contact details, files, quantities, material/process preferences, or other currently exposed quote fields.
3. Submit the current Web quote request.
4. Verify the UI confirms receipt or produces the expected draft estimate.
5. Verify downstream records/artifacts are created according to the current Web quote path.

**Features covered:**
- Current Web-owned quote entry.
- Quote/request validation.
- File or option handling where exposed.
- Contact/quote request persistence.

**Services involved:**
- `Maliev.Web.Bff`
- `UploadService` if file upload is exposed.
- `MaterialService` and `PricingService` if instant estimate is exposed.
- `ContactService` or current quote request owner.

**Data created or mutated:**
- Web quote request or draft.
- Optional upload artifact.
- Optional pricing estimate.

**Verification checklist:**
- Required quote fields validate clearly.
- Submit action does not route to QuoteEngine unless current Web behavior explicitly does so.
- Uploaded files are accepted/rejected with visible status.
- Created request/draft can be found through the current backend owner.
- Customer receives a confirmation state with next action.

**Observability checks:**
- WebBff resolves downstream services through Aspire.
- Upload/pricing/contact logs match the visible UI result.

**Current implementation status:** Ready to automate for the current Web quote page.

**Known product gaps:** Revisit Web-to-QuoteEngine handoff only after the deferred lifecycle consultation.

**Product direction implied by story:** Web quote entry should remain stable and testable while QuoteEngine architecture is discussed separately.

---

# Dedicated QuoteEngine Stories

## QUOTE-001: Customer creates or resumes a quote workspace

**Persona:** Customer using the dedicated quote platform.

**Entry point:** `Maliev.QuoteEngine` root route.

**Business value:** Confirms that quote work is not lost between navigation, refresh, or sign-in handoff.

**Prerequisites:**
- Aspire is running with `QuoteEngineBff`.
- QuoteEngine BFF is wired to required backend services.
- Test browser storage/session starts clean.

**User path:**
1. Open QuoteEngine.
2. Start a new quote workspace.
3. Enter or confirm initial customer/session context.
4. Refresh the browser.
5. Confirm the workspace resumes with the same draft parts and configuration.
6. Open the same quote from a second navigation path if supported.

**Features covered:**
- QuoteEngine app shell.
- Quote workspace/session state.
- Anonymous-to-authenticated continuity foundation.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- Future production path: `CustomerService`, `ProjectService`, `QuotationService`

**Data created or mutated:**
- Prototype quote session today.
- Future production quote draft/project record.

**Verification checklist:**
- QuoteEngine loads in Aspire with healthy BFF.
- Refresh does not clear in-progress work unexpectedly.
- Workspace id or draft identifier remains stable.
- Browser console has no fatal app-load errors.
- Session state is isolated between different customers/browsers.

**Observability checks:**
- `QuoteEngineBff` is healthy at `/quote/aspire-liveness`.
- Aspire shows the QuoteEngine resource and service references.

**Current implementation status:** Partial. QuoteEngine is currently prototype-backed.

**Known product gaps:** Replace prototype session store with durable service-backed quote draft behavior before production gate can pass.

## QUOTE-002: Customer uploads CAD files and sees geometry analysis

**Persona:** Customer uploading manufacturing files for instant quoting.

**Entry point:** QuoteEngine upload step.

**Business value:** This is the core quote-engine value: convert customer CAD into analyzable, viewable, priceable part data.

**Prerequisites:**
- Aspire is running with `QuoteEngineBff`, `UploadService`, `GeometryService`, RabbitMQ, Redis, and GCS/upload settings.
- Test STL/STEP files are available, including one valid file and one invalid file.

**User path:**
1. Open a quote workspace.
2. Upload a valid CAD file.
3. Watch upload progress complete.
4. Wait for geometry analysis to begin and finish.
5. Confirm thumbnail, viewer, body/part data, and DFM status appear.
6. Upload an invalid or unsupported file.
7. Confirm user-facing error state and no corrupted quote line.

**Features covered:**
- Browser file upload.
- UploadService artifact persistence.
- RabbitMQ upload/analysis event chain.
- GeometryService analysis.
- Viewer/thumbnail state.
- DFM status display.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `UploadService`
- `GeometryService`
- RabbitMQ
- `ProjectService` or future quote-draft owner

**Data created or mutated:**
- Upload record.
- CAD artifact/blob.
- Geometry analysis record/status.
- Quote part/draft line.

**Verification checklist:**
- Upload progress reaches 100 percent and stays complete.
- Uploaded file has a stored artifact reference.
- Geometry status transitions through analyzing to a terminal success/failure state.
- Viewer loads a non-empty model for valid files.
- Thumbnail is present where the platform expects one.
- Invalid files show actionable errors without breaking the entire quote.

**Observability checks:**
- UploadService logs successful upload/session completion.
- RabbitMQ message is published and consumed.
- GeometryService logs analysis completion or validation failure.
- No stale `Analyze your model...` state remains after terminal backend state.

**Current implementation status:** Partial. QuoteEngine must be moved from prototype upload/analysis behavior to real UploadService and GeometryService backed flow.

**Known product gaps:** Production QuoteEngine needs the same artifact enrichment rigor expected in Intranet ProjectNew.

## QUOTE-003: Customer configures manufacturing requirements and receives deterministic pricing

**Persona:** Customer configuring part requirements.

**Entry point:** QuoteEngine part configuration step after successful CAD analysis.

**Business value:** Converts technical requirements into a deterministic, explainable quote.

**Prerequisites:**
- A quote workspace has at least one analyzed part.
- Material/process/finish/tolerance catalog data is available.
- PricingService is healthy.

**User path:**
1. Select process.
2. Select material.
3. Select finish, tolerance, quantity, lead time, and delivery preference.
4. Apply configuration to one or more parts.
5. Review price breakdown.
6. Change a pricing input and verify price recalculates.
7. Revert to the original input and verify the original price returns.

**Features covered:**
- Material/process configuration.
- Pricing input validation.
- Deterministic pricing.
- Price breakdown display.
- Multi-part or per-part configuration.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `MaterialService`
- `PricingService`
- `ProjectService` or quote-draft owner
- `DeliveryService` if delivery option affects quote

**Data created or mutated:**
- Part configuration.
- Pricing request/response.
- Quote totals.

**Verification checklist:**
- Required configuration fields cannot be skipped.
- Same inputs return the same price.
- Price changes only when meaningful pricing inputs change.
- Price breakdown includes enough detail for the customer to trust it.
- Employee-only details such as outsourced markup internals are not exposed to the customer.

**Observability checks:**
- PricingService receives expected part/material/quantity inputs.
- No fallback to cached stale pricing after input changes.
- Logs do not expose customer-sensitive CAD data.

**Current implementation status:** Partial.

**Known product gaps:** QuoteEngine must call real MaterialService and PricingService for production. Prototype pricing cannot pass this story.

## QUOTE-004: Customer resolves manufacturability issues and sees updated analysis

**Persona:** Customer adjusting quote inputs after DFM feedback.

**Entry point:** QuoteEngine DFM/error state.

**Business value:** Helps customers self-correct quote blockers without employee intervention.

**Prerequisites:**
- At least one uploaded part can produce a DFM warning or failure.
- Reupload or configuration adjustment is supported.

**User path:**
1. Upload or select a part with DFM warnings.
2. Open DFM issue details.
3. Change a configuration that may resolve the issue or upload a corrected file.
4. Wait for analysis/pricing to update.
5. Confirm old warnings do not remain if they no longer apply.
6. Confirm unresolved warnings remain visible and actionable.

**Features covered:**
- DFM warning display.
- Reanalysis.
- Part replacement.
- Stale-state prevention.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `UploadService`
- `GeometryService`
- `PricingService`
- RabbitMQ

**Data created or mutated:**
- New upload artifact or updated part configuration.
- New geometry/DFM result.
- Updated price.

**Verification checklist:**
- DFM issues are tied to the correct part/revision.
- Reupload creates a new artifact reference instead of overwriting incorrectly.
- Old analysis does not overwrite newer analysis.
- User can continue only when required manufacturing blockers are resolved or explicitly acknowledged.

**Observability checks:**
- GeometryService result correlation id matches the current part/revision.
- RabbitMQ message ordering does not produce stale final UI state.

**Current implementation status:** Partial.

**Known product gaps:** QuoteEngine must implement revision-aware upload and analysis state before this can be production-gate ready.

## QUOTE-005: Customer signs in from QuoteEngine and links anonymous quote work

**Persona:** Customer who starts quoting anonymously and signs in before requesting a formal quote.

**Entry point:** QuoteEngine sign-in/sign-up prompt.

**Business value:** Preserves quote effort and connects it to a real customer account.

**Prerequisites:**
- Anonymous quote workspace exists.
- Customer account exists or can be created.
- AuthService and CustomerService are healthy.

**User path:**
1. Start an anonymous quote and configure at least one part.
2. Choose sign in or sign up.
3. Complete email/password or Google auth.
4. Return to QuoteEngine.
5. Confirm the anonymous quote is now attached to the authenticated customer.
6. Refresh and confirm the linked quote remains available.

**Features covered:**
- Auth handoff from QuoteEngine.
- Customer account linking.
- Anonymous-to-authenticated quote ownership.
- Session preservation.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `AuthService`
- `CustomerService`
- `IAMService`
- `ProjectService` or quote-draft owner

**Data created or mutated:**
- Customer session.
- Quote/customer ownership link.
- Optional new customer account.

**Verification checklist:**
- Anonymous quote data is not lost during auth redirect.
- Authenticated customer can see the same parts/configuration.
- Another customer cannot claim the anonymous quote after it is linked.
- Sign-out hides customer-owned quote data from anonymous users.

**Observability checks:**
- AuthService logs customer login.
- Quote ownership update is recorded in the quote/project owner service.

**Current implementation status:** Partial. Prototype auth/quote-link behavior must be replaced with real service-backed ownership.

**Known product gaps:** Durable quote ownership and secure anonymous claim tokens are required.

## QUOTE-006: Customer requests formal quotation PDF

**Persona:** Customer requesting an official quote document.

**Entry point:** QuoteEngine review/submit step.

**Business value:** Turns a configured quote into a professional customer-facing document that sales and production can trust.

**Prerequisites:**
- Authenticated customer owns a quote with at least one priced part.
- QuotationService and PdfService are healthy.
- UploadService can store or retrieve generated PDF artifacts.

**User path:**
1. Review configured parts and totals.
2. Submit formal quote request.
3. Wait for quote generation.
4. Download or open generated PDF.
5. Open Intranet as an employee and find the same quotation.

**Features covered:**
- Formal quote submission.
- QuotationService record creation.
- PdfService document generation.
- Upload/artifact storage.
- Customer and employee visibility of the same quote.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `QuotationService`
- `PdfService`
- `UploadService`
- `CustomerService`
- `ProjectService`
- `NotificationService` if employee/customer alerts are sent

**Data created or mutated:**
- Quotation record/version.
- Generated PDF artifact.
- Optional notification record.

**Verification checklist:**
- Quote submission is idempotent or safely guarded against double-click duplicates.
- PDF includes customer information, part thumbnails, pricing, validity, and correct document type.
- PDF opens without corruption.
- Employee Intranet shows the same quote number/customer/total.
- Customer cannot access another customer's quote PDF.

**Observability checks:**
- PdfService receives quotation document request, not receipt request.
- UploadService stores the generated artifact with service authentication.
- No receipt consumer handles quotation PDF completion.

**Current implementation status:** Partial.

**Known product gaps:** QuoteEngine must connect to real QuotationService/PdfService path. Prototype PDF behavior is not sufficient.

## QUOTE-007: Customer accepts quote and starts order/payment/delivery

**Persona:** Customer approving a quote for production.

**Entry point:** QuoteEngine quote detail or approval step.

**Business value:** Converts quote approval into revenue workflow and production work.

**Prerequisites:**
- Customer owns a formal quote.
- Quote is valid and acceptable.
- OrderService, PaymentService, DeliveryService, and Job/Project downstream services are healthy where used.

**User path:**
1. Open quote detail.
2. Review validity, totals, and terms.
3. Accept quote.
4. Choose or confirm delivery address.
5. Start payment.
6. Confirm order is created and visible to customer.
7. Confirm employee can see order/job handoff in Intranet.

**Features covered:**
- Quote acceptance.
- Order creation.
- Payment initialization.
- Delivery intent.
- Employee production handoff.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `QuotationService`
- `OrderService`
- `PaymentService`
- `DeliveryService`
- `CustomerService`
- `JobService` or `ProjectService` where production handoff is owned

**Data created or mutated:**
- Accepted quote state.
- Order record.
- Payment intent/record.
- Delivery record.
- Optional job/production record.

**Verification checklist:**
- Expired quote cannot be accepted.
- Accepted quote cannot create duplicate orders on repeated clicks.
- Order totals match quote totals.
- Payment status is visible and tied to the order.
- Delivery address is preserved.
- Intranet order/job view matches customer-facing status.

**Observability checks:**
- QuotationService, OrderService, PaymentService, and DeliveryService logs share a trace or correlation id.
- Notification events are emitted if configured.

**Current implementation status:** Partial.

**Known product gaps:** End-to-end quote acceptance from QuoteEngine must be service-backed before production.

## QUOTE-008: Customer uploads NDA and supporting documents separately from CAD

**Persona:** Customer providing commercial/legal documents with a quote.

**Entry point:** QuoteEngine documents step.

**Business value:** Keeps legal/customer documents separate from manufacturing files so privacy and retention rules can differ.

**Prerequisites:**
- Authenticated customer quote workspace.
- UploadService supports document kind metadata.

**User path:**
1. Open quote documents area.
2. Upload NDA document.
3. Upload customer supporting document.
4. Confirm both files show document names, types, and status.
5. Confirm CAD/manufacturing upload list does not mix with NDA/customer documents.
6. Open Intranet as authorized employee and verify documents are visible in the correct section.

**Features covered:**
- Document upload.
- Document categorization.
- Employee document visibility.
- Separation from CAD manufacturing files.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `UploadService`
- `CustomerService` or document owner service
- `ProjectService` or `QuotationService`
- `Maliev.Intranet.Bff`

**Data created or mutated:**
- NDA document artifact.
- Customer supporting document artifact.
- Quote/customer document references.

**Verification checklist:**
- NDA and customer documents have separate labels/kinds.
- CAD analysis does not run for non-CAD documents.
- Unauthorized users cannot download the documents.
- Employee UI shows the documents under the correct customer/quote.

**Observability checks:**
- UploadService records document kind.
- No GeometryService message is emitted for NDA/customer documents.

**Current implementation status:** Partial.

**Known product gaps:** Ensure QuoteEngine and Intranet use the same document-kind contract.

## QUOTE-009: Customer returns later to quote/order history and manufacturing status

**Persona:** Returning customer checking quote/order progress.

**Entry point:** QuoteEngine dashboard/history or Web account order history.

**Business value:** Reduces status inquiries and keeps customer informed after acceptance.

**Prerequisites:**
- Customer has previous quote and order data.
- Authenticated customer session.

**User path:**
1. Sign in.
2. Open QuoteEngine history or account history.
3. Open prior quote.
4. Open accepted order.
5. Verify payment, delivery, and manufacturing status.
6. Download quote/PDF artifacts where available.

**Features covered:**
- Quote history.
- Order status.
- Payment status.
- Delivery/manufacturing progress.
- Artifact retrieval.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `CustomerService`
- `QuotationService`
- `OrderService`
- `PaymentService`
- `DeliveryService`
- `JobService` or `ProjectService`
- `PdfService`/`UploadService`

**Data created or mutated:**
- None required unless status refresh creates audit/access records.

**Verification checklist:**
- Customer sees only their own quotes and orders.
- Quote detail matches original PDF/order totals.
- Payment and delivery status are current.
- Manufacturing status is understandable and not employee-only jargon.
- Download links remain valid or fail with a clear recovery path.

**Observability checks:**
- Downstream order/payment/delivery calls are customer-scoped.
- No cross-customer data leakage appears in logs or UI.

**Current implementation status:** Partial.

**Known product gaps:** QuoteEngine history needs durable backend ownership before this can pass.

## QUOTE-010: Customer manages QuoteEngine profile and preferences

**Persona:** Signed-in customer using the dedicated quote portal.

**Entry point:** `Maliev.QuoteEngine` `/profile` and `/preferences`.

**Business value:** Gives quoting customers a portal-specific place to maintain contact, company, communication, and quote preferences.

**Prerequisites:**
- Customer is signed in to QuoteEngine.
- QuoteEngine account endpoints are reachable.
- CustomerService/AuthService integration is available or prototype store is seeded.

**User path:**
1. Open `/profile`.
2. Review customer name, company, email, phone, and billing/contact context.
3. Update editable profile fields where supported.
4. Open `/preferences`.
5. Update preferred process, unit/currency/language, notification, or delivery preferences where supported.
6. Refresh and verify values persist.

**Features covered:**
- QuoteEngine customer profile.
- Portal preferences.
- Customer session scoping.
- Persistence across refresh.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `CustomerService` in production.
- `AuthService`/`IAMService` for signed-in context.
- `QuoteEnginePrototypeStore` where still prototype-backed.

**Data created or mutated:**
- Customer profile fields.
- QuoteEngine/customer preferences.

**Verification checklist:**
- Anonymous users are redirected to sign-in for profile/preferences.
- UI shows the signed-in customer's data only.
- Editable fields validate before save.
- Saved changes survive refresh and sign-out/sign-in.
- Another customer cannot see or mutate the profile.

**Observability checks:**
- QuoteEngineBff logs customer-scoped account calls.
- Production implementation calls CustomerService rather than trusting browser-supplied customer ids.

**Current implementation status:** Partial. Current routes exist, but durable backend behavior depends on replacing prototype-backed account handling.

**Known product gaps:** Define which preferences are customer-owned versus quote-draft-owned before broadening UI.

**Product direction implied by story:** QuoteEngine should become a real customer portal while remaining independent from the deferred ProjectNew lifecycle discussion.

## QUOTE-011: Customer views quote list and quote detail history

**Persona:** Returning customer reviewing previous quote requests.

**Entry point:** `Maliev.QuoteEngine` `/quotes/{QuoteId:guid}` and quote-history navigation.

**Business value:** Lets customers recover prior quote context without asking employees to resend information.

**Prerequisites:**
- Signed-in customer has at least one QuoteEngine quote record or prototype quote fixture.
- Quote detail route is reachable.

**User path:**
1. Sign in to QuoteEngine.
2. Open quote history/list if exposed from navigation.
3. Select a quote.
4. Verify quote detail shows quote status, line items, totals, uploaded file references, and next action.
5. Attempt to open another customer's quote id directly.

**Features covered:**
- Quote history.
- Quote detail.
- Customer-scoped access control.
- Direct-url authorization.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- Production owner: `QuotationService` or customer quote-history service.
- `UploadService`/`PdfService` when artifacts are shown.
- `QuoteEnginePrototypeStore` where still prototype-backed.

**Data created or mutated:**
- None required for read-only history.

**Verification checklist:**
- Customer sees only their quote records.
- Quote detail totals/status match the source quote.
- PDF/download links work or show a clear unavailable state.
- Direct access to another customer's quote returns sign-in, forbidden, or not-found without data leakage.
- Browser refresh preserves route state.

**Observability checks:**
- BFF resolves customer identity server-side.
- Backend logs show customer-scoped read and no browser-supplied trusted customer id.

**Current implementation status:** Partial. Mark as prototype-backed until real quote history is durable.

**Known product gaps:** Quote list navigation should be explicit if only detail route currently exists.

**Product direction implied by story:** QuoteEngine should own customer-visible quote history without changing employee ProjectNew flow yet.

## QUOTE-012: Customer views order list and order detail

**Persona:** Customer checking accepted QuoteEngine order progress.

**Entry point:** `Maliev.QuoteEngine` `/orders` and `/orders/{OrderId:guid}`.

**Business value:** Reduces status requests by giving customers a self-service order view.

**Prerequisites:**
- Signed-in customer has at least one order or prototype order fixture.
- Order detail route is reachable.

**User path:**
1. Sign in to QuoteEngine.
2. Open `/orders`.
3. Select an order.
4. Verify order detail includes status, line items, payment state, delivery state, and related quote/document links where available.
5. Attempt direct access to another customer's order id.

**Features covered:**
- Customer order list.
- Customer order detail.
- Payment/delivery status visibility.
- Customer authorization boundary.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `OrderService`
- `PaymentService`
- `DeliveryService`
- `QuotationService`
- `QuoteEnginePrototypeStore` where still prototype-backed.

**Data created or mutated:**
- None required for read-only status.

**Verification checklist:**
- Orders list is customer-scoped.
- Order status is understandable and not employee-only jargon.
- Payment and delivery statuses match backend records.
- Direct access to another customer order does not leak data.
- Empty order history has a helpful state and route back to quoting.

**Observability checks:**
- BFF order reads use server-resolved customer identity.
- Order/Payment/Delivery services remain healthy.

**Current implementation status:** Partial.

**Known product gaps:** Production order history must replace prototype-backed order records.

**Product direction implied by story:** QuoteEngine can mature as a customer status portal without requiring ProjectNew lifecycle unification now.

## QUOTE-013: Customer manages NDA records

**Persona:** Customer sharing or reviewing legal documents in the quote portal.

**Entry point:** `Maliev.QuoteEngine` `/ndas`.

**Business value:** Gives customers a clear legal-document surface separate from CAD and quote documents.

**Prerequisites:**
- Signed-in customer.
- NDA records or upload workflow are available.
- UploadService is healthy if uploads are implemented.

**User path:**
1. Open `/ndas`.
2. Review existing NDA records and statuses.
3. Upload or attach NDA if supported.
4. Download/view an existing NDA if authorized.
5. Confirm NDA documents are not mixed into CAD upload lists.

**Features covered:**
- NDA list/status.
- NDA upload/download where implemented.
- Legal-document separation.
- Customer authorization.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `CustomerService` or document ownership service.
- `UploadService`
- `QuoteEnginePrototypeStore` where still prototype-backed.

**Data created or mutated:**
- NDA document record.
- NDA upload artifact if upload is supported.

**Verification checklist:**
- NDA page requires customer sign-in.
- NDA records are customer-scoped.
- NDA artifact has legal/NDA kind, not CAD kind.
- Unauthorized customer cannot access another customer's NDA.
- CAD analysis is not triggered for NDA files.

**Observability checks:**
- UploadService records correct document kind.
- GeometryService receives no NDA-related CAD analysis message.

**Current implementation status:** Partial.

**Known product gaps:** Define NDA approval/signature statuses if the current UI only lists placeholder records.

**Product direction implied by story:** Legal documents need their own portal lifecycle independent from manufacturing file analysis.

## QUOTE-014: Customer manages supporting documents

**Persona:** Customer providing drawings, requirements, purchase documents, or other supporting files.

**Entry point:** `Maliev.QuoteEngine` `/documents`.

**Business value:** Keeps supporting customer documents traceable and separate from CAD files that drive geometry analysis.

**Prerequisites:**
- Signed-in customer.
- UploadService is healthy if document upload is implemented.

**User path:**
1. Open `/documents`.
2. Upload a supporting document where supported.
3. Assign or verify document description/type.
4. Download/view document.
5. Confirm document is visible only to the owning customer and authorized employees.

**Features covered:**
- Customer document list.
- Supporting document upload/download.
- Document categorization.
- Authorization.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `UploadService`
- `CustomerService` or document owner service.
- `QuoteEnginePrototypeStore` where still prototype-backed.

**Data created or mutated:**
- Supporting document record.
- Upload artifact.

**Verification checklist:**
- Documents require sign-in.
- File type/size validation is visible.
- Supporting documents are not submitted to GeometryService for CAD analysis.
- Download/open links are authorized and stable.
- Employee visibility should appear in Intranet only where an authorized employee surface exists.

**Observability checks:**
- UploadService records document kind and owner.
- No cross-customer document access appears in logs.

**Current implementation status:** Partial.

**Known product gaps:** Define document categories and employee review surface before automating broad document workflows.

**Product direction implied by story:** QuoteEngine should distinguish CAD, NDA, and supporting documents as separate customer concepts.

## QUOTE-015: Customer receives real-time QuoteEngine notifications

**Persona:** Customer waiting for upload analysis, quote generation, or order status updates.

**Entry point:** `Maliev.QuoteEngine` quote workspace, quote detail, and `/hubs/quote-notifications`.

**Business value:** Prevents stale UI during long-running quote operations and reduces manual refresh.

**Prerequisites:**
- Signed-in customer where the hub requires session context.
- SignalR hub is reachable.
- A quote/upload/order event can be triggered in the test environment.

**User path:**
1. Open a QuoteEngine page that subscribes to notifications.
2. Trigger upload analysis, quote generation, or order status update.
3. Wait for UI to update through SignalR.
4. Refresh and confirm the final state remains persisted.
5. Disconnect/reconnect network where automation supports it and verify recovery behavior.

**Features covered:**
- QuoteEngine SignalR hub.
- Real-time analysis/quote/order updates.
- Reconnect behavior.
- Stale UI prevention.

**Services involved:**
- `Maliev.QuoteEngine.Bff`
- `/hubs/quote-notifications`
- `UploadService`/`GeometryService` for analysis events.
- `QuotationService`, `OrderService`, or prototype store depending on event type.

**Data created or mutated:**
- Event-driven status updates.
- No new data required beyond the triggering workflow.

**Verification checklist:**
- Hub connection establishes for the signed-in customer.
- UI updates without manual refresh after the event.
- Reconnect does not duplicate messages or show stale terminal state.
- Customer receives only events for their own quote/order.
- Refresh shows the same final state loaded from persisted backend/prototype data.

**Observability checks:**
- QuoteEngineBff logs hub connection and customer scope.
- Event source logs match the visible UI update.

**Current implementation status:** Partial.

**Known product gaps:** Production event source must replace prototype-only notifications for quote/order workflows.

**Product direction implied by story:** QuoteEngine should feel live and trustworthy for long-running customer operations.

---

# Employee Intranet Sales And CRM Stories

## INT-001: Employee signs into Intranet and sees permission-shaped navigation

**Persona:** MALIEV employee.

**Entry point:** `Maliev.Intranet` sign-in.

**Business value:** Confirms employee-only ERP/CRM access is protected and role-based.

**Prerequisites:**
- Aspire test admin or employee seed user exists.
- AuthService, EmployeeService, IAMService, and IntranetBff are healthy.

**User path:**
1. Open Intranet.
2. Sign in with employee Google/test admin path.
3. Land on the employee dashboard.
4. Verify navigation items match assigned permissions.
5. Attempt a route outside the user's permissions.
6. Sign out.

**Features covered:**
- Employee authentication.
- IAM permission loading.
- Protected Intranet routes.
- Permission-shaped navigation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `AuthService`
- `EmployeeService`
- `IAMService`

**Data created or mutated:**
- Auth/session records.

**Verification checklist:**
- Anonymous browser cannot access protected Intranet pages.
- Employee login uses employee auth path, not customer auth.
- Navigation hides unauthorized modules.
- Direct unauthorized route access is blocked.
- Logout clears session.

**Observability checks:**
- AuthService logs employee login.
- IAMService permission checks succeed or deny as expected.

**Current implementation status:** Ready to automate.

**Known product gaps:** Role fixture coverage must include both broad admin and restricted employee personas.

## INT-002: Employee creates customer with registry lookup, address autocomplete, documents, and NDA separation

**Persona:** Sales or customer service employee.

**Entry point:** Intranet customer creation page.

**Business value:** Ensures employee onboarding captures correct legal/customer data without retyping unnecessary information.

**Prerequisites:**
- Employee is signed in with customer create permission.
- RegistryService, CustomerService, CountryService, UploadService, and IAMService are healthy.
- Test company registry data or deterministic lookup fixture is available.

**User path:**
1. Open customer creation.
2. Search for company using registry lookup.
3. Accept extracted/registry company data.
4. Fill contact person and account fields.
5. Use address autocomplete/reference data.
6. Upload customer document.
7. Upload NDA document separately.
8. Save customer.
9. Open customer detail.

**Features covered:**
- Customer onboarding.
- Registry/company lookup.
- Address autocomplete.
- Customer documents.
- NDA separation.
- Customer detail persistence.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CustomerService`
- `RegistryService`
- `CountryService`
- `UploadService`
- `IAMService`
- `SearchService`

**Data created or mutated:**
- Company/customer record.
- Contact/account data.
- Addresses.
- Document artifacts.
- Search index entries.

**Verification checklist:**
- Registry selection populates expected company fields.
- Branch normalization follows head office/branch rules.
- Address fields persist correctly.
- Customer document and NDA are stored under distinct document categories.
- Customer detail page shows saved data after refresh.
- Search can find the customer after indexing.

**Observability checks:**
- CustomerService publishes customer-created side effects.
- UploadService stores document artifacts with correct kind.
- SearchService receives or exposes updated customer data.

**Current implementation status:** Ready to automate where current UI exposes all fields; any missing field is a product gap.

**Known product gaps:** Ensure customer-facing documents and NDA upload flows remain separate across all customer and quote surfaces.

## INT-003: Employee edits customer detail and verifies search propagation

**Persona:** Sales or CRM employee maintaining customer records.

**Entry point:** Intranet customer detail page.

**Business value:** Keeps customer data accurate and searchable across the company.

**Prerequisites:**
- Existing customer from `INT-002`.
- Employee has customer update permission.

**User path:**
1. Open customer detail.
2. Update profile/company fields.
3. Change payment terms.
4. Add or edit address.
5. Upload another customer document.
6. Save changes.
7. Refresh page and verify persistence.
8. Use global search to find updated customer data.

**Features covered:**
- Customer detail editing.
- Payment term selection.
- Address management.
- Document upload.
- Search propagation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CustomerService`
- `CountryService`
- `UploadService`
- `SearchService`

**Data created or mutated:**
- Customer profile fields.
- Payment terms.
- Address/document records.
- Search index data.

**Verification checklist:**
- Input fields update immediately where the UI expects immediate binding.
- Payment term display is understandable and persisted.
- Uploaded documents remain accessible after refresh.
- Search returns updated values and links to customer detail.
- Unauthorized employee cannot edit the customer.

**Observability checks:**
- CustomerService emits update side effects.
- SearchService shows updated indexed data after eventual consistency wait.

**Current implementation status:** Ready to automate.

**Known product gaps:** Missing UI affordances should be tracked rather than bypassed with direct API calls.

## INT-004: Employee creates manufacturing quote in ProjectNew

**Persona:** Sales engineer creating a quote for a customer.

**Entry point:** Intranet `/sales/projects/new`.

**Business value:** Verifies the employee quote creation workflow from customer selection to priced technical quote.

**Prerequisites:**
- Employee has project/quote create permissions.
- Customer exists.
- Upload, Geometry, Material, Pricing, Project, Quotation, and supporting services are healthy.
- Test CAD file is available.

**User path:**
1. Open ProjectNew.
2. Select or create customer context.
3. Upload CAD file.
4. Wait for upload and geometry analysis.
5. Confirm viewer, thumbnail, and DFM status.
6. Configure process, material, finish, tolerance, quantity, and lead time.
7. Verify price summary updates.
8. Save or generate quote-ready draft.

**Features covered:**
- ProjectNew customer selection.
- Resumable upload.
- Geometry analysis.
- DFM results.
- Viewer/thumbnail.
- Material/process configuration.
- Pricing summary.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CustomerService`
- `UploadService`
- `GeometryService`
- `MaterialService`
- `PricingService`
- `ProjectService`
- `QuotationService`
- RabbitMQ

**Data created or mutated:**
- Project/draft record.
- Uploaded CAD artifact.
- Geometry analysis result.
- Part configuration.
- Pricing result.

**Verification checklist:**
- Upload progress and terminal analysis status are part-scoped.
- Viewer renders the uploaded model and thumbnail appears.
- DFM result matches the current part, not an old upload.
- Pricing recalculates deterministically from selected inputs.
- Saved draft can be reopened with files/configuration intact.

**Observability checks:**
- UploadService publishes upload/analysis message.
- GeometryService consumes and publishes terminal status.
- PricingService resolves MaterialService through Aspire service discovery.

**Current implementation status:** Ready to automate for current ProjectNew UI, with GeometryService infrastructure timing accounted for.

**Known product gaps:** Any parts still stuck in analyzing state after backend terminal status should fail this story.

## INT-005: Employee generates quotation PDF from ProjectNew

**Persona:** Sales engineer producing an official quotation.

**Entry point:** ProjectNew quote summary/generate PDF action.

**Business value:** Ensures the document sent to customers is accurate, branded, and traceable to the employee quote.

**Prerequisites:**
- ProjectNew quote draft from `INT-004`.
- PdfService, UploadService, QuotationService, and ReceiptService are healthy.

**User path:**
1. Open ProjectNew with a quote-ready draft.
2. Click generate quotation PDF.
3. Wait for generation to complete.
4. Open/download PDF.
5. Verify the PDF content.
6. Confirm no receipt workflow handles the quotation PDF event.

**Features covered:**
- Quotation PDF generation.
- PdfService document type routing.
- Upload artifact storage.
- Quoted-by employee identity.
- Thumbnail inclusion.

**Services involved:**
- `Maliev.Intranet.Bff`
- `QuotationService`
- `PdfService`
- `UploadService`
- `ReceiptService` as a negative verification boundary
- `EmployeeService`

**Data created or mutated:**
- Quotation/document request.
- PDF artifact.
- Quote document status.

**Verification checklist:**
- PDF has correct document type: quotation, not receipt.
- PDF includes customer info, employee quoted-by identity, part thumbnails, part specs, prices, totals, and validity.
- Manual and automatic PDF paths render equivalent business content.
- PDF opens without corruption.
- ReceiptService does not log or process quotation PDF completion as receipt completion.

**Observability checks:**
- PdfService logs quotation generation.
- UploadService logs artifact write.
- ReceiptService logs no false receipt handling for the quote request.

**Current implementation status:** Ready to automate.

**Known product gaps:** Missing thumbnails or missing employee identity should fail this story.

## INT-006: Employee duplicates or reorders a project

**Persona:** Sales employee creating repeat work from a prior project.

**Entry point:** Project detail duplicate/reorder action.

**Business value:** Speeds repeat quoting while preserving technical context.

**Prerequisites:**
- Existing project with files, configurations, analysis artifacts, and pricing.
- Employee has project create/update permissions.

**User path:**
1. Open an existing project detail.
2. Choose duplicate or reorder.
3. Confirm the new draft/project opens.
4. Verify files, thumbnails, configurations, DFM data, and pricing context carried over.
5. Change quantity or material.
6. Verify updated pricing and preserved source artifacts.

**Features covered:**
- Duplicate/reorder workflow.
- Artifact carryover.
- Part configuration carryover.
- Pricing recalculation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `ProjectService`
- `UploadService`
- `GeometryService`
- `PricingService`
- `MaterialService`
- `QuotationService`

**Data created or mutated:**
- New project/draft record.
- File references to existing artifacts or copied artifacts.
- Updated quote/pricing data.

**Verification checklist:**
- New project has a distinct id.
- Source project is unchanged.
- File references remain valid and thumbnails render.
- Existing DFM/analysis state is not lost.
- Pricing recalculates when changed inputs require it.

**Observability checks:**
- ProjectService logs duplicate/reorder creation.
- No unnecessary reupload is required for unchanged files.

**Current implementation status:** Ready to automate where duplicate/reorder UI is exposed.

**Known product gaps:** Reorder must preserve artifacts and not force customers/employees to reupload unchanged files.

## INT-007: Employee accepts quotation into order/job flow

**Persona:** Employee converting an approved quote into production work.

**Entry point:** Project detail or quotation detail accept action.

**Business value:** Bridges sales quote approval into execution.

**Prerequisites:**
- Formal quotation exists and is ready for acceptance.
- OrderService, JobService, ProjectService, NotificationService, and related services are healthy.

**User path:**
1. Open quotation or project detail.
2. Accept quotation.
3. Confirm order record is created.
4. Confirm production/job planning entry exists.
5. Verify customer-facing status updates where available.

**Features covered:**
- Quote acceptance.
- Order creation.
- Job/production handoff.
- Customer status visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `QuotationService`
- `OrderService`
- `ProjectService`
- `JobService`
- `NotificationService`

**Data created or mutated:**
- Accepted quotation state.
- Order record.
- Job/production record.
- Notification record.

**Verification checklist:**
- Quote acceptance is idempotent.
- Order total and line details match quotation.
- Job/production record references the accepted quote/order.
- Customer can see updated status where customer UI supports it.

**Observability checks:**
- Event chain from quote acceptance to order/job is visible.
- Notification event is emitted if configured.

**Current implementation status:** Ready to automate where UI action exists.

**Known product gaps:** Customer-facing status should align with employee order/job state.

## INT-008: Employee invoices, records payment, and generates receipt

**Persona:** Finance employee.

**Entry point:** Intranet finance/invoice/payment pages.

**Business value:** Verifies the money workflow from invoice to receipt artifact.

**Prerequisites:**
- Accepted order exists.
- InvoiceService, PaymentService, ReceiptService, PdfService, AccountingService, and UploadService are healthy.

**User path:**
1. Open order or finance page.
2. Create or open invoice.
3. Record payment or payment confirmation.
4. Generate receipt.
5. Download receipt PDF.
6. Verify accounting/payment status.

**Features covered:**
- Invoice creation.
- Payment recording.
- Receipt generation.
- Accounting side effects.
- PDF artifact.

**Services involved:**
- `Maliev.Intranet.Bff`
- `InvoiceService`
- `PaymentService`
- `ReceiptService`
- `PdfService`
- `UploadService`
- `AccountingService`
- `NotificationService`

**Data created or mutated:**
- Invoice.
- Payment record.
- Receipt record.
- Receipt PDF artifact.
- Accounting entry.

**Verification checklist:**
- Invoice total matches order/quote expectations.
- Payment status changes from unpaid/partial to paid as appropriate.
- Receipt PDF has correct receipt document type and customer/payment details.
- Accounting entries reflect the payment.
- Customer-facing order/payment status updates if exposed.

**Observability checks:**
- Payment event chain reaches ReceiptService/NotificationService.
- PdfService generates receipt document only for receipt workflow.

**Current implementation status:** Ready to automate where finance UI exposes the flow.

**Known product gaps:** Payment provider sandbox behavior must be deterministic for E2E.

## INT-009: Employee manages delivery and customer-visible status

**Persona:** Operations or logistics employee.

**Entry point:** Intranet delivery/order page.

**Business value:** Confirms shipment/delivery progress is operationally tracked and visible to the customer.

**Prerequisites:**
- Paid or ready-to-ship order exists.
- DeliveryService and OrderService are healthy.

**User path:**
1. Open order delivery page.
2. Create delivery note or delivery task.
3. Add tracking/evidence/status.
4. Mark delivery stage complete.
5. Open customer-facing order detail and verify visible status.

**Features covered:**
- Delivery note.
- Delivery status.
- Evidence upload.
- Customer order status.

**Services involved:**
- `Maliev.Intranet.Bff`
- `DeliveryService`
- `OrderService`
- `UploadService`
- `NotificationService`
- `Maliev.Web.Bff`

**Data created or mutated:**
- Delivery record.
- Evidence artifact.
- Order delivery status.
- Notification record.

**Verification checklist:**
- Delivery status updates persist.
- Evidence artifact opens/downloads for authorized employees.
- Customer sees safe status but not employee-only operational detail.
- Status history is ordered and understandable.

**Observability checks:**
- DeliveryService logs state transition.
- Notification event is emitted if configured.

**Current implementation status:** Ready to automate where UI exposes delivery workflow.

**Known product gaps:** Customer-visible order detail should be aligned with delivery statuses.

---

## INT-010: Admin creates IAM user and verifies access changes

**Persona:** Platform admin or HR administrator.

**Entry point:** Intranet IAM/user administration pages.

**Business value:** Confirms employee access is intentionally created, scoped, and revoked before production data is exposed.

**Prerequisites:**
- Admin user is signed in with IAM user-management permissions.
- IAMService, AuthService, EmployeeService, and NotificationService are healthy.
- Test role and permission category data exist.

**User path:**
1. Open Intranet IAM user administration.
2. Create a new user or invite an employee-linked user.
3. Assign a role and optional direct permissions.
4. Sign in or impersonation-check as the new user according to supported policy.
5. Verify visible navigation and direct URL access match the assigned permissions.
6. Remove a permission or deactivate the user and verify access changes on the next request/session refresh.

**Features covered:**
- IAM user creation.
- Role assignment.
- Direct permission override.
- Session/access refresh.
- Employee-to-user linkage.

**Services involved:**
- `Maliev.Intranet.Bff`
- `IAMService`
- `AuthService`
- `EmployeeService`
- `NotificationService`

**Data created or mutated:**
- IAM user/principal.
- Role assignment.
- Permission assignment.
- Optional employee linkage and invitation/notification record.

**Verification checklist:**
- UI route shows created user with stable identity, email, status, roles, and permissions.
- BFF endpoint forwards the request to IAMService without exposing admin-only fields to unauthorized users.
- Domain owner is IAMService for access state; EmployeeService owns employee profile linkage.
- New user can access only permitted Intranet modules; denied modules hide navigation and return a safe forbidden page on direct URL.
- Revoked permission takes effect after refresh, token renewal, or documented session invalidation behavior.
- Audit metadata records who made the access change.

**Observability checks:**
- IAMService logs user/role mutation with correlation id.
- AuthService traces token/session evaluation after permission change.
- Notification event is emitted if invitation or access-change messaging exists.
- Aspire dashboard keeps IAMService, AuthService, and Intranet BFF healthy.

**Current implementation status:** Partial. Automate the IAM screens that exist and keep invitation/session-refresh gaps visible.

**Product direction implied by story:** IAM administration should be a first-class Intranet flow with visible permission effects, not a backend-only setup task.

**Known product gaps:** Define whether new employees are invited by email, activated immediately, or created as pending users.

## INT-011: Admin manages role detail and permission categories

**Persona:** Platform admin.

**Entry point:** Intranet IAM role/permission pages.

**Business value:** Confirms permission design can be managed without code changes while preventing accidental broad access.

**Prerequisites:**
- Admin user has role-management permissions.
- IAMService is healthy.
- At least one non-critical test role exists.

**User path:**
1. Open role list.
2. Create or edit a role.
3. Add permissions from grouped categories.
4. Save changes and reopen the role detail.
5. Assign the role to a test user.
6. Verify the test user's navigation and restricted actions change accordingly.

**Features covered:**
- Role list and detail.
- Permission category grouping.
- Role-to-user assignment.
- Permission propagation.
- Auditability.

**Services involved:**
- `Maliev.Intranet.Bff`
- `IAMService`
- `AuthService`

**Data created or mutated:**
- Role record.
- Role-permission mappings.
- User-role assignment.
- Audit record where implemented.

**Verification checklist:**
- Role detail route displays name, description, category-grouped permissions, and assigned users where supported.
- BFF endpoint reaches IAMService as the domain owner for role state.
- Duplicate role names or invalid permission keys are blocked with visible validation.
- Permission category labels match the actual permission keys used by protected endpoints.
- A user with the edited role can perform newly granted actions and cannot perform non-granted actions.

**Observability checks:**
- IAMService logs role mutations.
- AuthService evaluates the updated permission set on a new token/session check.
- Aspire traces show no unexpected downstream calls outside IAM/Auth.

**Current implementation status:** Partial. Ready where role-management UI exists; permission category UX may need product work.

**Product direction implied by story:** Permission categories must be understandable to non-developer admins and map cleanly to protected routes.

**Known product gaps:** Role templates and guardrails for high-risk permissions should be defined.

## INT-012: Employee manages material master data

**Persona:** Operations, estimating, or production employee.

**Entry point:** Intranet material master-data pages.

**Business value:** Ensures quoting, procurement, and manufacturing use trusted material definitions.

**Prerequisites:**
- Employee has material-management permission.
- MaterialService, PricingService, InventoryService, and SearchService are healthy.
- Test material category and supplier data exist where required.

**User path:**
1. Open material list.
2. Create or edit material name, category, process compatibility, unit, cost, and visible flags.
3. Save and reopen material detail.
4. Verify the material appears in search and eligible quoting/configuration pickers.
5. Disable/archive the material and verify it is hidden from new operational selections while historical records remain readable.

**Features covered:**
- Material master data.
- Process compatibility.
- Pricing inputs.
- Search indexing.
- Archive/active behavior.

**Services involved:**
- `Maliev.Intranet.Bff`
- `MaterialService`
- `PricingService`
- `InventoryService`
- `SearchService`

**Data created or mutated:**
- Material record.
- Compatibility/settings data.
- Cost or pricing metadata.
- Search index document where implemented.

**Verification checklist:**
- UI route shows saved material fields after refresh.
- BFF endpoint writes to MaterialService as domain owner.
- PricingService uses active material data for price calculations when applicable.
- Archived materials do not appear in new quote/project selectors but remain visible on historical records.
- Unauthorized users cannot create, edit, or archive materials.

**Observability checks:**
- MaterialService logs create/update/archive action.
- SearchService receives index update where configured.
- PricingService traces show current material id/version during price calculation.
- Aspire dashboard shows dependent service health during the flow.

**Current implementation status:** Partial. Automate implemented material pages first and mark missing pricing/search propagation as gaps.

**Product direction implied by story:** Material data should be governed centrally and reused by quoting, purchasing, inventory, and production.

**Known product gaps:** Define material versioning rules so historical quotes keep the exact material assumptions used at quote time.

## INT-013: Employee manages equipment and facility master data

**Persona:** Production manager or maintenance employee.

**Entry point:** Intranet facility/equipment master-data pages.

**Business value:** Confirms production scheduling uses accurate machine/work-center capabilities and maintenance status.

**Prerequisites:**
- Employee has facility/equipment management permissions.
- FacilityService, JobService, SearchService, and NotificationService are healthy.
- Test process/material capability data exists.

**User path:**
1. Open equipment/work-center list.
2. Create or edit machine name, process capability, capacity, status, and location.
3. Add maintenance notes or downtime status.
4. Save and reopen detail.
5. Verify production scheduling selectors reflect active equipment and exclude unavailable equipment.

**Features covered:**
- Equipment/work-center master data.
- Maintenance notes.
- Capability and availability.
- Production schedule integration.
- Search visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `FacilityService`
- `JobService`
- `SearchService`
- `NotificationService`

**Data created or mutated:**
- Equipment/work-center record.
- Capability metadata.
- Maintenance note/status.
- Search index document where implemented.

**Verification checklist:**
- Equipment route displays saved capabilities, status, and notes.
- BFF endpoint writes to FacilityService as domain owner.
- Unavailable equipment is blocked or clearly warned when assigned to a production job.
- Maintenance notes remain visible to authorized employees and hidden from customer-facing surfaces.
- Search results reflect the latest equipment status if equipment is indexed.

**Observability checks:**
- FacilityService logs equipment and maintenance changes.
- JobService traces assignment validation against current equipment status.
- NotificationService emits maintenance or downtime events where configured.
- Aspire health remains green for facility and job services.

**Current implementation status:** Partial. Ready where facility/equipment UI exists; production assignment validation may still be a gap.

**Product direction implied by story:** Equipment availability should actively guide production scheduling instead of being passive reference data.

**Known product gaps:** Define maintenance schedule recurrence, downtime calendar, and whether capacity is hour-based, quantity-based, or both.

## INT-014: Employee uses dashboard overview to detect work needing attention

**Persona:** Sales, operations, finance, or management employee.

**Entry point:** Intranet dashboard/business overview.

**Business value:** Confirms employees can spot stalled work without manually opening every module.

**Prerequisites:**
- Employee is signed in with permissions for at least one operational module.
- SearchService, NotificationService, OrderService, ProjectService, QuotationService, InvoiceService, and JobService are healthy as applicable.
- Test data includes overdue quote, pending payment, production job, delivery, or customer follow-up.

**User path:**
1. Open Intranet dashboard.
2. Review KPI cards, work queues, alerts, or recent activity.
3. Click a dashboard item.
4. Verify navigation lands on the correct domain detail page.
5. Resolve or update the underlying work item.
6. Return to dashboard and verify stale alert/count behavior is resolved or refreshes according to policy.

**Features covered:**
- Business overview.
- Work queue.
- Cross-module navigation.
- Permission-scoped dashboard data.
- Stale-data refresh.

**Services involved:**
- `Maliev.Intranet.Bff`
- `SearchService`
- `NotificationService`
- `ProjectService`
- `QuotationService`
- `OrderService`
- `InvoiceService`
- `JobService`
- `IAMService`

**Data created or mutated:**
- Underlying domain record updated by linked action.
- Dashboard state may be read-only.
- Notification/read state where implemented.

**Verification checklist:**
- Dashboard route shows only data from modules the employee can access.
- Each clicked item opens the correct URL and visible detail state.
- BFF aggregates data through service-owned endpoints instead of direct database access.
- Counts refresh after mutation or show a clear last-updated state.
- Direct URL to a restricted detail remains denied even if a dashboard card count exists.

**Observability checks:**
- Aspire traces show bounded dashboard aggregation calls.
- Slow dashboard calls are visible with correlation ids.
- Notification/Search health remains current during long-running dashboard sessions.

**Current implementation status:** Partial. Automate existing dashboard widgets and capture missing work queues as product gaps.

**Product direction implied by story:** Dashboard should become a permission-scoped action surface for operational risk, not just static metrics.

**Known product gaps:** Define the critical work queues and SLA thresholds that deserve first-screen visibility.

## INT-015: Employee uses chat or AI assistant and verifies tool callbacks

**Persona:** Employee using guided assistance inside Intranet.

**Entry point:** Intranet chat/assistant surface.

**Business value:** Confirms AI-assisted workflows are grounded in authorized system data and produce auditable actions.

**Prerequisites:**
- Employee has permission to use assistant feature and the target module.
- Chat/assistant service and any tool-backed domain services are healthy.
- Test customer, quote, order, or document data exists.

**User path:**
1. Open Intranet assistant.
2. Ask a domain-specific question or request a safe action, such as finding a customer or summarizing a quote.
3. Assistant invokes the appropriate tool callback or service endpoint.
4. User reviews the result and confirms any mutating action if supported.
5. Open the referenced domain record and verify the assistant's result/action matches system state.

**Features covered:**
- Assistant UI.
- Tool callback execution.
- Permission-scoped data retrieval.
- Human confirmation for mutations.
- Audit/log trail.

**Services involved:**
- `Maliev.Intranet.Bff`
- Assistant/chat service if deployed
- `SearchService`
- Target domain service such as `CustomerService`, `ProjectService`, `QuotationService`, `OrderService`, or `Document`/`UploadService`
- `IAMService`

**Data created or mutated:**
- Assistant conversation.
- Tool-call audit record.
- Optional domain mutation only after explicit user confirmation.

**Verification checklist:**
- Chat route shows assistant response tied to the requested domain object.
- BFF or assistant gateway enforces IAM permissions before tool callback execution.
- Tool callback uses service-owned APIs and returns traceable identifiers.
- Mutating actions require confirmation and show resulting domain state.
- Assistant does not reveal restricted customer, pricing, document, or employee-only data.

**Observability checks:**
- Assistant/tool-call logs include correlation id, user id, tool name, and target record id without leaking secrets.
- Target domain service traces show the tool-originated call.
- Unauthorized tool attempts are denied and logged safely.

**Current implementation status:** Required gap unless the assistant surface and tool callbacks are already deployed.

**Product direction implied by story:** AI assistance must be permission-aware, auditable, and useful inside real employee workflows.

**Known product gaps:** Define supported assistant tools, approval policy for mutating actions, retention policy, and redaction rules.

---

# Commerce, Catalog, And Storefront Stories

## COM-001: Employee creates or edits catalog product

**Persona:** Employee managing products for the storefront.

**Entry point:** Intranet `/commerce/catalog`.

**Business value:** Allows MALIEV to manage storefront listings without Shopify/import tooling.

**Prerequisites:**
- Employee has commerce catalog permission.
- CommerceService is healthy.
- Test image/media artifact is available if media upload is part of the listing.

**User path:**
1. Open commerce catalog manager.
2. Create new product with title, handle, description, category, pricing, variant, and media.
3. Save as draft.
4. Edit product fields.
5. Verify draft remains hidden from public storefront.

**Features covered:**
- Managed product CRUD.
- Product variants.
- Product media.
- Draft visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CommerceService`
- `UploadService` if media uses upload service

**Data created or mutated:**
- Product record.
- Variant records.
- Media references.

**Verification checklist:**
- Required product fields validate in UI.
- Product handle is unique and stable.
- Draft product appears in employee catalog manager.
- Draft product does not appear in Web `/shop`.
- Editing product preserves variants/media unless explicitly changed.

**Observability checks:**
- CommerceService managed product endpoints are used for employee UI.
- Public product endpoints do not return drafts.

**Current implementation status:** Ready to automate.

**Known product gaps:** Media upload behavior should be included once final media storage path is fixed.

## COM-002: Employee publishes product and Web shop shows it

**Persona:** Employee publishing a storefront listing.

**Entry point:** Intranet `/commerce/catalog`, then Web `/shop`.

**Business value:** Confirms employee catalog changes become customer-visible only when published.

**Prerequisites:**
- Draft product from `COM-001`.
- WebBff is healthy and references CommerceService.

**User path:**
1. Open draft product in Intranet.
2. Publish product.
3. Open Web `/shop`.
4. Search or browse for the product.
5. Open product detail.

**Features covered:**
- Publish state transition.
- Storefront listing.
- Storefront product detail.
- Public/managed route separation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CommerceService`
- `Maliev.Web.Bff`

**Data created or mutated:**
- Product status changes from draft to published.

**Verification checklist:**
- Published product appears in Web shop.
- Product detail uses the public handle route.
- Public detail matches safe customer-facing fields.
- Employee-only fields are not exposed publicly.

**Observability checks:**
- WebBff resolves CommerceService through Aspire service discovery.
- CommerceService public route returns published product only.

**Current implementation status:** Ready to automate.

**Known product gaps:** Public storefront caching must invalidate quickly enough for E2E to observe publication.

## COM-003: Customer browses storefront categories, product detail, cart, and checkout draft

**Persona:** Customer buying a published product.

**Entry point:** Web `/shop`, `/shop/{handle}`, `/cart`.

**Business value:** Verifies customer shopping behavior from published catalog to checkout draft.

**Prerequisites:**
- Published product from `COM-002`.
- Customer account exists if checkout requires login.

**User path:**
1. Open `/shop`.
2. Filter or browse category.
3. Open product detail.
4. Add product/variant to cart.
5. Update cart quantity.
6. Create checkout draft.

**Features covered:**
- Storefront category/listing.
- Product detail.
- Cart.
- Checkout draft.

**Services involved:**
- `Maliev.Web.Bff`
- `CommerceService`
- `OrderService`
- `PaymentService`
- `DeliveryService`
- `CustomerService`

**Data created or mutated:**
- Cart state.
- Checkout/order draft.

**Verification checklist:**
- Product selected in cart matches CommerceService product/variant.
- Quantity update changes totals.
- Checkout draft reflects customer identity if signed in.
- Draft can be resumed or safely abandoned.

**Observability checks:**
- WebBff logs CommerceService and checkout calls.
- No archived/draft products appear.

**Current implementation status:** Ready to automate.

**Known product gaps:** Final checkout/payment behavior may require separate payment-provider sandbox work.

## COM-004: Employee archives product and Web storefront hides it

**Persona:** Employee removing a product from sale.

**Entry point:** Intranet `/commerce/catalog`, then Web `/shop`.

**Business value:** Prevents customers from buying discontinued or invalid products.

**Prerequisites:**
- Published product exists.
- Employee has catalog update permission.

**User path:**
1. Open published product in Intranet.
2. Archive or unpublish it.
3. Open Web `/shop`.
4. Confirm product is absent from listing.
5. Open old product detail URL.
6. Confirm detail is hidden, 404, or safe unavailable state.

**Features covered:**
- Archive/unpublish state.
- Storefront visibility rules.
- Old URL safety.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CommerceService`
- `Maliev.Web.Bff`

**Data created or mutated:**
- Product status changes to archived/unpublished.

**Verification checklist:**
- Archived product remains available in employee manager.
- Archived product disappears from public listing.
- Public detail URL does not allow purchase.
- Existing cart behavior is safe if product was already in cart.

**Observability checks:**
- CommerceService public route filters archived products.
- WebBff does not serve stale published data indefinitely.

**Current implementation status:** Ready to automate.

**Known product gaps:** Define exact customer-facing behavior for old product URLs.

---

# Operations, Manufacturing, Procurement, And HR Stories

## OPS-001: Employee uses global search with permission-scoped results

**Persona:** Employee searching across MALIEV data.

**Entry point:** Intranet global search.

**Business value:** Lets employees find records quickly while respecting permissions.

**Prerequisites:**
- SearchService is healthy and seeded/indexed.
- At least one customer, project, order, and product exists.
- Two employee personas exist: broad access and restricted access.

**User path:**
1. Sign in as broad-access employee.
2. Search for known customer/project/order/product terms.
3. Open result and verify navigation.
4. Sign in as restricted employee.
5. Repeat search.
6. Verify unauthorized result types or records are absent.

**Features covered:**
- Global search UI.
- Search index propagation.
- Permission filtering.
- Result navigation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `SearchService`
- `IAMService`
- Domain services that publish searchable records

**Data created or mutated:**
- Search index entries.

**Verification checklist:**
- Search dropdown opens and closes correctly.
- Results include expected allowed records.
- Results exclude unauthorized records.
- Clicking result navigates to correct Intranet route.
- Search remains responsive for common queries.

**Observability checks:**
- SearchService receives permission-scoped query.
- IAM permissions are applied through service/BFF context.

**Current implementation status:** Ready to automate.

**Known product gaps:** Missing index coverage for a domain should be logged as product/search backlog.

## OPS-002: Employee monitors system health during long-running session

**Persona:** Admin/operations employee.

**Entry point:** Intranet `/admin/system-health`.

**Business value:** Confirms that employees can trust the Intranet health view while Aspire shows services healthy.

**Prerequisites:**
- Aspire stack is running.
- Employee has admin/system-health permission.

**User path:**
1. Open system health page.
2. Verify service groups and columns are aligned.
3. Compare several service statuses with Aspire dashboard.
4. Leave page mounted through at least one refresh interval.
5. Confirm status/history updates without manual browser refresh.
6. Open a service detail where supported.

**Features covered:**
- System health aggregation.
- Liveness/readiness route usage.
- Long-running page auto-refresh.
- Visual alignment and current/history consistency.

**Services involved:**
- `Maliev.Intranet.Bff`
- All probed service health endpoints
- `IAMService` for admin authorization

**Data created or mutated:**
- Health history records if persisted.

**Verification checklist:**
- Page probes service-owned `/liveness` and `/readiness` where operational health requires it.
- Current status and history strip do not contradict each other.
- Page auto-refreshes while mounted.
- Healthy services are not shown red due to display compression errors.

**Observability checks:**
- Intranet BFF health controller logs successful probes.
- Aspire dashboard health agrees for sampled services.

**Current implementation status:** Ready to automate.

**Known product gaps:** Any service missing clear liveness/readiness should be fixed at service level.

## OPS-003: Employee receives notifications for key events

**Persona:** Employee tracking customer/project/order changes.

**Entry point:** Intranet notifications surface.

**Business value:** Ensures event-driven work reaches the right people without manual polling.

**Prerequisites:**
- NotificationService is healthy.
- Employee has notification preferences configured.
- Trigger event exists, such as customer created, quote generated, payment received, or delivery updated.

**User path:**
1. Sign in as employee.
2. Open notification preferences and confirm desired channel.
3. Trigger a business event.
4. Open notification center.
5. Verify notification content and link.
6. Mark notification read.

**Features covered:**
- Notification preferences.
- Event consumption.
- Notification center.
- Read/unread state.

**Services involved:**
- `Maliev.Intranet.Bff`
- `NotificationService`
- RabbitMQ
- Triggering domain service

**Data created or mutated:**
- Notification preference.
- Notification delivery/log record.
- Read/unread state.

**Verification checklist:**
- Notification is created for subscribed event.
- Notification is not created for disabled preference.
- Notification link routes to the correct record.
- Read state persists after refresh.

**Observability checks:**
- RabbitMQ event is consumed by NotificationService.
- Delivery log shows success/failure state.

**Current implementation status:** Ready to automate for implemented notification events.

**Known product gaps:** Event types without notification mappings should be cataloged.

## FIN-001: Employee creates invoice with attachments, billing notes, and credit terms

**Persona:** Finance or sales operations employee.

**Entry point:** Intranet finance/invoice pages.

**Business value:** Confirms billing can be created with enough commercial context for customer payment and internal audit.

**Prerequisites:**
- Customer and billable order/quotation exist.
- InvoiceService, CustomerService, OrderService, UploadService, PdfService, and AccountingService are healthy.
- Employee has invoice-create permission.

**User path:**
1. Open invoice creation page from finance module or order detail.
2. Select customer/order and verify billing address/payment terms are loaded.
3. Add invoice lines, credit terms, billing notes, tax/discounts where supported, and attachment files.
4. Save draft invoice and reopen it.
5. Finalize or issue invoice according to supported policy.
6. Verify generated invoice PDF/artifact and customer/accounting visibility.

**Features covered:**
- Invoice creation.
- Billing terms and notes.
- Attachment upload.
- Invoice PDF artifact.
- Accounting handoff.

**Services involved:**
- `Maliev.Intranet.Bff`
- `InvoiceService`
- `CustomerService`
- `OrderService`
- `UploadService`
- `PdfService`
- `AccountingService`
- `NotificationService`

**Data created or mutated:**
- Invoice draft/issued record.
- Invoice line items.
- Attachment artifact references.
- PDF artifact.
- Accounting entry where implemented.

**Verification checklist:**
- UI route displays draft and issued invoice states with stable invoice id/number.
- BFF endpoint calls InvoiceService as domain owner and uses UploadService for attachments.
- Billing address, customer tax fields, credit terms, and notes persist after refresh.
- PDF artifact contains invoice number, customer data, line totals, tax/discounts, terms, and attachment/document references where applicable.
- Unauthorized employees cannot issue or edit invoice financial fields.

**Observability checks:**
- InvoiceService logs draft and issue transitions.
- UploadService stores and authorizes attachments.
- PdfService trace links generated invoice PDF to the invoice id.
- AccountingService receives invoice-issued event where implemented.
- Aspire dashboard keeps finance and document services healthy.

**Current implementation status:** Partial. Ready for implemented invoice screens; attachment and accounting propagation may remain gaps.

**Product direction implied by story:** Invoice creation should be a finance-owned workflow with auditable artifacts and clear payment terms.

**Known product gaps:** Define whether customer-facing invoice delivery happens through Web, QuoteEngine, email, or a finance-only process.

## FIN-002: Employee updates invoice and payment status

**Persona:** Finance employee.

**Entry point:** Intranet invoice detail/payment pages.

**Business value:** Confirms payment state, receipt generation, and accounting status stay aligned.

**Prerequisites:**
- Issued invoice exists.
- PaymentService, InvoiceService, Receipt/PdfService, AccountingService, and NotificationService are healthy.
- Employee has payment-update permission.

**User path:**
1. Open issued invoice detail.
2. Record full, partial, failed, refunded, or corrected payment status according to supported policy.
3. Attach payment evidence where supported.
4. Generate or open receipt artifact.
5. Verify invoice status, accounting effect, and customer/order payment state.

**Features covered:**
- Payment status update.
- Receipt artifact.
- Payment evidence.
- Accounting side effect.
- Customer/order payment visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `InvoiceService`
- `PaymentService`
- `PdfService`
- `AccountingService`
- `OrderService`
- `UploadService`
- `NotificationService`

**Data created or mutated:**
- Payment record/status.
- Invoice status.
- Receipt PDF/artifact.
- Payment evidence artifact.
- Accounting entry.
- Notification record where configured.

**Verification checklist:**
- Invoice detail route shows updated payment state and history.
- BFF endpoint delegates payment mutations to PaymentService or InvoiceService according to current ownership.
- Receipt PDF includes invoice number, customer, paid amount, payment date, method, and generated-by identity.
- Accounting/payment side effects match the amount and payment status.
- Unauthorized employees cannot mark payment complete or view restricted payment evidence.

**Observability checks:**
- PaymentService and InvoiceService traces share correlation id.
- PdfService logs receipt generation with correct document type.
- AccountingService event consumer handles payment event where implemented.
- NotificationService emits customer/internal payment notification where configured.

**Current implementation status:** Partial. Automate implemented payment/receipt paths and keep provider/accounting gaps visible.

**Product direction implied by story:** Payment updates should be one auditable flow, not disconnected invoice, receipt, and accounting actions.

**Known product gaps:** Define correction/refund rules and the source of truth between InvoiceService and PaymentService.

## MFG-001: Employee schedules manufacturing work

**Persona:** Shop-floor or production employee.

**Entry point:** Intranet manufacturing schedule/job pages.

**Business value:** Confirms accepted work can be planned into a production schedule without changing the current ProjectNew quote/project lifecycle.

**Prerequisites:**
- Accepted order/job exists from quote acceptance.
- FacilityService, MaterialService, InventoryService, JobService, and OrderService are healthy.
- Employee has manufacturing permissions.

**User path:**
1. Open production schedule.
2. Select job from accepted order.
3. Set planned start/end date, priority, and production queue.
4. Save schedule assignment.
5. Reopen schedule and job detail to verify planned state.
6. Verify customer/order status exposes only safe high-level production progress where applicable.

**Features covered:**
- Production schedule.
- Job planning.
- Queue/priority.
- Permission-scoped production data.
- Customer-safe status projection.

**Services involved:**
- `Maliev.Intranet.Bff`
- `JobService`
- `OrderService`
- `FacilityService`
- `NotificationService`

**Data created or mutated:**
- Job schedule/status.
- Planned dates/priority.
- Order production status.

**Verification checklist:**
- Job appears in production schedule at the selected date/queue.
- BFF endpoint writes scheduling state to JobService as domain owner.
- Invalid or conflicting schedule dates are blocked or clearly warned.
- Employee without manufacturing scheduling permission cannot create or edit schedule assignments.
- Customer-facing surfaces do not expose internal queue, machine, or labor notes.

**Observability checks:**
- JobService logs schedule mutation.
- NotificationService emits scheduling event where configured.
- Aspire dashboard shows JobService and Intranet BFF healthy during the flow.

**Current implementation status:** Partial. Automate implemented manufacturing pages first and mark missing UI as product gaps.

**Product direction implied by story:** Scheduling should be its own production-planning journey, separate from execution, equipment, and material-consumption checks.

**Known product gaps:** Define conflict rules for overlapping work, priority, and production queue capacity.

## MFG-002: Shop-floor employee completes mobile job status transition

**Persona:** Shop-floor employee using a phone or tablet.

**Entry point:** Mobile-width Intranet job detail or shop-floor task page.

**Business value:** Confirms production execution is fast enough for real shop-floor use and satisfies the 3-second rule.

**Prerequisites:**
- Scheduled job exists.
- Employee has job execution permission.
- JobService, OrderService, NotificationService, and Intranet BFF are healthy.

**User path:**
1. Open assigned job on a mobile viewport.
2. Review only the fields needed to act: job id, part/customer-safe context, current status, next action, and blockers.
3. Start job, pause job, mark step complete, or move to the next allowed status.
4. Verify visible confirmation appears within 3 seconds on a normal test environment.
5. Refresh or reopen job and verify persisted status.

**Features covered:**
- Mobile shop-floor UI.
- Job status transition.
- Fast-action UX.
- Permission boundary.
- Status persistence.

**Services involved:**
- `Maliev.Intranet.Bff`
- `JobService`
- `OrderService`
- `NotificationService`
- `IAMService`

**Data created or mutated:**
- Job status transition.
- Optional timestamp/operator record.
- Notification/readiness event where configured.

**Verification checklist:**
- Mobile route renders without horizontal overflow and keeps the primary action visible.
- BFF endpoint updates JobService as domain owner.
- Invalid transitions are disabled or rejected with visible feedback.
- Status change completes and paints the new state within 3 seconds in Aspire test conditions.
- Unauthorized employee cannot transition a job by direct endpoint or direct URL.

**Observability checks:**
- JobService logs transition, operator id, prior status, and next status.
- Aspire trace includes BFF-to-JobService call duration.
- NotificationService consumes job-status event where configured.

**Current implementation status:** Required gap unless a mobile shop-floor execution surface already exists.

**Product direction implied by story:** Production execution must be optimized for short, repeated shop-floor actions.

**Known product gaps:** Define exact status model, mobile navigation, and offline/poor-network behavior.

## MFG-003: Employee assigns equipment or work center from production schedule

**Persona:** Production planner or supervisor.

**Entry point:** Intranet production schedule/job assignment page.

**Business value:** Confirms jobs are matched to available equipment and work centers using current facility data.

**Prerequisites:**
- Scheduled job and active equipment/work-center records exist.
- FacilityService and JobService are healthy.
- Employee has equipment assignment permission.

**User path:**
1. Open production schedule.
2. Select a scheduled job.
3. Choose equipment or work center from eligible active options.
4. Save assignment and reopen job detail.
5. Attempt to assign unavailable or incompatible equipment and verify guardrail behavior.

**Features covered:**
- Equipment/work-center selection.
- Capability validation.
- Availability validation.
- Production assignment.
- Maintenance-state awareness.

**Services involved:**
- `Maliev.Intranet.Bff`
- `JobService`
- `FacilityService`
- `IAMService`
- `NotificationService`

**Data created or mutated:**
- Equipment/work-center assignment.
- Job planning metadata.
- Assignment audit event.

**Verification checklist:**
- Assignment UI lists only active/eligible equipment or clearly labels warnings.
- BFF reads equipment from FacilityService and writes assignment to JobService.
- Incompatible or unavailable equipment is blocked or requires explicit override permission.
- Saved assignment appears on schedule and job detail after refresh.
- Customer-facing views do not expose internal machine or maintenance details.

**Observability checks:**
- FacilityService traces equipment lookup.
- JobService logs assignment mutation.
- NotificationService emits assignment event where configured.
- Aspire health remains green for FacilityService and JobService.

**Current implementation status:** Partial. Automate where facility and production assignment UI exists.

**Product direction implied by story:** Equipment assignment should be constrained by current facility capabilities and maintenance state.

**Known product gaps:** Define whether equipment conflict detection is hard-blocking or advisory.

## MFG-004: Employee records material reservation or consumption for a job

**Persona:** Production or inventory employee.

**Entry point:** Intranet job material or inventory reservation page.

**Business value:** Confirms production consumes inventory in a traceable way without losing cost/accounting context.

**Prerequisites:**
- Scheduled job exists with material requirements.
- InventoryService, MaterialService, JobService, and AccountingService are healthy.
- Employee has material reservation/consumption permission.

**User path:**
1. Open job material tab.
2. Reserve required material from inventory or confirm material already issued.
3. Record actual consumption or scrap/waste where supported.
4. Save and reopen job/inventory detail.
5. Verify inventory quantity and job material status update.

**Features covered:**
- Material reservation.
- Material consumption.
- Inventory decrement.
- Job material status.
- Cost/accounting impact.

**Services involved:**
- `Maliev.Intranet.Bff`
- `JobService`
- `InventoryService`
- `MaterialService`
- `AccountingService`
- `NotificationService`

**Data created or mutated:**
- Material reservation/issue record.
- Inventory quantity/value.
- Job material status.
- Accounting/cost record where implemented.

**Verification checklist:**
- Job material route shows required, reserved, issued, consumed, and remaining quantities where supported.
- BFF writes inventory changes through InventoryService as domain owner.
- Consumption cannot exceed reserved/available quantity without explicit override policy.
- Inventory detail reflects the reservation or decrement after refresh.
- Accounting/cost impact is emitted or recorded where implemented.

**Observability checks:**
- InventoryService logs reservation/consumption transaction.
- JobService receives or reads updated material status.
- AccountingService consumes inventory/cost event where configured.
- Aspire dashboard shows no stale service health after the mutation.

**Current implementation status:** Partial. Ready only where job material and inventory UI are available.

**Product direction implied by story:** Material movement should be traceable from job requirement to inventory and accounting impact.

**Known product gaps:** Define lot/batch traceability, scrap reason codes, and override policy.

## MFG-005: Production status updates stay fresh through SignalR or refresh

**Persona:** Sales, operations, production, and customer-support employees watching job progress.

**Entry point:** Intranet production board, order detail, or job detail page.

**Business value:** Confirms production status does not become stale during long-running work sessions.

**Prerequisites:**
- Production job exists.
- JobService, OrderService, NotificationService, and any SignalR hub/BFF refresh path are healthy.
- Two employee sessions are available for cross-session verification.

**User path:**
1. Open production board or order/job detail in Session A.
2. Change job status from Session B or backend test fixture.
3. Wait for SignalR update or documented refresh interval.
4. Verify Session A shows the new status without manual full-page reload where product requires live behavior.
5. Open customer-safe status surface and verify it updates only to approved external milestones.

**Features covered:**
- Real-time or timed status refresh.
- Cross-session state consistency.
- Production board.
- Customer-safe progress projection.
- Stale UI handling.

**Services involved:**
- `Maliev.Intranet.Bff`
- `JobService`
- `OrderService`
- `NotificationService`
- SignalR hub/BFF real-time path where implemented
- `IAMService`

**Data created or mutated:**
- Job status.
- Notification/live update message.
- Order production status where applicable.

**Verification checklist:**
- Session A visible status updates through SignalR or documented refresh interval.
- BFF/SignalR channel enforces user permissions before sending job data.
- Stale state is clearly refreshed or marked with last-updated time if real-time is not implemented.
- Customer-facing status hides internal statuses and shows only approved progress.
- Long-running page session keeps auth valid or redirects safely when expired.

**Observability checks:**
- SignalR or polling traces include job id and correlation id.
- JobService logs source status transition.
- NotificationService/live update path records delivery where configured.
- Aspire dashboard shows no connection or health degradation.

**Current implementation status:** Required gap unless production live-update UI is already implemented.

**Product direction implied by story:** Production status surfaces should be trustworthy during active operations, not stale snapshots.

**Known product gaps:** Define which production pages require SignalR versus timed refresh and the acceptable update latency.

## PROC-001: Employee creates purchase order

**Persona:** Procurement employee.

**Entry point:** Intranet purchasing pages.

**Business value:** Confirms procurement can create a controlled purchase request without bundling receiving/accounting into the same E2E gate.

**Prerequisites:**
- Supplier and material exist.
- PurchaseOrderService, SupplierService, MaterialService, and IAMService are healthy.
- Employee has purchase-order create permission.

**User path:**
1. Open purchasing.
2. Create purchase order for supplier/material.
3. Add line items, quantities, requested date, price/terms, and internal notes.
4. Save as draft.
5. Submit or approve according to current workflow.
6. Reopen PO detail and verify persisted state.

**Features covered:**
- Purchase order creation.
- Supplier/material selection.
- Draft/submitted status.
- Line item validation.
- Permission boundary.

**Services involved:**
- `Maliev.Intranet.Bff`
- `PurchaseOrderService`
- `SupplierService`
- `MaterialService`
- `IAMService`
- `NotificationService`

**Data created or mutated:**
- Purchase order.
- PO line items.
- Approval/submission status.
- Notification/audit record where configured.

**Verification checklist:**
- PO creation route shows supplier, material, line totals, requested date, and status after save.
- BFF endpoint writes to PurchaseOrderService as domain owner.
- Invalid supplier/material combinations or missing required fields are blocked with visible validation.
- Unauthorized employee cannot create, submit, or approve a PO.
- PO remains editable or locked according to documented draft/submitted policy.

**Observability checks:**
- PurchaseOrderService logs create and submit/approve transitions.
- SupplierService and MaterialService traces show lookup calls.
- NotificationService emits approval/request event where configured.
- Aspire service health remains green.

**Current implementation status:** Partial. Ready where PO creation UI exists.

**Product direction implied by story:** PO creation should be a procurement-owned journey with clear draft/submitted/approved behavior.

**Known product gaps:** Define approval workflow, PO numbering, and whether supplier price terms are copied from SupplierService or entered per PO.

## PROC-002: Employee creates and edits supplier profile

**Persona:** Procurement employee.

**Entry point:** Intranet supplier pages.

**Business value:** Confirms supplier records can support procurement, quality, billing, and contact workflows.

**Prerequisites:**
- Employee has supplier-management permission.
- SupplierService, SearchService, UploadService, and IAMService are healthy.
- Optional test tax/company lookup data exists where supported.

**User path:**
1. Open supplier list.
2. Create supplier with company name, contact, tax/registration, address, payment terms, categories, and notes.
3. Attach supplier documents where supported.
4. Save and reopen supplier detail.
5. Edit key fields and verify search/PO selectors use the latest supplier data.

**Features covered:**
- Supplier create/edit.
- Contact/address/payment terms.
- Supplier documents.
- Search indexing.
- PO selector integration.

**Services involved:**
- `Maliev.Intranet.Bff`
- `SupplierService`
- `UploadService`
- `SearchService`
- `PurchaseOrderService`
- `IAMService`

**Data created or mutated:**
- Supplier record.
- Supplier contacts/addresses/payment terms.
- Supplier document artifact.
- Search index entry where implemented.

**Verification checklist:**
- Supplier detail route shows saved fields and document links after refresh.
- BFF endpoint writes supplier data to SupplierService as domain owner.
- Supplier documents use UploadService and are not mixed with customer NDA/manufacturing uploads.
- Updated supplier appears in search and PO supplier selectors.
- Unauthorized users cannot view restricted financial notes or edit supplier data.

**Observability checks:**
- SupplierService logs create/update.
- UploadService logs supplier document artifact.
- SearchService receives index update where configured.
- PurchaseOrderService selector lookup sees active supplier state.

**Current implementation status:** Partial. Ready where supplier UI exists.

**Product direction implied by story:** Supplier profile should be the procurement source of truth and feed PO creation without duplicate manual entry.

**Known product gaps:** Define supplier document categories, duplicate detection, and supplier onboarding approval.

## PROC-003: Employee manages PO detail, attachments, cancellation, and audit

**Persona:** Procurement employee or manager.

**Entry point:** Intranet purchase order detail page.

**Business value:** Confirms PO changes after creation are controlled, documented, and auditable.

**Prerequisites:**
- Draft or submitted PO exists.
- PurchaseOrderService, UploadService, SupplierService, and IAMService are healthy.
- Employee has PO edit/cancel permissions as appropriate.

**User path:**
1. Open PO detail.
2. Edit allowed PO fields or line notes while in editable status.
3. Attach quote, drawing, supplier document, or internal supporting file.
4. Cancel PO with required reason.
5. Reopen PO and verify status, reason, attachments, and audit history.

**Features covered:**
- PO detail.
- Attachment upload.
- Status-specific edit rules.
- Cancellation reason.
- Audit trail.

**Services involved:**
- `Maliev.Intranet.Bff`
- `PurchaseOrderService`
- `UploadService`
- `SupplierService`
- `IAMService`
- `NotificationService`

**Data created or mutated:**
- PO detail fields.
- PO attachment artifact references.
- Cancellation status and reason.
- Audit/event record.

**Verification checklist:**
- PO detail route displays attachment list, status history, and cancellation reason after refresh.
- BFF stores files through UploadService and writes status to PurchaseOrderService.
- Submitted/approved PO edit restrictions match policy.
- Cancellation requires a reason and blocks receiving on canceled PO.
- Unauthorized employee cannot cancel or edit restricted PO fields.

**Observability checks:**
- PurchaseOrderService logs edit and cancel transitions.
- UploadService logs attachment storage with PO scope.
- NotificationService emits cancellation event where configured.
- Aspire traces show no cross-domain direct database access.

**Current implementation status:** Partial. Ready where PO detail/attachment UI exists.

**Product direction implied by story:** Procurement changes must remain explainable after the fact, especially cancellations and attached supplier evidence.

**Known product gaps:** Define immutable fields after approval and whether canceled POs can be reopened.

## PROC-004: Employee receives purchased material and verifies inventory/accounting impact

**Persona:** Procurement or warehouse employee.

**Entry point:** Intranet PO receiving or inventory receipt page.

**Business value:** Confirms purchased material becomes inventory and creates the expected financial effect.

**Prerequisites:**
- Approved/submitted PO exists with outstanding quantity.
- PurchaseOrderService, InventoryService, MaterialService, InvoiceService, AccountingService, and SupplierService are healthy.
- Employee has receiving permission.

**User path:**
1. Open approved PO.
2. Start receiving against one or more PO lines.
3. Enter received quantity, lot/batch/serial data where supported, receipt date, and evidence/packing slip attachment.
4. Save receipt.
5. Verify inventory quantity/value updates and accounting/supplier invoice state is created or updated where implemented.

**Features covered:**
- PO receiving.
- Partial/complete receipt.
- Inventory update.
- Receiving evidence.
- Accounting/invoice side effect.

**Services involved:**
- `Maliev.Intranet.Bff`
- `PurchaseOrderService`
- `InventoryService`
- `MaterialService`
- `UploadService`
- `InvoiceService`
- `AccountingService`
- `SupplierService`
- `NotificationService`

**Data created or mutated:**
- Receiving record.
- Inventory quantity/value.
- PO received status.
- Evidence artifact.
- Supplier invoice/accounting record where implemented.

**Verification checklist:**
- Receiving route shows outstanding, received, and remaining quantities.
- BFF records receipt through PurchaseOrderService/InventoryService according to current ownership.
- Receiving cannot exceed PO quantity without explicit override policy.
- Inventory detail reflects quantity/value change after refresh.
- Accounting or supplier-invoice impact matches received amount and PO terms where implemented.
- Canceled PO cannot be received.

**Observability checks:**
- PurchaseOrderService logs receiving event.
- InventoryService logs stock movement with PO reference.
- AccountingService/InvoiceService consumes receipt event where implemented.
- UploadService authorizes evidence artifact.
- Aspire dashboard keeps supply-chain and finance services healthy.

**Current implementation status:** Partial. Ready only where receiving and inventory screens exist.

**Product direction implied by story:** Receiving is the bridge between procurement, inventory, and finance and should be tested independently from PO creation.

**Known product gaps:** Define partial receiving, over-receiving tolerance, lot traceability, and accounting posting rules.

## HR-001: Employee lifecycle creates employee profile and IAM access

**Persona:** HR/admin employee onboarding another employee.

**Entry point:** Intranet employee/lifecycle/IAM pages.

**Business value:** Ensures employee access is created intentionally and can be revoked.

**Prerequisites:**
- HR/admin user has employee and IAM permissions.
- EmployeeService, LifecycleService, IAMService, and AuthService are healthy.

**User path:**
1. Open employee creation/lifecycle page.
2. Create employee profile.
3. Assign role or permissions.
4. Verify employee can sign in or appears as pending depending on policy.
5. Offboard or deactivate employee.
6. Verify access is revoked.

**Features covered:**
- Employee profile.
- IAM role/permission assignment.
- Employee auth readiness.
- Offboarding/revocation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `EmployeeService`
- `LifecycleService`
- `IAMService`
- `AuthService`

**Data created or mutated:**
- Employee record.
- IAM principal/role assignments.
- Lifecycle/offboarding record.

**Verification checklist:**
- Employee profile fields persist.
- Role assignment changes visible permissions.
- Deactivated employee cannot access Intranet.
- Existing sessions are revoked or blocked according to policy.

**Observability checks:**
- Employee-created event reaches IAM/lifecycle consumers where configured.
- AuthService denies deactivated employee.

**Current implementation status:** Ready to automate for implemented HR/IAM UI.

**Known product gaps:** Define whether new employees are active immediately or pending invitation.

## HR-002: Employee requests leave and manager approves

**Persona:** Employee and manager.

**Entry point:** Intranet leave pages.

**Business value:** Confirms internal HR operations work through employee self-service and approval.

**Prerequisites:**
- Employee and manager accounts exist.
- LeaveService and NotificationService are healthy.

**User path:**
1. Sign in as employee.
2. Create leave request.
3. Sign in as manager.
4. Review pending leave request.
5. Approve or reject.
6. Sign back in as employee and verify status.

**Features covered:**
- Leave request creation.
- Manager approval.
- Notification.
- Status visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `LeaveService`
- `EmployeeService`
- `IAMService`
- `NotificationService`

**Data created or mutated:**
- Leave request.
- Approval decision.
- Notification records.

**Verification checklist:**
- Employee cannot approve their own request unless policy allows it.
- Manager sees pending request.
- Approval changes leave status.
- Employee sees updated status.
- Notification is created where configured.

**Observability checks:**
- LeaveService logs status transition.
- NotificationService consumes leave event where configured.

**Current implementation status:** Ready to automate for implemented leave UI.

**Known product gaps:** Calendar/status integration should be added if business requires it.

## HR-003: HR manages candidate and application workflow

**Persona:** HR employee.

**Entry point:** Intranet career/candidate pages.

**Business value:** Confirms hiring candidates can be tracked before they become employees.

**Prerequisites:**
- HR user has candidate-management permission.
- CareerService, EmployeeService, UploadService, and NotificationService are healthy.
- Test applicant/candidate data or resume file exists.

**User path:**
1. Open candidate list.
2. Create candidate/application with contact details, role, source, resume, and notes.
3. Move candidate through screening/interview/offer/rejected or supported states.
4. Attach or review resume/supporting documents.
5. Convert to employee or mark final state where supported.
6. Reopen candidate and verify history, documents, and status.

**Features covered:**
- Candidate/career workflow.
- Resume/document upload.
- Hiring status history.
- Candidate-to-employee handoff.
- HR permissions.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CareerService`
- `EmployeeService`
- `UploadService`
- `NotificationService`
- `IAMService`

**Data created or mutated:**
- Candidate/application record.
- Candidate status/history.
- Resume/supporting document artifact.
- Optional employee/lifecycle record on conversion.

**Verification checklist:**
- Candidate route shows saved contact, role, source, notes, documents, and status after refresh.
- BFF writes candidate state to CareerService as domain owner.
- Resume/supporting files are stored through UploadService under HR/candidate scope.
- Invalid status transitions are blocked or clearly warned.
- Non-HR employee cannot view candidate notes or documents.

**Observability checks:**
- CareerService logs candidate creation and status transition.
- UploadService logs candidate document artifact.
- NotificationService emits hiring workflow event where configured.
- Unauthorized attempts are denied and logged safely.

**Current implementation status:** Partial. Automate candidate pages where implemented.

**Product direction implied by story:** Candidate management should be its own HR gate and should not be mixed with employee-only records.

**Known product gaps:** Define candidate stages, conversion rules, retention policy, and sensitive document permissions.

## HR-004: HR manages compliance and training records

**Persona:** HR or compliance employee.

**Entry point:** Intranet compliance/training pages.

**Business value:** Confirms mandatory employee qualifications and compliance obligations can be tracked and audited.

**Prerequisites:**
- Employee record exists.
- ComplianceService, EmployeeService, UploadService, NotificationService, and IAMService are healthy.
- HR/compliance user has training/compliance permissions.

**User path:**
1. Open employee compliance/training profile.
2. Add training requirement, certification, policy acknowledgement, or compliance record.
3. Attach evidence document where supported.
4. Mark status as pending, completed, expired, or renewed according to supported policy.
5. Reopen employee/compliance view and verify status and evidence.
6. Verify reminder/notification behavior for due or expired items where configured.

**Features covered:**
- Training record.
- Compliance/certification status.
- Evidence upload.
- Due/expiry tracking.
- Notification.

**Services involved:**
- `Maliev.Intranet.Bff`
- `ComplianceService`
- `EmployeeService`
- `UploadService`
- `NotificationService`
- `IAMService`

**Data created or mutated:**
- Compliance/training record.
- Evidence artifact.
- Due/expiry status.
- Notification/reminder record where implemented.

**Verification checklist:**
- Compliance route displays employee, requirement, status, due date, and evidence after refresh.
- BFF writes compliance data to ComplianceService as domain owner.
- Evidence files are stored through UploadService and restricted to authorized HR/compliance users.
- Expired or due records appear in dashboard/notification surfaces where configured.
- Non-HR users cannot view restricted compliance evidence.

**Observability checks:**
- ComplianceService logs record creation/status update.
- UploadService logs evidence artifact.
- NotificationService consumes due/expiry event where implemented.
- Aspire dashboard shows HR services healthy.

**Current implementation status:** Partial. Automate implemented compliance/training pages first.

**Product direction implied by story:** Compliance/training should be operationally visible and audit-ready, separate from candidate and compensation flows.

**Known product gaps:** Define mandatory training catalog, renewal rules, reminders, and document retention.

## HR-005: HR manages compensation records

**Persona:** HR or finance-authorized employee.

**Entry point:** Intranet compensation pages.

**Business value:** Confirms sensitive pay information is permission-protected and auditable.

**Prerequisites:**
- Employee record exists.
- CompensationService, EmployeeService, IAMService, and AccountingService/Payroll integration where implemented are healthy.
- HR/finance user has compensation permissions.

**User path:**
1. Open employee compensation profile.
2. Add or update compensation package, effective date, currency, allowance, bonus, or payroll note where supported.
3. Save and reopen the record.
4. Verify restricted field visibility for HR/finance user.
5. Sign in as a non-authorized employee and verify compensation data is hidden or forbidden.

**Features covered:**
- Compensation detail.
- Effective-dated pay records.
- Sensitive field protection.
- Audit history.
- Optional accounting/payroll handoff.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CompensationService`
- `EmployeeService`
- `IAMService`
- `AccountingService` or payroll integration where implemented

**Data created or mutated:**
- Compensation record.
- Effective-date history.
- Audit record.
- Optional accounting/payroll export event.

**Verification checklist:**
- Compensation route shows correct employee, currency, amount, effective date, and history for authorized user.
- BFF writes compensation state to CompensationService as domain owner.
- Non-authorized users cannot access the route, direct endpoint, or restricted fields.
- Audit metadata records who changed pay-sensitive data.
- Optional payroll/accounting side effect is emitted only after allowed status.

**Observability checks:**
- CompensationService logs mutation without exposing sensitive amounts in unsafe logs.
- IAMService denial is logged safely for unauthorized attempts.
- Accounting/payroll event is traceable where implemented.

**Current implementation status:** Partial. Automate only if compensation UI exists; otherwise keep as high-priority required gap.

**Product direction implied by story:** Compensation must be treated as one of the strongest permission boundaries in Intranet.

**Known product gaps:** Define payroll integration, approval workflow, and log redaction policy for sensitive compensation data.

## HR-006: HR or manager manages performance records

**Persona:** HR employee or manager.

**Entry point:** Intranet performance review pages.

**Business value:** Confirms employee performance data can be captured, reviewed, and protected.

**Prerequisites:**
- Employee and manager records exist.
- PerformanceService, EmployeeService, IAMService, and NotificationService are healthy.
- Reviewer has performance-review permissions.

**User path:**
1. Open employee performance profile or review cycle.
2. Create review, goals, feedback, rating, or development notes according to supported UI.
3. Save as draft or submit final review.
4. Notify employee/manager where configured.
5. Verify visibility differs between HR, manager, employee, and unrelated employee roles.

**Features covered:**
- Performance review.
- Goals/feedback.
- Draft/final state.
- Notification.
- Role-scoped visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `PerformanceService`
- `EmployeeService`
- `IAMService`
- `NotificationService`

**Data created or mutated:**
- Performance review.
- Goal/feedback/rating records.
- Draft/final status.
- Notification record where implemented.

**Verification checklist:**
- Performance route displays review content and status after refresh for authorized users.
- BFF writes review state to PerformanceService as domain owner.
- Draft review is visible only to allowed reviewers/HR.
- Final review follows the documented employee visibility policy.
- Unrelated employee cannot view or mutate another employee's performance record.

**Observability checks:**
- PerformanceService logs review creation/submission.
- NotificationService emits review event where configured.
- IAMService denial traces are safe and do not expose review content.
- Aspire dashboard keeps HR domain services healthy.

**Current implementation status:** Partial. Automate implemented performance pages and keep missing review-cycle UI as gap.

**Product direction implied by story:** Performance records should be managed separately from compensation and compliance because visibility rules differ.

**Known product gaps:** Define review cycles, employee acknowledgement, manager calibration, and retention policy.

---

# Security And Negative Journey Stories

## SEC-001: Customer cannot access another customer's private records

**Persona:** Signed-in customer attempting to access another customer's data.

**Entry point:** Maliev.Web account pages, Maliev.QuoteEngine customer portal pages, direct document/PDF URLs, and BFF endpoints.

**Business value:** Confirms customer isolation across profiles, quotes, orders, NDA records, documents, and generated PDFs.

**Prerequisites:**
- Customer A and Customer B exist with separate quotes, orders, NDA records, documents, and PDF artifacts where implemented.
- Web BFF, QuoteEngine BFF, AuthService, IAM/authorization boundary, CustomerService, QuotationService, OrderService, UploadService, and PdfService are healthy.
- Customer A is signed in.

**User path:**
1. Sign in as Customer A.
2. Open Customer A profile, quote, order, NDA, document, and PDF links to establish allowed access.
3. Attempt to navigate to Customer B profile/quote/order/document/PDF URLs by changing identifiers or using captured links.
4. Attempt equivalent direct BFF/API calls where Playwright test support can safely exercise them.
5. Verify the UI remains on an authorized page or shows a safe not-found/forbidden state without leaking Customer B data.

**Features covered:**
- Customer tenant/data isolation.
- Quote/order ownership.
- NDA/document/PDF authorization.
- Direct URL protection.
- Safe error handling.

**Services involved:**
- `Maliev.Web.Bff`
- `Maliev.QuoteEngine.Bff`
- `AuthService`
- `CustomerService`
- `QuotationService`
- `OrderService`
- `UploadService`
- `PdfService`
- `IAMService` or authorization service boundary where applicable

**Data created or mutated:**
- None expected except safe audit/security logs.

**Verification checklist:**
- Customer A sees only Customer A records in list and detail routes.
- BFF endpoints reject Customer B identifiers even if the URL shape is valid.
- Upload/PDF artifact links require owner authorization and do not expose signed URLs for another customer.
- Error responses do not reveal Customer B name, email, quote number, order detail, document title, or internal ids beyond what policy allows.
- No domain data is mutated by denied access attempts.

**Observability checks:**
- Auth/authorization traces show denied access with correlation id and customer principal.
- Denial logs avoid sensitive customer payloads.
- Aspire dashboard keeps Web, QuoteEngine, and backend services healthy after denied attempts.

**Current implementation status:** Required production gate. Automate wherever customer portal/list/detail routes exist; mark missing QuoteEngine prototype-backed surfaces as partial.

**Product direction implied by story:** Every customer-facing surface must enforce ownership at the BFF/service boundary, not only by hiding links in UI.

**Known product gaps:** Define consistent forbidden versus not-found behavior for cross-customer resource probing.

## SEC-002: Employee without permission cannot access restricted Intranet modules by direct URL

**Persona:** Employee with limited permissions.

**Entry point:** Intranet restricted module URLs and menu navigation.

**Business value:** Confirms IAM permissions protect employee-only operations even when users know the route.

**Prerequisites:**
- Limited employee and admin employee accounts exist.
- IAMService, AuthService, Intranet BFF, and target domain services are healthy.
- Restricted modules include examples from finance, IAM admin, HR, procurement, manufacturing, and customer documents where implemented.

**User path:**
1. Sign in as limited employee.
2. Verify restricted menu items are hidden or disabled.
3. Navigate directly to restricted URLs such as IAM role management, compensation, invoice issue/payment, supplier management, or production scheduling.
4. Attempt restricted mutations through UI or direct BFF calls where safe test support exists.
5. Sign in as admin/authorized employee and verify the same route/action is allowed.

**Features covered:**
- Intranet IAM enforcement.
- Direct URL protection.
- Restricted mutation protection.
- Navigation shaping.
- Audit/security logging.

**Services involved:**
- `Maliev.Intranet.Bff`
- `IAMService`
- `AuthService`
- Restricted target services such as `InvoiceService`, `CompensationService`, `SupplierService`, `JobService`, `CustomerService`, `UploadService`

**Data created or mutated:**
- None from denied attempts.
- Optional audit/security log records.

**Verification checklist:**
- Limited employee cannot see restricted navigation.
- Direct restricted route shows forbidden/not-authorized state without rendering sensitive content.
- BFF endpoint returns denied response for restricted mutation attempts.
- Authorized employee can access the same route/action, proving the test target is valid.
- Denied attempts do not create, update, or delete domain records.

**Observability checks:**
- IAMService/AuthService traces show permission evaluation.
- Intranet BFF logs denied action with route/action and correlation id.
- Target domain service is not called for denied mutations unless policy intentionally performs authorization there too.

**Current implementation status:** Ready to automate for implemented restricted routes and permissions.

**Product direction implied by story:** UI navigation shaping is helpful, but every protected action must also be enforced at BFF/service boundaries.

**Known product gaps:** Maintain a route-to-permission catalog so future modules are not missed.

## SEC-003: Expired sessions redirect safely and preserve intended return URL

**Persona:** Customer or employee returning to a long-running browser session.

**Entry point:** Maliev.Web account/checkout, Maliev.QuoteEngine workspace, and Maliev.Intranet protected pages.

**Business value:** Confirms expired sessions fail safely without data loss, broken loops, or accidental access.

**Prerequisites:**
- Protected page is available in each target app.
- AuthService, Web BFF, QuoteEngine BFF, Intranet BFF, and IAMService are healthy.
- Test can simulate expired access token/refresh token or use a short-lived test token.

**User path:**
1. Sign in and open a protected page with unsaved or in-progress context where supported.
2. Expire or invalidate the session/token.
3. Trigger navigation, refresh, BFF call, or autosave.
4. Verify redirect to login or safe session-expired page.
5. Sign in again and verify return URL restores the intended page when appropriate.
6. Verify unsafe POST/mutation is not replayed automatically without explicit user action.

**Features covered:**
- Session expiration.
- Return URL preservation.
- Safe redirect.
- BFF auth refresh behavior.
- Unsaved-work handling.

**Services involved:**
- `Maliev.Web.Bff`
- `Maliev.QuoteEngine.Bff`
- `Maliev.Intranet.Bff`
- `AuthService`
- `IAMService`
- Target domain service for the protected page

**Data created or mutated:**
- Session/token state.
- No domain mutation expected during denied/re-auth redirect unless user explicitly resubmits.

**Verification checklist:**
- Expired GET request redirects safely and preserves intended return URL where product supports it.
- Expired mutation request is denied or asks for re-auth without double-submitting.
- UI shows clear session-expired/sign-in state and does not expose protected data after expiration.
- Re-auth returns to the intended route and reloads current data.
- Auth cookies/tokens are cleared or refreshed according to policy.

**Observability checks:**
- AuthService logs token expiration/revocation path.
- BFF traces show 401/redirect handling with correlation id.
- Target domain service does not receive unauthorized mutation after token expiration.
- Aspire dashboard shows no auth retry loop or repeated failed requests.

**Current implementation status:** Required production gate. Automate once test hooks can reliably expire sessions.

**Product direction implied by story:** Long-running Web, QuoteEngine, and Intranet sessions must be safe and recoverable.

**Known product gaps:** Define exact return URL and unsaved-work policies per app.

## SEC-004: Employee-only pricing and operational fields remain hidden from customer-facing surfaces

**Persona:** Customer viewing quote/order documents and portal pages; employee verifying internal data exists.

**Entry point:** Maliev.Web customer account/order pages, Maliev.QuoteEngine quote/order pages, customer-facing PDFs, and Intranet internal quote/order pages.

**Business value:** Confirms internal costing, outsourced pricing, margins, supplier details, and operational notes are never exposed to customers.

**Prerequisites:**
- Quote/order exists with both customer-facing price and employee-only internal fields.
- PricingService, QuotationService, OrderService, PdfService, Web BFF, QuoteEngine BFF, and Intranet BFF are healthy.
- Employee and customer test accounts exist.

**User path:**
1. Sign in as employee and open internal quote/order detail.
2. Verify internal fields exist where authorized, such as cost, margin, outsourced/internal pricing, supplier, machine/work-center, and internal notes.
3. Sign in as customer and open the related customer-facing quote/order page.
4. Download or view customer-facing quote/order PDF.
5. Verify all employee-only fields are absent while customer-facing totals and terms remain correct.

**Features covered:**
- Customer-safe DTO projection.
- PDF redaction/field selection.
- Pricing data separation.
- Internal versus external notes.
- Role-based visibility.

**Services involved:**
- `Maliev.Intranet.Bff`
- `Maliev.Web.Bff`
- `Maliev.QuoteEngine.Bff`
- `PricingService`
- `QuotationService`
- `OrderService`
- `PdfService`
- `CustomerService`
- `IAMService`

**Data created or mutated:**
- None expected, except optional generated customer-facing PDF artifact.

**Verification checklist:**
- Internal Intranet route shows employee-only fields only to authorized employees.
- Customer route/DTO omits internal cost, margin, supplier, outsourced price, internal notes, equipment/work-center, and employee-only statuses.
- Customer-facing PDF omits employee-only fields and includes only approved quote/order fields.
- BFF and service projections are verified, not just CSS-hidden UI labels.
- Customer cannot retrieve internal fields through browser network calls or direct BFF endpoint.

**Observability checks:**
- PdfService trace identifies customer-facing document type.
- BFF traces show customer-safe endpoint/projection paths.
- Denied/internal-field attempts are logged safely.
- Aspire health remains green for pricing, quote/order, and PDF services.

**Current implementation status:** Required production gate. Ready to automate where customer-facing quote/order/PDF surfaces exist; partial for QuoteEngine prototype-backed pages.

**Product direction implied by story:** Internal commercial data must be explicitly separated from customer-facing contracts, pages, and PDFs.

**Known product gaps:** Maintain a documented allowlist of customer-facing quote/order/PDF fields.

---

# Production Gate Acceptance

Before production deployment, the E2E suite derived from these stories should provide:

- At least one passing customer path through `Maliev.Web`.
- At least one passing dedicated quote path through `Maliev.QuoteEngine` once it is service-backed.
- At least one passing employee sales/ProjectNew path through `Maliev.Intranet`.
- At least one passing quote-to-order/payment/delivery path.
- At least one passing commerce publish-to-storefront path.
- Passing or intentionally skipped Web trust/conversion, customer portal, admin/master-data, finance, procurement, manufacturing execution, HR, and security negative-path stories according to their current status.
- Explicit failing/skipped tests or tracked product gaps for email verification, password reset email delivery, and QuoteEngine prototype replacement until those flows are complete.
- No passing criterion in this catalog depends on making ProjectNew and QuoteEngine share the same quote/project lifecycle model.

The final gate should report stories by id, category, status, and linked failure evidence. A story that is intentionally not automated yet must remain visible as a required gap rather than disappearing from the gate.
