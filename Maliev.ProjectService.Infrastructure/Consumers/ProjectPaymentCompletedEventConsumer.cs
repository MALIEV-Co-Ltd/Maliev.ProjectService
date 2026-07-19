using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Domain.Enums;
using Maliev.ProjectService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="PaymentCompletedEvent"/> published by PaymentService and advances the
/// linked project to <c>Paid</c> status.
/// <para>
/// Matching strategy: find a project that has at least one part linked to the payment's
/// <c>OrderId</c> and is not already in a terminal state (Paid, Completed, Cancelled).
/// Publishing <c>ProjectStatusChangedEvent</c> is handled inside
/// <see cref="IProjectService.UpdateStatusAsync"/>.
/// </para>
/// </summary>
public class ProjectPaymentCompletedEventConsumer : IConsumer<PaymentCompletedEvent>
{
    private static readonly TimeSpan JobLinkRetryInterval = TimeSpan.FromMilliseconds(250);
    private const int JobLinkRetryAttempts = 240;

    private readonly ProjectDbContext _db;
    private readonly IProjectService _projectService;
    private readonly IJobServiceClient _jobServiceClient;
    private readonly ILogger<ProjectPaymentCompletedEventConsumer> _logger;

    /// <summary>Initializes a new instance of <see cref="ProjectPaymentCompletedEventConsumer"/>.</summary>
    public ProjectPaymentCompletedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        IJobServiceClient jobServiceClient,
        ILogger<ProjectPaymentCompletedEventConsumer> logger)
    {
        _db = db;
        _projectService = projectService;
        _jobServiceClient = jobServiceClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("Ignoring PaymentCompletedEvent without payload");
            return;
        }

        var ct = context.CancellationToken;

        // Find a project that has a part linked to this order and is not yet paid/complete/cancelled
        var project = await _db.Projects
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p =>
                p.Parts.Any(part => part.OrderId == payload.OrderId) &&
                p.Status != ProjectStatus.Cancelled, ct);

        if (project is null)
        {
            _logger.LogDebug(
                "No eligible Project found for OrderId={OrderId} on PaymentCompleted " +
                "— may be a standalone order or the project is already in a terminal state",
                payload.OrderId);
            return;
        }

        _logger.LogInformation(
            "Advancing Project {ProjectNumber} to Paid " +
            "(OrderId={OrderId}, PaymentId={PaymentId}, Amount={Amount} {Currency})",
            project.ProjectNumber, payload.OrderId, payload.PaymentId,
            payload.Amount, payload.Currency);

        if (project.Status != ProjectStatus.Paid && project.Status != ProjectStatus.Completed)
        {
            // UpdateStatusAsync publishes ProjectStatusChangedEvent internally (Stream 3)
            await _projectService.UpdateStatusAsync(project.Id, "Paid", ct);
        }

        await LinkProductionJobsAsync(payload.OrderId, project, ct);
    }

    private async Task LinkProductionJobsAsync(Guid orderId, Domain.Entities.Project project, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= JobLinkRetryAttempts; attempt++)
        {
            var unlinkedPartIds = project.Parts
                .Where(part => part.OrderId == orderId && part.JobId is null)
                .Select(part => part.Id)
                .ToHashSet();

            if (unlinkedPartIds.Count == 0)
            {
                return;
            }

            List<ProjectJobReference> jobs;
            try
            {
                jobs = await _jobServiceClient.GetJobsForOrderAsync(orderId, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                if (attempt == JobLinkRetryAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not resolve production jobs for Project {ProjectNumber}, OrderId={OrderId}",
                        project.ProjectNumber,
                        orderId);
                    return;
                }

                await Task.Delay(JobLinkRetryInterval, ct);
                continue;
            }
            var linkedCount = 0;

            foreach (var job in jobs)
            {
                if (job.SourceProjectPartId is not { } partId ||
                    !unlinkedPartIds.Contains(partId) ||
                    job.JobId == Guid.Empty)
                {
                    continue;
                }

                await _projectService.LinkJobAsync(partId, job.JobId, ct);
                linkedCount++;
            }

            if (linkedCount > 0 || attempt == JobLinkRetryAttempts)
            {
                if (linkedCount == 0)
                {
                    _logger.LogDebug(
                        "No production jobs found to link for Project {ProjectNumber}, OrderId={OrderId}",
                        project.ProjectNumber,
                        orderId);
                }

                return;
            }

            await Task.Delay(JobLinkRetryInterval, ct);
        }
    }
}
