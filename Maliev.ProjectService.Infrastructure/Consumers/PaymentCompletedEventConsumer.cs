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
public class PaymentCompletedEventConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly ProjectDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<PaymentCompletedEventConsumer> _logger;

    /// <summary>Initializes a new instance of <see cref="PaymentCompletedEventConsumer"/>.</summary>
    public PaymentCompletedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        ILogger<PaymentCompletedEventConsumer> logger)
    {
        _db = db;
        _projectService = projectService;
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
                p.Status != ProjectStatus.Paid &&
                p.Status != ProjectStatus.Completed &&
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

        // UpdateStatusAsync publishes ProjectStatusChangedEvent internally (Stream 3)
        await _projectService.UpdateStatusAsync(project.Id, "Paid", ct);
    }
}
