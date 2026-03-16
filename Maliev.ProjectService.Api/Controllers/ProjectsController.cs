using Maliev.ProjectService.Api.Authorization;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using System.Security.Claims;

namespace Maliev.ProjectService.Api.Controllers;

/// <summary>
/// Manages project lifecycle — from initial file upload through quoting, production, and delivery.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
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
        return Ok(project);
    }

    /// <summary>Returns projects for a specific customer.</summary>
    [HttpGet("customer/{customerId:guid}")]
    [RequirePermission(ProjectPermissions.Projects.Read)]
    [ProducesResponseType(typeof(PaginatedProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedProjectResponse>> GetByCustomer(Guid customerId, CancellationToken ct = default)
    {
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
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        var project = await _projectService.UpdateAsync(id, request, principalId, ct);
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
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var project = await _projectService.GenerateQuotationAsync(id, request, principalId, ct);
        return Ok(project);
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
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var project = await _projectService.MarkQuotationSentAsync(id, principalId, ct);
        return Ok(project);
    }

    /// <summary>
    /// Manually marks a quotation as accepted (used when customer calls or emails acceptance).
    /// Triggers order creation via event publishing.
    /// </summary>
    [HttpPost("{id:guid}/accept-quotation")]
    [RequirePermission(ProjectPermissions.Projects.Accept)]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDetailResponse>> AcceptQuotation(
        Guid id,
        CancellationToken ct = default)
    {
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var project = await _projectService.AcceptQuotationAsync(id, principalId, ct);
        return Ok(project);
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
        var principalId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var principalName = User.FindFirst("name")?.Value ?? "Unknown";
        var note = await _projectService.AddNoteAsync(id, request, principalId, principalName, ct);
        return StatusCode(StatusCodes.Status201Created, note);
    }
}
