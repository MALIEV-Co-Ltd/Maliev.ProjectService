using MassTransit;
using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Infrastructure.Persistence;
using Maliev.ProjectService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="JobStatusChangedEvent"/> from JobService.
/// Updates the corresponding project part's status to reflect production progress.
/// When all parts reach a terminal status, the project status is advanced accordingly.
/// </summary>
public class JobStatusChangedEventConsumer : IConsumer<JobStatusChangedEvent>
{
    private readonly ProjectDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<JobStatusChangedEventConsumer> _logger;

    /// <summary>Initializes a new instance of <see cref="JobStatusChangedEventConsumer"/>.</summary>
    public JobStatusChangedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        ILogger<JobStatusChangedEventConsumer> logger)
    {
        _db = db;
        _projectService = projectService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<JobStatusChangedEvent> context)
    {
        var payload = context.Message.Payload;
        _logger.LogDebug("Received JobStatusChanged: Job {JobId} -> {NewStatus}", payload.JobId, payload.NewStatus);

        // Find the part linked to this job
        var part = await _db.ProjectParts
            .FirstOrDefaultAsync(p => p.JobId == payload.JobId);

        if (part is null)
        {
            _logger.LogDebug("No project part linked to job {JobId}", payload.JobId);
            return;
        }

        // Map job status to part status
        var newPartStatus = payload.NewStatus switch
        {
            "InProgress" or "Printing" or "Machining" or "Processing" => PartStatus.InProduction,
            "QualityCheck" or "Inspection" => PartStatus.QualityCheck,
            "Approved" or "Complete" or "Completed" => PartStatus.Approved,
            _ => part.Status // keep existing status for unknown transitions
        };

        if (newPartStatus == part.Status) return;

        part.Status = newPartStatus;
        part.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);

        // Check if all parts in the project reached the same terminal state
        await TryAdvanceProjectStatusAsync(part.ProjectId, context.CancellationToken);
    }

    private async Task TryAdvanceProjectStatusAsync(Guid projectId, CancellationToken ct)
    {
        var allParts = await _db.ProjectParts
            .Where(p => p.ProjectId == projectId && p.Status != PartStatus.Removed)
            .ToListAsync(ct);

        if (!allParts.Any()) return;

        var allApproved = allParts.All(p => p.Status == PartStatus.Approved || p.Status == PartStatus.Delivered);
        var allInQc = allParts.All(p => p.Status >= PartStatus.QualityCheck);
        var anyInProduction = allParts.Any(p => p.Status == PartStatus.InProduction);

        if (allApproved)
        {
            await _projectService.UpdateStatusAsync(projectId, ProjectStatus.ReadyToShip.ToString(), ct);
            _logger.LogInformation("All parts in project {ProjectId} approved — advancing to ReadyToShip", projectId);
        }
        else if (allInQc && !anyInProduction)
        {
            await _projectService.UpdateStatusAsync(projectId, ProjectStatus.QualityCheck.ToString(), ct);
        }
        else if (anyInProduction)
        {
            await _projectService.UpdateStatusAsync(projectId, ProjectStatus.InProduction.ToString(), ct);
        }
    }
}
