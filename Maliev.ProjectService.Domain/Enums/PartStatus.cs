namespace Maliev.ProjectService.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an individual project part.
/// </summary>
public enum PartStatus
{
    /// <summary>File has been uploaded but process/material not yet configured.</summary>
    Uploaded = 1,

    /// <summary>Process, material, and quantity have been configured. Awaiting pricing.</summary>
    Configured = 2,

    /// <summary>AI pricing has been calculated and is awaiting employee review.</summary>
    Priced = 3,

    /// <summary>Employee has confirmed (or overridden) the price. Ready for quotation.</summary>
    Confirmed = 4,

    /// <summary>Part has been included in a generated quotation.</summary>
    Quoted = 5,

    /// <summary>Quotation accepted — order has been created for this part.</summary>
    Ordered = 6,

    /// <summary>Manufacturing job is in progress for this part.</summary>
    InProduction = 7,

    /// <summary>Manufacturing is complete; awaiting QC approval.</summary>
    QualityCheck = 8,

    /// <summary>Part has passed QC and is ready for delivery.</summary>
    Approved = 9,

    /// <summary>Part has been delivered to the customer.</summary>
    Delivered = 10,

    /// <summary>Part was removed from the project before quotation.</summary>
    Removed = 11
}
