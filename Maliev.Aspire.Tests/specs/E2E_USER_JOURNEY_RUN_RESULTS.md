# MALIEV E2E User Journey Run Results

> Dated execution evidence for the production-gate E2E journey catalog and Aspire integrated environment checks.
> Keep the stable story definitions in [E2E_USER_JOURNEY_STORIES.md](./E2E_USER_JOURNEY_STORIES.md); use this file for run results, blockers, and fixes.
> Latest sections appear first. Older manual sections are retained as historical evidence and may include blockers that later automated runs resolved.

## 2026-06-20 Make Studio Agent Account Context Persistence Gate

### Scope

- Updated the QuoteEngine Make Studio `quote_get_account_context` tool so signed-in profile and default checkout addresses come from CustomerService instead of `QuoteEnginePrototypeStore`.
- Kept prototype account-context fallback limited to development/testing; production now returns an explicit `account_context_available` gate when CustomerService account context cannot be served.
- Strengthened endpoint coverage so the signed-in account context must expose CustomerService-backed company, VAT, billing address, and shipping address values.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Agent_account_context_returns_auth_handoff_without_customer_defaults_when_anonymous\|FullyQualifiedName~Agent_account_context_returns_signed_in_profile_and_default_checkout_addresses" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: anonymous context stays auth-only, while signed-in Make Studio account context uses CustomerService-backed profile and default checkout addresses |
| `dotnet build Maliev.QuoteEngine.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: QuoteEngine solution compiled with 0 warnings and 0 errors |

## 2026-06-20 Make Studio Agent Document Search Persistence Gate

### Scope

- Updated the QuoteEngine Make Studio `quote_search_customer_data` tool so customer document results come from CustomerService-backed account documents instead of `QuoteEnginePrototypeStore` sample document rows.
- Preserved durable ProjectService search ordering, session file/part search results, and artifact search results while filtering prototype document rows out of the assistant search surface.
- Added focused endpoint coverage proving the agent finds a document created through the account document endpoint, marks it with `source=customer_service`, preserves order-number metadata, and does not return the legacy `manufacturing-requirements.pdf` prototype document.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Agent_search_customer_data_returns_customer_scoped_resources" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: agent customer-data search returns ProjectService project results plus CustomerService-backed document results and filters prototype documents |
| `dotnet build Maliev.QuoteEngine.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: QuoteEngine solution compiled with 0 warnings and 0 errors |

## 2026-06-20 Make Studio Account Documents And NDA Persistence Gate

### Scope

- Moved QuoteEngine account document and NDA endpoints to CustomerService-backed records for signed Make Studio customers, replacing the previous prototype-store read/write path whenever the downstream service is available.
- Kept prototype account fallback limited to development/testing; production now returns a 503 instead of acknowledging document metadata or NDA reads that cannot be durably served.
- Preserved customer-owned document validation, multipart upload streaming through UploadService, signed document downloads, and cross-customer denial while adding focused NDA endpoint coverage.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Account_ndas_reads_customer_service_records\|FullyQualifiedName~Account_documents_allow_customer_scoped_purchase_order_uploads\|FullyQualifiedName~Account_documents_reject_unsafe_storage_paths_and_unknown_kinds\|FullyQualifiedName~Account_documents_download_returns_signed_url_for_owner_only\|FullyQualifiedName~Account_documents_upload_streams_file_bytes_to_upload_service" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 5 focused tests covering CustomerService-backed NDA reads, customer-scoped document create/list, invalid document rejection, owner-only signed downloads, and multipart upload streaming |
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~QuoteEngineSourceTests.Documents_page_supports_customer_purchase_order_invoice_receipt_uploads" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: source guard now requires the account controller to call `CreateCustomerDocumentAsync(customerId, ...)` rather than the prototype document store |
| `dotnet build Maliev.QuoteEngine.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: QuoteEngine solution compiled with 0 warnings and 0 errors |

## 2026-06-20 Make Studio Partial-Delivery Quantity Gate

### Scope

- Fixed DeliveryService persistence so multiple active delivery notes can exist for the same Make Studio order, removing the filtered unique `order_id` index that blocked partial shipments.
- Strengthened cumulative delivered-quantity validation so existing deliveries are matched by `PurchaseOrderItemId` when available, falling back to `ProductCode` only for older payloads without line identity.
- Added focused service tests proving a second valid partial delivery persists, cumulative over-delivery is rejected, and two order lines with the same product code can each be delivered without being incorrectly collapsed into one SKU total.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.DeliveryService.Tests\Maliev.DeliveryService.Tests.csproj --filter "FullyQualifiedName~CreateAsync_WithSecondPartialDeliveryWithinOrderedQuantity_PersistsDeliveryQuantity\|FullyQualifiedName~CreateAsync_WithCumulativeQuantityOverOrderedQuantity_ThrowsArgumentException\|FullyQualifiedName~CreateAsync_WithSameProductCodeOnDifferentPurchaseOrderItems_AllowsSeparateLineQuantities" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 3 focused tests covering accepted cumulative partial delivery, rejected over-delivery, and same-SKU separate order-line delivery |
| `dotnet build Maliev.DeliveryService.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: DeliveryService solution compiled with 0 warnings and 0 errors |

## 2026-06-20 Make Studio Delivery Proof And PDF Download Gate

### Scope

- Added a DeliveryService file-download contract for delivery-note attachments that enforces the delivery note/customer boundary and streams stored proof bytes from the configured file storage provider.
- Added an Intranet BFF proxy route for authenticated delivery-note file downloads so employee delivery workflows do not expose downstream service URLs or storage internals.
- Fixed generated delivery-note PDFs so DeliveryService publishes PDF requests outside the EF outbox-only entity mutation path, PdfService fetches delivery-note data with an authenticated service client, and DeliveryService can download PdfService/UploadService signed URL bytes.
- Strengthened `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` so the paid Make Studio production handoff uploads proof-of-delivery evidence, requests the delivery-note PDF, lists both DeliveryService file records, downloads both artifacts through Intranet, verifies the original PNG bytes plus `%PDF` bytes, and proves NotificationService records the customer delivery-completed notification log.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.DeliveryService.Tests\Maliev.DeliveryService.Tests.csproj --filter "FullyQualifiedName~RequestPdfGenerationAsync_WithExistingDeliveryNote_PublishesPdfRequest\|FullyQualifiedName~DownloadFileAsync_WithGeneratedPdfUrl_ReturnsSignedUrlBytes\|FullyQualifiedName~GeneratePdf_Success_ReturnsAccepted\|FullyQualifiedName~ServiceAccountPrincipal_ReturnsTrueWithoutIamCall" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 5 focused tests covering direct delivery PDF request publishing, generated signed-URL byte downloads, controller request handoff, and service-account delivery-note reads |
| `dotnet build Maliev.DeliveryService.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: DeliveryService solution compiled with 0 warnings and 0 errors |
| `dotnet build Maliev.PdfService.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: PdfService solution compiled with 0 warnings and 0 errors |
| `dotnet test Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~DownloadFileAsync_CallsDeliveryServiceDownloadRoute" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused test covering the Intranet DeliveryService client download route and byte response |
| `dotnet build Maliev.Intranet.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: Intranet shared/client/BFF/tests compiled with 0 warnings and 0 errors |
| `dotnet build Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-restore --verbosity quiet -p:UseSharedCompilation=false -m:1 /nr:false /clp:ErrorsOnly` | Passed: Aspire E2E test project compiled with 0 warnings and 0 errors |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal` | Passed: 1 focused browser E2E test covering Make Studio payment-to-delivery handoff plus proof-of-delivery upload/download, generated delivery-note PDF request/attachment/download/byte validation through Intranet, and NotificationService delivery-completed log creation for the customer/order |

## 2026-06-20 Make Studio Current-Version Acceptance Gate

### Scope

- Added a version-aware quotation acceptance contract so Intranet can post the expected quotation version id/number when accepting a project quote.
- ProjectService now rejects stale expected versions with `409 Conflict` before changing project status or publishing the accepted-quotation event, while preserving no-body acceptance for internal/backward-compatible callers.
- Strengthened the project quote lifecycle browser E2E to attempt accepting version 1 after version 2 is current, verify the BFF returns `409`, verify the project remains `QuotationGenerated`, and then accept the current version through the UI.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.ProjectService.Tests\Maliev.ProjectService.Tests.csproj --filter "FullyQualifiedName~AcceptQuotation_WithStaleExpectedVersion_ShouldReturnConflictAndLeaveProjectQuoted|FullyQualifiedName~QuotationAcceptedEventConsumer_WithoutPayload_IsIgnored" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 2 focused tests covering stale-version conflict and backward-compatible event-consumer acceptance |
| `dotnet build Maliev.Intranet.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: Intranet shared DTO, BFF, client, and tests compiled with 0 warnings and 0 errors |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_ProjectQuoteLifecycle_GeneratesVersionsPdfAcceptanceAndDuplicate" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused browser E2E test covering stale-version rejection, current-version UI acceptance, PDF artifact bytes, acceptance, and duplicate project restoration |

## 2026-06-20 Make Studio Quotation PDF Artifact Byte Gate

### Scope

- Strengthened `Intranet_ProjectQuoteLifecycle_GeneratesVersionsPdfAcceptanceAndDuplicate` so generated quotation versions must expose non-empty PDF artifact URLs and durable `pdfs/quotation/` storage paths.
- Added byte-level PDF retrieval through Playwright's request context for version 1, version 2, and the latest-PDF endpoint; each artifact must return HTTP 200, a non-trivial byte length, a `%PDF-` header, and a `%%EOF` trailer.
- Verified version 1 and version 2 use distinct PDF artifact storage paths, keeping quotation revision history from passing with a stale or reused current-version artifact.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-restore --verbosity quiet -p:UseSharedCompilation=false -m:1 /nr:false /clp:ErrorsOnly` | Passed: Aspire E2E test project compiled with 0 warnings and 0 errors |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~Intranet_ProjectQuoteLifecycle_GeneratesVersionsPdfAcceptanceAndDuplicate" --verbosity minimal` | Passed: 1 focused browser E2E test covering project quote version generation, per-version PDF artifact byte validity, current-version PDF validity, acceptance, and duplicate project restoration |

## 2026-06-20 Make Studio Project Quote Versioning Contract Gate

### Scope

- Updated QuoteEngine's QuotationService client to create the first formal quote for a source ProjectService project and revise the existing project quotation on later quote generation.
- Added project snapshot JSON/hash, source project id/number, and change summary fields to the QuoteEngine -> QuotationService create/update payloads.
- Added a focused QuoteEngine endpoint regression proving two formal quote submissions for the same project keep the same quotation id while advancing from version 1 to version 2.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Formal_quote_for_same_project_creates_new_version_on_existing_quotation|FullyQualifiedName~Order_creation_sends_quote_total_to_order_service|FullyQualifiedName~Agent_payment_confirmation_after_order_sets_payment_gate_and_artifact" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 3 focused tests covering one-quotation-per-project versioning plus preserved order quote-version metadata |

## 2026-06-20 Make Studio Quote Version Order Linkage Gate

### Scope

- Added nullable OrderService quote reference fields so a manufacturing order durably records the accepted formal quote id, quote number, immutable quote version id, and quote version number.
- Updated QuoteEngine's QuotationService client, Make Studio formal-quote artifact metadata, OrderService create payload, order artifact metadata, and customer order-detail mapping to preserve the same version identity.
- Strengthened the Make Studio browser E2E gate so the formal quote artifact, order artifact, paid OrderService order detail, and QuoteEngine customer order detail all agree on the exact accepted quote version.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.OrderService.Tests\Maliev.OrderService.Tests.csproj --filter "FullyQualifiedName~PostOrdersPersistsAcceptedQuoteVersionReference" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused contract test covering order create/detail persistence of quote id, quote number, quote version id, and quote version number |
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~CreateAsync_SendsOrderServiceWireContractWithQuoteTotal|FullyQualifiedName~Order_creation_sends_quote_total_to_order_service|FullyQualifiedName~Agent_payment_confirmation_after_order_sets_payment_gate_and_artifact" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 3 focused tests covering QuoteEngine OrderService wire JSON, self-service order version propagation, and Make Studio order artifact metadata |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused browser E2E test covering corrected reupload, formal quote version metadata, durable OrderService quote-version linkage, paid order detail restoration, customer order detail restoration, production job linkage, and delivery completion |

## 2026-06-20 Make Studio Prompt Context Contract Gate

### Scope

- Strengthened ChatbotService's QuoteEngine assistant prompt with explicit manufacturing process defaults, DFM interpretation guidance, Thai/English identity, and compact table guidance for comparable assumptions.
- Aligned `SendMessageRequest.Content` validation with `MessagePipelinePolicy.MaxContentCharacters` so QuoteEngine can send the full Make Studio turn up to the downstream 8000-character contract instead of the previous 4000-character request annotation.
- Updated QuoteEngine BFF prompt composition so durable manufacturing guidance is kept before dynamic state when content must be trimmed, preserving behavioral instructions while recoverable project state can be re-read through tools.
- Added/validated generated-preview input safety around invalid profile planes and preserved signed-upload replacement for large inline sketch previews.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.ChatbotService.Tests\Maliev.ChatbotService.Tests.csproj --filter "FullyQualifiedName~MessagePipelinePolicyTests|FullyQualifiedName~SystemInstructionDefaultPromptTests" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 25 focused tests covering message length policy and Make Studio system instruction content |
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Generate_3d_preview_tool_rejects_invalid_profile_plane_before_creating_ready_artifact|FullyQualifiedName~Agent_message_stream_with_uploaded_sketch_prefers_signed_storage_url_over_inline_preview" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 2 focused tests covering generated-preview validation and keeping streamed Make Studio messages under the downstream ChatbotService limit with signed sketch attachments |

## 2026-06-20 Make Studio Customer Order Tracking Gate

### Scope

- Strengthened `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` so the final customer-visible order detail route must restore the manufacturing progress section, completed delivery milestone, paid payment state, delivered handoff, and OrderService lifecycle timeline entries.
- This keeps the Make Studio payment-to-production-to-delivery path from passing only on downstream service records while the customer order page fails to show the restored status tracking.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused browser E2E test covering Make Studio payment-to-production-to-delivery plus customer-visible manufacturing progress, paid payment state, delivered handoff, and restored OrderService timeline entries |

## 2026-06-20 Make Studio Upload And Preview Guard Gate

### Scope

- Hardened the QuoteEngine Make Studio upload entry point so the hidden file picker cannot open before the workspace route state is initialized and repeated upload button/dropzone events cannot trigger duplicate picker opens.
- Hardened browser generated-preview profile shorthand handling so missing or invalid profile dimensions fail instead of silently falling back to default geometry.
- Reset the fake payment client state in the checkout shipping snapshot endpoint test so payment assertions are isolated from prior initiations in the shared test collection.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~QuoteWorkspace_FilePickerOpenIsGuardedUntilRouteStateAndSinglePendingOpen|FullyQualifiedName~ReplicadWorker_validates_profile_shorthand_dimensions_before_face_creation|FullyQualifiedName~Payment_initiation_persists_checkout_shipping_snapshot_on_order" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 3 focused tests covering upload picker guards, generated-preview profile dimension validation, and payment snapshot test isolation |

## 2026-06-20 Make Studio Real Record Isolation Gate

### Scope

- Strengthened `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` so the second signed customer must be denied direct access to the Make Studio project detail, scoped quote list/detail page, order detail, and generated order-file downloads created by the first customer.
- This extends the existing artifact ownership proof from file-download endpoints to the real ProjectService/QuotationService/OrderService records that Make Studio creates during the service-backed quote-to-order path.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused browser E2E test covering Make Studio project, quote, order, and order-file cross-customer denial while preserving the paid production and delivery flow |

## 2026-06-20 Make Studio Preview Transform Guard Gate

### Scope

- Hardened the browser Replicad worker used by Make Studio generated previews so translate vectors and rotate axis/angle values are validated before calling CAD operations.
- This prevents malformed BFF/tool-generated transform commands from falling through to unsafe default geometry behavior in the customer workbench preview.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~ReplicadWorker_validates_transform_params_before_cad_calls" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused source test covering generated-preview translate and rotate transform validation |

## 2026-06-20 Make Studio Login Restore E2E Gate

### Scope

- Added `QuoteEngine_MakeStudioAgent_LoginRedirectRestoresActiveChat` to prove an anonymous Make Studio chat can start before authentication, persist the active shared QuoteEngine agent session in browser storage, complete the real email sign-up redirect, and land back on `/quote/new`.
- The gate verifies the original customer message and the ChatbotService-backed assistant message are restored in the Make Studio thread after the authenticated return.
- This adds browser-level proof for the original Make Studio requirement that login redirects preserve the active chat with chat messages properly restored.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgent_LoginRedirectRestoresActiveChat" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused E2E test covering anonymous active chat creation, shared session persistence, real email sign-up return to QuoteEngine, and restored customer/assistant messages in Make Studio |

## 2026-06-20 Make Studio Employee Review Queue E2E Gate

### Scope

- Added a visible Intranet `Customer Review` project-list queue tab for ProjectService `CustomerReview` projects.
- Added `QuoteEngine_MakeStudioReviewRequest_AppearsInIntranetCustomerReviewQueue` to prove a signed Make Studio customer can create a draft project, confirm the `quote_request_employee_review` agent action, and then an authenticated Intranet employee session can open the review queue.
- The gate verifies the Intranet BFF project list returns the `CustomerReview` project and project detail restores the Make Studio review note.
- This closes the remaining browser/API queue proof for `QUOTE-021`.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build Maliev.Intranet.slnx --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: Intranet Client/BFF/tests compiled with 0 warnings and 0 errors after adding the `Customer Review` queue tab |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioReviewRequest_AppearsInIntranetCustomerReviewQueue" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused E2E test covering Make Studio review request, QuoteEngine confirmation artifact, Intranet `Customer Review` queue route, CustomerReview list membership, and durable review-note detail readback |

## 2026-06-20 Make Studio Employee Review Request Contract Gate

### Scope

- Added a ProjectService `CustomerReview` project status and customer-scoped `/project/v1/projects/{id}/request-review` endpoint that stores a durable project note for MALIEV employee review.
- Added the QuoteEngine `quote_request_employee_review` agent action as a confirmation-required Make Studio tool path that calls ProjectService, updates the project status, and records a `project_review_request` workbench artifact with project id, project number, status, and note metadata.
- Exposed the same tool through ChatbotService's QuoteEngine tool registry and forwarding allow-list so production tool selection can request review through the BFF instead of leaving the request as chat text.
- This closes the service contract portion of `QUOTE-021`; a later browser release gate can add an explicit Intranet employee queue/list interaction on top of the existing ProjectService-backed project status and notes.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.ProjectService.Tests\Maliev.ProjectService.Tests.csproj --filter "FullyQualifiedName~RequestReview_DraftProject_ShouldSetCustomerReviewStatusAndAddNote" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 integration test covering customer-scoped review request, `CustomerReview` status persistence, durable employee-facing project note, and status-filtered project list visibility |
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Agent_project_management_tool_requires_confirmation_and_updates_customer_project" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 5 theory rows covering project pin, unpin, archive, employee review request, and achieve confirmation flows through the QuoteEngine agent endpoint |
| `dotnet test Maliev.ChatbotService.Tests\Maliev.ChatbotService.Tests.csproj --filter "FullyQualifiedName~GetToolDeclarationsForProfile_ReturnsQuoteEngineWorkflowToolsOnlyForQuoteEngine" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 unit test covering QuoteEngine workflow tool declarations, including `quote_request_employee_review` exposure |

## 2026-06-20 Make Studio Quote Version History Contract Gate

### Scope

- Extended QuoteEngine's customer quote summary contract so `/quote/v1/account/quotes` preserves QuotationService `versions[]` metadata instead of flattening each formal quote to only the first PDF URL.
- The customer quote detail page now renders a `Quote revision history` section with version number, generated-by display name, created date, total/currency, change summary, and per-version PDF links when available.
- The focused QuoteEngine endpoint test now models two versions for one customer quote and verifies both version summaries remain scoped to the owning customer quote response.
- This covers the QuoteEngine customer boundary for `QUOTE-026`; a broader browser seed with real QuotationService multi-version data remains useful as a later release-gate proof.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Account_quote_and_order_history_is_scoped_to_signed_in_customer" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 focused endpoint test covering owner-scoped customer quote/order history plus two-version quote history mapping |

## 2026-06-20 Make Studio Multi-Part Project E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` from a single corrected CAD file to a corrected two-part Make Studio project.
- The gate verifies a blocked revision A upload is superseded by corrected revision B while a second corrected CAD part is added, both returned `partId` values are configured through `quote_update_part_configuration`, and the resulting order workbench artifact metadata carries both corrected file names plus total quantity.
- The downstream check now requires the paid Intranet project detail to link at least two Make Studio project parts to the OrderService order, OrderService items, and JobService production jobs.
- This moves `QUOTE-016` from blocked to covered for the current service-backed Make Studio agent-tool path.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering corrected multi-part Make Studio upload replacement, explicit per-part configuration, two-part order metadata, and two-part ProjectService/OrderService/JobService production linkage |

## 2026-06-20 Make Studio Order Artifact Ownership E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to prove OrderService can upload Make Studio manufacturing packet files through UploadService's authenticated internal artifact endpoint.
- The gate verifies UploadService seeds an OrderService policy for `orders/` artifacts, OrderService attaches order-confirmation and manufacturing-packet PDFs to the paid order, QuoteEngine exposes the order files only in the owning customer's order detail, returns mock-storage signed URLs, downloads the expected artifact bytes, and denies direct cross-customer file download.
- The gate exposed and fixed a production-readiness defect: OrderService used UploadService's old resumable-upload session shape and a plain unauthenticated service client, which failed in Aspire/Testing and could not satisfy UploadService's service authorization boundary.
- This closes the current service-backed `QUOTE-023` artifact ownership surface for customer documents, generated formal quotes, order-confirmation PDFs, and manufacturing-packet PDFs.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.UploadService.Tests\Maliev.UploadService.Tests.csproj --filter "FullyQualifiedName~SeedSamplePoliciesAsync_WebAndQuotePoliciesAllowFbxUploads|FullyQualifiedName~SeedSamplePoliciesAsync_AddsOrderPolicyWhenOlderSamplePoliciesExist" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 2 tests covering seeded OrderService `orders/` upload authorization and backfilling the policy into databases that already have older sample policies |
| `dotnet test Maliev.OrderService.Tests\Maliev.OrderService.Tests.csproj --filter "FullyQualifiedName~UploadServiceClientUploadFileAsyncSuccessReturnsResult" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test covering the UploadService `/upload/v1/uploads/artifacts` JSON contract, storage path, content type, and Base64 artifact payload |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering authenticated OrderService order-confirmation and manufacturing-packet uploads, QuoteEngine owner order-file detail/download, mock-storage signed artifact retrieval, and cross-customer denial |

## 2026-06-20 Make Studio Delivery Proof Evidence E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to prove the paid Make Studio production handoff uploads proof-of-delivery evidence to the Intranet BFF/DeliveryService delivery-note files endpoint, lists the persisted file record, and downloads the original proof bytes through Intranet.
- The gate verifies the proof upload returns a `Signature` file, marks the delivery `Delivered` with that proof file id as `signatureFileId`, reads the delivery detail back with the linked signature id, lists the delivery files, and confirms the proof record has a storage URL.
- The gate also keeps the existing customer-facing QuoteEngine checks for delivered order milestone and order-detail delivery status.
- Later focused gates now cover generated order-confirmation and manufacturing-packet PDF owner-only downloads.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering paid Make Studio delivery proof upload, `Signature` file response, delivered-status `signatureFileId` linking, delivery detail/files readback, proof storage URL, and existing customer-visible delivered order tracking |

## 2026-06-19 Make Studio Formal Quote Artifact Ownership E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to prove the signed Make Studio formal quote path receives a real QuotationService/PdfService/UploadService-backed PDF artifact, carries its `pdfs/quotation/` storage path plus signed PDF URL in the QuoteAgent workbench artifact, and lets only the owning customer session download it.
- The gate exposed and fixed a production-readiness defect: QuoteEngine artifact download signing checked only that a path belonged to the agent session, so another signed customer who knew the session id and storage path could request a signed URL for a customer-owned formal quote artifact.
- Later focused gates now cover generated order-confirmation and manufacturing-packet PDF owner-only downloads.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~Agent_formal_quote_artifact_download" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 2 tests covering registered formal quote PDF path download and cross-customer denial for a customer-owned agent session |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~AppHost_QuotationService_ReferencesCustomerService" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 AppHost reference test covering QuotationService references to CustomerService and PdfService |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering real QuotationService/PdfService/UploadService formal quote PDF generation, QuoteAgent workbench metadata, owner signed redirect, cross-customer denial, and the existing payment/production/delivery Make Studio flow |

## 2026-06-19 Make Studio Payment Receipt E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to prove the signed Make Studio checkout path prepares a CustomerService company billing identity, creates/finalizes an InvoiceService invoice whose `poNumber` is the OrderService order number, completes the PaymentService webhook, lets InvoiceService allocate the payment by order number, and verifies ReceiptService creates the customer receipt with the PaymentService transaction id.
- The gate exposed and fixed two production-readiness defects: QuoteEngine did not create the invoice required by the existing InvoiceService -> ReceiptService receipt contract, and ReceiptService did not expose `externalPaymentId` on receipt API responses even though it persisted the field.
- QuoteEngine now preserves invoice metadata in the Make Studio workbench artifact context, and the AppHost now wires QuoteEngine to InvoiceService for deployment service discovery.
- Remaining Make Studio deployment gaps: delivery PDF artifact download/content verification and broader generated document PDF semantic content checks remain separate gates.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~InvoiceServiceClientContractTests|FullyQualifiedName~Agent_message_forwards_payment_metadata_to_chatbot_service_after_payment_handoff" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 2 tests covering InvoiceService create/finalize route shape, order `poNumber`, invoice metadata in agent artifact context, and payment handoff context |
| `dotnet test Maliev.ReceiptService.Tests\Maliev.ReceiptService.Tests.csproj --filter "FullyQualifiedName~PostReceipts_WithValidFullPayment_Returns201WithValidSchema" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 contract test covering receipt response `externalPaymentId` serialization |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~AppHost_QuoteEngineBff_ReferencesProductionQuoteJourneyServices" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 AppHost reference test covering QuoteEngine -> InvoiceService service discovery |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering Make Studio invoice creation, payment webhook completion, InvoiceService allocation, and ReceiptService receipt creation |

## 2026-06-19 QuoteEngine Customer Document Ownership E2E Gate

### Scope

- Added `QuoteEngine_CustomerDocuments_DownloadIsOwnerScoped` to cover the executable customer-document portion of `QUOTE-023` and the document slice of `SEC-001`.
- The test signs one QuoteEngine customer into Aspire, uploads a receipt through `/quote/v1/account/documents/upload`, verifies the BFF persists a `customer-documents/` artifact record, verifies owner download returns an UploadService mock-storage signed URL, signs in a second customer, and verifies the second customer cannot list or directly download the first customer's document.
- The gate exposed and fixed two production-readiness defects: QuoteEngine was still calling the removed UploadService legacy `GET upload/v1/signed-url` route instead of `/upload/v1/files/by-path/signed-url`, and UploadService's QuoteEngine policy did not authorize `customer-documents/` paths or customer document MIME types.
- Remaining `QUOTE-023` deployment gaps at this point were generated formal quote, order, and manufacturing PDF artifacts needing service-backed fixtures and customer ownership checks; later focused gates now cover the formal quote PDF and manufacturing packet slices.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~QuoteUploadServiceClientContractTests" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test covering the UploadService by-path signed URL method, route, request body, and `signedUrl` response mapping |
| `dotnet test Maliev.UploadService.Tests\Maliev.UploadService.Tests.csproj --filter "FullyQualifiedName~MigratedQuoteEnginePolicy_AllowsCustomerDocumentUploads" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test covering migrated QuoteEngine policy access to `customer-documents/` paths and PDF content |
| `dotnet build Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed with 0 warnings and 0 errors |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_CustomerDocuments_DownloadIsOwnerScoped" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser/API E2E test covering owner upload/download and cross-customer list/download isolation |

## 2026-06-19 Make Studio Delivery Status E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to cover the paid Make Studio order through Intranet delivery-note creation, `InTransit` and `Delivered` status transitions, OrderService delivery snapshot persistence, QuoteEngine agent project-summary delivery milestone completion, and the customer order-detail delivery card.
- The gate exposed and fixed a production-readiness defect: DeliveryService created and delivered notes but did not synchronize customer-visible delivery dates/contact fields back to OrderService, and its OrderService HTTP client used a non-Aspire service-discovery target.
- DeliveryService now uses the shared authenticated `"OrderService"` service client and syncs promised/actual delivery dates plus contact fields using the current OrderService concurrency version.
- Remaining Make Studio deployment gaps: delivery PDF artifact download/content verification and broader generated document PDF semantic content checks remain separate gates.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.DeliveryService.Tests\Maliev.DeliveryService.Tests.csproj --filter "FullyQualifiedName~OrderServiceClientTests|FullyQualifiedName~UpdateDeliveryStatusAsync_Delivered_SyncsOrderDeliverySnapshot" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 6 tests covering DeliveryService OrderService delivery-snapshot sync request shape and delivered-note service behavior |
| `dotnet build Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed with 0 warnings and 0 errors |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering delivery note creation, `InTransit`/`Delivered` transitions, OrderService delivery snapshot, QuoteEngine agent delivery milestone, and customer order-detail delivered state after paid Make Studio production handoff |

## 2026-06-19 Make Studio Accounting Side Effect E2E Gate

### Scope

- Extended `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to prove the signed PaymentService webhook now produces an AccountingService posted payment journal for the Make Studio order.
- The gate exposed and fixed a production-readiness defect: PaymentService completed transactions by publishing `PaymentCompletedEvent`, but AccountingService consumes the accounting-domain `PaymentRecordedEvent`, so Make Studio payments did not create the cash/AR journal side effect.
- PaymentService now publishes `PaymentRecordedEvent` for completed webhook transactions, and the E2E verifies AccountingService records a posted journal with a cash debit (`1100`) and accounts-receivable credit (`1200`) whose lines reference the PaymentService transaction id.
- Remaining Make Studio deployment gaps: delivery PDF artifact download/content verification and broader generated document PDF semantic content checks remain separate gates.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.PaymentService.Tests\Maliev.PaymentService.Tests.csproj --no-build --filter "FullyQualifiedName~WebhookProcessingServiceTests" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 40 tests covering webhook status transitions plus PaymentCompletedEvent and PaymentRecordedEvent publication |
| `dotnet build Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed with 0 warnings and 0 errors |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" --verbosity minimal -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 browser E2E test covering PaymentService webhook completion through AccountingService posted cash/AR journal verification |

## 2026-06-19 Make Studio Agent Tools DFM To Payment E2E Gate

### Scope

- Added `QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob` to cover the signed Make Studio agent tool path through the real Aspire service graph.
- The test signs a customer into QuoteEngine, opens `/projects/new`, uses the trusted ChatbotService-to-QuoteEngine agent tool boundary, registers an initial blocked DFM revision, registers a corrected superseding revision, updates process/material/quantity, estimates, drafts a ProjectService-backed project, creates a formal quote, approves it, creates an OrderService-backed manufacturing order, records checkout context, starts a PaymentService-backed payment handoff artifact, processes the signed payment webhook, and verifies the restored customer-safe order tracking summary.
- The gate exposed and fixed two production-readiness defects: QuoteEngine generated a PaymentService idempotency key longer than PaymentService's `varchar(100)` persistence contract, and PaymentService lacked deterministic Testing-environment provider responses for hosted checkout URLs.
- QuoteEngine payment diagnostics now preserve downstream PaymentService response bodies during agent action confirmation, and PaymentService exposes Development/Testing-only exception detail for payment processing failures.
- Remaining Make Studio deployment gaps: delivery PDF artifact download/content verification and broader generated document PDF semantic content checks remain separate gates.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.PaymentService.Tests\Maliev.PaymentService.Tests.csproj --filter "FullyQualifiedName~PaymentProviderHttpClientRegistrationTests|FullyQualifiedName~PaymentsControllerTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 24 tests covering Testing provider HTTP client registration and payment controller behavior |
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~PaymentServiceClientContractTests|FullyQualifiedName~QuoteAgentEndpointTests|FullyQualifiedName~Payment_initiation" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 105 tests covering PaymentService response mapping, agent payment confirmation, HTTPS callback URLs, and bounded payment idempotency keys |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgentTools_CorrectsDfmCompletesPaymentAndLinksProductionJob" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test covering corrected DFM supersession, ProjectService draft, formal quote, approval, order creation, checkout context, PaymentService payment handoff and signed webhook completion, AccountingService payment journal side effect, paid QuoteEngine order status, restored paid artifact drawer, restored order-tracking summary URL/current manufacturing milestone, Intranet paid-order visibility, ProjectService part linkage to OrderService item plus JobService production job, and customer-visible delivered status |

## 2026-06-19 Make Studio Agent Conversation Restore E2E Gate

### Scope

- Added `QuoteEngine_MakeStudioAgent_RestoresSignedCustomerConversation` to cover the signed Make Studio agent path that was missing from the browser E2E catalog.
- The test signs a customer into the Aspire-hosted QuoteEngine BFF, opens `/projects/new`, waits for the Make Studio browser app, sends a message through `/quote/v1/agent/messages`, and verifies `/quote/v1/agent/sessions/{sessionId}` plus `/quote/v1/agent/sessions/{sessionId}/messages` restore the same authenticated session and ChatbotService-backed user/assistant transcript.
- While validating the catalog, fixed a pre-existing overview gap for the already-present `Intranet_ProjectNew_StlUploadProcessesThroughHiddenInput` method so the executable browser method list and specification table are aligned.
- This closes the immediate executable coverage gap for Make Studio conversation persistence/restoration. It does not yet cover the full DFM -> corrected reupload -> final quote/order/payment -> Intranet handoff path.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_MakeStudioAgent_RestoresSignedCustomerConversation" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test covering signed Make Studio UI startup, QuoteEngine BFF agent message send, authenticated state restore, and ChatbotService-backed message history restore |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 2 traceability tests covering story IDs and executable browser method/spec alignment |

### Follow-Up

- Add the next Make Studio E2E gate for payment completion/webhook return, receipt/accounting side effects, delivery/status tracking, and ProjectService/Intranet handoff visibility.

## 2026-06-05 Geometry Runtime Regression E2E Gate

### Scope

- Re-ran the focused browser geometry runtime device-profile checks for QuoteEngine and Intranet after the DFM/server-miss fallback work.
- Added and ran `GeometryRuntime_ExecutesBrowserWorkerAcrossFrontendsAndDeviceViewports`, which imports the frontend viewer modules in a real browser, feeds direct OBJ bytes to `runLocalAdvisoryGeometry`, verifies Web Worker + WASM execution, checks the local-primary DFM contract, confirms Blazor acceptance callbacks, and observes same-origin telemetry across QuoteEngine and Intranet mobile phone, tablet, and desktop viewport profiles.
- Hardened `GeometryRuntime_ExecutesBrowserWorkerAcrossFrontendsAndDeviceViewports` so browser-local runs fail the gate if they fetch a server DFM fallback endpoint, and so accepted completion telemetry must include positive offloaded input bytes and triangle workload for the BFF `*_dfm_server_avoided_*` metrics.
- The execution gate exposed a cold/transient QuoteEngine manifest fetch failure (`500` followed by `200`); QuoteEngine and Intranet viewer scripts now retry transient manifest failures before falling back to the server path.
- Re-ran the complete Aspire `Tier=E2E` browser gate to verify the broader integrated user journeys still pass.
- Current offload proof gap remains telemetry/load comparison against the GeometryService fallback path.

### Commands And Results

| Command | Result |
|---------|--------|
| `node --test Maliev.Intranet.Tests\Viewer\part-viewer-analysis-tools.test.mjs` | Passed: 36 viewer JavaScript tests, including transient browser-runtime manifest retry coverage |
| `dotnet test Maliev.QuoteEngine.Tests\Maliev.QuoteEngine.Tests.csproj --filter "FullyQualifiedName~QuoteEngineSourceTests.QuotePartViewerJs_has_correct_window_handle_and_no_internal_tools" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 source-contract test confirming the QuoteEngine viewer exposes the browser runtime path and manifest retry helper |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~GeometryRuntime_ExecutesBrowserWorkerAcrossFrontendsAndDeviceViewports" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test executing the local GeometryService-owned worker/WASM DFM path across QuoteEngine and Intranet mobile phone, tablet, and desktop viewport profiles |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~GeometryRuntime_ExecutesBrowserWorkerAcrossFrontendsAndDeviceViewports" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test after adding zero-server-DFM-fallback assertions and positive offloaded workload telemetry assertions |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_GeometryRuntime_LoadsAcrossDeviceViewports|FullyQualifiedName~Intranet_ProjectGeometryRuntime_LoadsAcrossDeviceViewports|FullyQualifiedName~GeometryRuntime_ExecutesBrowserWorkerAcrossFrontendsAndDeviceViewports" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 3 browser E2E tests covering runtime delivery and actual browser worker execution |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_GeometryRuntime_LoadsAcrossDeviceViewports|FullyQualifiedName~Intranet_ProjectGeometryRuntime_LoadsAcrossDeviceViewports" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 2 browser E2E tests across mobile phone, tablet, and desktop viewport profiles |
| `dotnet test Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 51 E2E tests |

## 2026-06-04 Geometry Browser Offload And Full E2E Gate Run

### Scope

- Verified the QuoteEngine browser DFM/upload journeys after the GeometryService browser-primary upload change (`Maliev.GeometryService` `abc0928`).
- Confirmed the affected QuoteEngine browser subset still completes customer-owned upload initiation, browser upload, upload completion, analysis-status polling, DFM visibility, estimating, formal quote creation, order creation, and account history checks.
- Re-ran the complete Aspire `Tier=E2E` gate. The first full run found one transient GeometryService liveness timeout in the system-health browser journey; a focused retry passed, and the final full rerun passed cleanly.
- Added device-profile browser coverage for the QuoteEngine same-origin geometry runtime package path across mobile phone, tablet, and desktop viewports. The test verifies the runtime manifest, immutable worker asset, execution headers, browser-viewable upload policy, and the mobile/tablet parts drawer path.
- Added authenticated Intranet project viewer device-profile coverage across mobile phone, tablet, and desktop viewports. The test creates a deterministic employee project part through the Intranet BFF, opens the project parts view per device class, and verifies the same-origin runtime manifest, immutable worker asset, execution headers, browser-viewable upload policy, and device profile contract.
- Current offload proof gap: server-load reduction still needs telemetry/load comparison against the GeometryService fallback path.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.slnx --filter "FullyQualifiedName~QuoteEngine_AnonymousDemo_EstimatesWithoutFormalArtifacts|FullyQualifiedName~QuoteEngine_PrototypeSignedCustomer_UploadsEstimatesQuotesOrdersAndRecordsHistory" --logger "console;verbosity=normal"` | Passed: selected QuoteEngine DFM/demo and signed customer upload-history browser e2e slice |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 6 QuoteEngine browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Failed: 48 passed, 1 failed. `Intranet_SystemHealth_ShowsIamAndGeometryReadiness` saw GeometryService `/geometry/liveness` time out after 5 seconds. |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~Intranet_SystemHealth_ShowsIamAndGeometryReadiness" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 49 E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_GeometryRuntime_LoadsAcrossDeviceViewports" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test across mobile phone, tablet, and desktop viewport profiles |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 7 QuoteEngine browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 50 E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_ProjectGeometryRuntime_LoadsAcrossDeviceViewports" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 authenticated Intranet browser E2E test across mobile phone, tablet, and desktop viewport profiles |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 51 E2E tests |

### Follow-Up

- Add GeometryService/browser-runtime telemetry comparison so server-load reduction can be measured, not inferred from route coverage.
- Keep the GeometryService system-health liveness timeout visible as a load-sensitivity signal; it passed on focused retry and on the final full rerun, but the first full gate shows this path can still be tight under suite load.

## 2026-05-20 MFG-005 Manufacturing Lifecycle Browser E2E

### Scope

- Added `Intranet_ManufacturingLifecycle_OrderToJobStatusUpdateWithSignalRBroadcast` to `BrowserJourneyGateTests` to close the MFG-005 gap ("Production status updates stay fresh through SignalR or refresh").
- Unblocked `MFG-005` which had been blocked since 2026-05-16 pending an authenticated job status event trigger.
- Added `OrderPaidEventConsumerTests` to `Maliev.JobService.Tests` (4 unit tests) to prove the `OrderPaidEvent → OrderPaidEventConsumer → CreateJobsForPaidOrderAsync` leg of the complete manufacturing event chain. This completed consumer-level coverage for the end-to-end flow: QuoteEngine BFF → OrderService → MassTransit → JobService.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj -p:UseSharedCompilation=false -m:1 /nr:false --verbosity minimal --output B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\bin\E2EBuildCheck` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.JobService\Maliev.JobService.Tests\Maliev.JobService.Tests.csproj -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 119 tests (including 4 new `OrderPaidEventConsumerTests`) |

### Browser Tests Added In This Slice

| Method | Story id(s) | Verifies |
|--------|-------------|----------|
| `Intranet_ManufacturingLifecycle_OrderToJobStatusUpdateWithSignalRBroadcast` | `MFG-005` | Authenticated employee creates a customer and order. OrderService advances the order to Paid, publishing `OrderPaidEvent`. JobService creates a production job via `OrderPaidEventConsumer`. The employee updates the job to InProgress via `PATCH /api/v1/jobs/{id}/status`; the 204 response proves `ProductionHub.SendAsync("JobStatusChanged")` fired. A queue re-read and page reload confirm the status persists and is visible to watching employees. |

### Story Coverage Status After This Slice

| Story id | Coverage status |
|----------|-----------------|
| `MFG-005` | Automated. Order-to-job event chain and SignalR-triggered status update covered. Kanban drag-and-drop UI click coverage and live push to a connected browser client (without reload) deferred. |
| All other ids | Unchanged from the 2026-05-18 catalog refresh. |

## 2026-05-18 Catalog Refresh After Recent Feature Waves

### Scope

- Reviewed the existing 95-story production-gate catalog against the customer-website, QuoteEngine, Intranet, ChatbotService, CommerceService, PdfService, FacilityService, JobService, AccountingService, NotificationService, EmployeeService, and AppHost wiring changes shipped between 2026-05-16 and 2026-05-18.
- Added seven new production-gate stories to capture genuinely new customer- and employee-facing surfaces that the prior catalog did not describe. The traceability assertion in `E2EStoryCatalogTraceabilityTests.E2EStoryRunResults_CoverEveryCatalogStoryId` was updated from 95 to 102 to match the refreshed catalog.
- Added browser coverage for the Web customer-assistant portion of `WEB-014`: anonymous manufacturing chat, account-specific login requirement, secure popup email sign-in, active-page continuation after authentication, authenticated account response, and shared assistant-session transport into QuoteEngine.
- Added executable QuoteEngine shared-window hydration coverage for `WEB-014` through `QuoteEngine_CustomerChatbotWindow_RetainsWebConversation`.
- Verified existing tests still target selectors that survive the recent Intranet topbar collapse, QuoteEngine topbar restructure, and Intranet profile preferences refresh. Where a selector is no longer guaranteed, the run-results entry below flags it as a follow-up.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj -p:UseSharedCompilation=false -m:1 /nr:false --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 2 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~Web_CustomerChatbot_LoginPromptContinuesAuthenticatedConversationInPlace" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Timed out after 15 minutes before a browser E2E verdict; AppHost/test process tree was stopped after timeout. |

### New Production-Gate Stories Added

| Story id | Title | Driver commits | Current status |
|----------|-------|----------------|----------------|
| `WEB-014` | Visitor and customer use Mali website chatbot | `Maliev.Web` `093414f feat: add customer chatbot`, `8cf3e1b feat: persist customer chatbot personalization`, `4f2495e feat: name customer chatbot Mali`, `Maliev.QuoteEngine` `1b0afe6 Add QuoteEngine customer assistant drawer`, `Maliev.Aspire` `0b68a1c fix: wire web bff to chatbot service` | Browser coverage includes Web chatbot/auth handoff and QuoteEngine shared-window hydration. |
| `QUOTE-001`-`QUOTE-026` | (existing QuoteEngine stories) | `Maliev.QuoteEngine` `950d6f3 fix: replace quote engine left rail with topbar`, `67ebfcd fix: use logo progress loader for quote engine startup`, `231896b feat: show quote upload live notifications`, `ba3e6d9 fix: scope quote engine prototype customers` | No catalog change required. Existing automated tests still target `.qe-qsb-total`, `Estimate`, `PDF`, `Quote`, `Create order`, `Demo only`, `Prototype viewer`, `DFM checks`, `No customer data is created`, `Signed-in customer project boundary`, and `Sign in to upload your own files`. These selectors are still present in `QuoteWorkspace.razor`. The new topbar layout does not move them. Confirmed by grep on `Maliev.QuoteEngine.Client/Pages/QuoteWorkspace.razor`. |
| `OPS-004` | Admin manages Intranet sidekick assistant instructions and persisted history | `Maliev.Intranet` `50047ff feat: manage chatbot instructions in intranet`, `54336ba Add sidekick conversation history picker`, `038762a Persist intranet chat history per employee` | Required gap. Existing `Intranet_AiAssistant_ExecutesQuotationOperationAndSuggestedAction` covers quotation operation prompt and suggested action. It does not cover the instructions editor or history picker. |
| `FIN-003` | Employee uses AI accounting extraction, journal entry, and accounting report PDF | `Maliev.Intranet` `0d01b45 Add AI accounting entry extraction`, `b4fa528 Add accounting report PDF export`, `90237d4 Add currency entry fields to accounting journals`, `0e22088 Clarify accounting reconciliation source selection` | Required gap. `FIN-001` and `FIN-002` cover invoice/receipt/payment. They do not cover AI extraction, multi-currency journal entry, reconciliation source selection, or accounting report PDF export. |
| `MFG-006` | Employee uses production schedule operational view with move conflicts and maintenance overlay | `Maliev.Intranet` `9a4e97f Pan schedule to current time on load`, `965c340 Show current time on production schedule`, `6bc9cf4 Handle schedule move conflicts inline`, `d364648 Lock machine in schedule move panel`, `8345289 Show production schedule slot details`, `6f68d65 Show maintenance on production schedule`, `e182d8a Add production schedule queue zoom` | Required gap. `MFG-001` covers basic scheduling. The new operational schedule view is a substantial product surface and needs its own story and browser coverage. |
| `INT-029` | Employee uses customer email template composer and AI-assisted customer extraction | `Maliev.Intranet` `7107220 Add customer email template composer`, `27dc37c Add customer email template metadata` (NotificationService), `5fc5103 Show customer AI extraction progress`, `48e1091 Improve AI address lookup`, `e98e2af Style customer AI extraction dropzone`, `aa82333 Polish customer AI extraction input`, `745b0fd Refine customer AI extraction summary`, `25537f2 Default address recipient from customer` | Required gap. `OPS-003` (`Intranet_CustomerNotification_QueuesDeliveryAndRespectsOptOutPreference`) covers freeform customer email. It does not cover template metadata, AI extraction, AI address lookup, or customer document workspace polish. |
| `COM-005` | Employee edits product Bill of Materials and exports BOM PDF | `Maliev.Intranet` `34bd564 Improve commerce BOM editor details`, `Maliev.CommerceService` `868cb09 Add commerce product BOM items`, `65a18cc Extend commerce BOM details`, `Maliev.PdfService` `c733a58 Add commerce BOM PDF document`, `b48cf26 Expand commerce BOM PDF details` | Required gap. `COM-001`/`COM-002` cover catalog CRUD and publishing. They do not cover BOM persistence or commerce BOM PDF generation. |
| `HR-007` | Employee maintains profile notification and work preferences | `Maliev.Intranet` `4773915 Fix profile preference save and application`, `9a9cc7e Improve profile preference signatures`, `0668312 Fix profile preferences editor`, `b96a3c9 Adjust profile preference row spacing`, `f451b3f Fix profile edit field spacing`, `cf3c274 Increase profile form table spacing` | Required gap. `Intranet_LimitedEmployee_CanUpdateOwnProfileOnly` covers preferred name, personal email, and mobile phone on the profile tab. It does not cover the preferences editor save/apply path or the preference signature. |

### Automated Coverage Added Or Re-Validated

| Surface | Recent change | Existing test impact | Action |
|---------|---------------|----------------------|--------|
| `Maliev.Web.Client/Components/CustomerChatbot.razor` | Customer chatbot now handles anonymous and authenticated account-specific questions. | Added `Web_CustomerChatbot_LoginPromptContinuesAuthenticatedConversationInPlace` for the visible assistant flow, sign-in popup, in-place auth refresh, and Web -> QuoteEngine shared session cookie. | Build and traceability passed; focused browser E2E timed out before verdict and needs a later full-gate rerun. |
| `Maliev.QuoteEngine.Client/Layout/MainLayout.razor` | QuoteEngine now hosts the customer chatbot drawer and hydrates signed Web handoff sessions. | Unskipped `QuoteEngine_CustomerChatbotWindow_RetainsWebConversation` and expanded the Web chatbot E2E to open the QuoteEngine drawer after handoff. | Re-run on next full E2E gate to confirm with the complete Aspire stack. |
| `Maliev.QuoteEngine.Client/Layout/MainLayout.razor` | Left rail replaced with topbar (`950d6f3`); workspace surface still wraps `QuoteWorkspace.razor`. | `QuoteEngine_AnonymousDemo_EstimatesWithoutFormalArtifacts` and `QuoteEngine_PrototypeSignedCustomer_UploadsEstimatesQuotesOrdersAndRecordsHistory` use selectors that live in the workspace, not the nav rail. | No code change. Re-run on next full E2E gate to confirm. |
| `Maliev.Intranet.Client/Layout/TopBar.razor` | Secondary navigation collapses into a `More` MudMenu on small viewports (`0573d3e`). | Tests navigate by URL through `page.GotoAsync`, not by clicking topbar nav. `Intranet_GlobalSearch_ReturnsEmployeeCreatedCustomerAndNavigatesToRecord` targets `.topbar-global-search .global-search-input` and `.global-search-backdrop`, both still present in `GlobalSearchBox.razor` and `TopBar.razor`. | No code change. Re-run on next full E2E gate to confirm. |
| `Maliev.Intranet.Client/Layout/MainLayout.razor.css` | Compact topbar docked at screen bottom on mobile (`c7127ef`). | Existing tests use desktop viewport (1440x1000) set by `NewContextAsync`. The compact-bottom layout only activates on small viewports. | No code change. Add a future mobile-viewport story (candidate for `MFG-002`) once a mobile shop-floor surface is in scope. |
| `Maliev.Intranet.Client/Pages/Hr/Profile.razor` | Profile field spacing, preference save fix, preference row signature (`f451b3f`, `4773915`, `9a9cc7e`, `b96a3c9`). | `Intranet_LimitedEmployee_CanUpdateOwnProfileOnly` targets `Edit profile` button, `Save changes` button, and `.profile-field` rows. All still present. | No code change. The preferences editor is now substantial enough to warrant its own `HR-007` browser test. |

### Browser Tests Added In This Slice

Seven new browser-level E2E tests were added to `BrowserJourneyGateTests.cs`. Each test signs in through the real Aspire-hosted BFF where authentication is required, then drives the new feature through the BFF API (`page.EvaluateAsync` with `fetch + credentials: 'include'`). API-driven assertions were preferred over UI selector clicks because the new UI surfaces are still moving; the test method names below can be promoted to richer click-driven flows in later slices.

| Method | Story id(s) | Verifies |
|--------|-------------|----------|
| `Web_MaliChatbot_AnonymousVisitorReceivesAssistantReplyThroughWebBff` | `WEB-014` | Anonymous Web visitor opens the Mali chatbot toggle, sends a message through `POST /web/v1/chatbot/messages`, and verifies the Web BFF returns an assistant reply with non-empty content. |
| `Intranet_CommerceBom_AddsItemsAndRequestsBomPdf` | `COM-005` | Authenticated employee creates a Commerce product with BOM items through `POST /api/v1/commerce/products`, requests the BOM PDF through `GET /api/v1/commerce/products/{handle}/bom/pdf`, and verifies the BOM editor route renders with the new product title and Export BOM PDF action. |
| `Intranet_ProductionSchedule_ReturnsBoardForActiveMachines` | `MFG-006` (also extends `MFG-001`, `MFG-003`) | Authenticated employee opens `/mfg/production-schedule` and verifies the schedule board (`GET /api/v1/jobs/schedule`), the queue (`GET /api/v1/jobs/queue`), and the stats (`GET /api/v1/jobs/stats`) endpoints all return 200 with a parseable payload. |
| `Intranet_ChatbotInstructions_AdminCanReadCreateAndUpdate` | `OPS-004` (also extends `INT-015`) | Authenticated admin reads chatbot instructions through `GET /api/v1/chat/instructions`, creates a new instruction through `POST /api/v1/chat/instructions`, verifies it appears in the next list call, and verifies `GET /api/v1/chat/conversations` is reachable. |
| `Intranet_AccountingJournalAndReportPdf_PersistEntryAndRequestPdf` | `FIN-003` | Authenticated employee opens `/accounting`, verifies the accounts tree, journal entries listing, accounting periods endpoints, requests a trial-balance report, and verifies the AI processing health endpoint exposes `canInitiateSession`. |
| `Intranet_CustomerEmailTemplatesAndAiExtraction_AreReachable` | `INT-029` (also extends `OPS-003`) | Authenticated employee reads notification templates (`GET /api/v1/notifications/templates`), AI processing health, creates a customer, opens the customer detail page, and verifies the customer email composer surface renders. |
| `Intranet_ProfilePreferences_SavesAndReturnsPreferenceSignature` | `HR-007` (also extends `HR-001`) | Limited employee reads an empty preferences scope, upserts a preference dictionary through `PUT /api/v1/preferences/{scope}`, reads the scope back, and verifies the persisted preference data round-trips with the saved locale value. |

### Out-Of-Scope Gaps Still Pending

- The seven new tests above are API-driven through the real BFF; richer UI-click coverage for each story (BOM editor field-by-field, accounting journal entry form, customer email template selection, schedule slot drag-and-drop, preferences editor save/apply UI) is the recommended next slice once the corresponding UI surfaces stabilize for 48 hours.
- `WEB-014` now has both the user-added shared-session/QuoteEngine handoff coverage and the API-driven anonymous Web chatbot reply test.
- The dedicated Web Google OAuth client (`4960167 fix: use dedicated web google oauth client`) is already covered by `AppHostReferenceTests.AppHost_WebBff_LoadsDedicatedGoogleOAuthConfiguration`. The customer-facing browser sign-in flow (`WEB-006`) remains a partial pending a deterministic test OAuth identity.
- Aspire monitoring container changes (`f139d29 Avoid Aspire monitoring bind mounts`) are covered by `AppHostReferenceTests.AppHost_MonitoringContainers_AvoidHostBindMounts` and `AppHostReferenceTests.AppHost_OpenTelemetryCollector_UsesContainerFileConfig`.

### Story Coverage Status After This Slice

| Story ids | Coverage status |
|-----------|-----------------|
| `WEB-014` | Automated. Web chatbot anonymous reply via Web BFF, anonymous login gate, in-place popup auth continuation, authenticated account response, Web -> QuoteEngine signed handoff cookie, and QuoteEngine same-window rehydration. |
| `OPS-004` | Automated (API-level). Chatbot instructions read/create/list-after-create and chat conversations endpoint covered. Full UI walkthrough (instructions editor form + history picker) deferred. |
| `FIN-003` | Automated (API-level). Accounts tree, journal entries listing, periods, trial-balance report, and AI processing health endpoints covered. Full UI walkthrough (journal entry form + AI accounting extraction dropzone + report PDF download) deferred. |
| `MFG-006` | Automated (API-level). Schedule board, queue, and stats endpoints covered. Full UI walkthrough (current-time marker, queue zoom, slot details, maintenance overlay, move conflicts, machine lock) deferred. |
| `INT-029` | Automated (API-level). Templates listing, AI processing health, customer email composer modal rendered. Full UI walkthrough (template selection + template-driven send + AI customer extraction + AI address lookup) deferred. |
| `COM-005` | Automated. Product create with BOM items, BOM PDF request, and BOM editor route rendering covered. Full UI walkthrough (per-row edit, supplier/drawing linkage, sourcing-time recompute) deferred. |
| `HR-007` | Automated. Preference scope read/upsert/round-trip covered. Full UI walkthrough (preference categories, notification opt-outs per category, preference signature display) deferred. |
| All other ids (`WEB-001`-`WEB-013`, `QUOTE-001`-`QUOTE-026`, `INT-001`-`INT-028`, `COM-001`-`COM-004`, `OPS-001`-`OPS-003`, `FIN-001`-`FIN-002`, `MFG-001`-`MFG-005`, `PROC-001`-`PROC-004`, `HR-001`-`HR-006`, `SEC-001`-`SEC-004`) | Unchanged from the 2026-05-16 catalog refresh. See sections below. |

## 2026-05-16 QuoteEngine Signed Customer And Full E2E Gate Run

### Scope

- Added executable browser coverage for the signed customer QuoteEngine prototype path: email sign-in fallback, customer-owned file upload, live SignalR analysis notification, DFM finding visibility, process/material/quantity configuration, estimate, formal quote creation, order creation, and account quote/order history APIs.
- Added a QuoteEngine customer isolation browser negative journey: Customer A creates quote/order history, Customer B signs in with a separate session, sees isolated quote/order history, and receives `404` when trying to create an order from Customer A's quote id.
- Implemented the missing QuoteEngine client SignalR subscription to `/hubs/quote-notifications` so upload analysis events are visible in the browser and can be asserted by `QUOTE-015`.
- Re-ran the complete Aspire `Tier=E2E` gate after the QuoteEngine changes. The current executable gate is 37 passing tests: 35 browser journey tests plus 2 story-catalog traceability tests.
- This remains partial QuoteEngine production coverage because the signed path still uses `QuoteEnginePrototypeStore`; the production gap is replacing it with ProjectService, UploadService, GeometryService, PricingService, QuotationService, PdfService, OrderService, PaymentService/Omise, DeliveryService, and customer ownership checks.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build B:\maliev\Maliev.QuoteEngine\Maliev.QuoteEngine.slnx --configuration Release --no-restore -p:UseSharedCompilation=false -m:1 /nr:false --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.QuoteEngine\Maliev.QuoteEngine.slnx --configuration Release -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 9 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_PrototypeSignedCustomer_UploadsEstimatesQuotesOrdersAndRecordsHistory" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~QuoteEngine_CustomerIsolation_BlocksCrossCustomerQuoteOrderHistoryAccess" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~QuoteEngine_" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 5 QuoteEngine browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 37 E2E tests |

### Automated Story Coverage Updated

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `QUOTE-001`, `QUOTE-005`, `QUOTE-017` | Signed customer opens QuoteEngine through `/auth/sign-in?returnUrl=/projects/new`, authenticates with the email/password fallback, lands on `/projects/new`, and sees the signed customer project boundary before uploading a customer-owned file. Remaining gaps are Google SSO completion, anonymous-work linking after sign-in, real ProjectService project ownership, and upload retry/invalid-file fixtures. |
| `QUOTE-002`, `QUOTE-003`, `QUOTE-018`, `QUOTE-019`, `QUOTE-020` | Signed customer uploads a STEP file, sees prototype analysis complete, sees the DFM warning `Threaded features should be confirmed`, receives the live SignalR analysis message, selects CNC machining and Aluminum 6061, acknowledges DFM review, changes quantity, estimates, and sees a THB total. Remaining gaps are real UploadService storage, GeometryService analysis/viewer/thumbnail output, PricingService explainable breakdown, multi-file editing, lead-time matrix coverage beyond the existing demo assertions, and stale-result prevention after reupload. |
| `QUOTE-006`, `QUOTE-007`, `QUOTE-009`, `QUOTE-011`, `QUOTE-012`, `QUOTE-022`, `QUOTE-025` | Signed customer requests a formal quote, sees an `MQ-yyyyMMdd-nnnn` quote number, creates an order, sees an `MO-yyyyMMdd-nnnn` order number with `Order received`, and verifies account quote/order APIs contain the new quote and order. Remaining gaps are service-backed QuotationVersion snapshots, real PDF artifact generation/download, quote terms/PO acceptance UI, OrderService/PaymentService/DeliveryService integration, multiple immutable versions on one project, and version comparison. |
| `QUOTE-015` | QuoteEngine client now opens a SignalR connection, joins the uploaded file group before completion, receives `FileAnalysisCompleted`, and renders `Analysis complete for <file>` in a live status region verified by Playwright. Remaining gaps are order/payment/manufacturing notification events, reconnect recovery, notification preferences, and customer-visible notification history. |
| `SEC-001`, `QUOTE-009`, `QUOTE-011`, `QUOTE-012` | Customer A and Customer B sign in through separate QuoteEngine browser contexts. Customer A creates a quote and order. Customer B's account quote/order APIs do not expose Customer A's records, and Customer B receives `404` when trying to create an order from Customer A's quote id. Remaining gaps are real ProjectService/QuotationService/OrderService ownership checks, NDA/document/PDF ownership checks, and employee permission-boundary checks. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.QuoteEngine` | `231896b`, `ba3e6d9` | Added QuoteEngine client SignalR subscription for upload analysis completion, rendered a live status region, and scoped prototype customer profiles, quotes, and orders by signed-in customer. | QuoteEngine Release tests passed 9/9; focused signed QuoteEngine browser E2E passed; focused QuoteEngine customer isolation browser E2E passed. |
| `Maliev.Aspire` | Current QuoteEngine E2E slice | Added the signed customer QuoteEngine browser journey, customer isolation browser journey, and tightened selectors around repeated file/material/quote text. | Focused signed QuoteEngine E2E passed; focused isolation E2E passed; QuoteEngine browser subset passed 5/5; full `Tier=E2E` passed 37/37. |

## 2026-05-16 Project Quote Lifecycle E2E Run

### Scope

- Added executable browser coverage for the Intranet project quote lifecycle around persisted project detail, DFM warning acknowledgement, formal quotation versions, version-specific PDF artifacts, quote acceptance, and duplicate/reorder source linkage.
- Verified the project remains the mutable workspace while QuotationService keeps one quotation with multiple immutable versions and exact PDF artifact references.
- Fixed the cross-service upload/PDF boundaries exposed by the run: Intranet BFF service-discovered resumable uploads, UploadService mock metadata and platform upload policies, ProjectService downstream error diagnostics, PdfService generated-PDF upload through UploadService proxy, and Aspire test-admin seeder readiness.
- Kept this as partial coverage for the ProjectNew/new-project editor stories because the current automated path creates the project through the BFF and verifies persisted project detail/revision behavior rather than driving every ProjectNew editor control.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_ProjectQuoteLifecycle_GeneratesVersionsPdfAcceptanceAndDuplicate" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.PdfService\Maliev.PdfService.Tests\Maliev.PdfService.Tests.csproj --filter "FullyQualifiedName~UploadServiceClientTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 3 tests |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~ProjectServiceClientCreateTests\|FullyQualifiedName~UploadsControllerResumableTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 19 tests |
| `dotnet test B:\maliev\Maliev.UploadService\Maliev.UploadService.Tests\Maliev.UploadService.Tests.csproj --filter "FullyQualifiedName~UploadsControllerTests\|FullyQualifiedName~UploadsControllerEdgeCaseTests\|FullyQualifiedName~IAMResourceScopedTests\|FullyQualifiedName~AuthorizationPolicyServiceTests\|FullyQualifiedName~EndToEndTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 51 tests |
| `dotnet test B:\maliev\Maliev.ProjectService\Maliev.ProjectService.Tests\Maliev.ProjectService.Tests.csproj --filter "FullyQualifiedName~QuotationServiceClientTests\|FullyQualifiedName~ProjectsControllerTests.GenerateQuotation" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 4 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~AppHostReferenceTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 12 tests |

### Automated Story Coverage Updated

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `INT-005`, `INT-023`, `INT-028` | Authenticated Intranet employee signs in through the real BFF/AuthService/IAM path, creates a customer/project context, uploads a STEP file through Intranet BFF to UploadService, adds a configured project part, confirms price, generates quotation version 1, changes quantity, generates quotation version 2 on the same quotation id, verifies immutable snapshot JSON/hash/change summaries, verifies PdfService generated distinct per-version quotation PDF artifacts, validates the downloadable PDF bytes for version 1, version 2, and the latest-PDF endpoint, verifies stale version 1 acceptance is rejected after version 2 becomes current, opens the project quote tab, and verifies revision history displays version 1, version 2, and the current marker. Remaining gaps are full ProjectNew editor UI creation, side-by-side diff UI, semantic PDF pixel/text inspection, and non-current version permission UI checks. |
| `INT-006`, `INT-007` | Same browser run accepts the current quotation into project accepted state, duplicates the accepted project, verifies the duplicate has a distinct id, source project id/number linkage, copied file/configuration context, and a visible `Duplicated from` marker. Remaining gaps are downstream OrderService/JobService creation checks after acceptance, accepted-project mutation guard UI, customer-facing status, and reorder policy checks from QuoteEngine. |
| `INT-017`, `INT-018`, `INT-019`, `INT-021`, `INT-022`, `INT-024`, `INT-025`, `INT-027` | Same run covers a service-backed project upload path, persisted file/artifact reference, DFM warning state and acknowledgement, quote-critical quantity/material/process configuration, deterministic confirmed pricing, drawing and supplementary attachment metadata, automatic quotation PDF generation, and failure evidence from upload/PDF boundary defects. Remaining gaps are true multi-file ProjectNew UI interaction, GeometryService-rendered viewer/thumbnail pixel checks, explicit retry/fault-injection controls, temporary-upload migration from unsaved ProjectNew sessions, and SignalR reconnect behavior. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.Aspire` | Current project quote lifecycle slice | Added `Intranet_ProjectQuoteLifecycle_GeneratesVersionsPdfAcceptanceAndDuplicate`, made UploadService use mock storage in Aspire Testing, and changed local test seeder dependencies to wait for completion so IAM/Auth wildcard permissions are ready before browser sign-in. | Focused lifecycle E2E passed; AppHost reference tests passed 12/12. |
| `Maliev.Intranet` | Current project quote lifecycle slice | Fixed Intranet BFF UploadService clients to use Aspire service discovery for resumable upload initiation/resume and preserve `HasDfmWarnings` through add/update/read ProjectService DTOs. | Focused Intranet BFF tests passed 19/19; lifecycle E2E rendered DFM warning state and completed upload. |
| `Maliev.UploadService` | Current project quote lifecycle slice | Made mock storage retain actual uploaded object metadata for resumable uploads and seeded deterministic platform upload policies for `Intranet` and `PdfService` service-owned paths. | Focused UploadService tests passed 51/51; lifecycle E2E no longer failed on forbidden initiations or mock metadata size mismatch. |
| `Maliev.ProjectService` | Current project quote lifecycle slice | Surfaced QuotationService downstream response details from the project quotation client/controller, making generation failures diagnosable at the correct service boundary. | Focused ProjectService tests passed 4/4; E2E diagnostics confirmed ProjectService and QuotationService succeeded before the PdfService upload fix. |
| `Maliev.PdfService` | Current project quote lifecycle slice | Uploaded generated PDFs through UploadService's resumable proxy route instead of trying to PUT to the returned mock/GCS session URI from inside PdfService. | Focused PdfService upload-client tests passed 3/3; lifecycle E2E generated and attached the quotation PDF artifact. |

## 2026-05-16 Customer Notification E2E Run

### Scope

- Added executable browser coverage for the implemented `OPS-003` customer notification path from Intranet customer detail.
- Verified customer-created notification preference provisioning, employee-triggered customer email dispatch, NotificationService delivery logging, provider/simulated provider message visibility, and opt-out skip behavior.
- Fixed the NotificationService customer-binding boundary so customer-created email/SMS bindings are encrypted before the router decrypts them.
- Fixed the Intranet notification BFF boundary so delivery logs preserve provider message ids and skip/error reasons.
- Kept this as partial `OPS-003` coverage; the full production gate still needs a customer/employee notification center UI, read/unread state, broader event-trigger mappings, external provider sandbox delivery, and permission-negative notification checks.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test B:\maliev\Maliev.NotificationService\Maliev.NotificationService.Tests\Maliev.NotificationService.Tests.csproj --filter "FullyQualifiedName~CustomerEventConsumerTests\|FullyQualifiedName~NotificationEventsApiTests\|FullyQualifiedName~PreferencesApiTests.GetPreferences_OtherUserWithWildcardPermission_ReturnsPreferences\|FullyQualifiedName~PreferencesApiTests.UpdatePreferences_OtherUserWithWildcardPermission_ReturnsUpdatedPreferences" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 13 tests |
| `dotnet build B:\maliev\Maliev.NotificationService\Maliev.NotificationService.slnx --configuration Release --no-restore -p:UseSharedCompilation=false -m:1 /nr:false --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~NotificationServiceClientTests\|FullyQualifiedName~CustomersControllerTests.SendEmail_ShouldPublishNotification_WhenCustomerHasPrincipal\|FullyQualifiedName~CustomersControllerTests.SendEmail_ShouldReturnBadRequest_WhenCustomerHasNoPrincipal" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 6 tests |
| `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release --no-restore -p:UseSharedCompilation=false -m:1 /nr:false --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_CustomerNotification_QueuesDeliveryAndRespectsOptOutPreference" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 2 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~BrowserJourneyGateTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 32 browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_EmployeeCreatedCustomer_CanBeOpenedAndSelectedInProjectWorkspace" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test after unique customer-name fix |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 34 E2E tests |

### Automated Story Coverage Updated

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `OPS-003` | Authenticated Intranet employee signs in through the real BFF/AuthService/IAM path, creates a corporate customer through CustomerService, waits for NotificationService to provision customer preferences and encrypted channel bindings from the customer-created event, opens customer detail, sends an email notification through the customer action modal, verifies NotificationService delivery logs show the customer principal, `email` channel, `delivered` status, and provider/simulated provider message id, updates notification preferences to opt out of a specific category, sends a second notification in that category, and verifies the log records a failed/skipped delivery with an opted-out reason. Remaining gaps are customer/employee notification center UI, read/unread persistence, push/live notification surfaces, external provider sandbox verification, broader automatic domain events, and low-permission negative checks. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.NotificationService` | Current notification E2E slice | Added an explicit `/notification/v1/events` dispatch endpoint that reuses the `NotificationEventConsumer` processing path, fixed wildcard permission checks for preference reads/updates, encrypted customer-created and customer-updated channel bindings, and added focused integration coverage. | Focused NotificationService tests passed 13/13; Release build passed with 0 warnings. |
| `Maliev.Intranet` | Current notification E2E slice | Sent customer email actions through NotificationService dispatch, exposed provider message ids and skip/error reasons in notification delivery-log DTOs, and added client/controller contract tests. | Focused Intranet tests passed 6/6; Release build passed with 0 warnings. |
| `Maliev.Aspire` | Current notification E2E slice | Added `Intranet_CustomerNotification_QueuesDeliveryAndRespectsOptOutPreference`, hardened browser sign-in readiness by waiting for AuthService-issued JWT permissions before form login, and made the Project workspace customer-picker E2E repeatable with unique customer names. | Focused notification E2E passed 1/1; full `Tier=E2E` passed 34/34. |

## 2026-05-16 Intranet AI Assistant E2E Run

### Scope

- Added executable browser coverage for the core `INT-015` assistant/tool-callback path.
- Fixed the Intranet assistant suggested-action boundary so a ChatbotService action with quotation context keeps that context when the employee clicks the action button.
- Fixed ChatbotService Aspire `Testing` runtime so assistant prompts are deterministic without a Gemini secret and Redis-backed session locks are available when AppHost wires Redis.
- Kept this as partial `INT-015` coverage; the full production gate still needs broader assistant tool coverage, production LLM behavior, full audit review surfaces, and permission-negative checks.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~ChatDrawerTests" --logger "console;verbosity=minimal"` | Passed: 3 tests |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~ChatControllerTests" --logger "console;verbosity=minimal"` | Passed: 3 tests |
| `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release --no-restore --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.ChatbotService\Maliev.ChatbotService.Tests\Maliev.ChatbotService.Tests.csproj --filter "FullyQualifiedName~MessagesApiTests" --logger "console;verbosity=minimal"` | Passed: 7 tests |
| `dotnet build B:\maliev\Maliev.ChatbotService\Maliev.ChatbotService.slnx --configuration Release --no-restore --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_AiAssistant_ExecutesQuotationOperationAndSuggestedAction" --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests" --logger "console;verbosity=minimal"` | Passed: 2 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~BrowserJourneyGateTests" --logger "console;verbosity=minimal"` | Passed: 31 browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" --logger "console;verbosity=minimal"` | Passed: 33 E2E tests |
| `dotnet build B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --configuration Release --no-restore --verbosity minimal` | Passed: 0 warnings, 0 errors |

### Automated Story Coverage Updated

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `INT-015` | Authenticated Intranet employee signs in through the real BFF/AuthService/IAM path, verifies `/api/v1/aiprocessing/health`, opens the assistant drawer from the employee shell, initiates a real ChatbotService session, sends a prompt for the seeded quotation number, receives a quotation response containing customer context and a `Send Reminder` suggested action, clicks that suggested action, and verifies the reminder follow-up keeps the quotation id and succeeds. Remaining gaps are production Gemini/tool-calling behavior beyond the deterministic Aspire client, complete supported-tool catalog, explicit user confirmation policy for each mutation, assistant audit-log review UI, production callback allow-list configuration, and permission-negative tests against restricted records/actions. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.Intranet` | `8762951` | Preserved assistant suggested-action data when rendering action buttons, so clicking `Send Reminder` carries the quotation id instead of sending only the display text. | Focused `ChatDrawerTests` passed 3/3; focused Aspire assistant browser journey passed. |
| `Maliev.Intranet` | `40f373d` | Surfaced downstream ChatbotService failure details from the BFF in non-production, keeping production responses generic while making Aspire E2E failures actionable. | Focused `ChatControllerTests` passed 3/3; Intranet Release build passed with 0 warnings. |
| `Maliev.ChatbotService` | `7f0aef8` | Added deterministic `TestingGeminiClient` and non-production loopback callback support so Aspire E2E does not depend on external Gemini secrets and can call the Intranet BFF callback endpoint. | Focused `MessagesApiTests` passed 7/7; ChatbotService Release build passed with 0 warnings. |
| `Maliev.ChatbotService` | `0bdcb18` | Registered a Redis `IConnectionMultiplexer` in Aspire `Testing` when a Redis connection string is present, fixing ChatbotService session-lock DI failure during browser E2E. | Focused Aspire assistant browser journey passed 1/1. |
| `Maliev.Aspire` | Current assistant E2E slice | Added `Intranet_AiAssistant_ExecutesQuotationOperationAndSuggestedAction` to the browser production-gate suite. | Focused browser E2E passed 1/1. |

## 2026-05-16 Delivery Note E2E Run

### Scope

- Added executable browser coverage for `INT-009` through the authenticated Intranet logistics workflow.
- Fixed the Intranet delivery-note UI/BFF boundary so employees can create DeliveryService-backed delivery notes from an order context, update shipment status, request the delivery PDF queue, and verify persisted state.
- Kept this as the logistics portion of `INT-009`; the later Make Studio delivery-status gate covers customer-visible QuoteEngine order status, while delivery evidence upload and artifact download checks remain separate gaps.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~DeliveryServiceClientTests\|FullyQualifiedName~DtoSerializationTests" --logger "console;verbosity=minimal"` | Passed: 17 tests |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --logger "console;verbosity=minimal"` | Passed: 602 tests, 7 skipped |
| `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release --no-restore --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~ProgramHttpClientConfigurationTests\|FullyQualifiedName~DeliveryServiceClientTests" --logger "console;verbosity=minimal"` | Passed: 9 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_DeliveryNotes_CreatesTracksPdfAndDeliveredStatus" --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests" --logger "console;verbosity=minimal"` | Passed: 2 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~BrowserJourneyGateTests" --logger "console;verbosity=minimal"` | Passed: 30 browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "Tier=E2E" --logger "console;verbosity=minimal"` | Passed: 32 E2E tests |

### Automated Story Coverage Updated

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `INT-009` | Authenticated Intranet employee signs in through the real BFF/AuthService/IAM path, creates a corporate customer, creates a real OrderService source order, opens `/finance/delivery-notes/new`, creates a DeliveryService delivery note with carrier, tracking, destination, contact, shipping cost, and item quantities, verifies the generated `DN-yyyy-nnnnnn` detail route, marks the shipment `InTransit`, marks it `Delivered` with receiver and actual delivery time, uploads proof-of-delivery evidence, downloads the proof bytes through Intranet, requests the delivery-note PDF queue, verifies persisted DeliveryService/BFF detail JSON, and verifies the delivered note appears in the list. The Make Studio delivery-status gate also verifies customer-visible QuoteEngine delivered status from a paid order. Remaining gaps are delivery PDF artifact download/content verification, notification delivery, partial delivery quantities, and low-permission negative checks. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.Intranet` | `6c42a1a` | Replaced placeholder DeliveryService client methods with real service-contract calls, added delivery-note list/create/detail pages, exposed finance navigation, and added DTO/client regression tests. | Full Intranet tests passed 602 with 7 skipped; Release build passed with 0 warnings; focused Aspire delivery browser journey passed after the interface registration fix. |
| `Maliev.Intranet` | `958ee58` | Registered `IDeliveryServiceClient` against `DeliveryServiceClient`, fixing the BFF 500 when `DeliveryNotesController` was first activated by the browser create request. | `ProgramHttpClientConfigurationTests` and `DeliveryServiceClientTests` passed 9/9; focused Aspire delivery browser journey passed. |
| `Maliev.Aspire` | Current delivery E2E slice | Added `Intranet_DeliveryNotes_CreatesTracksPdfAndDeliveredStatus` to the browser production-gate suite. | Focused browser E2E passed 1/1. |

## 2026-05-16 Finance Payment And Receipt E2E Run

### Scope

- Extended the authenticated Intranet finance browser journey from invoice creation/finalization into employee-recorded payment allocation and receipt creation.
- Fixed the service boundary so InvoiceService returns paid and outstanding invoice balances after payment allocation.
- Fixed the Intranet BFF/UI boundary so staff can record an invoice payment through InvoiceService and create a receipt through ReceiptService from the invoice detail page.

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet test B:\maliev\Maliev.InvoiceService\Maliev.InvoiceService.slnx --filter "FullyQualifiedName~PaymentLinkingTests" --logger "console;verbosity=minimal"` | Passed: 3 tests |
| `dotnet test B:\maliev\Maliev.InvoiceService\Maliev.InvoiceService.slnx --logger "console;verbosity=minimal"` | Passed: 109 tests |
| `dotnet build B:\maliev\Maliev.InvoiceService\Maliev.InvoiceService.slnx --configuration Release --no-restore --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --filter "FullyQualifiedName~InvoiceServiceClientTests\|FullyQualifiedName~ReceiptServiceClientTests\|FullyQualifiedName~DtoSerializationTests" --logger "console;verbosity=minimal"` | Passed: 22 tests |
| `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj --logger "console;verbosity=minimal"` | Passed: 598 tests, 7 skipped |
| `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release --no-restore --verbosity minimal` | Passed: 0 warnings, 0 errors |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_FinanceInvoice_CreatesAttachesAndFinalizesCustomerInvoice" --logger "console;verbosity=minimal"` | Passed: 1 browser E2E test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~BrowserJourneyGateTests" --logger "console;verbosity=minimal"` | Passed: 29 browser E2E tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "Tier=E2E" --logger "console;verbosity=minimal"` | Passed: 31 E2E tests |

### Automated Story Coverage Updated

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `FIN-001`, `FIN-002`, `INT-008` | Authenticated Intranet employee creates a corporate customer, creates a customer-backed invoice with PO evidence, verifies line/tax/total values, registers the PO attachment, finalizes the invoice, records a full bank-transfer payment from invoice detail, verifies InvoiceService/BFF paid amount and zero balance, creates a ReceiptService receipt, verifies the receipt row in the UI, and verifies the receipt through `/api/v1/receipts`. Remaining gaps are billing-note creation, invoice/receipt PDF artifact download verification, accounting journal/export effects, partial payment UI, and Omise sandbox completion. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.InvoiceService` | `ee2e608` | Exposed `PaidAmount` and `OutstandingBalance` on invoice responses and included payment allocations when mapping invoice detail/list/search responses. | Full InvoiceService tests passed 109/109; Release build passed with 0 warnings; focused Aspire finance browser journey passed. |
| `Maliev.Intranet` | `ce7913d` | Added the Intranet invoice detail payment/receipt workflow, BFF endpoint for InvoiceService payment creation/allocation, ReceiptService request/response mapping, and client/DTO regression tests. | Full Intranet tests passed 598 with 7 skipped; Release build passed with 0 warnings; focused Aspire finance browser journey passed. |

## 2026-05-15 Automated Playwright E2E Bootstrap Run

### Scope

- Added executable Playwright browser tests in `Maliev.Aspire.Tests/E2E/BrowserJourneyGateTests.cs`.
- Added catalog traceability checks in `Maliev.Aspire.Tests/E2E/E2EStoryCatalogTraceabilityTests.cs`.
- Installed the matching .NET Playwright Chromium runtime with:
  - `pwsh B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\bin\Debug\net10.0\playwright.ps1 install chromium`

### Commands And Results

| Command | Result |
|---------|--------|
| `dotnet build B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj -p:UseSharedCompilation=false -m:1 --no-restore` | Passed |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~E2EStoryCatalogTraceabilityTests"` | Passed: 2 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~Intranet_LimitedEmployee_CannotAccessRestrictedModuleApis"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~Intranet_GlobalSearch_ReturnsEmployeeCreatedCustomerAndNavigatesToRecord"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~Intranet_LimitedEmployee_CanUpdateOwnProfileOnly"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_CustomerOnboardingUi_CreatesCompanyAddressesAndInternalNote" -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_SystemHealth_ShowsIamAndGeometryReadiness" -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~MassTransitExtensionOptionsTests" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_IamAdmin_CreatesUserAssignsRoleAndViewsPermissionMatrix" -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_FinanceInvoice_CreatesAttachesAndFinalizesCustomerInvoice" -p:UseSharedCompilation=false -m:1 /nr:false` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_ProcurementPurchaseOrder_CreatesAttachesAndCancelsSupplierPo" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_ProcurementReceiving_ApprovesSendsAndReceivesSupplierPo" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_LeaveRequest_LimitedEmployeeSubmitsAndManagerApproves" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_DashboardBusinessOverview_SurfacesPendingLeaveApproval" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_MaterialMasterData_CreatesAndEditsMaterial" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_EquipmentMasterData_RegistersNotesAndMaintenance" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_CustomerDetail_EditsProfilePaymentTermsAndAddress" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --filter "FullyQualifiedName~Intranet_SupplierProfile_CreatesAndEditsSupplier" -p:UseSharedCompilation=false -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 1 test |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~BrowserJourneyGateTests" -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 29 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "Tier=E2E" -m:1 /nr:false --logger "console;verbosity=minimal"` | Passed: 31 tests |
| `dotnet test B:\maliev\Maliev.Aspire\Maliev.Aspire.Tests\Maliev.Aspire.Tests.csproj --no-build --filter "FullyQualifiedName~AspireTestAdminSeedOptionsTests\|FullyQualifiedName~AspireTestAdminIamSeederSourceTests"` | Passed: 7 tests |
| `dotnet test B:\maliev\Maliev.CommerceService\Maliev.CommerceService.Tests\Maliev.CommerceService.Tests.csproj -p:UseSharedCompilation=false -m:1` | Passed: 9 tests |
| `dotnet test B:\maliev\Maliev.Web\Maliev.Web.Tests\Maliev.Web.Tests.csproj -p:UseSharedCompilation=false -m:1` | Passed: 69 tests |

### Automated Story Coverage Added

| Story ids | Automated browser coverage |
|-----------|----------------------------|
| `WEB-001`, `WEB-010`, `WEB-011`, `WEB-012`, `WEB-013` | Web home, services, shop, cookie consent, `/quote`, local QuoteEngine demo handoff, and all public trust/policy/support routes: `/about`, `/materials`, `/industries`, `/case-studies`, `/blog`, `/faq`, `/contact`, `/shipping-returns`, `/privacy`, `/terms`, `/cookie-policy`, `/refund-policy`, and `/warranty-policy`. |
| `WEB-002`, `WEB-012` | Web contact/support route renders the contact form, submits a real inquiry through the Web BFF to ContactService, and shows the customer success state. Employee-side inquiry processing still requires authenticated Intranet coverage. |
| `WEB-003`, `WEB-005`, `WEB-009` | Email/password registration creates a customer session through AuthService/CustomerService, lands on the protected account page, opens profile, creates an address, signs out, and confirms protected account access redirects back to sign-in. Email verification remains a required product gap. |
| `SEC-001`, `SEC-003`, `WEB-009` | Two independently registered Web customers create isolated sessions. Customer A creates an account address; Customer B attempts to mutate that address through the Web BFF and receives the not-found ownership boundary. Clearing Customer B's session and opening `/account/addresses` redirects to sign-in with the original return URL preserved. |
| `WEB-006`, `WEB-007` | Google sign-in and password reset entry points render through browser routes. Completion remains blocked by local OAuth/test-token and local mail/reset-token fixtures. |
| `WEB-008`, `COM-001`, `COM-002`, `COM-003`, `COM-004` | Authenticated Intranet employee creates a draft Commerce product through `/api/v1/commerce/products`, verifies the draft is visible to employees but hidden from Web, publishes it, verifies Web shop/product detail/cart add/quantity edit/checkout sign-in redirect, registers a customer, verifies the signed customer session, creates a CommerceService-backed checkout draft, archives the product, and verifies the public product URL returns the customer-facing not-found state. Payment completion remains a separate Omise/product gap. |
| `QUOTE-002`, `QUOTE-003`, `QUOTE-004`, `QUOTE-018`, `QUOTE-019`, `QUOTE-020`, `QUOTE-024` | QuoteEngine anonymous demo loads the MALIEV sample file, shows the prototype viewer and DFM checks, switches to FDM, recalculates standard price, recalculates express lead-time price, recalculates after quantity edit, keeps formal PDF disabled, and states that no customer data is created. |
| `QUOTE-001`, `QUOTE-005`, `QUOTE-017` | QuoteEngine real project route shows the sign-in/upload gate before customer-owned upload. Real upload retry remains blocked until authenticated project mode is service-backed. |
| `QUOTE-008`, `QUOTE-009`, `QUOTE-010`, `QUOTE-011`, `QUOTE-012`, `QUOTE-013`, `QUOTE-014` | QuoteEngine prototype-backed profile, order tracking, NDA, and supporting document portal routes render. Real persistence, upload, ownership, and employee visibility checks remain blocked until service-backed customer projects replace prototype storage. |
| `INT-001`, `SEC-002`, `SEC-003` | Anonymous Intranet direct access to `/projects/new` redirects to login and preserves return URL. |
| `INT-001`, `SEC-002` | Aspire seeds a dedicated limited employee with only `auth.sessions.read` and `employee.profiles.read`. Browser E2E signs in through the real Intranet BFF/AuthService/IAM path, verifies the employee has no wildcard permission, confirms `/api/v1/employees/me/profile` is allowed, opens `/iam` by direct URL without a login loop, and verifies `/api/v1/iam/users`, `/api/v1/iam/roles`, `/api/v1/employees`, and `/api/v1/search` return `403`. |
| `INT-002`, `INT-003`, `INT-010`, `INT-011`, `INT-012`, `INT-013`, `INT-014`, `COM-001`, `FIN-001`, `FIN-002`, `PROC-002`, `PROC-003`, `MFG-001`, `MFG-003`, `MFG-004`, `HR-001`, `HR-002`, `OPS-001`, `SEC-002` | Authenticated Intranet automation employee signs in through the real BFF/AuthService/IAM path and reaches dashboard, search, admin, IAM user, customer, project, commerce catalog, accounting, purchasing, manufacturing material/equipment/schedule, and HR profile module routes without login loops, startup failures, or route-level permission denial. |
| `INT-012` | Authenticated Intranet employee opens `/mfg/materials`, creates new material master data through the browser UI, verifies the BFF maps Intranet `SKU`/`UnitPrice`/`QuantityOnHand` to MaterialService `Code`/`PricePerUnit`/`StockLevel`, lands on material detail, edits name/code/description/unit price/stock, and verifies the persisted detail through `/api/v1/materials/{id}`. Remaining material gaps are process/color/post-processing assignment UI, supplier linkage, bulk import/export, inventory lot movement, and low-permission action checks. |
| `INT-013` | Authenticated Intranet employee opens `/mfg/equipment`, registers CNC equipment through the browser UI, verifies FacilityService generated the asset code and persisted the equipment detail, opens `/mfg/equipment/{id}`, appends an operating note, appends a maintenance record with vendor/cost/type, verifies the UI refreshes, and reads equipment/detail/note/maintenance state back through BFF endpoints. Remaining facility gaps are equipment update/status-transition UI, attachment management, low-permission action checks, job availability effects, maintenance author identity, and scheduled work-center assignment. |
| `FIN-001`, `FIN-002`, `INT-008` | Authenticated Intranet employee creates a corporate customer through the BFF/CustomerService boundary, opens `/accounting/new`, selects that customer, verifies billing identity data and THB currency data, creates a draft invoice with customer PO evidence, verifies persisted line/tax/total values, registers the attachment through UploadService and InvoiceService, finalizes the invoice, verifies invoice number and finalized-by state, records a full invoice payment, verifies paid amount and zero balance, creates a ReceiptService receipt, and verifies the receipt through UI and BFF data. Billing-note creation, invoice/receipt PDF artifact checks, accounting journal/export effects, partial payment UI, and Omise completion remain separate finance gaps. |
| `INT-010`, `INT-011`, `SEC-002` | Authenticated Intranet admin opens the IAM module, loads role data from the BFF/IAM boundary, verifies `roles.aspire.limited` contains profile read/update permissions, opens the role detail page and permission matrix, creates a new IAM user through `/iam/users/new`, assigns the initial role, verifies the new principal through `/api/v1/iam/users`, opens `/iam/users/{principalId}`, and verifies the assigned role binding. Session/login for the invited user remains a separate product gap because invite email/password activation is not implemented end to end. |
| `INT-002`, `INT-003` | Authenticated Intranet employee opens `/sales/customers/new`, verifies BFF country reference data includes Thailand with `iso2 = TH`, enters customer profile fields, company fields, company billing address, customer billing and shipping addresses, and an internal note, submits the form, lands on `/customers/{id}`, verifies CustomerService detail JSON contains the customer, company, addresses, company billing address, and note data, then verifies the rendered Addresses and Notes tabs show the saved values. Remaining INT-002 gaps are registry lookup, address autocomplete, document upload, and NDA/customer-document separation. Remaining INT-003 gaps are document propagation and related-workflow propagation beyond customer list/search. |
| `INT-003` | Authenticated Intranet employee creates a corporate customer, opens `/customers/{id}`, edits full name, phone, lifecycle status, payment terms, and shipping address through the rendered customer detail UI, verifies the Intranet BFF and CustomerService detail JSON reflect the profile/payment/status/address mutations, verifies the audit trail shows profile and status changes, and verifies the customer list/search view reflects the edited record. Remaining INT-003 gaps are customer/NDA document upload and related-workflow propagation beyond the customer list. |
| `INT-003`, `INT-004` | Authenticated Intranet automation employee creates a new customer through `/api/v1/customers/create-basic`, finds the customer in `/customers`, opens the customer detail page, opens `/sales/projects/new`, searches the project customer picker, selects that customer, and verifies the quote workspace bill-to, upload dropzone, and quote total surfaces. |
| `INT-009` | Authenticated Intranet employee creates a DeliveryService-backed delivery note from a real OrderService order, verifies carrier/tracking/customer/item state, transitions status to `InTransit` and `Delivered`, uploads and downloads proof-of-delivery evidence through Intranet, requests the delivery PDF queue, reads persisted BFF detail JSON, and verifies the delivered note is listed. The Make Studio delivery-status gate adds customer-visible QuoteEngine delivered status from a paid order. Remaining gaps are delivery PDF artifact download/content verification, notifications, partial deliveries, and low-permission negative checks. |
| `OPS-001`, `INT-003` | Authenticated Intranet employee creates a customer, waits for CustomerService's search upsert event to reach SearchService, verifies `/api/v1/search` returns a customer result with `/customers` navigation, uses the top-bar global search UI, verifies click-away closes the result panel, clicks the result, and lands on the customer list with the created customer visible. |
| `OPS-002`, `INT-014` | Authenticated Intranet employee opens `/admin/system-health`, verifies the System Health dashboard renders auto-refresh information, fetches `/api/v1/system-health` through the browser session, waits until AuthService, IAMService, and GeometryService are healthy, verifies their service-owned `/liveness` and `/readiness` paths, verifies the history API includes IAMService and GeometryService, and clicks Refresh to confirm the live probe grid remains available. Longer multi-hour auto-refresh validation remains a separate endurance gate. |
| `HR-001`, `INT-001`, `SEC-002` | Limited employee signs in with only session/profile permissions, opens `/hr/profile`, updates preferred name, personal email, and mobile phone through the browser UI, reloads to verify persistence, and remains denied from broad `/api/v1/employees` access. |
| `HR-002` | Limited employee signs into `/hr/leave` with balance/request read and request-create permissions, submits an Annual leave request through the browser UI, LeaveService creates the leave request and pending approval assigned to the seeded manager, the manager signs in and approves it from Pending approvals, the manager approval list clears, and the employee signs back in and sees the same request as `Approved`. Notification delivery and calendar/status integrations remain product gaps. |
| `INT-014`, `HR-002` | Limited employee creates a pending leave request, the manager opens the Dashboard, verifies `/api/v1/dashboard` widgets are sourced from PaymentService, OrderService, QuotationService, and EmployeeService, verifies `/api/v1/dashboard/action-items` includes the pending leave approval, clicks the dashboard action item into `/hr/leave`, sees the matching approval row, and approves it. Remaining dashboard gaps are seeded commercial/order/procurement work queues, chart interactions, per-role dashboard customization, and long-running auto-refresh under real operational volume. |
| `PROC-002` | Authenticated Intranet employee opens `/purchasing/suppliers`, creates a supplier profile through the rendered UI with tax id, contact, address, and capabilities, lands on `/purchasing/suppliers/{id}`, verifies SupplierService row-version data, edits supplier name/address/city/postal code/capabilities, verifies the Intranet BFF and SupplierService detail JSON reflect the update, and verifies the supplier list shows the edited profile. Remaining supplier gaps are supplier documents, supplier payment terms, duplicate/tax-id negative validation, supplier audit display, and low-permission action checks. |
| `PROC-002`, `PROC-003` | Authenticated Intranet employee creates a supplier through the BFF/SupplierService boundary, creates a customer and source order fixture, opens `/purchasing/new`, selects supplier/source order/order item/currency, creates a purchase order through PurchaseOrderService, verifies persisted PO number/status/currency/customer PO/source order JSON, uploads customer PO evidence through the PO detail page, verifies the attachment record, cancels the PO with a reason, and verifies the detail API and UI show `Cancelled`. Supplier audit display, PO approval, and downstream receiving/accounting impact remain separate procurement gaps. |
| `PROC-004` | Authenticated Intranet employee creates a supplier/source-order PO fixture through the real BFF and services, opens the PO detail page, approves the PO, sends it to the supplier, receives goods, verifies UI status progression `Pending` -> `Approved` -> `Ordered` -> `Delivered`, verifies persisted PO/source-order/customer-PO/currency/item data, and confirms receive/cancel actions are disabled after delivery. Inventory stock movement, supplier invoice, and accounting journal effects remain product gaps because no current consumer translates `PurchaseOrderReceivedEvent` into those downstream records. |

### Fixes Made During E2E Execution

| Repo | Commit | Fix | Evidence |
|------|--------|-----|----------|
| `Maliev.AuthService` | `d4d6d26` | Preserved Authorization headers for employee/customer validation clients by using HTTPS-first Aspire service discovery. | `dotnet test B:\maliev\Maliev.AuthService\Maliev.AuthService.slnx --filter "FullyQualifiedName~AuthenticationServiceTests" --no-restore` passed 25 tests. |
| `Maliev.Intranet` | `143057c` | Enabled hosted Blazor static web assets in the Intranet BFF so Aspire browser runs can load CSS/images/Blazor framework assets. | `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.Bff\Maliev.Intranet.Bff.csproj -p:UseSharedCompilation=false -m:1 --no-restore` passed; `LoginPageControllerTests` passed 2 tests. |
| `Maliev.AuthService` | `34559d6` | Preserved Authorization headers for AuthService -> IAM permission resolution by using `https+http://IAMService`; this restored wildcard IAM claims in the Intranet browser session. | `dotnet test B:\maliev\Maliev.AuthService\Maliev.AuthService.slnx --filter "FullyQualifiedName~IAMServiceClient" --no-restore` passed 5 tests; authenticated Intranet browser route sweep passed. |
| `Maliev.Web` | `14cdd41` | Aligned Web contact submissions with ContactService by resolving Thailand through CountryService, sending numeric contact type/priority values, mapping attachments, and accepting ContactService's integer id response. | `dotnet test B:\maliev\Maliev.Web\Maliev.Web.Tests\Maliev.Web.Tests.csproj --no-build` passed 68 tests; `Web_ContactInquiry_SubmitsThroughContactBoundary` passed in Aspire. |
| `Maliev.Web` | `5810d48` | Forwarded browser auth cookies into internal same-origin Web BFF calls during server-side rendering, fixing the account page `Unauthorized` state after successful registration. | `dotnet test B:\maliev\Maliev.Web\Maliev.Web.Tests\Maliev.Web.Tests.csproj --no-build` passed 69 tests; `Web_CustomerEmailRegistration_CreatesAccountSessionAndSignsOut` reached the signed-in account page. |
| `Maliev.Web` | `ea8c3a4` | Added stable names and accessible labels to account address fields so address entry is testable and accessible. | `dotnet build B:\maliev\Maliev.Web\Maliev.Web.slnx -p:UseSharedCompilation=false -m:1 --no-restore` passed; full Web tests passed 69 tests. |
| `Maliev.Aspire` | `c8f4b97` | Added executable Web contact and customer registration/account browser stories, preferred secure endpoints in browser tests, and wired Web quote CTAs to the secure local QuoteEngine endpoint. | `BrowserJourneyGateTests` passed 11 tests; `Tier=E2E` passed 13 tests; `AppHostReferenceTests` passed 10 tests. |
| `Maliev.Aspire` | `97793a1` | Added the first deeper authenticated Intranet browser story for employee customer creation, customer detail verification, and Project quote workspace customer selection. The test intentionally avoids `/api/v1/seed/customers` because that seed endpoint uses service-account clients and currently returns downstream CustomerService 403 in the Aspire browser context. | `Intranet_EmployeeCreatedCustomer_CanBeOpenedAndSelectedInProjectWorkspace` passed; `BrowserJourneyGateTests` passed 12 tests; `Tier=E2E` passed 14 tests. |
| `Maliev.Web` | `a81ecc8` | Awaited storefront cart persistence from product grid/detail add-to-cart handlers so fast navigation to `/cart` cannot beat the localStorage write and lose the item. | `dotnet build B:\maliev\Maliev.Web\Maliev.Web.slnx -p:UseSharedCompilation=false -m:1 --no-restore` passed; full Web tests passed 69 tests; full Aspire browser gate passed after rebuild. |
| `Maliev.Aspire` | `b725e1d` | Added the Commerce storefront browser journey covering employee draft/publish/archive and customer Web shop/product/cart/checkout sign-in behavior. | `Commerce_EmployeePublishesProduct_WebCustomerCanBrowseCartAndArchivedProductIsHidden` passed; `BrowserJourneyGateTests` passed 13 tests; `Tier=E2E` passed 15 tests. |
| `Maliev.CommerceService` | `6c62bf5` | Fixed storefront cart-line persistence by explicitly adding new application-assigned `CartLine` entities through the repository instead of relying on EF relationship tracking from an existing cart collection. | New service-flow test verifies product publish, cart creation, cart-line insert, and checkout-session creation; full CommerceService tests passed 9 tests; the Web browser checkout journey passed. |
| `Maliev.Web` | `9b39065` | Replaced direct Order/Payment/Delivery checkout draft calls with CommerceService cart and checkout-session boundaries, added progressive-enhancement cart form submit, synchronized cart `localStorage` before submit, and exposed backend diagnostics only in Development/Testing. | Full Web tests passed 69 tests; signed customer browser checkout creates a draft and shows the ready state. |
| `Maliev.Aspire` | `7e901d2` | Expanded the Commerce browser gate to cover customer registration after checkout sign-in redirect, account-session verification, signed checkout draft creation, IAM readiness retries for the automation employee, and checkout diagnostics. | `Commerce_EmployeePublishesProduct_WebCustomerCanBrowseCartAndArchivedProductIsHidden` passed; full `BrowserJourneyGateTests` passed 13 tests. |
| `Maliev.Aspire` | `20b40c8` | Added the Web customer account security browser story for two-customer address ownership isolation and protected account return-url preservation. | `Web_CustomerAccountSecurity_BlocksCrossCustomerAddressMutationAndPreservesReturnUrl` passed; `BrowserJourneyGateTests` passed 14 tests; `Tier=E2E` passed 16 tests. |
| `Maliev.Intranet` | `e2c9df9` | Resolved current employee profile lookups from the cookie `ClaimTypes.NameIdentifier` claim as well as raw `sub` and `user_id`, fixing `/api/v1/employees/me/profile` for browser-authenticated Intranet sessions. | `EmployeesControllerProfileTests` passed 3 tests; `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release -p:UseSharedCompilation=false -m:1 --no-restore` passed. |
| `Maliev.Aspire` | `75b56f9` | Added the limited employee seed fixture and browser journey for Intranet permission-boundary validation, plus seed source guards to prevent accidental wildcard assignment. | `Intranet_LimitedEmployee_CannotAccessRestrictedModuleApis` passed; full `BrowserJourneyGateTests` passed 15 tests; `Tier=E2E` passed 17 tests; seed option/source tests passed 7 tests. |
| `Maliev.Aspire` | `dd1dc4d` | Added the Intranet global-search browser journey for CustomerService -> RabbitMQ -> SearchService -> Intranet BFF -> top-bar UI navigation, and ignored generated `.lscache` tooling artifacts. | `Intranet_GlobalSearch_ReturnsEmployeeCreatedCustomerAndNavigatesToRecord` passed; limited employee search denial passed; full `BrowserJourneyGateTests` passed 16 tests; `Tier=E2E` passed 18 tests. |
| `Maliev.Intranet` | `e0c7806` | Made `/hr/profile` render from the self-service profile endpoint when the full employee-detail or preference endpoints are not permitted, so a limited employee can still use their own profile page. | `HrProfilePageTests` and `EmployeesControllerProfileTests` passed 6 tests; `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release -p:UseSharedCompilation=false -m:1 --no-restore /nr:false` passed. |
| `Maliev.Aspire` | `bb5704e` | Added `employee.profiles.update` to the limited employee fixture and browser coverage for self-service profile editing under limited permissions. | `Intranet_LimitedEmployee_CanUpdateOwnProfileOnly` passed; limited permission-boundary test passed; full `BrowserJourneyGateTests` passed 17 tests; `Tier=E2E` passed 19 tests. |
| `Maliev.Intranet` | `1a69e97` | Stopped silently dropping customer onboarding addresses when downstream CustomerService address creation fails; the BFF now reports the upstream failure so the browser gate cannot pass with missing addresses. | `CustomerServiceClientTests` passed 14 tests; `dotnet build B:\maliev\Maliev.Intranet\Maliev.Intranet.slnx --configuration Release -p:UseSharedCompilation=false -m:1 --no-restore /nr:false` passed. |
| `Maliev.Intranet` | `9aaa81a` | Preserved service-account authorization on Intranet BFF CountryService, CustomerService, and UploadService clients by using HTTPS-first Aspire service discovery, and made country reference-data failures explicit instead of returning an empty list. | `ReferenceData`, `ProgramHttpClientConfigurationTests`, and `CustomerServiceClientTests` passed 25 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.Aspire` | `919e41e` | Seeded CountryService reference data in Aspire and made testing seeders block their target services, so CustomerService and Intranet BFF do not start address workflows before country data exists. | `AppHostReferenceTests` passed 11 tests; `dotnet build B:\maliev\Maliev.Aspire\Maliev.Aspire.AppHost\Maliev.Aspire.AppHost.csproj -p:UseSharedCompilation=false -m:1 --no-restore /nr:false` passed. |
| `Maliev.Aspire` | `fdd001c` | Added the Intranet customer-onboarding browser story covering customer profile, company, company billing address, customer billing/shipping addresses, internal notes, persisted CustomerService detail JSON, and rendered detail tabs. | `Intranet_CustomerOnboardingUi_CreatesCompanyAddressesAndInternalNote` passed; full `BrowserJourneyGateTests` passed 18 tests; `Tier=E2E` passed 20 tests. |
| `Maliev.Aspire` | `15f47b9` | Added the Intranet system-health browser story covering authenticated dashboard rendering, BFF health/history APIs, IAMService readiness, GeometryService readiness, service-owned liveness/readiness paths, and manual refresh behavior. | `Intranet_SystemHealth_ShowsIamAndGeometryReadiness` passed; full `BrowserJourneyGateTests` passed 19 tests; `Tier=E2E` passed 21 tests. |
| `Maliev.Intranet` | `ac99eca` | Preserved service-account authorization on the Intranet IAM admin and bootstrap clients by using HTTPS-first Aspire service discovery. | `ProgramHttpClientConfigurationTests` and `IAMServiceClientTests` passed 6 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.Aspire` | `fa118e0` | Added the IAM administration browser story and hardened the Intranet sign-in readiness helper against transient post-login navigation context resets. | `Intranet_IamAdmin_CreatesUserAssignsRoleAndViewsPermissionMatrix` passed; full `BrowserJourneyGateTests` passed 20 tests; `Tier=E2E` passed 22 tests. |
| `Maliev.InvoiceService` | `47826bd` | Normalized invoice date-only values before event publication so browser-created local dates no longer throw `DateTimeOffset` UTC-offset exceptions during invoice creation. | `CreateInvoiceAsync_WithLocalDateInputs_PublishesDateOnlyOffsetWithoutThrowing` passed; InvoiceService Release build passed with 0 warnings. |
| `Maliev.InvoiceService` | `3e32569` | Allowed file references such as customer PO evidence to be registered while an invoice is still in Draft, while keeping cancelled invoices blocked and leaving PDF registration policy separate. | `FileReferenceTests` passed 10 tests; InvoiceService Release build passed with 0 warnings. |
| `Maliev.Intranet` | `877091a` | Made invoice create/file-link BFF failures preserve downstream status and body, fixed invoice page validation to trust the selected customer id after billing data loads, and kept finalization payloads aligned with InvoiceService. | `InvoiceServiceClientTests` passed 7 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.Aspire` | `0c83881` | Added the finance browser journey covering customer-backed invoice creation, PO evidence upload, persisted invoice totals, attachment registration, finalization, and invoice detail reload. | `Intranet_FinanceInvoice_CreatesAttachesAndFinalizesCustomerInvoice` passed; full `BrowserJourneyGateTests` passed 21 tests; `Tier=E2E` passed 23 tests. |
| `Maliev.Intranet` | `f5a193c` | Raised the browser WASM `MalievAPI` timeout above the BFF's 180-second resilience budget so cold Aspire/service-discovery calls return real downstream results instead of being aborted by the client at 30 seconds. | Intranet Release build passed with 0 warnings; procurement browser gate no longer failed with `net_http_request_timedout, 30`. |
| `Maliev.PurchaseOrderService` | `e2a00fd` | Normalized browser-origin purchase order delivery dates to UTC before PostgreSQL persistence and kept non-production downstream errors visible enough for E2E diagnosis while preserving production-safe generic errors. | PurchaseOrderService Release build passed; `PurchaseOrdersControllerTests.CreatePurchaseOrder_WithValidRequest_ReturnsCreatedOrder` passed with a local-date delivery input. |
| `Maliev.Aspire` | `87d7ae4` | Added the supplier-backed purchase-order browser journey covering supplier creation, PO creation, evidence attachment, cancellation, persisted API verification, and UI assertions. | `Intranet_ProcurementPurchaseOrder_CreatesAttachesAndCancelsSupplierPo` passed. |
| `Maliev.Intranet` | `a347ead` | Bounded concurrent system-health probes so the Intranet dashboard and sampler do not trigger a readiness storm across 35 services during Aspire E2E runs. | `SystemHealthControllerTests` passed 7 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.Intranet` | `874fbaf` | Configured the health-probe HTTP client so its own short probe timeouts/retries are not hidden by stale global circuit-breaker state, fixing GeometryService/IAM false-red readiness in the full E2E gate. | `SystemHealthControllerTests` plus the system-health source guard passed 8 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.Aspire` | `3461ed1` | Hardened browser readiness polling for transient post-login fetch failures and expanded system-health diagnostics for required services. | Full `Tier=E2E` passed 24 tests after the Intranet health-probe fixes. |
| `Maliev.LeaveService` | `abd5d95` | Created a pending approval when a leave request is submitted with a trusted approver id, and updated existing pending approvals instead of duplicating approval records. | `LeaveApprovalTests` passed 3 tests; LeaveService Release build passed with 0 warnings. |
| `Maliev.Intranet` | `009ab13` | Added the Intranet `/hr/leave` browser workflow, BFF endpoints for balances/requests/approvals/decisions, and LeaveService client payload mapping. | `LeaveServiceClientTests` passed 2 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.LeaveService` | `62bcabd` | Preferred the `employee_id` claim for self-scoped leave access when present, preserving the IAM principal `sub` for identity. | `LeaveApprovalTests` passed 4 tests; LeaveService Release build passed with 0 warnings. |
| `Maliev.LeaveService` | `1d8cbc8` | Resolved self-scoped leave access from the IAM principal through EmployeeService when browser-forwarded tokens do not contain an employee id claim. | `LeaveApprovalTests` passed 4 tests; LeaveService Release build passed with 0 warnings. |
| `Maliev.Intranet` | `060632c` | Sent date-only leave requests as UTC values through the BFF and preserved downstream LeaveService rejection details for diagnosis. | `LeaveServiceClientTests` passed 3 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.LeaveService` | `b724b7a` | Split leave balance read permission from leave request read permission and registered `leave.balances.read` explicitly. | `LeaveApprovalTests` passed 4 tests; LeaveService Release build passed with 0 warnings. |
| `Maliev.Intranet` | `fbee90b` | Required `leave.requests.read` for leave request history and manager approval reads while keeping balance reads on `leave.balances.read`. | `LeaveServiceClientTests` passed 3 tests; Intranet Release build passed with 0 warnings. |
| `Maliev.Intranet` | `402be13` | Added the material master-data create workflow and fixed the Intranet BFF MaterialService contract mapping from `SKU`/`UnitPrice`/`QuantityOnHand` to `Code`/`PricePerUnit`/`StockLevel`. | `MaterialServiceClientTests` passed 3 tests; Intranet Release build passed with 0 warnings; `Intranet_MaterialMasterData_CreatesAndEditsMaterial` passed. |
| `Maliev.Aspire` | `2220fe2` | Kept Testing MassTransit startup non-blocking but restored a 60-second bus start timeout so IAM readiness does not permanently fault during the full Aspire RabbitMQ connection surge. | `MassTransitExtensionOptionsTests` passed; focused `Intranet_SystemHealth_ShowsIamAndGeometryReadiness` passed; full `BrowserJourneyGateTests` passed 26 tests; `Tier=E2E` passed 28 tests. |
| `Maliev.CustomerService` | `b858fef` | Added first-class customer lifecycle status persistence so Intranet customer detail edits to `Lead`/`Active`/`Inactive` survive the BFF -> CustomerService -> database boundary and are reflected in search upsert status. | `dotnet test B:\maliev\Maliev.CustomerService\Maliev.CustomerService.slnx` passed 340 tests; CustomerService Release build passed with 0 warnings; `Intranet_CustomerDetail_EditsProfilePaymentTermsAndAddress` passed. |
| `Maliev.SupplierService` | `92be59a` | Refreshed supplier `xmin` row-version values after create/detail/update and invalidated supplier row-version cache with supplier cache so browser edits can use the service-issued concurrency token. Split capability mutations from the supplier scalar update to avoid false concurrency conflicts. | `dotnet test B:\maliev\Maliev.SupplierService\Maliev.SupplierService.slnx` passed 43 tests; SupplierService Release build passed with 0 warnings; `Intranet_SupplierProfile_CreatesAndEditsSupplier` passed. |
| `Maliev.Intranet` | `133ee96` | Added Intranet supplier profile list/create/detail/edit UI and mapped SupplierService detail/update DTOs including tax id, city, postal code, capabilities, and row version. | `dotnet test B:\maliev\Maliev.Intranet\Maliev.Intranet.Tests\Maliev.Intranet.Tests.csproj` passed 596 tests with 7 skipped; Intranet Release build passed with 0 warnings; `SupplierServiceClientTests` passed 3 tests. |

### Remaining Full-Catalog Blockers

- Full 95-story browser verification still requires local mail capture for verification/reset links, OAuth test mode, payment completion, service-backed QuoteEngine project/quotation/order/payment workflows, cross-customer ownership checks beyond Web account addresses, per-module low-permission UI/action checks, and deeper browser actions inside each Intranet module beyond the route-level and customer-onboarding coverage now present.
- Stories not listed above remain partial or blocked as recorded in the manual story matrix below. They must not be counted as fully verified until they have automated browser coverage or an explicitly accepted product-gap failure.

## 2026-05-15 Manual Browser E2E Correction Run

### Scope

- Corrective run after confirming that `Maliev.Aspire.Tests` does not yet contain browser E2E automation.
- Manually exercised the Aspire-hosted frontends with browser automation:
  - `Maliev.Web`: `http://localhost:5026`
  - `Maliev.QuoteEngine`: `http://localhost:5012`
  - `Maliev.Intranet`: `http://localhost:5071`
- Used story-level pass, partial, blocked, and failed outcomes. A blocked result means the current integrated environment lacks a required prerequisite, not that the story passed.

### Browser Story Results

| Story id | Result | Browser verification performed | Fix or next required fix |
|----------|--------|------------------------------|--------------------------|
| `WEB-001` | Passed after fix | Opened Web home, service content, and quote CTA. | Fixed Aspire Web CTA to local QuoteEngine URL. |
| `WEB-002` | Partial | Contact page rendered full inquiry form. | Need browser form submission after email-input automation issue is removed. |
| `WEB-003` | Partial | Sign-up page rendered Google primary path and email fallback. | Email verification issue remains a product gap; need local mail sink and submit-capable browser automation. |
| `WEB-004` | Blocked | No email verification token/link flow available to execute. | Implement verification-token issue, email delivery, and verify-link route. |
| `WEB-005` | Partial | Account sign-in page rendered Google, email fallback, reset, and create-account routes. | Need seeded/created customer credentials and form submission. |
| `WEB-006` | Partial | Google customer sign-in CTA rendered. | Need local OAuth/test-token strategy. |
| `WEB-007` | Partial | Forgot-password page rendered reset email form. | Need local mail sink and reset-token browser path. |
| `WEB-008` | Partial | Shop page rendered category/search UI and empty state. | Seed/publish storefront product before cart and checkout draft can pass. |
| `WEB-009` | Partial | Account page rendered customer account entry point. | Need authenticated customer session and seeded profile/order data. |
| `WEB-010` | Passed | Home, service detail, case-study/blog/support links rendered. | No immediate fix. |
| `WEB-011` | Passed | Cookie settings opened; `Accept optional` dismissed consent panel. | No immediate fix. |
| `WEB-012` | Partial | Contact/support information and contact form rendered. | Need contact form submission after email-input automation issue is removed. |
| `WEB-013` | Passed after fix | `/quote` rendered local `Try demo` and `Start project`; clicking `Try demo` loaded QuoteEngine demo. | Fixed Web quote base URL override in Aspire. |
| `QUOTE-001` | Partial after automated run | Signed customer opens QuoteEngine with email/password fallback, lands on `/projects/new`, uploads a customer file, and sees quote workspace state through the prototype store. | Need real ProjectService create/resume, persisted project ownership, Google SSO completion, and cross-device resume. |
| `QUOTE-002` | Partial after automated run | Anonymous demo loads the sample STEP; signed prototype run uploads a STEP file, completes resumable upload, shows analyzed state, DFM finding, and live analysis notification. | Need real UploadService storage, GeometryService analysis, GLB viewer, thumbnail, and DFM status for customer files. |
| `QUOTE-003` | Partial after automated run | Demo verifies deterministic price changes for process/lead time/quantity; signed prototype run selects CNC machining and Aluminum 6061, changes quantity, estimates, and sees THB total. | Need real PricingService breakdown, pricing determinism assertions by material/process/finish/tolerance, and production quote explanation. |
| `QUOTE-004` | Partial | Demo configuration update refreshes estimate after process/lead-time/quantity changes. | Need signed reupload/change/reanalysis path and stale-result prevention in real project mode. |
| `QUOTE-005` | Partial after automated run | Signed-upload gate shows Google primary and email fallback; signed prototype run completes email/password sign-in and continues to the upload workspace. | Need Google SSO test mode and anonymous-work-to-customer-account linking for real projects. |
| `QUOTE-006` | Partial after automated run | Signed prototype run generates an `MQ-yyyyMMdd-nnnn` formal quote and verifies it appears in account quote history. | Need ProjectService -> QuotationService immutable version creation and PdfService-generated quote PDF artifact. |
| `QUOTE-007` | Partial after automated run | Signed prototype run creates an `MO-yyyyMMdd-nnnn` order from the generated quote and verifies `Order received`. | Need OrderService record, PaymentService/Omise checkout, DeliveryService delivery intent, and version-specific acceptance. |
| `QUOTE-008` | Partial | `NDAs` and `Documents` portal routes render. | Need authenticated NDA/supporting-document upload, separation from CAD files, employee visibility, and authorization checks. |
| `QUOTE-009` | Partial after automated run | Signed prototype run verifies account quote and order APIs contain the created quote/order; portal order route also renders. | Need real quote history, order status, payment status, and manufacturing progress records. |
| `QUOTE-010` | Partial | `Profile` route renders against prototype-backed account data. | Need authenticated customer profile/preferences update flow and persistence. |
| `QUOTE-011` | Partial after automated run | Signed prototype run verifies account quote API contains the generated quote number. | Need real project list, quotation-version list/detail, PDF links, and version history UI. |
| `QUOTE-012` | Partial after automated run | Signed prototype run verifies account order API contains the generated order number. | Need real OrderService list/detail, payment/delivery/manufacturing status, and customer status visibility. |
| `QUOTE-013` | Partial | `NDAs` route renders. | Need NDA upload/list/delete, employee visibility, retention, and ownership denial checks. |
| `QUOTE-014` | Partial | `Documents` route renders. | Need supporting document upload/list/delete, document-type separation, employee visibility, and ownership denial checks. |
| `QUOTE-015` | Partial after automated run | QuoteEngine client now joins the upload SignalR group, receives `FileAnalysisCompleted`, and renders `Analysis complete for <file>` in a live status region. | Need order/payment/manufacturing notifications, reconnect recovery, preferences, and notification history. |
| `QUOTE-016` | Blocked | Multi-part workspace still requires customer-owned multi-file workflow beyond the current single-file prototype run. | Need signed customer project with multiple files and per-part configuration assertions. |
| `QUOTE-017` | Partial after automated run | Signed prototype run proves customer-owned upload is blocked before sign-in and succeeds after sign-in. | Need invalid file, large file, retry/resume, cancel, and failure-recovery fixtures. |
| `QUOTE-018` | Partial after automated run | Demo and signed prototype run show DFM warnings/info and `DFM reviewed`; signed run acknowledges DFM before quote. | Need real DFM warning fixture, hard-stop behavior before acknowledgement, and reanalysis after customer changes. |
| `QUOTE-019` | Partial after automated run | Demo verifies economy/standard/express price modifiers and quantity changes; signed run verifies configured estimate before quote. | Need full lead-time matrix assertions on authenticated PricingService-backed quotes. |
| `QUOTE-020` | Partial after automated run | Signed prototype run edits process/material/quantity before quote generation. | Need service-backed draft autosave, restore, multi-part editing, and save-conflict handling. |
| `QUOTE-021` | Passed after 2026-06-20 queue gate | Make Studio can request employee review through a confirmation-required QuoteEngine agent tool; ChatbotService exposes/forwards the tool, ProjectService persists `CustomerReview` status plus a review note, and Intranet exposes the `Customer Review` queue with authenticated list/detail readback. | No immediate fix. |
| `QUOTE-022` | Partial after automated run | Signed prototype run generates a formal quote and creates an order from that quote. | Need explicit quote terms/PO acceptance UI, version-specific accepted quotation, and immutable accepted snapshot. |
| `QUOTE-023` | Blocked | Artifact downloads still require generated service-backed quote/order/manufacturing records. | Need authenticated PDF/artifact download endpoints and ownership checks. |
| `QUOTE-024` | Passed after fix | Anonymous demo loaded sample file, DFM, pricing, and disabled formal artifacts. | Fixed QuoteEngine static web asset hosting. |
| `QUOTE-025` | Partial after automated run | Signed prototype run creates one quote/order and verifies history APIs. | Need one real project with multiple immutable QuotationVersion records. |
| `QUOTE-026` | Blocked | Version comparison requires multiple generated quotation versions. | Need version-history UI and seeded/created service-backed versions. |
| `INT-001` | Partial after automated run | Login page rendered email/password and Google; protected routes redirected. Later automated browser runs sign in as both the Aspire automation employee and a limited employee through the real BFF/AuthService/IAM path. | Need Google SSO/OAuth test mode and UI navigation assertions for role-shaped menus, not only API permission checks. |
| `INT-002` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, opens `/sales/customers/new`, verifies Thailand country reference data, creates a customer with company, company billing address, customer billing/shipping addresses, and internal note, then verifies persisted CustomerService detail JSON. | Need registry/company lookup, address autocomplete, document upload, and NDA/customer-document separation coverage. |
| `INT-003` | Partial after automated run | Later automated browser E2E verifies the created customer detail page renders the profile, company, addresses, and notes data; separate global-search automation verifies search propagation to customer navigation; later customer-detail automation verifies profile, lifecycle status, payment terms, shipping address, audit trail, and list/search propagation. | Need customer/NDA document upload and related workflow propagation beyond customer list/search. |
| `INT-004` | Blocked | `/projects/new` redirected to login. | Need Aspire test admin before ProjectNew verification. |
| `INT-005` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, generates quotation version PDFs from a persisted project, verifies the latest PdfService artifact exists, verifies QuotationService attaches the artifact to the exact current version, and validates downloadable PDF bytes. | Need semantic PDF text/pixel inspection and full ProjectNew editor UI path into the same PDF assertion. |
| `INT-006` | Partial after automated run | Later automated browser E2E duplicates an accepted project, verifies the duplicate has a distinct id, source project id/number linkage, copied file/configuration context, and a visible `Duplicated from` marker. | Need accepted-project mutation guard UI, customer reorder path, and downstream order/job preservation checks. |
| `INT-007` | Partial after automated run | Later automated browser E2E rejects stale expected quotation version acceptance, accepts the current project quotation version through the UI, and verifies the project moves to accepted quotation state before duplication. | Need OrderService/JobService record creation checks and version-specific order/job references. |
| `INT-008` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, creates and finalizes a customer-backed invoice, records a full payment from invoice detail, verifies paid/balance state, creates a receipt, and verifies the receipt through UI and BFF data. | Need invoice and receipt PDF artifact checks, accounting journal/export effects, partial payment UI, void/refund behavior, and Omise sandbox completion. |
| `INT-009` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, creates a corporate customer and real OrderService order, creates a DeliveryService delivery note through `/finance/delivery-notes/new`, verifies carrier/tracking/item/customer state, marks the shipment `InTransit` and `Delivered`, uploads proof-of-delivery evidence, downloads the proof bytes through Intranet, requests the delivery PDF queue, verifies persisted detail JSON, and verifies the note appears in the list. The Make Studio delivery-status gate adds customer-visible QuoteEngine delivered status from a paid order. | Need delivery PDF artifact download/content verification, notification delivery, partial delivery quantities, and low-permission negative checks. |
| `INT-010` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, creates a new IAM principal through `/iam/users/new`, assigns the `roles.aspire.limited` role, verifies the principal through the BFF/IAM API, opens the user detail page, and verifies the role binding. | Need invited-user activation/session verification, role-change effects on a signed-in target user, disable/offboard behavior, and permission-scoped UI assertions beyond the created binding. |
| `INT-011` | Partial after automated run | Later automated browser E2E opens IAM roles, verifies the `roles.aspire.limited` role detail page, verifies the BFF permission matrix, and confirms expected profile permissions are granted. | Need role create/edit/delete UI, category grouping assertions, high-risk permission guardrails, and assigned-user visibility where supported. |
| `INT-012` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, opens `/mfg/materials`, creates a material, edits quote-critical material fields, and verifies persisted BFF/MaterialService detail data. | Need process/color/post-processing assignment UI, supplier linkage, bulk import/export, inventory lot movement, and low-permission action checks. |
| `INT-013` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, registers equipment through `/mfg/equipment`, verifies generated asset detail, appends an operating note, appends a maintenance log, and verifies persisted BFF/FacilityService state. | Need equipment update/status-transition UI, attachment management, low-permission action checks, job availability effects, maintenance author identity, and scheduled work-center assignment. |
| `INT-014` | Partial after automated run | Later automated browser E2E signs in as a limited employee to create a pending leave approval, signs in as the manager, verifies the dashboard service widgets and action-item BFF payload, clicks the leave action item, and verifies the approval row in `/hr/leave`. | Need seeded commercial/order/procurement action items, dashboard chart interactions, per-role dashboard customization, and long-running refresh checks under real operational volume. |
| `INT-015` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, verifies assistant health, opens the Intranet assistant drawer, initiates a ChatbotService session, sends a prompt for the seeded quotation number, verifies a quotation response and `Send Reminder` suggested action, clicks the action, and verifies the reminder follow-up keeps the quotation id. | Need production Gemini/tool-calling behavior beyond deterministic Aspire client, full supported-tool catalog, explicit confirmation policy for mutations, assistant audit-log review UI, production callback allow-list configuration, and permission-negative checks against restricted records/actions. |
| `INT-016` | Blocked | Requires authenticated project draft autosave. | Need Aspire test admin and ProjectNew access. |
| `INT-017` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee and uploads a STEP file through Intranet BFF resumable upload into UploadService. | Need true multi-file ProjectNew UI upload and browser progress assertions. |
| `INT-018` | Partial after automated run | Later automated browser E2E verifies persisted DFM warning state, DFM acknowledgement, drawing/supporting-document metadata, and project detail quote state after upload. | Need GeometryService-rendered viewer/thumbnail pixel checks and model analysis artifacts from real geometry processing. |
| `INT-019` | Partial after automated run | Later automated browser E2E configures material/process/quantity on a persisted part, confirms pricing, changes quantity, and verifies the regenerated quotation total/version reflects the new quantity. | Need ProjectNew live pricing UI assertions and reference-data picker coverage. |
| `INT-020` | Blocked | Requires authenticated bulk part editing. | Need multi-part project fixture. |
| `INT-021` | Partial after automated run | Later automated browser E2E creates a DFM-warning project part, verifies the warning state is preserved through the BFF/ProjectService contract, acknowledges DFM, and still generates quotation versions. | Need failing DFM fixture, retry/reupload behavior, and stale-result prevention checks. |
| `INT-022` | Partial after automated run | Later automated browser E2E supplies manual discount, shipping cost, delivery expectations, and change summaries, then verifies automatic quotation PDF artifact creation and byte validity. | Need ProjectNew commercial-terms UI, draft PDF watermark/status policy, and semantic PDF content comparison. |
| `INT-023` | Partial after automated run | Later automated browser E2E generates version 1, changes quote-critical quantity, generates version 2 on the same quotation id, and verifies snapshot JSON/hash/change summaries. | Need duplicate-click/idempotency checks and failure recovery if PDF generation fails after version creation. |
| `INT-024` | Partial after automated run | Later automated browser E2E verifies a persisted project file reference survives project detail reload, quote generation, PDF generation, and duplicate project creation. | Need unsaved ProjectNew temporary-upload migration and cleanup/rollback checks. |
| `INT-025` | Partial after automated run | Later automated browser E2E uploads drawing and supplementary project-part attachments and verifies the persisted part attachment metadata. | Need browser ProjectNew attachment controls, per-part concurrent upload isolation, and generated PDF drawing-reference content checks. |
| `INT-026` | Blocked | Requires duplicate-part action in project draft. | Need authenticated ProjectNew draft. |
| `INT-027` | Partial after automated run | Later automated browser E2E exposed and fixed multiple real upload/PDF boundary failures before the project quote lifecycle could pass. | Need deterministic fault-injection controls for upload, geometry, pricing, and SignalR reconnect without relying on incidental defects. |
| `INT-028` | Partial after automated run | Later automated browser E2E creates two versions on one quotation, verifies version 1/version 2/current marker in the project quote tab, verifies version PDF artifact linkage, validates PDF bytes per version, proves version 1/version 2 storage paths differ, and verifies non-current version 1 cannot be accepted after version 2 is current. | Need side-by-side comparison UI, non-current version permission UI affordances, and semantic PDF text/pixel validation per version. |
| `COM-001` | Partial after automated run | Authenticated Intranet employee creates a draft Commerce product and verifies employee-side draft visibility. | Need full catalog media/category/editor validation, bulk edits, low-permission action checks, and production media storage checks. |
| `COM-002` | Partial after automated run | Authenticated Intranet employee publishes the product; Web storefront shows the published product while hiding the draft before publish. | Need scheduled publishing, channel/region rules, SEO metadata, and cache invalidation checks. |
| `COM-003` | Partial after automated run | Web customer browses shop, opens product detail, adds item to cart, edits quantity, signs in from checkout, and creates a checkout draft. | Need Omise payment completion, tax/shipping rules, inventory reservation, and order confirmation. |
| `COM-004` | Partial after automated run | Authenticated employee archives the product; Web product detail returns customer-facing not-found state. | Need unpublish-vs-archive distinction, search index removal, and cache invalidation checks. |
| `OPS-001` | Partial after automated run | Authenticated employee creates a customer, waits for SearchService indexing, searches through the BFF and top-bar UI, click-away closes results, and clicking the result navigates to the customer list. | Need permission-scoped search across projects, orders, catalog, purchase orders, invoices, and restricted-record denial checks. |
| `OPS-002` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, opens `/admin/system-health`, verifies AuthService, IAMService, and GeometryService are healthy through BFF `/api/v1/system-health`, verifies `/liveness` and `/readiness` paths, verifies history contains IAMService and GeometryService, and verifies the Refresh action keeps the probe grid available. | Need endurance-style validation that the page continues auto-refreshing correctly during a long-running session. |
| `OPS-003` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, creates a customer, verifies NotificationService preference/channel provisioning, sends a customer email notification from Intranet customer detail, verifies delivered delivery-log state and provider/simulated provider id, opts out of a notification category, sends that category again, and verifies the skipped/opted-out log. | Need customer/employee notification center UI, read/unread state, live push surface, broader automatic event mappings, external provider sandbox delivery, and low-permission negative checks. |
| `FIN-001` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, creates a corporate customer, creates a draft invoice with PO evidence, verifies persisted billing, line, tax, total, and attachment data, finalizes the invoice, and reloads invoice detail. | Need billing-note creation, credit-term selection beyond default payment terms, invoice PDF artifact checks, and accounting export effects. |
| `FIN-002` | Partial after automated run | Later automated browser E2E records a full invoice payment after finalization, verifies InvoiceService/BFF paid amount and zero balance, creates a ReceiptService receipt, and verifies the receipt through UI and BFF data. | Need receipt PDF artifact verification, accounting journal/export effects, partial payment/void/refund UI, and Omise sandbox payment completion. |
| `MFG-001` | Blocked | Requires authenticated manufacturing scheduling. | Need Aspire test admin and job/equipment seed. |
| `MFG-002` | Blocked | Requires shop-floor mobile authenticated job. | Need test employee role and assigned job. |
| `MFG-003` | Blocked | Requires equipment/work-center assignment UI. | Need facility/equipment/job seed. |
| `MFG-004` | Blocked | Requires material reservation/consumption UI. | Need inventory/material/job seed. |
| `MFG-005` | Automated | `Intranet_ManufacturingLifecycle_OrderToJobStatusUpdateWithSignalRBroadcast` creates a customer and order, advances the order through New → Reviewing → Reviewed → Quoted → Accepted → Paid via OrderService, waits for JobService to create a production job via the `OrderPaidEvent` → `OrderPaidEventConsumer` event chain, updates the job to InProgress via `PATCH /api/v1/jobs/{id}/status`, and verifies the 204 response proves `ProductionHub.JobStatusChanged` fired. A queue re-read and page reload confirm the status is fresh for watching employees. | Rich UI click coverage (Kanban drag-and-drop, live SignalR push to a connected browser client without page reload) deferred. |
| `PROC-001` | Blocked | Requires authenticated PO creation. | Need Aspire test admin and supplier/material seed. |
| `PROC-002` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, opens `/purchasing/suppliers`, creates a supplier through the rendered UI, verifies the supplier detail route, edits profile/address/capabilities through the UI, verifies SupplierService JSON, and also verifies the supplier is selectable in `/purchasing/new` for a supplier-backed PO. | Need supplier document upload, payment terms, duplicate/tax-id validation, supplier audit display, supplier status transitions, and low-permission action checks. |
| `PROC-003` | Partial after automated run | Later automated browser E2E creates a PO, verifies persisted detail JSON, uploads customer PO evidence, confirms the attachment record, cancels the PO with a reason, and verifies `Cancelled` state in UI and API. | Need approval workflow, cancellation audit trail display, PO PDF/output artifacts, and permission/action-level negative checks. |
| `PROC-004` | Partial after automated run | Later automated browser E2E signs in as the Aspire automation employee, creates a supplier/source-order PO, opens the PO detail page, approves, sends, receives, and verifies persisted `Delivered` state plus disabled post-delivery receive/cancel actions. | Need InventoryService stock movement, supplier invoice, and AccountingService journal impact from the receiving event. Need partial receipt quantities, lot/batch traceability, and receiving evidence artifact checks. |
| `HR-001` | Partial after automated run | Later automated browser E2E verifies a limited employee can open `/hr/profile`, update their own preferred name, personal email, and mobile phone, reload, and see the persisted self-profile data. | Need full employee lifecycle creation, IAM role assignment, manager/department setup, and offboarding behavior. |
| `HR-002` | Partial after automated run | Later automated browser E2E signs in as a limited employee, submits an Annual leave request through `/hr/leave`, signs in as the seeded manager, approves the pending request, verifies the manager queue clears, and verifies the employee sees the request as `Approved`. | Need notification delivery, manager calendar/status visibility, leave balance close/accrual edge cases, rejection/cancellation paths, and mobile-friendly manager approval checks. |
| `HR-003` | Blocked | Requires career candidate module. | Need authenticated HR user and candidate seed. |
| `HR-004` | Blocked | Requires compliance/training module. | Need authenticated HR/compliance user and records. |
| `HR-005` | Blocked | Requires compensation module. | Need authenticated HR/finance user and permission boundary checks. |
| `HR-006` | Blocked | Requires performance module. | Need manager/employee identities and review records. |
| `SEC-001` | Partial after automated run | Later automated Web E2E created two authenticated customers and verified Customer B cannot mutate Customer A's address. Later QuoteEngine E2E created Customer A quote/order history, signed in Customer B, verified Customer B cannot see Customer A's quote/order history, and verified Customer B receives `404` when trying to order from Customer A's quote id. | Need extend ownership denial to real ProjectService/QuotationService/OrderService records, NDAs, supporting documents, PDFs, and employee permission-scoped access. |
| `SEC-002` | Partial after automated run | Anonymous direct URLs to restricted Intranet pages redirected to login. Later automated browser E2E signs in as a limited employee, verifies self-profile access, and verifies IAM users, IAM roles, employees, and global search APIs return `403`. | Need expand the low-permission checks to every restricted Intranet module, hidden navigation item, and action-level command. |
| `SEC-003` | Partial after automated run | Direct protected URLs preserved return URL on login redirect; later automated Web E2E also cleared a customer session and verified `/account/addresses` redirects with return URL preserved. | Need true expired-token/refresh-session behavior, not only anonymous or missing-cookie redirects. |
| `SEC-004` | Partial | QuoteEngine demo hides formal/internal artifacts and disables PDF. | Need authenticated customer surfaces plus employee quote with internal pricing. |

### Fixed During Manual Browser Run

| Issue | Fix | Verification |
|-------|-----|--------------|
| QuoteEngine BFF served `index.html`, but the Blazor app stayed stuck on `Loading quote engine` during browser verification. | Added `builder.WebHost.UseStaticWebAssets()` to `Maliev.QuoteEngine.Bff` so hosted WASM static assets are available under Aspire. | Browser reload of `http://localhost:5012/demo` rendered QuoteEngine shell, demo sample file, DFM panel, configuration controls, and pricing. `dotnet build Maliev.QuoteEngine.Bff.csproj -p:UseSharedCompilation=false` passed. |
| Web quote CTAs left Aspire and pointed at production `https://quote.maliev.com/...`. | Added a runtime `QuoteEngine__BaseUrl` override in `Maliev.Web`; the current AppHost wiring uses `quoteEngineBff.GetEndpoint("https")` so Web and QuoteEngine handoff stay on secure local endpoints. Production default remains `https://quote.maliev.com`. | Latest automated run verified Web `/quote` `Try demo` routes to the Aspire-hosted QuoteEngine demo and `BrowserJourneyGateTests` passed. |

### Manual Browser Blockers

| Blocker | Evidence | Impact | Recommended next action |
|---------|----------|--------|-------------------------|
| No deterministic employee test login is enabled. | Aspire parameter `AspireTestAdminEnabled` is `false`; login-only browser checks could not enter Intranet. | Employee, manufacturing, procurement, finance, HR, admin, and ProjectNew stories cannot be manually completed. | Enable a non-production Aspire test admin seed path with a secret-sourced password, then run authenticated Intranet browser stories. |
| Local email/OAuth providers are not represented in Aspire. | Automated Web email/password registration now works, but verification email, password reset email, and Google OAuth cannot complete without local provider fixtures. | Email verification, reset-token, and Google sign-in remain partial/blocked even though customer registration/session/account/address/sign-out now passes. | Add a local mail sink and OAuth/test-token strategy for Aspire E2E. |
| Storefront has no published seeded products. | `/shop` rendered `No products listed yet`. | Cart and checkout draft stories cannot be completed end to end. | Add an Aspire seed command/data set for published CommerceService products. |

## 2026-05-15 Run

### Scope

- Requested: run all available E2E tests, document the results, and fix issues found during the run.
- Browser E2E automation status: no implemented Playwright/browser E2E tests are currently present in `Maliev.Aspire.Tests`; `Tier=E2E` discovery matched zero tests.
- Executed available service-level end-to-end tests, QuoteEngine journey smoke tests, and focused Aspire system integration checks.
- Did not re-run the full Aspire suite after the known long-running blockers were isolated because the full run timed out before completion.

### Result Summary

| Area | Command or filter | Result | Evidence and coverage |
|------|-------------------|--------|-----------------------|
| Aspire browser E2E discovery | `dotnet test Maliev.Aspire.Tests.csproj --no-build --list-tests --filter "Tier=E2E"` | No tests matched | Confirms the story catalog is not yet backed by Playwright automation. |
| Currency service E2E/final controller tests | `FullyQualifiedName~FinalRatesControllerTests\|FinalCurrenciesControllerTests\|FinalSnapshotsControllerTests\|FinalSystemControllerTests` | Passed, 39/39 | Rates, currencies, snapshots, and system endpoints. |
| Upload service E2E tests | `FullyQualifiedName~EndToEndTests` | Passed, 3/3 | Upload, retrieve, delete, isolation, and resumable large upload paths. |
| Prediction service E2E tests | `FullyQualifiedName~PredictionsEndToEndTests` | Passed, 7/7 | Print-time prediction, demand forecast, cache hit/miss behavior. |
| QuoteEngine endpoint journey smoke tests | `FullyQualifiedName~QuoteEngineEndpointTests` | Passed, 6/6 | Reference data, non-mutating demo, signed upload gate, content-range upload, quote estimate gating, server-side profile identity. |
| Aspire reference and fixture unit checks | `FullyQualifiedName~AppHostReferenceTests\|Unit\|IAMRegistrationHealthCheckTests\|BackgroundIAMRegistrationServiceTests\|UrlQueryRedactionProcessorTests` | Passed, 30/30 | AppHost references, IAM registration helpers, and URL query redaction. |
| Aspire AppHost reference tests | `FullyQualifiedName~AppHostReferenceTests` | Passed, 10/10 | QuoteEngine and Web BFF wiring guardrails. |
| Aspire AppHost build | `dotnet build Maliev.Aspire.AppHost.csproj --no-dependencies` | Passed, 0 warnings/errors | Confirms AppHost compile after Omise parameter fallback and fixture changes. |
| Aspire core readiness checks | `FullyQualifiedName~CoreService_PassesReadinessCheck` | Passed, 8/8 | IAM, Auth, Customer, Employee, Country, Order, Payment, and Invoice readiness. |
| Aspire Geometry service checks | `FullyQualifiedName~GeometryServiceTests` | Passed, 3/3 | Geometry liveness, Scalar documentation endpoint, and protected-route behavior. |
| Aspire full test suite | `dotnet test Maliev.Aspire.Tests.csproj --no-build` | Timed out after 20 minutes | Full-suite execution currently cannot be used as a clean production gate. |
| Aspire service discovery | `FullyQualifiedName~Integration.ServiceDiscoveryTests` | Passed, 39/39 | PaymentService, IAM readiness, GeometryService liveness, and Intranet system-health checks are green after fixes. |

### Fixed During This Run

| Issue | Fix | Verification |
|-------|-----|--------------|
| `PaymentService` blocked Aspire startup when `PaymentProviders:Omise:WebhookSecret` was not configured locally. | `AppHost.cs` now gives `OmiseWebhookSecret` a local/testing fallback through `AddParameterFromConfig` while still allowing configured secrets to override it. | PaymentService was no longer present in the latest failing `ServiceDiscoveryTests` result. AppHost build and AppHost reference tests passed. |
| Aspire fixture liveness timing was too short for slow local container startup. | `AspireTestFixture` now gives `PaymentService` and `GeometryService` longer startup windows. | Focused AppHost reference tests passed; final ServiceDiscovery passed. |
| Aspire fixture only tried HTTPS endpoint lookup before falling back to generic client creation. | `AspireTestFixture` now tries explicit HTTP endpoint lookup for HTTP-only resources before the generic fallback, with Geometry using the factory client path. | AppHost reference tests and build passed; final ServiceDiscovery passed. |
| `GeometryService` was blocked by `otelcollector` failure during Aspire test startup. | `GeometryService` still receives the OTLP endpoint, but only waits for `otelcollector` outside Testing. This keeps normal local startup ordering while preventing optional observability from blocking the test gate. | `GeometryServiceTests` passed 3/3 and `ServiceDiscoveryTests` passed 39/39. |
| Geometry test clients could be routed through an unhealthy fixed DCP port. | Removed the fixed host port from the Geometry AppHost endpoint and kept only `targetPort: 8081`; the Aspire test fixture uses the factory client base address for Geometry. | `GeometryService_Liveness_ReturnsOk` passed after rebuild. |

### Remaining Blockers

| Blocker | Evidence | Impact | Recommended next action |
|---------|----------|--------|-------------------------|
| Browser E2E automation is not implemented yet. | `Tier=E2E` discovery matched zero tests. | `E2E_USER_JOURNEY_STORIES.md` is ready as the production-gate catalog, but the user journeys cannot yet be executed in a browser gate. | Add Playwright suites for `Maliev.Web`, `Maliev.QuoteEngine`, and `Maliev.Intranet`; each test should cite one or more story ids. |
| Full Aspire suite timed out. | `dotnet test Maliev.Aspire.Tests.csproj --no-build` exceeded 20 minutes without a final result. | Full suite is not currently usable as a deterministic deployment gate. | Run narrower suites while fixing IAM/Geometry; then re-enable the full suite as a final gate once service-discovery is green. |

### Production-Gate Interpretation

- Current automated evidence supports several service-level journeys, QuoteEngine BFF smoke paths, and AppHost wiring.
- The documented customer and employee browser journeys are not yet executable as E2E tests.
- The next production-gate milestone should be a small Playwright foundation that starts Aspire once, opens the three frontends, signs in through the supported test identities, and records story-id level pass/fail evidence here.
