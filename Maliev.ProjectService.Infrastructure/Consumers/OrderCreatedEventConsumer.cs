using MassTransit;
using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="OrderCreatedEvent"/> from OrderService.
/// Links the created order back to the project parts so employees can
/// track order IDs from within the project detail view.
/// </summary>
public class OrderCreatedEventConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ProjectDbContext _db;
    private readonly IProjectService _projectService;
    private readonly ILogger<OrderCreatedEventConsumer> _logger;

    /// <summary>Initializes a new instance of <see cref="OrderCreatedEventConsumer"/>.</summary>
    public OrderCreatedEventConsumer(
        ProjectDbContext db,
        IProjectService projectService,
        ILogger<OrderCreatedEventConsumer> logger)
    {
        _db = db;
        _projectService = projectService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("Ignoring OrderCreatedEvent without payload");
            return;
        }

        _logger.LogInformation("Received OrderCreated for order {OrderId}", payload.OrderId);

        var orderItems = payload.Items?.ToList() ?? [];
        var sourceLinks = orderItems
            .Where(item => item.SourceProjectId.HasValue && item.SourceProjectPartId.HasValue)
            .GroupBy(item => item.SourceProjectId!.Value)
            .ToList();
        foreach (var sourceProject in sourceLinks)
        {
            var sourceMatchedProject = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == sourceProject.Key &&
                    p.CustomerId == payload.CustomerId,
                    context.CancellationToken);
            if (sourceMatchedProject is null)
            {
                continue;
            }

            var sourcePartLinks = sourceProject
                .Select(item => (PartId: item.SourceProjectPartId!.Value, OrderItemId: item.ProductId))
                .ToList();
            if (sourcePartLinks.Count > 0)
            {
                await _projectService.LinkOrderAsync(sourceMatchedProject.Id, payload.OrderId, sourcePartLinks, context.CancellationToken);
                _logger.LogInformation(
                    "Linked order {OrderId} to {Count} source-identified parts in project {ProjectNumber}",
                    payload.OrderId,
                    sourcePartLinks.Count,
                    sourceMatchedProject.ProjectNumber);
                return;
            }
        }

        // Find project parts that are in Ordered/QuotationAccepted status and
        // belong to the customer who placed this order. Match by QuotationId if available.
        var project = await _db.Projects
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p =>
                p.CustomerId == payload.CustomerId &&
                p.QuotationId != null &&
                (p.Status == Domain.Enums.ProjectStatus.QuotationAccepted || p.Status == Domain.Enums.ProjectStatus.InProduction));

        if (project is null)
        {
            _logger.LogDebug("No matching project found for OrderCreated {OrderId} — order may be standalone", payload.OrderId);
            return;
        }

        // Link order to unlinked project parts (match by order item index/description heuristic)
        var unlinkedParts = project.Parts
            .Where(p => p.OrderId == null && p.Status == Domain.Enums.PartStatus.Quoted)
            .OrderBy(p => p.PartNumber)
            .ToList();

        var links = unlinkedParts
            .Zip(orderItems, (part, item) => (PartId: part.Id, OrderItemId: item.ProductId))
            .ToList();

        if (links.Any())
        {
            await _projectService.LinkOrderAsync(project.Id, payload.OrderId, links, context.CancellationToken);
            _logger.LogInformation("Linked order {OrderId} to {Count} parts in project {ProjectNumber}",
                payload.OrderId, links.Count, project.ProjectNumber);
        }
    }
}
