using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.ProjectService.Application.Abstractions;
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

public sealed class JobStatusChangedEventConsumerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
#pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
#pragma warning restore CS0618

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Consume_WhenJobCompleted_MovesPartToQualityCheck()
    {
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await using var db = await CreateDbAsync();
        db.Projects.Add(new Project
        {
            Id = projectId,
            ProjectNumber = "PRJ-2026-QC1",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "QC routing project",
            Status = ProjectStatus.InProduction,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    FileName = "part.stl",
                    OrderId = orderId,
                    JobId = jobId,
                    Status = PartStatus.InProduction
                }
            ]
        });
        await db.SaveChangesAsync();

        var projectServiceMock = new Mock<IProjectService>();
        var logger = new Mock<ILogger<JobStatusChangedEventConsumer>>();
        var consumer = new JobStatusChangedEventConsumer(db, projectServiceMock.Object, logger.Object);

        await consumer.Consume(CreateConsumeContext(CreateJobStatusChangedEvent(jobId, orderId, "InProgress", "Completed")).Object);

        var part = await db.ProjectParts.SingleAsync(p => p.JobId == jobId);
        Assert.Equal(PartStatus.QualityCheck, part.Status);
        projectServiceMock.Verify(
            service => service.UpdateStatusAsync(projectId, ProjectStatus.QualityCheck.ToString(), CancellationToken.None),
            Times.Once);
        projectServiceMock.Verify(
            service => service.UpdateStatusAsync(projectId, ProjectStatus.ReadyToShip.ToString(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<ProjectDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new ProjectDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    private static Mock<ConsumeContext<T>> CreateConsumeContext<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.Setup(c => c.Message).Returns(message);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static JobStatusChangedEvent CreateJobStatusChangedEvent(
        Guid jobId,
        Guid orderId,
        string previousStatus,
        string newStatus)
    {
        return new JobStatusChangedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(JobStatusChangedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "JobService",
            ConsumedBy: ["ProjectService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobStatusChangedEventPayload(
                JobId: jobId,
                OrderId: orderId,
                OrderNumber: $"ORD-{orderId:N}",
                PreviousStatus: previousStatus,
                NewStatus: newStatus,
                Technology: "FDM",
                AssignedMachineId: "FDM-001",
                ChangedAt: DateTimeOffset.UtcNow,
                ChangedBy: "scanner-operator"));
    }
}
