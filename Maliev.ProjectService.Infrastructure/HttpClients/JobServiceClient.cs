using System.Net.Http.Json;
using Maliev.ProjectService.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.HttpClients;

/// <summary>
/// HTTP client for reading production jobs from JobService.
/// </summary>
public class JobServiceClient : IJobServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobServiceClient> _logger;

    /// <summary>Initializes a new instance of <see cref="JobServiceClient"/>.</summary>
    public JobServiceClient(HttpClient httpClient, ILogger<JobServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ProjectJobReference>> GetJobsForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/job/v1/jobs?page=1&pageSize=100", ct);
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<JobPageResponse>(ct);
        var jobs = page?.Items?
            .Where(job => job.OrderId == orderId)
            .Select(job => new ProjectJobReference
            {
                JobId = job.JobId,
                OrderId = job.OrderId,
                OrderItemId = job.OrderItemId,
                SourceProjectPartId = job.SourceProjectPartId
            })
            .ToList() ?? [];

        _logger.LogDebug("Resolved {Count} production jobs for OrderId {OrderId}", jobs.Count, orderId);
        return jobs;
    }

    private sealed class JobPageResponse
    {
        public List<JobResponse> Items { get; set; } = [];
    }

    private sealed class JobResponse
    {
        public Guid JobId { get; set; }

        public Guid OrderId { get; set; }

        public Guid OrderItemId { get; set; }

        public Guid? SourceProjectPartId { get; set; }
    }
}
