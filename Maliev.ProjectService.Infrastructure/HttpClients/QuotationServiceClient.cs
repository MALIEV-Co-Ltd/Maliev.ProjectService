using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;

namespace Maliev.ProjectService.Infrastructure.HttpClients;

/// <summary>
/// HTTP client for calling QuotationService to create formal quotations.
/// </summary>
public class QuotationServiceClient : IQuotationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QuotationServiceClient> _logger;

    /// <summary>Initializes a new instance of <see cref="QuotationServiceClient"/>.</summary>
    public QuotationServiceClient(HttpClient httpClient, ILogger<QuotationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreatedQuotationResponse> CreateQuotationAsync(
        CreateQuotationFromProjectRequest request,
        CancellationToken ct = default)
    {
        var payload = new
        {
            customerId = request.CustomerId,
            validityPeriodStart = request.ValidityPeriodStart,
            validityPeriodEnd = request.ValidityPeriodEnd,
            deliveryExpectations = request.DeliveryExpectations,
            discountStructure = request.BulkDiscountAmount > 0m
                ? new
                {
                    discountType = 2,
                    discountValue = request.BulkDiscountAmount,
                    conditions = "Automatic bulk-order savings",
                    authorizationReason = "System-calculated volume pricing discount"
                }
                : null,
            lineItems = request.Items.Select(i => new
            {
                materialServiceId = i.MaterialServiceId,
                quantity = i.Quantity,
                unitOfMeasure = string.IsNullOrWhiteSpace(i.UnitOfMeasure) ? "pcs" : i.UnitOfMeasure,
                unitPrice = i.UnitPrice,
                manufacturingProcess = i.ManufacturingProcess,
                notes = i.Notes ?? i.Description
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync("/quotation/v1/quotations", payload, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QuotationServiceCreateResponse>(ct);
        if (result is null)
            throw new InvalidOperationException("QuotationService returned an empty response.");

        var quotation = new CreatedQuotationResponse
        {
            QuotationId = result.Id,
            QuotationNumber = CreateQuotationNumber(result.Id)
        };

        _logger.LogInformation("Quotation {QuotationNumber} created in QuotationService", quotation.QuotationNumber);
        return quotation;
    }

    private static string CreateQuotationNumber(Guid id) =>
        $"Q-{id.ToString("N")[..8].ToUpperInvariant()}";

    private sealed class QuotationServiceCreateResponse
    {
        public Guid Id { get; set; }
    }
}
