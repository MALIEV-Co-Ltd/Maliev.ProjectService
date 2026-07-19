namespace Maliev.ProjectService.Application.Abstractions;

/// <summary>
/// Client abstraction for reading production jobs from JobService.
/// </summary>
public interface IJobServiceClient
{
    /// <summary>Returns production jobs for the specified order.</summary>
    Task<List<ProjectJobReference>> GetJobsForOrderAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// Minimal production job reference used to link project parts after payment.
/// </summary>
public sealed class ProjectJobReference
{
    /// <summary>Gets or sets the production job identifier.</summary>
    public Guid JobId { get; set; }

    /// <summary>Gets or sets the paid order identifier.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Gets or sets the paid order item identifier.</summary>
    public Guid OrderItemId { get; set; }

    /// <summary>Gets or sets the source project part identifier captured on the job.</summary>
    public Guid? SourceProjectPartId { get; set; }
}
