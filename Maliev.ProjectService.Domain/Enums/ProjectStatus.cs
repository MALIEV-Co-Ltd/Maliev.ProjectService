namespace Maliev.ProjectService.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a project engagement.
/// </summary>
public enum ProjectStatus
{
    /// <summary>Project has been created but parts are not yet configured.</summary>
    Draft = 1,

    /// <summary>Parts are being configured, priced, and confirmed by the employee.</summary>
    Configuring = 2,

    /// <summary>Customer has requested employee review before quotation proceeds.</summary>
    CustomerReview = 3,

    /// <summary>A formal quotation has been generated from confirmed parts.</summary>
    QuotationGenerated = 4,

    /// <summary>The quotation has been sent to the customer for review.</summary>
    QuotationSent = 5,

    /// <summary>Customer has accepted the quotation. Orders will be/have been created.</summary>
    QuotationAccepted = 6,

    /// <summary>Production jobs are in progress for one or more parts.</summary>
    InProduction = 7,

    /// <summary>All jobs are complete and awaiting quality check approval.</summary>
    QualityCheck = 8,

    /// <summary>All parts have passed QC and are ready for shipment.</summary>
    ReadyToShip = 9,

    /// <summary>Order has been shipped to the customer.</summary>
    Shipped = 10,

    /// <summary>Customer has confirmed receipt of the delivery.</summary>
    Delivered = 11,

    /// <summary>Invoice has been issued for the completed order.</summary>
    Invoiced = 12,

    /// <summary>Payment has been received. Project lifecycle is complete.</summary>
    Paid = 13,

    /// <summary>All financial and delivery aspects are complete.</summary>
    Completed = 14,

    /// <summary>Project was cancelled before completion.</summary>
    Cancelled = 15
}
