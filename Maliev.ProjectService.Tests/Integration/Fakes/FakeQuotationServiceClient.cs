using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;

namespace Maliev.ProjectService.Tests.Integration.Fakes;

/// <summary>
/// Fake implementation of IQuotationServiceClient for integration tests.
/// Returns a deterministic quotation creation result without calling the real QuotationService.
/// </summary>
public class FakeQuotationServiceClient : IQuotationServiceClient
{
    /// <inheritdoc />
    public Task<CreatedQuotationResponse> CreateQuotationAsync(CreateQuotationFromProjectRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new CreatedQuotationResponse
        {
            QuotationId = Guid.NewGuid(),
            QuotationNumber = $"QUO-TEST-{DateTime.UtcNow:yyyyMMdd}-0001"
        });
    }
}
