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
public class JobCreatedEventConsumer : IConsumer<JobCreatedEvent>
{
    private readonly ProjectDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<JobCreatedEventConsumer> _logger;

    /// <summary>Initializes a new instance of <see cref="JobCreatedEventConsumer"/>.</summary>
    public JobCreatedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        ILogger<JobCreatedEventConsumer> logger)
    {
        _db = db;
        _projectService = projectService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<JobCreatedEvent> context)
    {
        var payload = context.Message.Payload;
        var ct = context.CancellationToken;

        // Find the unlinked ProjectPart whose order + order-item IDs match
        var part = await _db.ProjectParts
            .FirstOrDefaultAsync(p =>
                p.OrderId == payload.OrderId &&
                p.OrderItemId == payload.OrderItemId &&
                p.JobId == null, ct);

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
}
