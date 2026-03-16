using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;

namespace Maliev.ProjectService.Tests.Integration.Fakes;

/// <summary>
/// Fake implementation of IPricingServiceClient for integration tests.
/// Returns a deterministic pricing result without calling the real PricingService.
/// </summary>
public class FakePricingServiceClient : IPricingServiceClient
{
    /// <inheritdoc />
    public Task<PricingResultResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken ct = default)
    {
        var unitPrice = 1500m + (request.Geometry?.VolumeCm3 ?? 0m) * 10m;
        return Task.FromResult(new PricingResultResponse
        {
            Strategy = "RuleBased",
            MaterialCost = unitPrice * 0.4m,
            SupportMaterialCost = 50m,
            MachineTimeCost = unitPrice * 0.3m,
            SetupCost = 200m,
            ComplexitySurcharge = 0m,
            SubtotalBeforeMargin = unitPrice * 0.7m,
            MarginAmount = unitPrice * 0.3m,
            TotalUnitPrice = unitPrice,
            TotalPrice = unitPrice * request.Quantity,
            ConfidenceLevel = 0.85m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            PricingStrategyValue = 1
        });
    }
}
