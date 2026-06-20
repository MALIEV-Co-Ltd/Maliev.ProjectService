using Maliev.ProjectService.Application.DTOs;

namespace Maliev.ProjectService.Application.Abstractions;

/// <summary>
/// Application service interface for project lifecycle management.
/// </summary>
public interface IProjectService
{
    /// <summary>Creates a new project for a customer engagement.</summary>
    Task<ProjectDetailResponse> CreateAsync(CreateProjectRequest request, string principalId, string principalName, CancellationToken ct = default);

    /// <summary>Gets a project by ID, including all parts and notes.</summary>
    Task<ProjectDetailResponse?> GetByIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Returns a paginated, filtered list of projects.</summary>
    Task<PaginatedProjectResponse> SearchAsync(ProjectFilterRequest filter, CancellationToken ct = default);

    /// <summary>Updates project metadata (title, description).</summary>
    Task<ProjectDetailResponse> UpdateAsync(Guid projectId, UpdateProjectRequest request, string principalId, CancellationToken ct = default);

    /// <summary>Sets whether a project is pinned for quick customer access.</summary>
    Task<ProjectDetailResponse> SetPinnedAsync(Guid projectId, bool isPinned, string principalId, CancellationToken ct = default);

    /// <summary>Sets whether a project is archived from active customer views.</summary>
    Task<ProjectDetailResponse> SetArchivedAsync(Guid projectId, bool isArchived, string principalId, CancellationToken ct = default);

    /// <summary>Soft-deletes a project. Only Draft projects may be deleted.</summary>
    Task DeleteAsync(Guid projectId, string principalId, CancellationToken ct = default);

    /// <summary>Adds a new part (file) to a project.</summary>
    Task<ProjectPartResponse> AddPartAsync(Guid projectId, AddProjectPartRequest request, string principalId, CancellationToken ct = default);

    /// <summary>Updates the configuration of an existing part.</summary>
    Task<ProjectPartResponse> UpdatePartAsync(Guid projectId, Guid partId, UpdateProjectPartRequest request, string principalId, CancellationToken ct = default);

    /// <summary>Removes a part from a project. Only valid before quotation is generated.</summary>
    Task RemovePartAsync(Guid projectId, Guid partId, string principalId, CancellationToken ct = default);

    /// <summary>Requests AI pricing for a specific part. Calls PricingService synchronously.</summary>
    Task<ProjectPartResponse> RequestPricingAsync(Guid projectId, Guid partId, string principalId, CancellationToken ct = default);

    /// <summary>Employee confirms or overrides the AI-suggested price for a part.</summary>
    Task<ProjectPartResponse> ConfirmPriceAsync(Guid projectId, Guid partId, ConfirmPartPriceRequest request, string principalId, CancellationToken ct = default);

    /// <summary>Generates a formal quotation from all confirmed parts. Calls QuotationService.</summary>
    Task<ProjectDetailResponse> GenerateQuotationAsync(Guid projectId, GenerateQuotationRequest request, string principalId, CancellationToken ct = default);

    /// <summary>Marks the quotation as sent to the customer.</summary>
    Task<ProjectDetailResponse> MarkQuotationSentAsync(Guid projectId, string principalId, CancellationToken ct = default);

    /// <summary>Manually marks quotation as accepted (for employee-assisted acceptance). Triggers order creation event.</summary>
    Task<ProjectDetailResponse> AcceptQuotationAsync(Guid projectId, AcceptQuotationRequest request, string principalId, CancellationToken ct = default);

    /// <summary>Routes a customer project to employee review and records the customer's note.</summary>
    Task<ProjectDetailResponse> RequestCustomerReviewAsync(Guid projectId, RequestProjectReviewRequest request, string principalId, string principalName, CancellationToken ct = default);

    /// <summary>Updates project status (used by event consumers).</summary>
    Task UpdateStatusAsync(Guid projectId, string newStatus, CancellationToken ct = default);

    /// <summary>Stores the order reference on project parts after order creation (called by event consumer).</summary>
    Task LinkOrderAsync(Guid projectId, Guid orderId, IEnumerable<(Guid PartId, Guid OrderItemId)> partLinks, CancellationToken ct = default);

    /// <summary>Stores the job reference on a project part after job creation (called by event consumer).</summary>
    Task LinkJobAsync(Guid partId, Guid jobId, CancellationToken ct = default);

    /// <summary>Adds an internal note to a project.</summary>
    Task<ProjectNoteResponse> AddNoteAsync(Guid projectId, AddProjectNoteRequest request, string principalId, string principalName, CancellationToken ct = default);

    /// <summary>Returns summary statistics for the dashboard widget.</summary>
    Task<ProjectStatsResponse> GetStatsAsync(CancellationToken ct = default);
}
