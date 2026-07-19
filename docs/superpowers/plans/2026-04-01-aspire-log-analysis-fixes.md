# Aspire Log Analysis Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all bugs, warnings, and performance issues identified during the Aspire debug session log analysis.

**Architecture:** Add missing metrics/count endpoints following existing CQRS and controller patterns. Fix performance anti-patterns (missing AsNoTracking, client-side evaluation, cartesian explosion). Reduce log noise via shared ServiceDefaults. Fix IAM promote idempotency.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core (Npgsql/PostgreSQL), MediatR, MassTransit

---

## Scope

| # | Area | Fix | Repos Affected |
|---|------|-----|----------------|
| 1 | LeaveService pending-count (405) | Add endpoint + BFF update | Maliev.LeaveService, Maliev.Intranet |
| 2 | OrderService on-hold-count (404) | Add endpoint | Maliev.OrderService |
| 3 | InvoiceService overdue-count (404) | Create MetricsController | Maliev.InvoiceService |
| 4 | IAM promote race condition (400) | Add idempotency check | Maliev.IAMService |
| 5 | IAM principal repair log noise | Reduce log level | Maliev.IAMService |
| 6 | ServiceDefaults log noise | Add filters + EF Core comment | Maliev.Aspire (ServiceDefaults) |
| 7 | PricingService performance | Add AsNoTracking + CancellationToken | Maliev.PricingService |
| 8 | MaterialService performance | Add AsNoTracking + fix client-side eval + AsSplitQuery | Maliev.MaterialService |

**Out of scope (no fix needed):**
- EmployeeService preferences/dashboard 404 — by design, BFF swallows it
- CurrencyService 1004ms — first-call cold cache, already has Redis caching
- OrderService `status:"Open"` discrepancy — investigate separately

---

### Task 1: LeaveService — Add `pending-count` Endpoint

**Files:**
- Create: `Maliev.LeaveService.Application/Queries/GetPendingApprovalsCountQuery.cs`
- Create: `Maliev.LeaveService.Application/Queries/Handlers/GetPendingApprovalsCountQueryHandler.cs`
- Modify: `Maliev.LeaveService.Application/Interfaces/ILeaveRequestRepository.cs` — add `GetPendingApprovalsCountAsync`
- Modify: `Maliev.LeaveService.Infrastructure/Repositories/LeaveRequestRepository.cs` — implement count method
- Modify: `Maliev.LeaveService.Api/Controllers/LeaveRequestsController.cs` — add endpoint

- [ ] **Step 1: Add repository interface method**

In `Maliev.LeaveService.Application/Interfaces/ILeaveRequestRepository.cs`, add:

```csharp
Task<int> GetPendingApprovalsCountAsync(Guid managerId, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implement repository method**

In `Maliev.LeaveService.Infrastructure/Repositories/LeaveRequestRepository.cs`, add:

```csharp
public async Task<int> GetPendingApprovalsCountAsync(Guid managerId, CancellationToken cancellationToken = default)
{
    return await _context.LeaveRequests
        .Where(r => r.Approvals.Any(a => a.ApproverId == managerId && a.Status == Domain.Enums.ApprovalStatus.Pending))
        .CountAsync(cancellationToken);
}
```

- [ ] **Step 3: Create query class**

Create `Maliev.LeaveService.Application/Queries/GetPendingApprovalsCountQuery.cs`:

```csharp
using MediatR;

namespace Maliev.LeaveService.Application.Queries;

public class GetPendingApprovalsCountQuery : IRequest<int>
{
    public Guid ApproverId { get; set; }
}
```

- [ ] **Step 4: Create query handler**

Create `Maliev.LeaveService.Application/Queries/Handlers/GetPendingApprovalsCountQueryHandler.cs`:

```csharp
using Maliev.LeaveService.Application.Interfaces;
using MediatR;

namespace Maliev.LeaveService.Application.Queries.Handlers;

public class GetPendingApprovalsCountQueryHandler : IRequestHandler<GetPendingApprovalsCountQuery, int>
{
    private readonly ILeaveRequestRepository _requestRepository;

    public GetPendingApprovalsCountQueryHandler(ILeaveRequestRepository requestRepository)
    {
        _requestRepository = requestRepository;
    }

    public async Task<int> Handle(GetPendingApprovalsCountQuery request, CancellationToken cancellationToken)
    {
        return await _requestRepository.GetPendingApprovalsCountAsync(request.ApproverId, cancellationToken);
    }
}
```

- [ ] **Step 5: Add controller endpoint**

In `Maliev.LeaveService.Api/Controllers/LeaveRequestsController.cs`, add after `GetPendingApprovals` (after line 92):

```csharp
    /// <summary>
    /// Gets the count of pending leave approvals for a manager.
    /// </summary>
    /// <param name="managerId">The manager identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of pending approvals.</returns>
    [HttpGet("pending-count")]
    [RequirePermission(LeavePermissions.Read)]
    public async Task<IActionResult> GetPendingApprovalsCount([FromQuery] Guid managerId, CancellationToken cancellationToken)
    {
        var query = new GetPendingApprovalsCountQuery { ApproverId = managerId };
        var count = await _mediator.Send(query, cancellationToken);
        return Ok(new { count });
    }
```

- [ ] **Step 6: Update BFF LeaveServiceClient interface**

In `Maliev.Intranet/Maliev.Intranet.Bff/Clients/LeaveServiceClient.cs`, update interface method signature (line 39):

```csharp
Task<int> GetPendingApprovalCountAsync(Guid managerId, CancellationToken ct = default);
```

Update implementation (lines 91-97):

```csharp
    public async Task<int> GetPendingApprovalCountAsync(Guid managerId, CancellationToken ct = default)
    {
        var httpResponse = await httpClient.GetAsync($"/leave/v1/LeaveRequests/pending-count?managerId={managerId}", ct);
        if (!httpResponse.IsSuccessStatusCode) return 0;
        var response = await httpResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);
        return response.TryGetProperty("count", out var count) ? count.GetInt32() : 0;
    }
```

- [ ] **Step 7: Update BFF DashboardController to pass managerId**

In `Maliev.Intranet/Maliev.Intranet.Bff/Controllers/DashboardController.cs`:

Add `GetEmployeeIdAsync` helper method (following `TimeOffController` pattern). Add it after the `GetActionItems` method:

```csharp
    private async Task<Guid> GetEmployeeIdAsync(CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirst("employee_id")?.Value;
        if (Guid.TryParse(employeeIdClaim, out var employeeId))
        {
            return employeeId;
        }

        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdString, out var principalId))
        {
            var employee = await employeeClient.GetByPrincipalIdAsync(principalId, ct);
            if (employee != null)
            {
                return employee.Id;
            }
        }

        return Guid.Empty;
    }
```

Then update line 157 from:
```csharp
var pendingLeaveTask      = SafeCount(t => leaveClient.GetPendingApprovalCountAsync(t), ct);
```
to:
```csharp
var employeeId = await GetEmployeeIdAsync(ct);
var pendingLeaveTask      = SafeCount(t => leaveClient.GetPendingApprovalCountAsync(employeeId, t), ct);
```

Note: This requires pulling the `employeeId` resolution BEFORE the parallel task block (it's needed before firing the tasks). Move it to just before line 154.

- [ ] **Step 8: Build and verify**

Run: `dotnet build` in the LeaveService, Intranet, and Aspire solution.
Expected: Clean build with no warnings.

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: add LeaveService pending-count endpoint and BFF integration"
```

---

### Task 2: OrderService — Add `on-hold-count` Endpoint

**Files:**
- Modify: `Maliev.OrderService/Maliev.OrderService.Api/Controllers/MetricsController.cs`

- [ ] **Step 1: Add endpoint to MetricsController**

Add after the `GetActiveCount` method (after line 51):

```csharp
    /// <summary>
    /// Get the count of orders on hold.
    /// </summary>
    [HttpGet("on-hold-count")]
    [RequirePermission(OrderPermissions.ReportsAnalytics)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOnHoldCount(CancellationToken cancellationToken)
    {
        var response = await _orderManagementService.GetOrdersAsync(
            page: 1,
            pageSize: 1,
            user: User,
            status: "OnHold",
            cancellationToken: cancellationToken);

        return Ok(new { count = response.TotalCount });
    }
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build` in Maliev.OrderService
Expected: Clean build with no warnings.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add OrderService on-hold-count metrics endpoint"
```

---

### Task 3: InvoiceService — Create `MetricsController` with `overdue-count`

**Files:**
- Create: `Maliev.InvoiceService/Maliev.InvoiceService.Api/Controllers/MetricsController.cs`

- [ ] **Step 1: Create MetricsController**

Create `Maliev.InvoiceService/Maliev.InvoiceService.Api/Controllers/MetricsController.cs`:

```csharp
using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.InvoiceService.Api.Authorization;
using Maliev.InvoiceService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.InvoiceService.Api.Controllers;

/// <summary>
/// Lightweight business metrics for dashboards.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("invoice/v{version:apiVersion}/metrics")]
public class MetricsController : ControllerBase
{
    private readonly InvoiceDbContext _db;
    private readonly ILogger<MetricsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsController"/>.
    /// </summary>
    public MetricsController(InvoiceDbContext db, ILogger<MetricsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get the count of overdue invoices.
    /// </summary>
    [HttpGet("overdue-count")]
    [RequirePermission(InvoicePermissions.ReportsAnalytics)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdueCount(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        var count = await _db.Invoices
            .Where(i => (i.Status == "Finalized" || i.Status == "PartiallyPaid") && i.DueDate < today)
            .CountAsync(cancellationToken);

        return Ok(new { count });
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build` in Maliev.InvoiceService
Expected: Clean build with no warnings.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add InvoiceService MetricsController with overdue-count endpoint"
```

---

### Task 4: IAMService — Fix Promote Endpoint Idempotency

**Files:**
- Modify: `Maliev.IAMService/Maliev.IAMService.Api/Controllers/PrincipalsController.cs` (lines 278-289)

- [ ] **Step 1: Replace the blunt `platformOwnerExists` check with caller-specific idempotency**

Replace lines 278-289 in `PrincipalsController.cs`:

```csharp
        var iamDb = HttpContext.RequestServices.GetRequiredService<IAMDbContext>();
        var platformOwnerExists = await (
            from b in iamDb.PrincipalRoleBindings
            join p in iamDb.Principals on b.PrincipalId equals p.PrincipalId
            where b.RoleId == "roles.platform.owner" && p.PrincipalType == "user"
            select b.BindingId
        ).AnyAsync(cancellationToken);

        if (platformOwnerExists)
        {
            return BadRequest(new { error = "System is already bootstrapped." });
        }
```

with:

```csharp
        var iamDb = HttpContext.RequestServices.GetRequiredService<IAMDbContext>();

        // Resolve caller identity first for idempotency check
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(new { error = "Valid principal ID not found in token." });
        }

        Guid.TryParse(userIdClaim, out var callerPrincipalId);
        var callerEmail = User.FindFirst("email")?.Value;

        // Check if this specific caller already has platform.owner (idempotent success)
        if (callerPrincipalId != Guid.Empty)
        {
            var callerAlreadyOwner = await iamDb.PrincipalRoleBindings
                .AnyAsync(b => b.PrincipalId == callerPrincipalId && b.RoleId == "roles.platform.owner", cancellationToken);
            if (callerAlreadyOwner)
            {
                return Ok(new { message = "Already promoted to IAM Administrator." });
            }
        }
        else if (!string.IsNullOrEmpty(callerEmail))
        {
            var callerAlreadyOwner = await (
                from b in iamDb.PrincipalRoleBindings
                join p in iamDb.Principals on b.PrincipalId equals p.PrincipalId
                where b.RoleId == "roles.platform.owner" && p.Email == callerEmail
                select b.BindingId
            ).AnyAsync(cancellationToken);
            if (callerAlreadyOwner)
            {
                return Ok(new { message = "Already promoted to IAM Administrator." });
            }
        }

        // Check if another user already owns the platform (prevent hijacking)
        var platformOwnerExists = await (
            from b in iamDb.PrincipalRoleBindings
            join p in iamDb.Principals on b.PrincipalId equals p.PrincipalId
            where b.RoleId == "roles.platform.owner" && p.PrincipalType == "user"
            select b.BindingId
        ).AnyAsync(cancellationToken);

        if (platformOwnerExists)
        {
            return BadRequest(new { error = "System is already bootstrapped." });
        }
```

Also remove the now-duplicate identity extraction block that was at lines 291-300 (the `var userIdClaim = ...` and `Guid.TryParse` lines), since they're now at the top of the method.

- [ ] **Step 2: Build and verify**

Run: `dotnet build` in Maliev.IAMService
Expected: Clean build with no warnings.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "fix: make IAM promote endpoint idempotent for caller"
```

---

### Task 5: IAMService — Reduce Principal Repair Log Noise

**Files:**
- Modify: `Maliev.IAMService/Maliev.IAMService.Application/Services/PrincipalService.cs` (line ~210)

- [ ] **Step 1: Change "Repairing system principal" log from Information to Debug**

In `PrincipalService.cs`, find:

```csharp
_logger.LogInformation("Repairing system principal {PrincipalId}: Granting missing {RoleId}", principalId, ownerRoleId);
```

Replace with:

```csharp
_logger.LogDebug("Repairing system principal {PrincipalId}: Granting missing {RoleId}", principalId, ownerRoleId);
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build` in Maliev.IAMService
Expected: Clean build with no warnings.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "fix: reduce IAM principal repair log level to Debug"
```

---

### Task 6: ServiceDefaults — Reduce Log Noise + EF Core Comment

**Files:**
- Modify: `Maliev.Aspire/Maliev.Aspire.ServiceDefaults/Extensions.cs` (lines 37-64)

- [ ] **Step 1: Add missing log filters and EF Core comment**

In `Extensions.cs`, after line 43 (`ResponseCaching`), add:

```csharp
        builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure", LogLevel.Warning); // Suppress "Executing OkObjectResult" per-request logs
        builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.ModelBinding", LogLevel.Warning); // Suppress model binding details
```

Change line 46 from `LogLevel.Information` to `LogLevel.Warning`:

```csharp
        // Health checks - reduce noise from periodic probes
        builder.Logging.AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService", LogLevel.Warning);
```

Change line 59 from `LogLevel.Information` to `LogLevel.Warning` (MassTransit transport noise):

```csharp
        // MassTransit/RabbitMQ - Warning for transport, Information for message processing
        builder.Logging.AddFilter("MassTransit", LogLevel.Warning);
        builder.Logging.AddFilter("MassTransit.Messages", LogLevel.Information);
```

Add comment about EF Core sensitive data warning (after line 56):

Change:
```csharp
        // EF Core checks __EFMigrationsHistory on fresh DBs and logs a command error that is handled internally
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Critical);
```

to:
```csharp
        // EF Core checks __EFMigrationsHistory on fresh DBs and logs a command error that is handled internally
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Critical);
        // NOTE: "Sensitive data logging is enabled" warnings at startup are expected in Development environment.
        // This is intentional for debugging and should not be flagged as noisy — it helps verify query parameter values locally.
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build` in Maliev.Aspire solution root
Expected: Clean build with no warnings.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "fix: reduce log noise in ServiceDefaults (MVC infrastructure, health checks, MassTransit)"
```

---

### Task 7: PricingService — Add AsNoTracking + CancellationToken

**Files:**
- Modify: `Maliev.PricingService/Maliev.PricingService.Api/Controllers/PricingCatalogController.cs`
- Modify: `Maliev.PricingService/Maliev.PricingService.Application/Services/PricingOrchestrator.cs` (line 82-83)

- [ ] **Step 1: Fix PricingCatalogController — add AsNoTracking + CancellationToken**

Replace the full `PricingCatalogController.cs` class contents with:

```csharp
    /// <summary>Returns all active lead time options.</summary>
    [HttpGet("lead-times")]
    [RequirePermission(PricingPermissions.CatalogRead)]
    public async Task<ActionResult<IEnumerable<LeadTimeOptionResponse>>> GetLeadTimeOptions(CancellationToken cancellationToken)
    {
        var options = await db.LeadTimeOptions
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.SortOrder)
            .Select(o => new LeadTimeOptionResponse(
                o.Code, o.Name, o.MinBusinessDays, o.MaxBusinessDays, o.PriceMultiplier, o.IsDefault))
            .ToListAsync(cancellationToken);

        return Ok(options);
    }

    /// <summary>Returns all active volume discount tiers.</summary>
    [HttpGet("volume-tiers")]
    [RequirePermission(PricingPermissions.CatalogRead)]
    public async Task<ActionResult<IEnumerable<VolumeDiscountTierResponse>>> GetVolumeDiscountTiers(CancellationToken cancellationToken)
    {
        var tiers = await db.VolumeDiscountTiers
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new VolumeDiscountTierResponse(t.MinQuantity, t.MaxQuantity, t.DiscountPercent))
            .ToListAsync(cancellationToken);

        return Ok(tiers);
    }

    /// <summary>
    /// Calculates bulk pricing for a range of quantities given a base unit price.
    /// Applies volume discounts and an optional lead time multiplier.
    /// </summary>
    [HttpPost("bulk-pricing")]
    [RequirePermission(PricingPermissions.CalculationsCreate)]
    public async Task<ActionResult<IEnumerable<BulkPriceTierResponse>>> CalculateBulkPricing(
        [FromBody] BulkPricingRequest request, CancellationToken cancellationToken)
    {
        var tiers = await db.VolumeDiscountTiers
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);

        decimal leadTimeMultiplier = 1.0m;
        if (!string.IsNullOrEmpty(request.LeadTimeCode))
        {
            var lt = await db.LeadTimeOptions
                .AsNoTracking()
                .Where(o => o.IsActive && o.Code == request.LeadTimeCode.ToUpperInvariant())
                .FirstOrDefaultAsync(cancellationToken);
            if (lt is not null) leadTimeMultiplier = lt.PriceMultiplier;
        }

        var results = request.Quantities.Select(qty =>
        {
            var tier = tiers.FirstOrDefault(t =>
                t.MinQuantity <= qty && (t.MaxQuantity == null || t.MaxQuantity >= qty));

            var discountPercent = tier?.DiscountPercent ?? 0m;
            var unitPrice = request.BaseUnitPrice * leadTimeMultiplier * (1 - discountPercent / 100m);
            return new BulkPriceTierResponse(qty, Math.Round(unitPrice, 2), Math.Round(unitPrice * qty, 2), discountPercent);
        }).ToList();

        return Ok(results);
    }
```

Note: Requires `using Microsoft.EntityFrameworkCore;` — check if already present.

- [ ] **Step 2: Fix PricingOrchestrator capacity lookup**

In `PricingOrchestrator.cs`, line 82-83, change:

```csharp
        var capacity = await _context.MachineCapacityConfigs
            .FirstOrDefaultAsync(m => m.ProcessType == request.ManufacturingProcessName && m.IsActive, cancellationToken);
```

to:

```csharp
        var capacity = await _context.MachineCapacityConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProcessType == request.ManufacturingProcessName && m.IsActive, cancellationToken);
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build` in Maliev.PricingService
Expected: Clean build with no warnings.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "perf: add AsNoTracking and CancellationToken to PricingService catalog endpoints"
```

---

### Task 8: MaterialService — Add AsNoTracking + Fix Client-Side Evaluation + AsSplitQuery

**Files:**
- Modify: `Maliev.MaterialService/Maliev.MaterialService.Api/Controllers/ManufacturingCatalogController.cs`
- Modify: `Maliev.MaterialService/Maliev.MaterialService.Infrastructure/Services/MaterialService.cs` (lines 301-315, 380-400)

- [ ] **Step 1: Fix ManufacturingCatalogController — add AsNoTracking + CancellationToken + fix client-side eval**

Replace the full controller class with these methods (imports already include `Microsoft.EntityFrameworkCore`):

```csharp
    /// <summary>Returns all active manufacturing processes.</summary>
    [HttpGet("processes")]
    public async Task<ActionResult<IEnumerable<ProcessCatalogResponse>>> GetProcesses(CancellationToken cancellationToken)
    {
        var processes = await db.ManufacturingProcesses
            .AsNoTracking()
            .Where(p => p.Active)
            .OrderBy(p => p.SortOrder)
            .Select(p => new ProcessCatalogResponse
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                SortOrder = p.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(processes);
    }

    /// <summary>Returns all active materials for the given process code.</summary>
    [HttpGet("processes/{processCode}/materials")]
    public async Task<ActionResult<IEnumerable<MaterialCatalogResponse>>> GetMaterialsByProcess(string processCode, CancellationToken cancellationToken)
    {
        var processId = await db.ManufacturingProcesses
            .AsNoTracking()
            .Where(p => p.Active && p.Code == processCode.ToUpperInvariant())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (processId is null) return NotFound();

        var materials = await db.Materials
            .AsNoTracking()
            .Where(m => m.Active && m.ManufacturingProcesses.Any(p => p.Id == processId))
            .OrderBy(m => m.SortOrder)
            .Select(m => new MaterialCatalogResponse
            {
                Id = m.Id,
                Name = m.Name,
                Code = m.Code,
                Category = m.Category,
                DensityGCm3 = m.DensityGCm3,
                Description = m.Description,
                SortOrder = m.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(materials);
    }

    /// <summary>Returns all active surface finishes for the given process code.</summary>
    [HttpGet("processes/{processCode}/finishes")]
    public async Task<ActionResult<IEnumerable<SurfaceFinishCatalogResponse>>> GetFinishesByProcess(string processCode, CancellationToken cancellationToken)
    {
        var processId = await db.ManufacturingProcesses
            .AsNoTracking()
            .Where(p => p.Active && p.Code == processCode.ToUpperInvariant())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (processId is null) return NotFound();

        var finishes = await db.SurfaceFinishes
            .AsNoTracking()
            .Where(sf => sf.Active && sf.AvailableForProcesses.Any(p => p.Id == processId))
            .OrderBy(sf => sf.SortOrder)
            .Select(sf => new SurfaceFinishCatalogResponse
            {
                Id = sf.Id,
                Name = sf.Name,
                Code = sf.Code,
                RaValueUm = sf.RaValueUm,
                AdditionalCostPercent = sf.AdditionalCostPercent,
                Description = sf.Description,
                SortOrder = sf.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(finishes);
    }

    /// <summary>Returns all active tolerance classes for the given process code.</summary>
    [HttpGet("processes/{processCode}/tolerances")]
    public async Task<ActionResult<IEnumerable<ToleranceClassCatalogResponse>>> GetTolerancesByProcess(string processCode, CancellationToken cancellationToken)
    {
        var processId = await db.ManufacturingProcesses
            .AsNoTracking()
            .Where(p => p.Active && p.Code == processCode.ToUpperInvariant())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (processId is null) return NotFound();

        var tolerances = await db.ToleranceClasses
            .AsNoTracking()
            .Where(tc => tc.Active && tc.AvailableForProcesses.Any(p => p.Id == processId))
            .OrderBy(tc => tc.SortOrder)
            .Select(tc => new ToleranceClassCatalogResponse
            {
                Id = tc.Id,
                Name = tc.Name,
                Code = tc.Code,
                IsoStandard = tc.IsoStandard,
                Grade = tc.Grade,
                ToleranceRange = tc.ToleranceRange,
                AdditionalCostPercent = tc.AdditionalCostPercent,
                SortOrder = tc.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(tolerances);
    }

    /// <summary>Returns all active configuration options for the given process code.</summary>
    [HttpGet("processes/{processCode}/config-options")]
    public async Task<ActionResult<IEnumerable<ProcessConfigOptionCatalogResponse>>> GetConfigOptionsByProcess(string processCode, CancellationToken cancellationToken)
    {
        var processId = await db.ManufacturingProcesses
            .AsNoTracking()
            .Where(p => p.Active && p.Code == processCode.ToUpperInvariant())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (processId is null) return NotFound();

        var options = await db.ProcessConfigOptions
            .AsNoTracking()
            .Where(o => o.Active && o.ManufacturingProcessId == processId)
            .OrderBy(o => o.SortOrder)
            .Select(o => new ProcessConfigOptionCatalogResponse
            {
                Id = o.Id,
                ConfigKey = o.ConfigKey,
                Label = o.Label,
                ConfigType = o.ConfigType,
                DefaultValue = o.DefaultValue,
                OptionsJson = o.OptionsJson,
                Unit = o.Unit,
                HelpText = o.HelpText,
                IsRequired = o.IsRequired,
                SortOrder = o.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(options);
    }

    /// <summary>Returns surface finishes compatible with a specific material.</summary>
    [HttpGet("materials/{materialId:guid}/finishes")]
    public async Task<ActionResult<IEnumerable<SurfaceFinishCatalogResponse>>> GetFinishesByMaterial(Guid materialId, CancellationToken cancellationToken)
    {
        var finishes = await db.SurfaceFinishes
            .AsNoTracking()
            .Where(sf => sf.Active && sf.CompatibleMaterials.Any(m => m.Id == materialId))
            .OrderBy(sf => sf.SortOrder)
            .Select(sf => new SurfaceFinishCatalogResponse
            {
                Id = sf.Id,
                Name = sf.Name,
                Code = sf.Code,
                RaValueUm = sf.RaValueUm,
                AdditionalCostPercent = sf.AdditionalCostPercent,
                Description = sf.Description,
                SortOrder = sf.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(finishes);
    }
```

- [ ] **Step 2: Fix MaterialService.GetAllMaterialsAsync — add AsSplitQuery**

In `MaterialService.cs` (~line 301), change:

```csharp
    public async Task<IEnumerable<MaterialResponse>> GetAllMaterialsAsync()
    {
        var materials = await _context.Materials
            .AsNoTracking()
            .Include(m => m.Supplier)
            .Include(m => m.ManufacturingProcesses)
            .Include(m => m.AvailableColors)
            .Include(m => m.PostProcessingMethods)
            .Include(m => m.MechanicalProperties)
                .ThenInclude(mp => mp.MechanicalProperty)
            .Where(m => m.Active)
            .ToListAsync();

        return materials.Select(m => m.ToMaterialResponse());
    }
```

to:

```csharp
    public async Task<IEnumerable<MaterialResponse>> GetAllMaterialsAsync()
    {
        var materials = await _context.Materials
            .AsNoTracking()
            .Include(m => m.Supplier)
            .Include(m => m.ManufacturingProcesses)
            .Include(m => m.AvailableColors)
            .Include(m => m.PostProcessingMethods)
            .Include(m => m.MechanicalProperties)
                .ThenInclude(mp => mp.MechanicalProperty)
            .Where(m => m.Active)
            .AsSplitQuery()
            .ToListAsync();

        return materials.Select(m => m.ToMaterialResponse());
    }
```

- [ ] **Step 3: Fix MaterialService.GetMaterialsAsync — count before Includes**

In `MaterialService.cs` (~line 385), restructure to count BEFORE adding Includes:

Move the `CountAsync()` call to BEFORE the `.Include()` block. The pattern should be:

```csharp
// Count before adding Includes (more efficient, avoids constructing join pipeline)
var totalCount = await query.CountAsync(cancellationToken);

query = query
    .Include(m => m.Supplier)
    .Include(m => m.ManufacturingProcesses.Where(mp => mp.Active))
    .Include(m => m.AvailableColors.Where(c => c.Active))
    .Include(m => m.PostProcessingMethods.Where(ppm => ppm.Active))
    .Include(m => m.MechanicalProperties.Where(mp => mp.MechanicalProperty.Active))
        .ThenInclude(mp => mp.MechanicalProperty)
    .AsSplitQuery();
```

Remove the original `var totalCount = await query.CountAsync();` line that was after the Includes.

- [ ] **Step 4: Build and verify**

Run: `dotnet build` in Maliev.MaterialService
Expected: Clean build with no warnings.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "perf: fix MaterialService client-side evaluation, add AsNoTracking/AsSplitQuery/CancellationToken"
```

---

### Task 9: Final Build Verification

- [ ] **Step 1: Full solution build**

Run from `Maliev.Aspire` root:

```bash
dotnet build
```

Expected: Clean build, zero warnings (TreatWarningsAsErrors is enabled).

- [ ] **Step 2: Run existing tests**

```bash
dotnet test
```

Expected: All existing tests pass. New endpoints will be tested in a follow-up once the Aspire test suite is updated.

- [ ] **Step 3: Commit any remaining changes**

```bash
git add -A && git commit -m "chore: final verification build"
```
