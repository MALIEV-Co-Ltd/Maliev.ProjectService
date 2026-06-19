using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="JobCreatedEvent"/> published by JobService and links the new job to the
/// matching <c>ProjectPart</c> by calling <see cref="IProjectService.LinkJobAsync"/>.
/// <para>
/// Matching strategy: find a part whose <c>OrderId</c> and <c>OrderItemId</c> match the event
/// payload and whose <c>JobId</c> is still null. This is the first moment ProjectService learns
/// the exact <c>JobId</c> for a part — before this event the part is in <c>Ordered</c> status
/// with no job reference.
/// </para>
/// </summary>
public class ProjectJobCreatedEventConsumer : IConsumer<JobCreatedEvent>
{
    private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMilliseconds(250);
    private const int DefaultRetryAttempts = 240;

    private readonly ProjectDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectJobCreatedEventConsumer> _logger;
    private readonly int _retryAttempts;
    private readonly TimeSpan _retryInterval;

    /// <summary>Initializes a new instance of <see cref="ProjectJobCreatedEventConsumer"/>.</summary>
    public ProjectJobCreatedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        ILogger<ProjectJobCreatedEventConsumer> logger)
        : this(db, projectService, logger, DefaultRetryAttempts, DefaultRetryInterval)
    {
    }

    internal ProjectJobCreatedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        ILogger<ProjectJobCreatedEventConsumer> logger,
        int retryAttempts,
        TimeSpan retryInterval)
    {
        _db = db;
        _projectService = projectService;
        _logger = logger;
        _retryAttempts = Math.Max(0, retryAttempts);
        _retryInterval = retryInterval;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<JobCreatedEvent> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("Ignoring JobCreatedEvent without payload");
            return;
        }

        var ct = context.CancellationToken;

        var part = await FindLinkedPartAsync(payload, ct);

        if (part is null)
        {
            _logger.LogDebug(
                "No unlinked ProjectPart found for OrderId={OrderId} OrderItemId={OrderItemId} " +
                "— may be a standalone job not originating from a project",
                payload.OrderId, payload.OrderItemId);
            return;
        }

        _logger.LogInformation(
            "Linking Job {JobId} ({JobNumber}) to ProjectPart {PartId} in Project {ProjectId}",
            payload.JobId, payload.JobNumber, part.Id, part.ProjectId);

        await _projectService.LinkJobAsync(part.Id, payload.JobId, ct);
    }

    private async Task<Domain.Entities.ProjectPart?> FindLinkedPartAsync(JobCreatedEventPayload payload, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= _retryAttempts; attempt++)
        {
            var part = await _db.ProjectParts
                .FirstOrDefaultAsync(p =>
                    p.OrderId == payload.OrderId &&
                    p.OrderItemId == payload.OrderItemId &&
                    p.JobId == null, ct);

            if (part is not null || attempt == _retryAttempts)
            {
                return part;
            }

            await Task.Delay(_retryInterval, ct);
        }

        return null;
    }
}
