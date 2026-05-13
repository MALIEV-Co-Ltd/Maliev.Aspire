# MALIEV Aspire E2E User Journey Stories

> Production-gate user journey catalog for browser-driven E2E tests against the Aspire integrated environment.
>
> Last updated: 2026-05-13

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

## MFG-001: Employee schedules manufacturing work and tracks job progress

**Persona:** Shop-floor or production employee.

**Entry point:** Intranet manufacturing schedule/job pages.

**Business value:** Confirms accepted work becomes trackable production activity.

**Prerequisites:**
- Accepted order/job exists from quote acceptance.
- FacilityService, MaterialService, InventoryService, JobService, and OrderService are healthy.
- Employee has manufacturing permissions.

**User path:**
1. Open production schedule.
2. Select job from accepted order.
3. Assign equipment or work center.
4. Reserve or confirm material.
5. Move job through planned/in-progress/done statuses.
6. Verify order/customer status updates where expected.

**Features covered:**
- Production schedule.
- Equipment/facility assignment.
- Material/inventory availability.
- Job lifecycle.
- Status propagation.

**Services involved:**
- `Maliev.Intranet.Bff`
- `JobService`
- `OrderService`
- `FacilityService`
- `MaterialService`
- `InventoryService`
- `NotificationService`

**Data created or mutated:**
- Job schedule/status.
- Equipment assignment.
- Material reservation/consumption.
- Order production status.

**Verification checklist:**
- Job appears in production schedule.
- Equipment assignment persists.
- Invalid status transitions are blocked.
- Shop-floor action path is fast and not overloaded with unnecessary fields.
- Customer/order status reflects meaningful progress only.

**Observability checks:**
- JobService logs lifecycle transition.
- Inventory/material events occur where configured.

**Current implementation status:** Partial. Automate implemented manufacturing pages first and mark missing UI as product gaps.

**Known product gaps:** Shop-floor workflows must satisfy the 3-second rule.

## PROC-001: Employee creates purchase order, receives supplier material, and updates inventory/accounting

**Persona:** Procurement employee.

**Entry point:** Intranet purchasing pages.

**Business value:** Confirms supply chain replenishment connects purchasing, supplier, inventory, invoice, and accounting.

**Prerequisites:**
- Supplier and material exist.
- PurchaseOrderService, SupplierService, MaterialService, InventoryService, InvoiceService, and AccountingService are healthy.

**User path:**
1. Open purchasing.
2. Create purchase order for supplier/material.
3. Approve or submit PO.
4. Receive material.
5. Verify inventory quantity/value updates.
6. Verify invoice/accounting record where applicable.

**Features covered:**
- Purchase order creation.
- Supplier/material selection.
- Receiving.
- Inventory update.
- Invoice/accounting side effect.

**Services involved:**
- `Maliev.Intranet.Bff`
- `PurchaseOrderService`
- `SupplierService`
- `MaterialService`
- `InventoryService`
- `InvoiceService`
- `AccountingService`

**Data created or mutated:**
- Purchase order.
- Receiving record.
- Inventory quantity/value.
- Supplier invoice/accounting record.

**Verification checklist:**
- PO line references correct supplier/material.
- Receiving cannot exceed allowed quantity without explicit rule.
- Inventory changes after receiving.
- Accounting/invoice impact matches PO/receipt.
- Unauthorized employee cannot approve or receive.

**Observability checks:**
- PurchaseOrderService event chain reaches inventory/accounting where implemented.
- Service health remains green.

**Current implementation status:** Ready to automate where UI exposes the complete flow.

**Known product gaps:** Any missing receiving/accounting UI should be documented as product backlog.

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

## HR-003: HR manages candidate, training, compliance, compensation, and performance records

**Persona:** HR employee.

**Entry point:** Intranet HR modules.

**Business value:** Confirms the employee lifecycle beyond hiring is visible and controlled.

**Prerequisites:**
- HR user has required permissions.
- Career, Compliance, Compensation, Performance, and Lifecycle services are healthy.
- Test employee/candidate exists.

**User path:**
1. Create or open candidate/application.
2. Move candidate through hiring state where UI exists.
3. Open employee training/compliance record.
4. Add or verify compliance/training item.
5. Open compensation/performance records.
6. Update review or compensation data according to permissions.

**Features covered:**
- Candidate/career workflow.
- Training/compliance tracking.
- Compensation records.
- Performance records.
- HR permissions.

**Services involved:**
- `Maliev.Intranet.Bff`
- `CareerService`
- `ComplianceService`
- `CompensationService`
- `PerformanceService`
- `LifecycleService`
- `EmployeeService`
- `IAMService`

**Data created or mutated:**
- Candidate/application record.
- Compliance/training record.
- Compensation/performance record.

**Verification checklist:**
- HR-only records are not visible to non-HR users.
- Candidate/employee status transitions persist.
- Sensitive compensation fields are permission-protected.
- Performance/compliance updates appear after refresh.

**Observability checks:**
- Each HR domain service remains healthy.
- Unauthorized attempts are denied and logged safely.

**Current implementation status:** Partial. Automate the HR modules that have production UI; document missing pages as product gaps.

**Known product gaps:** HR modules should be prioritized according to current operational need and data sensitivity.

---

# Production Gate Acceptance

Before production deployment, the E2E suite derived from these stories should provide:

- At least one passing customer path through `Maliev.Web`.
- At least one passing dedicated quote path through `Maliev.QuoteEngine` once it is service-backed.
- At least one passing employee sales/ProjectNew path through `Maliev.Intranet`.
- At least one passing quote-to-order/payment/delivery path.
- At least one passing commerce publish-to-storefront path.
- Explicit failing/skipped tests or tracked product gaps for email verification, password reset email delivery, and QuoteEngine prototype replacement until those flows are complete.

The final gate should report stories by id, category, status, and linked failure evidence. A story that is intentionally not automated yet must remain visible as a required gap rather than disappearing from the gate.
