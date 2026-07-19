using Maliev.MessagingContracts.Contracts.Search;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;
using Maliev.ProjectService.Infrastructure.Consumers;
using Maliev.ProjectService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;

namespace Maliev.ProjectService.Tests.Unit;

/// <summary>
/// Tests ProjectService search reindex publishing.
/// </summary>
public class ProjectSearchReindexRequestedConsumerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
#pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
#pragma warning restore CS0618

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Reindex requests should republish project and active project-part documents.
    /// </summary>
    [Fact]
    public async Task Consume_WithGlobalRequest_PublishesProjectAndPartDocuments()
    {
        await using var db = await CreateDbContextAsync();
        var project = CreateProject();
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var published = new List<SearchDocumentUpsertedEvent>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(It.IsAny<SearchDocumentUpsertedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SearchDocumentUpsertedEvent, CancellationToken>((message, _) => published.Add(message))
            .Returns(Task.CompletedTask);

        var consumer = new SearchReindexRequestedConsumer(
            db,
            publishEndpoint.Object,
            Mock.Of<ILogger<SearchReindexRequestedConsumer>>());

        await consumer.Consume(CreateConsumeContext(null).Object);

        Assert.Equal(2, published.Count);
        Assert.Contains(published, message => message.Payload.ResourceType == "project");
        Assert.Contains(published, message => message.Payload.ResourceType == "project-part" && message.Payload.Title == "d15-16.stp");
        Assert.DoesNotContain(published, message => message.Payload.Title == "removed.stl");
    }

    /// <summary>
    /// Source-scoped reindex requests for another service should be ignored.
    /// </summary>
    [Fact]
    public async Task Consume_WithDifferentSourceService_DoesNotPublishDocuments()
    {
        await using var db = await CreateDbContextAsync();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new SearchReindexRequestedConsumer(
            db,
            publishEndpoint.Object,
            Mock.Of<ILogger<SearchReindexRequestedConsumer>>());

        await consumer.Consume(CreateConsumeContext("CustomerService").Object);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.IsAny<SearchDocumentUpsertedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<ProjectDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new ProjectDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    private static Mock<ConsumeContext<SearchReindexRequestedCommand>> CreateConsumeContext(string? sourceService)
    {
        var context = new Mock<ConsumeContext<SearchReindexRequestedCommand>>();
        context.Setup(item => item.Message).Returns(new SearchReindexRequestedCommand(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchReindexRequestedCommand),
            MessageType: MessageType.Command,
            MessageVersion: "1.0.0",
            PublishedBy: "SearchService",
            ConsumedBy: sourceService is null ? ["*"] : [sourceService],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new SearchReindexRequestedCommandPayload(
                SourceService: sourceService,
                RequestedBy: "test",
                RequestedAtUtc: DateTimeOffset.UtcNow)));
        context.Setup(item => item.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static Project CreateProject()
    {
        var projectId = Guid.NewGuid();
        return new Project
        {
            Id = projectId,
            ProjectNumber = "PRJ-2026-0001",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Wanassriwilai Engineering",
            Title = "Project 2026-05-06",
            Status = ProjectStatus.QuotationGenerated,
            Currency = "THB",
            CreatedBy = "employee:nat",
            CreatedByName = "Natthapol Wanasrivila",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    PartNumber = 1,
                    FileName = "d15-16.stp",
                    ProcessType = ManufacturingProcess.FDM,
                    MaterialName = "Polycarbonate",
                    MaterialCode = "PC",
                    Quantity = 12,
                    BoundingBoxX = 38,
                    BoundingBoxY = 22,
                    BoundingBoxZ = 38,
                    Status = PartStatus.Quoted,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    PartNumber = 2,
                    FileName = "removed.stl",
                    ProcessType = ManufacturingProcess.FDM,
                    Status = PartStatus.Removed,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
    }
}
