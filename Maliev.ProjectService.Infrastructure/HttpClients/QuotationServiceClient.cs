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
            sourceProjectId = request.SourceProjectId,
            sourceProjectNumber = request.SourceProjectNumber,
            validityPeriodStart = request.ValidityPeriodStart,
            validityPeriodEnd = request.ValidityPeriodEnd,
            deliveryExpectations = request.DeliveryExpectations,
            manualDiscountAmount = request.ManualDiscountAmount,
            shippingCost = request.ShippingCost,
            taxAmount = request.TaxAmount,
            specialTerms = request.QuotationTerms,
            projectSnapshotJson = request.ProjectSnapshotJson,
            projectSnapshotHash = request.ProjectSnapshotHash,
            generatedByDisplayName = request.GeneratedByDisplayName,
            changeSummary = request.ChangeSummary,
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
        await EnsureQuotationServiceSuccessAsync(response, "create quotation", ct);

        var result = await response.Content.ReadFromJsonAsync<QuotationServiceCreateResponse>(ct);
        if (result is null)
            throw new InvalidOperationException("QuotationService returned an empty response.");

        var quotation = new CreatedQuotationResponse
        {
            QuotationId = result.Id,
            QuotationNumber = string.IsNullOrWhiteSpace(result.QuotationNumber)
                ? CreateQuotationNumber(result.Id)
                : result.QuotationNumber,
            CurrentVersionId = result.ResolveCurrentVersionId(),
            CurrentVersionNumber = result.CurrentVersionNumber,
            Total = result.Total
        };

        _logger.LogInformation("Quotation {QuotationNumber} created in QuotationService", quotation.QuotationNumber);
        return quotation;
    }

    /// <inheritdoc />
    public async Task<CreatedQuotationResponse> UpdateQuotationAsync(
        Guid quotationId,
        CreateQuotationFromProjectRequest request,
        CancellationToken ct = default)
    {
        var payload = new
        {
            deliveryExpectations = request.DeliveryExpectations,
            manualDiscountAmount = request.ManualDiscountAmount,
            shippingCost = request.ShippingCost,
            taxAmount = request.TaxAmount,
            specialTerms = request.QuotationTerms,
            projectSnapshotJson = request.ProjectSnapshotJson,
            projectSnapshotHash = request.ProjectSnapshotHash,
            generatedByDisplayName = request.GeneratedByDisplayName,
            changeSummary = string.IsNullOrWhiteSpace(request.ChangeSummary)
                ? "Regenerated quotation from project state"
                : request.ChangeSummary,
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

        var response = await _httpClient.PutAsJsonAsync($"/quotation/v1/quotations/{quotationId}", payload, ct);
        await EnsureQuotationServiceSuccessAsync(response, "update quotation", ct);

        var result = await response.Content.ReadFromJsonAsync<QuotationServiceCreateResponse>(ct);
        if (result is null)
            throw new InvalidOperationException("QuotationService returned an empty response.");

        var quotation = new CreatedQuotationResponse
        {
            QuotationId = result.Id,
            QuotationNumber = string.IsNullOrWhiteSpace(result.QuotationNumber)
                ? CreateQuotationNumber(result.Id)
                : result.QuotationNumber,
            CurrentVersionId = result.ResolveCurrentVersionId(),
            CurrentVersionNumber = result.CurrentVersionNumber,
            Total = result.Total
        };

        _logger.LogInformation(
            "Quotation {QuotationNumber} updated in QuotationService with version {VersionNumber}",
            quotation.QuotationNumber,
            quotation.CurrentVersionNumber);

        return quotation;
    }

    private static string CreateQuotationNumber(Guid id) =>
        $"Q-{id.ToString("N")[..8].ToUpperInvariant()}";

    private static async Task EnsureQuotationServiceSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var message = string.IsNullOrWhiteSpace(responseBody)
            ? $"QuotationService failed to {operation} with HTTP {(int)response.StatusCode} {response.StatusCode}."
            : $"QuotationService failed to {operation} with HTTP {(int)response.StatusCode} {response.StatusCode}: {responseBody}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private sealed class QuotationServiceCreateResponse
    {
        public Guid Id { get; set; }

        public string? QuotationNumber { get; set; }

        public int? CurrentVersionNumber { get; set; }

        public decimal Total { get; set; }

        public List<QuotationServiceVersionResponse> Versions { get; set; } = [];

        public Guid? ResolveCurrentVersionId()
        {
            if (!CurrentVersionNumber.HasValue)
            {
                return Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .FirstOrDefault()
                    ?.Id;
            }

            return Versions
                .FirstOrDefault(version => version.VersionNumber == CurrentVersionNumber.Value)
                ?.Id;
        }
    }

    private sealed class QuotationServiceVersionResponse
    {
        public Guid Id { get; set; }

        public int VersionNumber { get; set; }
    }
}
