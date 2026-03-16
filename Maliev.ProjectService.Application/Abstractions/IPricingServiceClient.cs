using Maliev.ProjectService.Application.DTOs;

namespace Maliev.ProjectService.Application.Abstractions;

/// <summary>
/// Interface for calling PricingService to calculate part pricing.
/// Implemented in Infrastructure via HttpClient.
/// </summary>
public interface IPricingServiceClient
{
    /// <summary>
    /// Calculates the unit price for a project part based on its geometry and manufacturing configuration.
    /// Direct HTTP call — synchronous response required for real-time employee UX.
    /// </summary>
    Task<PricingResultResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken ct = default);
}
