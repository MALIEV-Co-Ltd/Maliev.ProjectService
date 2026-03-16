using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

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
        // Map to QuotationService's expected create request format
        var payload = new
        {
            customerId = request.CustomerId,
            validityPeriodStart = request.ValidityPeriodStart,
            validityPeriodEnd = request.ValidityPeriodEnd,
            deliveryExpectations = request.DeliveryExpectations,
            items = request.Items.Select(i => new
            {
                description = i.Description,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice
            }).ToList(),
            internalNote = request.InternalNote
        };

        var response = await _httpClient.PostAsJsonAsync("/quotation/v1/quotations", payload, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreatedQuotationResponse>(ct);
        if (result is null)
            throw new InvalidOperationException("QuotationService returned an empty response.");

        _logger.LogInformation("Quotation {QuotationNumber} created in QuotationService", result.QuotationNumber);
        return result;
    }
}
