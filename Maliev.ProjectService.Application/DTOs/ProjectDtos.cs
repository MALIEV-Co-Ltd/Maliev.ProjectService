using System.ComponentModel.DataAnnotations;
using Maliev.ProjectService.Domain.Enums;

namespace Maliev.ProjectService.Application.DTOs;

// ─── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Request to create a new project.</summary>
public class CreateProjectRequest
{
    /// <summary>ID of the customer this project is for.</summary>
    [Required]
    public Guid CustomerId { get; set; }

    /// <summary>Denormalized customer name for display.</summary>
    [Required, MaxLength(500)]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Project title / name.</summary>
    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional project description or context.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Currency code. Defaults to THB.</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "THB";

    /// <summary>Source project identifier when creating a duplicate/reorder project.</summary>
    public Guid? SourceProjectId { get; set; }

    /// <summary>Source project number when creating a duplicate/reorder project.</summary>
    [MaxLength(30)]
    public string? SourceProjectNumber { get; set; }
}

/// <summary>Request to update project metadata.</summary>
public class UpdateProjectRequest
{
    /// <summary>Updated project title.</summary>
    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Updated description.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }
}

/// <summary>Filter parameters for project list queries.</summary>
public class ProjectFilterRequest
{
    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Results per page. Maximum 100.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Filter by project status.</summary>
    public string? Status { get; set; }

    /// <summary>Filter by customer ID.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Free-text search against project number, title, and customer name.</summary>
    public string? Query { get; set; }
}

/// <summary>Attachment metadata for project part drawings and supplementary files.</summary>
public class ProjectPartAttachmentDto
{
    /// <summary>UploadService file ID for the attachment.</summary>
    public Guid? FileId { get; set; }

    /// <summary>Original file name for display.</summary>
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Raw GCS storage path for the attachment.</summary>
    [MaxLength(1000)]
    public string? StoragePath { get; set; }

    /// <summary>Signed URL if one has been resolved by a caller.</summary>
    [MaxLength(2000)]
    public string? SignedUrl { get; set; }

    /// <summary>Attachment size in bytes.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>MIME content type.</summary>
    [MaxLength(200)]
    public string? ContentType { get; set; }

    /// <summary>UTC upload timestamp.</summary>
    public DateTime? UploadedAt { get; set; }
}

/// <summary>Request to add a part (file) to a project.</summary>
public class AddProjectPartRequest
{
    /// <summary>Original filename of the uploaded file.</summary>
    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>File ID in UploadService (if already uploaded).</summary>
    public Guid? FileId { get; set; }

    /// <summary>GCS storage path (if already uploaded).</summary>
    [MaxLength(1000)]
    public string? FileReference { get; set; }

    /// <summary>Raw GCS path for the small thumbnail artifact.</summary>
    [MaxLength(1000)]
    public string? ThumbnailSmallGcsPath { get; set; }

    /// <summary>Raw GCS path for the large thumbnail artifact.</summary>
    [MaxLength(1000)]
    public string? ThumbnailLargeGcsPath { get; set; }

    /// <summary>Raw GCS path for the GLB viewer artifact.</summary>
    [MaxLength(1000)]
    public string? GlbStoragePath { get; set; }

    /// <summary>Raw GCS overlay artifact paths keyed by process/category.</summary>
    public Dictionary<string, string> OverlayPaths { get; set; } = [];

    /// <summary>Manufacturing process for this part.</summary>
    public ManufacturingProcess ProcessType { get; set; }

    /// <summary>Material ID from MaterialService.</summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Material name (denormalized).</summary>
    [MaxLength(500)]
    public string? MaterialName { get; set; }

    /// <summary>Material SKU code used for pricing.</summary>
    [MaxLength(100)]
    public string? MaterialCode { get; set; }

    /// <summary>Number of units required.</summary>
    [Range(1, 10000)]
    public int Quantity { get; set; } = 1;

    /// <summary>Surface finish type.</summary>
    [MaxLength(100)]
    public string? FinishType { get; set; }

    /// <summary>Colour specification.</summary>
    [MaxLength(200)]
    public string? Color { get; set; }

    /// <summary>Dimensional tolerance.</summary>
    [MaxLength(100)]
    public string? Tolerance { get; set; }

    /// <summary>CNC surface roughness code.</summary>
    [MaxLength(100)]
    public string? RoughnessCode { get; set; }

    /// <summary>Marking type selected for the part.</summary>
    [MaxLength(100)]
    public string? MarkingType { get; set; }

    /// <summary>Marking text selected for the part.</summary>
    [MaxLength(500)]
    public string? MarkingText { get; set; }

    /// <summary>Whether DFM warnings have been acknowledged.</summary>
    public bool DfmAcknowledged { get; set; }

    /// <summary>Whether DFM warnings were detected for this part.</summary>
    public bool HasDfmWarnings { get; set; }

    /// <summary>Whether this part requires threaded holes.</summary>
    public bool HasThreadedHoles { get; set; }

    /// <summary>Threaded hole specification.</summary>
    [MaxLength(200)]
    public string? ThreadedHoleSpec { get; set; }

    /// <summary>Threaded hole count.</summary>
    [Range(0, 10000)]
    public int ThreadedHoleCount { get; set; }

    /// <summary>Whether this part requires inserts.</summary>
    public bool HasInserts { get; set; }

    /// <summary>Insert type selected for this part.</summary>
    [MaxLength(100)]
    public string? InsertType { get; set; }

    /// <summary>Insert count.</summary>
    [Range(0, 10000)]
    public int InsertCount { get; set; }

    /// <summary>Whether this part should be individually bagged and tagged.</summary>
    public bool BagAndTag { get; set; } = true;

    /// <summary>Inspection level selected for this part.</summary>
    [MaxLength(100)]
    public string? InspectionLevel { get; set; }

    /// <summary>Requested certificates for this part.</summary>
    public List<string> Certificates { get; set; } = [];

    /// <summary>Drawing attachments for this part.</summary>
    public List<ProjectPartAttachmentDto> DrawingFiles { get; set; } = [];

    /// <summary>Supplementary attachments for this part.</summary>
    public List<ProjectPartAttachmentDto> SupplementaryFiles { get; set; } = [];

    /// <summary>Dynamic process configuration selections.</summary>
    public Dictionary<string, string> ProcessConfig { get; set; } = [];

    /// <summary>Number of mesh bodies detected in the uploaded file.</summary>
    public int? BodyCount { get; set; }

    /// <summary>Serialized body metadata from geometry analysis.</summary>
    public string? BodiesJson { get; set; }

    /// <summary>Selected body index for multi-body files.</summary>
    public int? SelectedBodyIndex { get; set; }

    /// <summary>Thread inserts or fastener requirements.</summary>
    [MaxLength(500)]
    public string? ThreadsInserts { get; set; }

    /// <summary>Custom notes for this part.</summary>
    [MaxLength(2000)]
    public string? CustomNotes { get; set; }

    // Geometry metrics (from GeometryService, if already analysed)
    /// <summary>Volume in cm³.</summary>
    public decimal? VolumeCm3 { get; set; }
    /// <summary>Support volume in cm³.</summary>
    public decimal? SupportVolumeCm3 { get; set; }
    /// <summary>Surface area in cm².</summary>
    public decimal? SurfaceAreaCm2 { get; set; }
    /// <summary>Bounding box X in mm.</summary>
    public decimal? BoundingBoxX { get; set; }
    /// <summary>Bounding box Y in mm.</summary>
    public decimal? BoundingBoxY { get; set; }
    /// <summary>Bounding box Z in mm.</summary>
    public decimal? BoundingBoxZ { get; set; }
    /// <summary>Whether the mesh is manifold.</summary>
    public bool? IsManifold { get; set; }
}

/// <summary>Request to update an existing part's configuration.</summary>
public class UpdateProjectPartRequest
{
    /// <summary>Manufacturing process.</summary>
    public ManufacturingProcess? ProcessType { get; set; }

    /// <summary>Material ID.</summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Material name (denormalized).</summary>
    [MaxLength(500)]
    public string? MaterialName { get; set; }

    /// <summary>Material code.</summary>
    [MaxLength(100)]
    public string? MaterialCode { get; set; }

    /// <summary>Number of units.</summary>
    [Range(1, 10000)]
    public int? Quantity { get; set; }

    /// <summary>Finish type.</summary>
    [MaxLength(100)]
    public string? FinishType { get; set; }

    /// <summary>Colour.</summary>
    [MaxLength(200)]
    public string? Color { get; set; }

    /// <summary>Tolerance.</summary>
    [MaxLength(100)]
    public string? Tolerance { get; set; }

    /// <summary>CNC surface roughness code.</summary>
    [MaxLength(100)]
    public string? RoughnessCode { get; set; }

    /// <summary>Marking type selected for the part.</summary>
    [MaxLength(100)]
    public string? MarkingType { get; set; }

    /// <summary>Marking text selected for the part.</summary>
    [MaxLength(500)]
    public string? MarkingText { get; set; }

    /// <summary>Whether DFM warnings have been acknowledged.</summary>
    public bool? DfmAcknowledged { get; set; }

    /// <summary>Whether DFM warnings were detected for this part.</summary>
    public bool? HasDfmWarnings { get; set; }

    /// <summary>Whether this part requires threaded holes.</summary>
    public bool? HasThreadedHoles { get; set; }

    /// <summary>Threaded hole specification.</summary>
    [MaxLength(200)]
    public string? ThreadedHoleSpec { get; set; }

    /// <summary>Threaded hole count.</summary>
    [Range(0, 10000)]
    public int? ThreadedHoleCount { get; set; }

    /// <summary>Whether this part requires inserts.</summary>
    public bool? HasInserts { get; set; }

    /// <summary>Insert type selected for this part.</summary>
    [MaxLength(100)]
    public string? InsertType { get; set; }

    /// <summary>Insert count.</summary>
    [Range(0, 10000)]
    public int? InsertCount { get; set; }

    /// <summary>Whether this part should be individually bagged and tagged.</summary>
    public bool? BagAndTag { get; set; }

    /// <summary>Inspection level selected for this part.</summary>
    [MaxLength(100)]
    public string? InspectionLevel { get; set; }

    /// <summary>Requested certificates for this part.</summary>
    public List<string>? Certificates { get; set; }

    /// <summary>Drawing attachments for this part.</summary>
    public List<ProjectPartAttachmentDto>? DrawingFiles { get; set; }

    /// <summary>Supplementary attachments for this part.</summary>
    public List<ProjectPartAttachmentDto>? SupplementaryFiles { get; set; }

    /// <summary>Dynamic process configuration selections.</summary>
    public Dictionary<string, string>? ProcessConfig { get; set; }

    /// <summary>Raw GCS path for the small thumbnail artifact.</summary>
    [MaxLength(1000)]
    public string? ThumbnailSmallGcsPath { get; set; }

    /// <summary>Raw GCS path for the large thumbnail artifact.</summary>
    [MaxLength(1000)]
    public string? ThumbnailLargeGcsPath { get; set; }

    /// <summary>Raw GCS path for the GLB viewer artifact.</summary>
    [MaxLength(1000)]
    public string? GlbStoragePath { get; set; }

    /// <summary>Raw GCS overlay artifact paths keyed by process/category.</summary>
    public Dictionary<string, string>? OverlayPaths { get; set; }

    /// <summary>Number of mesh bodies detected in the uploaded file.</summary>
    public int? BodyCount { get; set; }

    /// <summary>Serialized body metadata from geometry analysis.</summary>
    public string? BodiesJson { get; set; }

    /// <summary>Selected body index for multi-body files.</summary>
    public int? SelectedBodyIndex { get; set; }

    /// <summary>Thread inserts.</summary>
    [MaxLength(500)]
    public string? ThreadsInserts { get; set; }

    /// <summary>Custom notes.</summary>
    [MaxLength(2000)]
    public string? CustomNotes { get; set; }
}

/// <summary>Request to confirm (or override) the AI suggested price for a part.</summary>
public class ConfirmPartPriceRequest
{
    /// <summary>
    /// Employee-confirmed unit price. If null, the AI-suggested price is accepted as-is.
    /// If set to a different value than AI suggestion, PriceOverrideReason is required.
    /// </summary>
    public decimal? ConfirmedUnitPrice { get; set; }

    /// <summary>Reason for overriding the AI suggested price. Required if ConfirmedUnitPrice differs from AI suggestion.</summary>
    [MaxLength(500)]
    public string? PriceOverrideReason { get; set; }
}

/// <summary>Request to generate a quotation from confirmed project parts.</summary>
public class GenerateQuotationRequest
{
    /// <summary>Number of days from today the quotation is valid.</summary>
    [Range(1, 365)]
    public int ValidityDays { get; set; } = 30;

    /// <summary>Delivery expectations / lead time description.</summary>
    [MaxLength(1000)]
    public string? DeliveryExpectations { get; set; }

    /// <summary>Automatic bulk-order discount amount to display and apply to the quotation.</summary>
    [Range(0, (double)decimal.MaxValue)]
    public decimal BulkDiscountAmount { get; set; }

    /// <summary>Manual discount amount to display and apply to the quotation.</summary>
    [Range(0, (double)decimal.MaxValue)]
    public decimal ManualDiscountAmount { get; set; }

    /// <summary>Shipping or delivery cost to apply to the quotation.</summary>
    [Range(0, (double)decimal.MaxValue)]
    public decimal ShippingCost { get; set; }

    /// <summary>VAT or tax amount calculated for the quotation.</summary>
    [Range(0, (double)decimal.MaxValue)]
    public decimal TaxAmount { get; set; }

    /// <summary>Customer-facing quotation terms shown on generated PDFs.</summary>
    [MaxLength(2000)]
    public string? QuotationTerms { get; set; }

    /// <summary>Human-readable summary of the project changes captured in this quotation version.</summary>
    [MaxLength(1000)]
    public string? ChangeSummary { get; set; }

    /// <summary>Optional idempotency key supplied by BFFs for duplicate submit protection.</summary>
    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }
}

/// <summary>Request to add an internal note to a project.</summary>
public class AddProjectNoteRequest
{
    /// <summary>Note content.</summary>
    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;
}

// ─── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>Summary DTO for project list views.</summary>
public class ProjectSummaryResponse
{
    /// <summary>Project unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable project number.</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>Customer ID.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer display name.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Project title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Current project status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether the project is pinned by the customer for quick access.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Whether the project is archived from active customer views.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Number of parts in this project.</summary>
    public int PartsCount { get; set; }

    /// <summary>Number of confirmed parts.</summary>
    public int ConfirmedPartsCount { get; set; }

    /// <summary>Total estimated price across all confirmed parts.</summary>
    public decimal TotalEstimatedPrice { get; set; }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = "THB";

    /// <summary>Quotation number (if generated).</summary>
    public string? QuotationNumber { get; set; }

    /// <summary>Current quotation version ID (if generated).</summary>
    public Guid? CurrentQuotationVersionId { get; set; }

    /// <summary>Current quotation version number (if generated).</summary>
    public int? CurrentQuotationVersionNumber { get; set; }

    /// <summary>Source project identifier when this project was duplicated for reorder or revision work.</summary>
    public Guid? SourceProjectId { get; set; }

    /// <summary>Source project number when this project was duplicated for reorder or revision work.</summary>
    public string? SourceProjectNumber { get; set; }

    /// <summary>UTC timestamp of creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Created by employee name.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>Small preview records for the first active parts in this project.</summary>
    public List<ProjectPartPreviewResponse> PartPreviews { get; set; } = [];
}

/// <summary>Compact project part preview for project list views.</summary>
public class ProjectPartPreviewResponse
{
    /// <summary>Part unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Sequential part number within the project.</summary>
    public int PartNumber { get; set; }

    /// <summary>Original file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>GCS file reference path.</summary>
    public string? FileReference { get; set; }

    /// <summary>Thumbnail URL for display.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Raw GCS path for the small thumbnail artifact.</summary>
    public string? ThumbnailSmallGcsPath { get; set; }

    /// <summary>Raw GCS path for the large thumbnail artifact.</summary>
    public string? ThumbnailLargeGcsPath { get; set; }

    /// <summary>Manufacturing process name.</summary>
    public string ProcessType { get; set; } = string.Empty;

    /// <summary>Material display name.</summary>
    public string? MaterialName { get; set; }

    /// <summary>Ordered quantity.</summary>
    public int Quantity { get; set; }
}

/// <summary>Detailed DTO for project detail view, including all parts and notes.</summary>
public class ProjectDetailResponse
{
    /// <summary>Project unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable project number.</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>Customer ID.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer display name.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Project title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Project description.</summary>
    public string? Description { get; set; }

    /// <summary>Current project status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether the project is pinned by the customer for quick access.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Whether the project is archived from active customer views.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Quotation ID (if generated).</summary>
    public Guid? QuotationId { get; set; }

    /// <summary>Quotation number (if generated).</summary>
    public string? QuotationNumber { get; set; }

    /// <summary>Current quotation version ID (if generated).</summary>
    public Guid? CurrentQuotationVersionId { get; set; }

    /// <summary>Current quotation version number (if generated).</summary>
    public int? CurrentQuotationVersionNumber { get; set; }

    /// <summary>Source project identifier when this project was duplicated for reorder or revision work.</summary>
    public Guid? SourceProjectId { get; set; }

    /// <summary>Source project number when this project was duplicated for reorder or revision work.</summary>
    public string? SourceProjectNumber { get; set; }

    /// <summary>Total estimated price.</summary>
    public decimal TotalEstimatedPrice { get; set; }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = "THB";

    /// <summary>Quotation validity date.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Created by principal ID.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Created by display name.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>All parts in this project.</summary>
    public List<ProjectPartResponse> Parts { get; set; } = [];

    /// <summary>Internal notes on this project.</summary>
    public List<ProjectNoteResponse> Notes { get; set; } = [];
}

/// <summary>DTO for a single project part, including pricing and status.</summary>
public class ProjectPartResponse
{
    /// <summary>Part unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Parent project ID.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Sequential part number within the project.</summary>
    public int PartNumber { get; set; }

    /// <summary>Original file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>UploadService file ID.</summary>
    public Guid? FileId { get; set; }

    /// <summary>GCS file reference path.</summary>
    public string? FileReference { get; set; }

    /// <summary>Thumbnail URL for 3D preview.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Raw GCS path for the small thumbnail artifact.</summary>
    public string? ThumbnailSmallGcsPath { get; set; }

    /// <summary>Raw GCS path for the large thumbnail artifact.</summary>
    public string? ThumbnailLargeGcsPath { get; set; }

    /// <summary>Raw GCS path for the GLB viewer artifact.</summary>
    public string? GlbStoragePath { get; set; }

    /// <summary>Raw GCS overlay artifact paths keyed by process/category.</summary>
    public Dictionary<string, string> OverlayPaths { get; set; } = [];

    // Configuration
    /// <summary>Manufacturing process name.</summary>
    public string ProcessType { get; set; } = string.Empty;

    /// <summary>Material ID.</summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Material display name.</summary>
    public string? MaterialName { get; set; }

    /// <summary>Material code (SKU).</summary>
    public string? MaterialCode { get; set; }

    /// <summary>Quantity to manufacture.</summary>
    public int Quantity { get; set; }

    /// <summary>Finish type.</summary>
    public string? FinishType { get; set; }

    /// <summary>Colour.</summary>
    public string? Color { get; set; }

    /// <summary>Tolerance.</summary>
    public string? Tolerance { get; set; }

    /// <summary>CNC surface roughness code.</summary>
    public string? RoughnessCode { get; set; }

    /// <summary>Marking type selected for the part.</summary>
    public string? MarkingType { get; set; }

    /// <summary>Marking text selected for the part.</summary>
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
    public List<ProjectPartAttachmentDto> DrawingFiles { get; set; } = [];

    /// <summary>Supplementary attachments for this part.</summary>
    public List<ProjectPartAttachmentDto> SupplementaryFiles { get; set; } = [];

    /// <summary>Dynamic process configuration selections.</summary>
    public Dictionary<string, string> ProcessConfig { get; set; } = [];

    /// <summary>Number of mesh bodies detected in the uploaded file.</summary>
    public int? BodyCount { get; set; }

    /// <summary>Serialized body metadata from geometry analysis.</summary>
    public string? BodiesJson { get; set; }

    /// <summary>Selected body index for multi-body files.</summary>
    public int? SelectedBodyIndex { get; set; }

    /// <summary>Thread inserts.</summary>
    public string? ThreadsInserts { get; set; }

    /// <summary>Custom notes.</summary>
    public string? CustomNotes { get; set; }

    // Geometry
    /// <summary>Volume in cm³.</summary>
    public decimal? VolumeCm3 { get; set; }

    /// <summary>Bounding box dimensions in mm.</summary>
    public decimal? BoundingBoxX { get; set; }

    /// <summary>Bounding box Y in mm.</summary>
    public decimal? BoundingBoxY { get; set; }

    /// <summary>Bounding box Z in mm.</summary>
    public decimal? BoundingBoxZ { get; set; }

    /// <summary>Whether mesh is manifold.</summary>
    public bool? IsManifold { get; set; }

    // Pricing
    /// <summary>AI-suggested unit price.</summary>
    public decimal? AiSuggestedPrice { get; set; }

    /// <summary>Employee-confirmed unit price.</summary>
    public decimal? ConfirmedUnitPrice { get; set; }

    /// <summary>Effective unit price (confirmed ?? AI suggested).</summary>
    public decimal? EffectiveUnitPrice => ConfirmedUnitPrice ?? AiSuggestedPrice;

    /// <summary>Computed total price for this part.</summary>
    public decimal TotalPrice => (EffectiveUnitPrice ?? 0m) * Quantity;

    /// <summary>Price override reason.</summary>
    public string? PriceOverrideReason { get; set; }

    /// <summary>AI confidence level (0-1).</summary>
    public decimal? PricingConfidence { get; set; }

    /// <summary>Pricing strategy used.</summary>
    public int? PricingStrategy { get; set; }

    // Order/Job references
    /// <summary>Order ID (set after quotation acceptance).</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Order item ID.</summary>
    public Guid? OrderItemId { get; set; }

    /// <summary>JobService job ID.</summary>
    public Guid? JobId { get; set; }

    // Status & audit
    /// <summary>Current part status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>DTO for a project internal note.</summary>
public class ProjectNoteResponse
{
    /// <summary>Note unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Project ID.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Author display name.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Author principal ID.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Note content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp of creation.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Customer request to route a project back to an employee review queue.
/// </summary>
public class RequestProjectReviewRequest
{
    /// <summary>Customer-visible reason or question that needs employee review.</summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Request to accept the currently displayed quotation version for a project.
/// </summary>
public class AcceptQuotationRequest
{
    /// <summary>Expected current quotation version identifier.</summary>
    public Guid? ExpectedQuotationVersionId { get; set; }

    /// <summary>Expected current quotation version number.</summary>
    public int? ExpectedQuotationVersionNumber { get; set; }
}

/// <summary>
/// Request to link a production job to a project part.
/// </summary>
public class LinkProjectPartJobRequest
{
    /// <summary>Gets or sets the production job identifier.</summary>
    public Guid JobId { get; set; }
}

/// <summary>Paginated project list response.</summary>
public class PaginatedProjectResponse
{
    /// <summary>List of projects for current page.</summary>
    public List<ProjectSummaryResponse> Data { get; set; } = [];

    /// <summary>Current page number.</summary>
    public int CurrentPage { get; set; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; set; }

    /// <summary>Total number of matching projects.</summary>
    public int TotalCount { get; set; }

    /// <summary>Page size.</summary>
    public int PageSize { get; set; }
}

/// <summary>Dashboard statistics for project widget.</summary>
public class ProjectStatsResponse
{
    /// <summary>Total active projects (not Completed/Cancelled).</summary>
    public int ActiveCount { get; set; }

    /// <summary>Projects currently in Draft or Configuring status.</summary>
    public int ConfiguringCount { get; set; }

    /// <summary>Projects with sent/pending quotations.</summary>
    public int QuotedCount { get; set; }

    /// <summary>Projects currently in production.</summary>
    public int InProductionCount { get; set; }

    /// <summary>Projects completed this month.</summary>
    public int CompletedThisMonth { get; set; }
}

// ─── External Service DTOs ──────────────────────────────────────────────────────

/// <summary>Request to PricingService to calculate part price.</summary>
public class CalculatePriceRequest
{
    /// <summary>File ID in UploadService.</summary>
    public Guid? FileId { get; set; }

    /// <summary>Customer ID (for pricing tier).</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Material ID.</summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Material code (SKU).</summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>Manufacturing process ID (numeric value of ManufacturingProcess enum).</summary>
    public int ManufacturingProcessId { get; set; }

    /// <summary>Manufacturing process name.</summary>
    public string ManufacturingProcessName { get; set; } = string.Empty;

    /// <summary>Number of units.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Geometry metrics.</summary>
    public GeometryMetricsDto? Geometry { get; set; }

    /// <summary>Correlation ID for tracing.</summary>
    public string? CorrelationId { get; set; }
}

/// <summary>Geometry metrics passed to PricingService.</summary>
public class GeometryMetricsDto
{
    /// <summary>Volume in cm³.</summary>
    public decimal VolumeCm3 { get; set; }

    /// <summary>Support volume in cm³.</summary>
    public decimal SupportVolumeCm3 { get; set; }

    /// <summary>Surface area in cm².</summary>
    public decimal SurfaceAreaCm2 { get; set; }

    /// <summary>Bounding box X in mm.</summary>
    public decimal BoundingBoxX { get; set; }

    /// <summary>Bounding box Y in mm.</summary>
    public decimal BoundingBoxY { get; set; }

    /// <summary>Bounding box Z in mm.</summary>
    public decimal BoundingBoxZ { get; set; }

    /// <summary>Whether mesh is manifold.</summary>
    public bool IsManifold { get; set; }

    /// <summary>Triangle count.</summary>
    public int TriangleCount { get; set; }
}

/// <summary>Pricing result from PricingService.</summary>
public class PricingResultResponse
{
    /// <summary>Pricing strategy used.</summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>ML model version.</summary>
    public string? MlModelVersion { get; set; }

    /// <summary>Material cost component.</summary>
    public decimal MaterialCost { get; set; }

    /// <summary>Support material cost component.</summary>
    public decimal SupportMaterialCost { get; set; }

    /// <summary>Machine time cost component.</summary>
    public decimal MachineTimeCost { get; set; }

    /// <summary>Setup cost component.</summary>
    public decimal SetupCost { get; set; }

    /// <summary>Complexity surcharge.</summary>
    public decimal ComplexitySurcharge { get; set; }

    /// <summary>Subtotal before margin.</summary>
    public decimal SubtotalBeforeMargin { get; set; }

    /// <summary>Margin amount.</summary>
    public decimal MarginAmount { get; set; }

    /// <summary>Unit price including margin.</summary>
    public decimal TotalUnitPrice { get; set; }

    /// <summary>Total price for requested quantity.</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>AI confidence level (0-1).</summary>
    public decimal ConfidenceLevel { get; set; }

    /// <summary>Date until which this pricing is valid.</summary>
    public DateTime ValidUntil { get; set; }

    /// <summary>Pricing strategy integer value.</summary>
    public int PricingStrategyValue { get; set; }
}

/// <summary>Request to create a quotation in QuotationService from project parts.</summary>
public class CreateQuotationFromProjectRequest
{
    /// <summary>Customer ID.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Existing QuotationService quotation ID to update with a new immutable version.</summary>
    public Guid? ExistingQuotationId { get; set; }

    /// <summary>Source ProjectService project ID.</summary>
    public Guid SourceProjectId { get; set; }

    /// <summary>Source ProjectService project number.</summary>
    public string SourceProjectNumber { get; set; } = string.Empty;

    /// <summary>Quotation validity start.</summary>
    public DateTime ValidityPeriodStart { get; set; }

    /// <summary>Quotation validity end.</summary>
    public DateTime ValidityPeriodEnd { get; set; }

    /// <summary>Delivery expectations / lead time text.</summary>
    public string? DeliveryExpectations { get; set; }

    /// <summary>Line items from confirmed project parts.</summary>
    public List<QuotationLineItemRequest> Items { get; set; } = [];

    /// <summary>Automatic bulk-order discount amount to display and apply to the quotation.</summary>
    public decimal BulkDiscountAmount { get; set; }

    /// <summary>Manual discount amount to display and apply to the quotation.</summary>
    public decimal ManualDiscountAmount { get; set; }

    /// <summary>Shipping or delivery cost to apply to the quotation.</summary>
    public decimal ShippingCost { get; set; }

    /// <summary>VAT or tax amount calculated for the quotation.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Customer-facing quotation terms shown on generated PDFs.</summary>
    public string? QuotationTerms { get; set; }

    /// <summary>Immutable JSON snapshot of the project state being quoted.</summary>
    public string? ProjectSnapshotJson { get; set; }

    /// <summary>Deterministic hash of the immutable project snapshot.</summary>
    public string? ProjectSnapshotHash { get; set; }

    /// <summary>Human-readable display name for the quote generator.</summary>
    public string? GeneratedByDisplayName { get; set; }

    /// <summary>Human-readable summary of changes captured in this quotation version.</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>Optional idempotency key supplied by BFFs for duplicate submit protection.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Internal note to attach to the quotation.</summary>
    public string? InternalNote { get; set; }
}

/// <summary>A single line item for the quotation.</summary>
public class QuotationLineItemRequest
{
    /// <summary>Part description (typically the filename + process).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>MaterialService material identifier required by QuotationService.</summary>
    public Guid MaterialServiceId { get; set; }

    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }

    /// <summary>Unit of measure for the quoted quantity.</summary>
    public string UnitOfMeasure { get; set; } = "pcs";

    /// <summary>Confirmed unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Manufacturing process selected for this line item.</summary>
    public string? ManufacturingProcess { get; set; }

    /// <summary>Additional quotation notes for this line item.</summary>
    public string? Notes { get; set; }
}

/// <summary>Response after creating a quotation in QuotationService.</summary>
public class CreatedQuotationResponse
{
    /// <summary>Quotation ID.</summary>
    public Guid QuotationId { get; set; }

    /// <summary>Human-readable quotation number.</summary>
    public string QuotationNumber { get; set; } = string.Empty;

    /// <summary>Current quotation version ID.</summary>
    public Guid? CurrentVersionId { get; set; }

    /// <summary>Current quotation version number.</summary>
    public int? CurrentVersionNumber { get; set; }

    /// <summary>Quotation total for the current version.</summary>
    public decimal Total { get; set; }
}
