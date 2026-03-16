using MassTransit;
using Maliev.MessagingContracts.Contracts.Quotations;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Maliev.ProjectService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="QuotationAcceptedEvent"/> from QuotationService.
/// When a quotation is accepted (e.g., via quote.maliev.com), this consumer
/// advances the matching project to QuotationAccepted status and publishes
/// ProjectQuotationAccepted to trigger order creation in OrderService.
/// </summary>
public class QuotationAcceptedEventConsumer : IConsumer<QuotationAcceptedEvent>
{
    private readonly IProjectService _projectService;
    private readonly ProjectDbContext _db;
    private readonly ILogger<QuotationAcceptedEventConsumer> _logger;

    /// <summary>Initializes a new instance of <see cref="QuotationAcceptedEventConsumer"/>.</summary>
    public QuotationAcceptedEventConsumer(
        IProjectService projectService,
        ProjectDbContext db,
        ILogger<QuotationAcceptedEventConsumer> logger)
    {
        _projectService = projectService;
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<QuotationAcceptedEvent> context)
    {
        var payload = context.Message.Payload;
        _logger.LogInformation("Received QuotationAccepted for quotation {QuotationId}", payload.QuotationId);

        // Find the project that owns this quotation
        var project = await _db.Projects
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.QuotationId == payload.QuotationId);

        if (project is null)
        {
            _logger.LogWarning("No project found for quotation {QuotationId} — may be a direct quotation not from a project", payload.QuotationId);
            return;
        }

        // Advance project to accepted state and trigger order creation
        await _projectService.AcceptQuotationAsync(project.Id, "system", context.CancellationToken);

        _logger.LogInformation("Project {ProjectNumber} advanced to QuotationAccepted via event from QuotationService", project.ProjectNumber);
    }
}
