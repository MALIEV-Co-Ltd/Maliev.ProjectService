using Maliev.ProjectService.Application.DTOs;

namespace Maliev.ProjectService.Application.Abstractions;

/// <summary>
/// Interface for calling QuotationService to create formal quotations from project parts.
/// </summary>
public interface IQuotationServiceClient
{
    /// <summary>
    /// Creates a new quotation in QuotationService from confirmed project parts.
    /// Returns the created quotation ID and number.
    /// </summary>
    Task<CreatedQuotationResponse> CreateQuotationAsync(CreateQuotationFromProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adds a new immutable version to an existing QuotationService quotation.
    /// </summary>
    Task<CreatedQuotationResponse> UpdateQuotationAsync(Guid quotationId, CreateQuotationFromProjectRequest request, CancellationToken ct = default);
}
