using Maliev.ProjectService.Domain.Enums;

namespace Maliev.ProjectService.Domain.Entities;

/// <summary>
/// Aggregate root representing a customer project engagement.
/// A project groups related 3D files (parts), their configurations, pricing, and the resulting quotation and orders.
/// </summary>
public class Project
{
    /// <summary>Unique identifier for the project.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable project number (e.g., "PRJ-2026-0001").</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>Reference to the customer in CustomerService.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Denormalized customer name for display without cross-service lookup.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Employee-assigned title for this project engagement.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional description or context for the project.</summary>
    public string? Description { get; set; }

    /// <summary>Current lifecycle status of the project.</summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>Reference to the generated quotation in QuotationService. Null until generated.</summary>
    public Guid? QuotationId { get; set; }

    /// <summary>Human-readable quotation number. Denormalized for display.</summary>
    public string? QuotationNumber { get; set; }

    /// <summary>Sum of all confirmed part prices. Recomputed when parts are confirmed.</summary>
    public decimal TotalEstimatedPrice { get; set; }

    /// <summary>Currency code (ISO 4217). Defaults to THB.</summary>
    public string Currency { get; set; } = "THB";

    /// <summary>Date until which the quotation is valid. Set when quotation is generated.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Principal ID of the employee who created this project.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Display name of the employee who created this project.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>UTC timestamp of project creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Soft-delete flag. Soft-deleted projects are filtered from queries.</summary>
    public bool IsDeleted { get; set; }

    // --- Navigation properties ---

    /// <summary>Parts (files) belonging to this project.</summary>
    public List<ProjectPart> Parts { get; set; } = [];

    /// <summary>Internal notes added by employees during the project lifecycle.</summary>
    public List<ProjectNote> Notes { get; set; } = [];
}
