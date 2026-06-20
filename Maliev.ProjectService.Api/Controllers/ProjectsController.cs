using Maliev.ProjectService.Api.Authorization;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Asp.Versioning;
using System.Security.Claims;

namespace Maliev.ProjectService.Api.Controllers;

/// <summary>
/// Manages project lifecycle — from initial file upload through quoting, production, and delivery.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("project/v{version:apiVersion}/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    /// <summary>Initializes a new instance of <see cref="ProjectsController"/>.</summary>
    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>Returns a paginated list of projects, optionally filtered.</summary>
    [HttpGet]
    [RequirePermission(ProjectPermissions.Projects.Read)]
    [ProducesResponseType(typeof(PaginatedProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedProjectResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? query = null,
        CancellationToken ct = default)
    {
        if (TryGetCustomerScope(out var scopedCustomerId))
        {
            if (customerId.HasValue && customerId.Value != scopedCustomerId)
            {
                return Forbid();
            }

            customerId = scopedCustomerId;
        }

        var filter = new ProjectFilterRequest
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            CustomerId = customerId,
            Query = query
        };

        var result = await _projectService.SearchAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>Returns the full detail of a single project including all parts and notes.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Read)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailResponse>> GetById(Guid id, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(id, ct);
        if (project is null) return NotFound();
        if (IsOutsideCustomerScope(project.CustomerId)) return Forbid();

        return Ok(project);
    }

    /// <summary>Returns projects for a specific customer.</summary>
    [HttpGet("customer/{customerId:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Read)]
    [ProducesResponseType(typeof(PaginatedProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedProjectResponse>> GetByCustomer(Guid customerId, CancellationToken ct = default)
    {
        if (IsOutsideCustomerScope(customerId)) return Forbid();

        var filter = new ProjectFilterRequest { CustomerId = customerId, PageSize = 100 };
        var result = await _projectService.SearchAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>Returns project statistics for the dashboard widget.</summary>
    [HttpGet("stats")]
    [RequirePermission(ProjectPermissions.Projects.Read)]
    [ProducesResponseType(typeof(ProjectStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectStatsResponse>> GetStats(CancellationToken ct = default)
    {
        if (TryGetCustomerScope(out _)) return Forbid();

        var stats = await _projectService.GetStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>Creates a new project for a customer engagement.</summary>
    [HttpPost]
    [RequirePermission(ProjectPermissions.Projects.Create)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDetailResponse>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken ct = default)
    {
        if (IsOutsideCustomerScope(request.CustomerId)) return Forbid();

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";
        var principalName = User.FindFirst("name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";

        var project = await _projectService.CreateAsync(request, principalId, principalName, ct);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>Updates project metadata (title, description).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailResponse>> Update(
        Guid id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        var project = await _projectService.UpdateAsync(id, request, principalId, ct);
        return Ok(project);
    }

    /// <summary>Pins a project for quick customer access.</summary>
    [HttpPost("{id:guid}/pin")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailResponse>> Pin(Guid id, CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        var project = await _projectService.SetPinnedAsync(id, isPinned: true, principalId, ct);
        return Ok(project);
    }

    /// <summary>Removes a project from pinned quick access.</summary>
    [HttpDelete("{id:guid}/pin")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailResponse>> Unpin(Guid id, CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        var project = await _projectService.SetPinnedAsync(id, isPinned: false, principalId, ct);
        return Ok(project);
    }

    /// <summary>Archives a project from active customer views.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailResponse>> Archive(Guid id, CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        var project = await _projectService.SetArchivedAsync(id, isArchived: true, principalId, ct);
        return Ok(project);
    }

    /// <summary>Soft-deletes a project. Only Draft status projects may be deleted.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        await _projectService.DeleteAsync(id, principalId, ct);
        return NoContent();
    }

    // ─── Part Management ─────────────────────────────────────────────────────────

    /// <summary>Adds a new part (file) to a project.</summary>
    [HttpPost("{id:guid}/parts")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectPartResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectPartResponse>> AddPart(
        Guid id,
        [FromBody] AddProjectPartRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var part = await _projectService.AddPartAsync(id, request, principalId, ct);
        return StatusCode(StatusCodes.Status201Created, part);
    }

    /// <summary>Updates the configuration of an existing part.</summary>
    [HttpPut("{id:guid}/parts/{partId:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectPartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectPartResponse>> UpdatePart(
        Guid id,
        Guid partId,
        [FromBody] UpdateProjectPartRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var part = await _projectService.UpdatePartAsync(id, partId, request, principalId, ct);
        return Ok(part);
    }

    /// <summary>Removes a part from a project (soft-remove, marks as Removed).</summary>
    [HttpDelete("{id:guid}/parts/{partId:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemovePart(Guid id, Guid partId, CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        await _projectService.RemovePartAsync(id, partId, principalId, ct);
        return NoContent();
    }

    /// <summary>
    /// Requests AI pricing for a specific part.
    /// Calls PricingService synchronously and stores the result.
    /// </summary>
    [HttpPost("{id:guid}/parts/{partId:guid}/price")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectPartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectPartResponse>> RequestPricing(
        Guid id,
        Guid partId,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var part = await _projectService.RequestPricingAsync(id, partId, principalId, ct);
        return Ok(part);
    }

    /// <summary>Employee confirms or overrides the AI-suggested price for a part.</summary>
    [HttpPost("{id:guid}/parts/{partId:guid}/confirm-price")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectPartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectPartResponse>> ConfirmPrice(
        Guid id,
        Guid partId,
        [FromBody] ConfirmPartPriceRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var part = await _projectService.ConfirmPriceAsync(id, partId, request, principalId, ct);
        return Ok(part);
    }

    // ─── Quotation Lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates a formal quotation from all confirmed parts.
    /// All parts must be in Confirmed status before calling this.
    /// </summary>
    [HttpPost("{id:guid}/generate-quotation")]
    [RequirePermission(ProjectPermissions.Projects.Quote)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectDetailResponse>> GenerateQuotation(
        Guid id,
        [FromBody] GenerateQuotationRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        try
        {
            var project = await _projectService.GenerateQuotationAsync(id, request, principalId, ct);
            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Project {ProjectId} quotation generation failed validation.", id);
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Project {ProjectId} quotation generation failed while calling a downstream service.", id);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Marks the project quotation as sent to the customer.</summary>
    [HttpPost("{id:guid}/send-quotation")]
    [RequirePermission(ProjectPermissions.Projects.Quote)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDetailResponse>> SendQuotation(
        Guid id,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var project = await _projectService.MarkQuotationSentAsync(id, principalId, ct);
        return Ok(project);
    }

    /// <summary>
    /// Manually marks a quotation as accepted (used when customer calls or emails acceptance).
    /// Triggers order creation via event publishing.
    /// </summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="request">Optional expected quotation version for stale-acceptance protection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated project detail.</returns>
    [HttpPost("{id:guid}/accept-quotation")]
    [RequirePermission(ProjectPermissions.Projects.Accept)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDetailResponse>> AcceptQuotation(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AcceptQuotationRequest? request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        try
        {
            var project = await _projectService.AcceptQuotationAsync(id, request ?? new AcceptQuotationRequest(), principalId, ct);
            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Project {ProjectId} quotation acceptance failed validation.", id);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Routes a customer project to employee review and records the customer's note.</summary>
    [HttpPost("{id:guid}/request-review")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDetailResponse>> RequestReview(
        Guid id,
        [FromBody] RequestProjectReviewRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var principalName = User.FindFirst("name")?.Value ?? "Customer";
        try
        {
            var project = await _projectService.RequestCustomerReviewAsync(id, request, principalId, principalName, ct);
            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Links a production job to a project part after JobService creates the job.
    /// </summary>
    [HttpPost("parts/{partId:guid}/job-link")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkPartJob(
        Guid partId,
        [FromBody] LinkProjectPartJobRequest request,
        CancellationToken ct = default)
    {
        if (request.JobId == Guid.Empty)
        {
            return BadRequest("jobId is required.");
        }

        await _projectService.LinkJobAsync(partId, request.JobId, ct);
        return NoContent();
    }

    // ─── Notes ───────────────────────────────────────────────────────────────────

    /// <summary>Adds an internal employee note to a project.</summary>
    [HttpPost("{id:guid}/notes")]
    [RequirePermission(ProjectPermissions.Projects.Update)]
    [ProducesResponseType(typeof(ProjectNoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectNoteResponse>> AddNote(
        Guid id,
        [FromBody] AddProjectNoteRequest request,
        CancellationToken ct = default)
    {
        var scopeResult = await EnsureProjectInCustomerScopeAsync(id, ct);
        if (scopeResult is not null) return scopeResult;

        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var principalName = User.FindFirst("name")?.Value ?? "Unknown";
        var note = await _projectService.AddNoteAsync(id, request, principalId, principalName, ct);
        return StatusCode(StatusCodes.Status201Created, note);
    }

    private async Task<ActionResult?> EnsureProjectInCustomerScopeAsync(Guid projectId, CancellationToken ct)
    {
        if (!TryGetCustomerScope(out var scopedCustomerId))
        {
            return null;
        }

        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
        {
            return NotFound();
        }

        return project.CustomerId == scopedCustomerId ? null : Forbid();
    }

    private bool IsOutsideCustomerScope(Guid customerId) =>
        TryGetCustomerScope(out var scopedCustomerId) && customerId != scopedCustomerId;

    private bool TryGetCustomerScope(out Guid customerId)
    {
        var rawCustomerId = User.FindFirst("customer_id")?.Value
            ?? User.FindFirst("customerId")?.Value;

        return Guid.TryParse(rawCustomerId, out customerId);
    }
}
