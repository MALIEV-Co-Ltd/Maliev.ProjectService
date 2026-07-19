using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;
using System.Collections.Concurrent;

namespace Maliev.ProjectService.Tests.Integration.Fakes;

/// <summary>
/// Fake implementation of IQuotationServiceClient for integration tests.
/// Returns a deterministic quotation creation result without calling the real QuotationService.
/// </summary>
public class FakeQuotationServiceClient : IQuotationServiceClient
{
    private readonly ConcurrentDictionary<Guid, int> _versionsByQuotationId = new();

    /// <inheritdoc />
    public Task<CreatedQuotationResponse> CreateQuotationAsync(CreateQuotationFromProjectRequest request, CancellationToken ct = default)
    {
        var quotationId = Guid.NewGuid();
        _versionsByQuotationId[quotationId] = 1;

        return Task.FromResult(new CreatedQuotationResponse
        {
            QuotationId = quotationId,
            QuotationNumber = $"QUO-TEST-{DateTime.UtcNow:yyyyMMdd}-0001",
            CurrentVersionId = Guid.NewGuid(),
            CurrentVersionNumber = 1,
            Total = request.Items.Sum(item => item.Quantity * item.UnitPrice)
                - Math.Max(0m, request.BulkDiscountAmount)
                - Math.Max(0m, request.ManualDiscountAmount)
                + Math.Max(0m, request.ShippingCost)
                + Math.Max(0m, request.TaxAmount)
        });
    }

    /// <inheritdoc />
    public Task<CreatedQuotationResponse> UpdateQuotationAsync(Guid quotationId, CreateQuotationFromProjectRequest request, CancellationToken ct = default)
    {
        var versionNumber = _versionsByQuotationId.AddOrUpdate(quotationId, 2, (_, current) => current + 1);

        return Task.FromResult(new CreatedQuotationResponse
        {
            QuotationId = quotationId,
            QuotationNumber = $"QUO-TEST-{DateTime.UtcNow:yyyyMMdd}-0001",
            CurrentVersionId = Guid.NewGuid(),
            CurrentVersionNumber = versionNumber,
            Total = request.Items.Sum(item => item.Quantity * item.UnitPrice)
                - Math.Max(0m, request.BulkDiscountAmount)
                - Math.Max(0m, request.ManualDiscountAmount)
                + Math.Max(0m, request.ShippingCost)
                + Math.Max(0m, request.TaxAmount)
        });
    }
}
