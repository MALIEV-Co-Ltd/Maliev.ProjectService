using Maliev.ProjectService.Domain.Enums;

namespace Maliev.ProjectService.Domain.Entities;

/// <summary>
/// Represents an individual part (file) within a project.
/// Each part corresponds to one 3D file to be manufactured, with its own process configuration and pricing.
/// </summary>
public class ProjectPart
{
    /// <summary>Unique identifier for this part.</summary>
    public Guid Id { get; set; }

    /// <summary>Parent project ID.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Sequential part number within the project (1-based, e.g., 1, 2, 3).</summary>
    public int PartNumber { get; set; }

    /// <summary>Original filename uploaded by the employee.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Reference to the file in UploadService. Null if not yet uploaded.</summary>
    public Guid? FileId { get; set; }

    /// <summary>GCS storage path for the file. Null if not yet uploaded.</summary>
    public string? FileReference { get; set; }

    /// <summary>URL to the thumbnail image for display. Null if not yet generated.</summary>
    public string? ThumbnailUrl { get; set; }

    // --- Manufacturing Configuration ---

    /// <summary>Selected manufacturing process for this part.</summary>
    public ManufacturingProcess ProcessType { get; set; }

    /// <summary>Reference to the selected material in MaterialService. Null if not yet selected.</summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Denormalized material name for display.</summary>
    public string? MaterialName { get; set; }

    /// <summary>Material SKU code used by PricingService for calculation.</summary>
    public string? MaterialCode { get; set; }

    /// <summary>Number of units to manufacture.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Surface finish type (e.g., "Standard", "Polished", "Anodized", "Painted").</summary>
    public string? FinishType { get; set; }

    /// <summary>Colour specification (e.g., "RAL 9003 Signal White", "Natural").</summary>
    public string? Color { get; set; }

    /// <summary>Dimensional tolerance specification (e.g., "+/- 0.1mm", "ISO 2768-m").</summary>
    public string? Tolerance { get; set; }

    /// <summary>Any thread inserts or special fastener requirements.</summary>
    public string? ThreadsInserts { get; set; }

    /// <summary>Additional employee notes for this part (e.g., "customer confirmed orientation").</summary>
    public string? CustomNotes { get; set; }

    // --- Geometry Metrics (from GeometryService) ---

    /// <summary>Part volume in cubic centimetres. Used by PricingService.</summary>
    public decimal? VolumeCm3 { get; set; }

    /// <summary>Support material volume in cubic centimetres.</summary>
    public decimal? SupportVolumeCm3 { get; set; }

    /// <summary>Surface area in square centimetres.</summary>
    public decimal? SurfaceAreaCm2 { get; set; }

    /// <summary>Bounding box X dimension in millimetres.</summary>
    public decimal? BoundingBoxX { get; set; }

    /// <summary>Bounding box Y dimension in millimetres.</summary>
    public decimal? BoundingBoxY { get; set; }

    /// <summary>Bounding box Z dimension in millimetres.</summary>
    public decimal? BoundingBoxZ { get; set; }

    /// <summary>Whether the mesh is manifold (watertight). Used for printability validation.</summary>
    public bool? IsManifold { get; set; }

    // --- Pricing ---

    /// <summary>AI-suggested unit price from PricingService. Null until pricing is requested.</summary>
    public decimal? AiSuggestedPrice { get; set; }

    /// <summary>Employee-confirmed unit price. If null and AiSuggestedPrice is set, AI price is used.</summary>
    public decimal? ConfirmedUnitPrice { get; set; }

    /// <summary>Reason the employee overrode the AI suggested price. Optional.</summary>
    public string? PriceOverrideReason { get; set; }

    /// <summary>AI pricing confidence level (0.0 to 1.0). Null until pricing is requested.</summary>
    public decimal? PricingConfidence { get; set; }

    /// <summary>Pricing strategy used (RuleBased=1, MLEnhanced=2, Manual=3, Hybrid=4).</summary>
    public int? PricingStrategy { get; set; }

    /// <summary>Computed total price for this part (ConfirmedUnitPrice ?? AiSuggestedPrice) * Quantity.</summary>
    public decimal TotalPrice =>
        ((ConfirmedUnitPrice ?? AiSuggestedPrice) ?? 0m) * Quantity;

    // --- Order / Job References (set after quotation acceptance) ---

    /// <summary>Order ID in OrderService. Set when quotation is accepted and orders are created.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Order item ID within the order. Set when order is created.</summary>
    public Guid? OrderItemId { get; set; }

    /// <summary>Job ID in JobService. Set when the manufacturing job is created.</summary>
    public Guid? JobId { get; set; }

    // --- Status ---

    /// <summary>Current lifecycle status of this part.</summary>
    public PartStatus Status { get; set; } = PartStatus.Uploaded;

    // --- Audit ---

    /// <summary>UTC timestamp of part creation (file upload).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of most recent update.</summary>
    public DateTime UpdatedAt { get; set; }

    // --- Navigation ---

    /// <summary>Parent project.</summary>
    public Project? Project { get; set; }
}
