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

    /// <summary>Raw GCS path for the small thumbnail artifact.</summary>
    public string? ThumbnailSmallGcsPath { get; set; }

    /// <summary>Raw GCS path for the large thumbnail artifact.</summary>
    public string? ThumbnailLargeGcsPath { get; set; }

    /// <summary>Raw GCS path for the GLB viewer artifact.</summary>
    public string? GlbStoragePath { get; set; }

    /// <summary>Raw GCS overlay artifact paths keyed by process/category.</summary>
    public Dictionary<string, string> OverlayPaths { get; set; } = [];

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

    /// <summary>CNC surface roughness code.</summary>
    public string? RoughnessCode { get; set; }

    /// <summary>Marking type selected for this part.</summary>
    public string? MarkingType { get; set; }

    /// <summary>Marking text selected for this part.</summary>
    public string? MarkingText { get; set; }

    /// <summary>Whether DFM warnings have been acknowledged.</summary>
    public bool DfmAcknowledged { get; set; }

    /// <summary>Whether DFM warnings were detected for this part.</summary>
    public bool HasDfmWarnings { get; set; }

    /// <summary>Whether this part requires threaded holes.</summary>
    public bool HasThreadedHoles { get; set; }

    /// <summary>Threaded hole specification.</summary>
    public string? ThreadedHoleSpec { get; set; }

    /// <summary>Threaded hole count.</summary>
    public int ThreadedHoleCount { get; set; }

    /// <summary>Whether this part requires inserts.</summary>
    public bool HasInserts { get; set; }

    /// <summary>Insert type selected for this part.</summary>
    public string? InsertType { get; set; }

    /// <summary>Insert count.</summary>
    public int InsertCount { get; set; }

    /// <summary>Whether this part should be individually bagged and tagged.</summary>
    public bool BagAndTag { get; set; } = true;

    /// <summary>Inspection level selected for this part.</summary>
    public string? InspectionLevel { get; set; }

    /// <summary>Requested certificates for this part.</summary>
    public List<string> Certificates { get; set; } = [];

    /// <summary>Drawing attachments for this part.</summary>
    public List<ProjectPartAttachment> DrawingFiles { get; set; } = [];

    /// <summary>Supplementary attachments for this part.</summary>
    public List<ProjectPartAttachment> SupplementaryFiles { get; set; } = [];

    /// <summary>Dynamic process configuration selections.</summary>
    public Dictionary<string, string> ProcessConfig { get; set; } = [];

    /// <summary>Number of mesh bodies detected in the uploaded file.</summary>
    public int? BodyCount { get; set; }

    /// <summary>Serialized body metadata from geometry analysis.</summary>
    public string? BodiesJson { get; set; }

    /// <summary>Selected body index for multi-body files.</summary>
    public int? SelectedBodyIndex { get; set; }

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

/// <summary>
/// Attachment metadata persisted with a project part.
/// </summary>
public class ProjectPartAttachment
{
    /// <summary>UploadService file ID for the attachment.</summary>
    public Guid? FileId { get; set; }

    /// <summary>Original file name for display.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Raw GCS storage path for the attachment.</summary>
    public string? StoragePath { get; set; }

    /// <summary>Signed URL if one has been resolved by a caller.</summary>
    public string? SignedUrl { get; set; }

    /// <summary>Attachment size in bytes.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>MIME content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>UTC upload timestamp.</summary>
    public DateTime? UploadedAt { get; set; }
}
