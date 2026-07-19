using Maliev.MessagingContracts.Contracts.Search;
using Maliev.ProjectService.Domain.Enums;
using Maliev.ProjectService.Infrastructure.Persistence;
using Maliev.ProjectService.Infrastructure.Search;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Consumers;

/// <summary>
/// Republishes project and project-part search documents when SearchService requests a reindex.
/// </summary>
public class SearchReindexRequestedConsumer : IConsumer<SearchReindexRequestedCommand>
{
    private const string SourceService = "ProjectService";
    private readonly ProjectDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SearchReindexRequestedConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchReindexRequestedConsumer"/> class.
    /// </summary>
    /// <param name="context">Project database context.</param>
    /// <param name="publishEndpoint">MassTransit publish endpoint.</param>
    /// <param name="logger">Logger instance.</param>
    public SearchReindexRequestedConsumer(
        ProjectDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<SearchReindexRequestedConsumer> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<SearchReindexRequestedCommand> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("Ignoring SearchReindexRequestedCommand without payload");
            return;
        }

        if (!ShouldHandle(payload.SourceService))
        {
            return;
        }

        var projects = await _context.Projects
            .AsNoTracking()
            .Include(project => project.Parts.Where(part => part.Status != PartStatus.Removed))
            .AsSplitQuery()
            .ToListAsync(context.CancellationToken);

        var occurredAtUtc = DateTimeOffset.UtcNow;
        var publishedCount = 0;
        foreach (var project in projects)
        {
            foreach (var message in ProjectSearchDocumentMapper.ToUpsertEvents(project, occurredAtUtc))
            {
                await _publishEndpoint.Publish(message, context.CancellationToken);
                publishedCount++;
            }
        }

        _logger.LogInformation("Republished {Count} project search documents", publishedCount);
    }

    private static bool ShouldHandle(string? sourceService)
    {
        return string.IsNullOrWhiteSpace(sourceService) ||
            string.Equals(sourceService, SourceService, StringComparison.OrdinalIgnoreCase);
    }
}
