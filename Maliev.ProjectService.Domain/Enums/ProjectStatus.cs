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

    /// <summary>A formal quotation has been generated from confirmed parts.</summary>
    QuotationGenerated = 3,

    /// <summary>The quotation has been sent to the customer for review.</summary>
    QuotationSent = 4,

    /// <summary>Customer has accepted the quotation. Orders will be/have been created.</summary>
    QuotationAccepted = 5,

    /// <summary>Production jobs are in progress for one or more parts.</summary>
    InProduction = 6,

    /// <summary>All jobs are complete and awaiting quality check approval.</summary>
    QualityCheck = 7,

    /// <summary>All parts have passed QC and are ready for shipment.</summary>
    ReadyToShip = 8,

    /// <summary>Order has been shipped to the customer.</summary>
    Shipped = 9,

    /// <summary>Customer has confirmed receipt of the delivery.</summary>
    Delivered = 10,

    /// <summary>Invoice has been issued for the completed order.</summary>
    Invoiced = 11,

    /// <summary>Payment has been received. Project lifecycle is complete.</summary>
    Paid = 12,

    /// <summary>All financial and delivery aspects are complete.</summary>
    Completed = 13,

    /// <summary>Project was cancelled before completion.</summary>
    Cancelled = 14
}
