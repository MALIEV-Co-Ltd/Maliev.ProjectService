using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Maliev.ProjectService.Infrastructure.HttpClients;

/// <summary>
/// HTTP client for calling PricingService to calculate part pricing.
/// Uses a direct synchronous HTTP call — pricing requires immediate response for UX.
/// </summary>
public class PricingServiceClient : IPricingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PricingServiceClient> _logger;

    /// <summary>Initializes a new instance of <see cref="PricingServiceClient"/>.</summary>
    public PricingServiceClient(HttpClient httpClient, ILogger<PricingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PricingResultResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/pricing/v1/calculate", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PricingResultResponse>(ct);
        if (result is null)
            throw new InvalidOperationException("PricingService returned an empty response.");

        _logger.LogDebug("Pricing calculated: {Price} THB (confidence: {Confidence:P0})",
            result.TotalUnitPrice, result.ConfidenceLevel);

        return result;
    }
}
