using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Projects;
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

/// <summary>
/// Unit tests for Phase 1 messaging additions:
/// - Typed publish calls in ProjectManagementService (verified via shape)
/// - ProjectJobCreatedEventConsumer
/// - ProjectPaymentCompletedEventConsumer
/// - ProjectStatusChangedEvent published from UpdateStatusAsync
/// </summary>
public class Phase1MessagingTests : IAsyncLifetime
{
    // ── Shared helpers ────────────────────────────────────────────────────────

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

    /// <summary>Creates a PostgreSQL-backed DbContext for the current test.</summary>
    private async Task<ProjectDbContext> MakeDbAsync()
    {
        var connectionString = _postgres.GetConnectionString();
        var opts = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var db = new ProjectDbContext(opts);
        await db.Database.MigrateAsync();
        return db;
    }

    private static Mock<ConsumeContext<T>> MakeConsumeContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.Setup(c => c.Message).Returns(message);
        ctx.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }

    private static JobCreatedEvent MakeJobCreatedEvent(Guid jobId, Guid orderId, Guid orderItemId) =>
        new(
            MessageId: Guid.NewGuid(),
            MessageName: "JobCreatedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "JobService",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobCreatedEventPayload(
                JobId: jobId,
                OrderId: orderId,
                OrderItemId: orderItemId,
                ProcessType: "FDM",
                JobNumber: "JOB-2026-0001",
                CreatedAt: DateTimeOffset.UtcNow
            )
        );

    private static PaymentCompletedEvent MakePaymentCompletedEvent(Guid orderId, Guid paymentId) =>
        new(
            MessageId: Guid.NewGuid(),
            MessageName: "PaymentCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "PaymentService",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new PaymentCompletedEventPayload(
                OrderId: orderId,
                OrderNumber: "ORD-001",
                CustomerId: Guid.NewGuid().ToString(),
                PaymentId: paymentId,
                Amount: 5000.0,
                Currency: "THB"
            )
        );

    // ── ProjectJobCreatedEventConsumer tests ─────────────────────────────────────────

    [Fact]
    public async Task JobCreatedConsumer_WhenMatchingPartExists_CallsLinkJobAsync()
    {
        var jobId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var partId = Guid.NewGuid();

        await using var db = await MakeDbAsync();

        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-1001",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "Job part project",
            Status = ProjectStatus.QuotationAccepted,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = partId,
                    FileName = "part.stl",
                    OrderId = orderId,
                    OrderItemId = orderItemId,
                    JobId = null,
                    Status = PartStatus.Ordered
                }
            ]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var logger = new Mock<ILogger<ProjectJobCreatedEventConsumer>>();
        var consumer = new ProjectJobCreatedEventConsumer(
            db,
            svcMock.Object,
            logger.Object,
            retryAttempts: 0,
            retryInterval: TimeSpan.Zero);

        var ctx = MakeConsumeContext(MakeJobCreatedEvent(jobId, orderId, orderItemId));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.LinkJobAsync(partId, jobId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task JobCreatedConsumer_WhenNoMatchingPart_DoesNotCallLinkJobAsync()
    {
        var jobId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        await using var db = await MakeDbAsync();
        // No parts in DB

        var svcMock = new Mock<IProjectService>();
        var logger = new Mock<ILogger<ProjectJobCreatedEventConsumer>>();
        var consumer = new ProjectJobCreatedEventConsumer(
            db,
            svcMock.Object,
            logger.Object,
            retryAttempts: 0,
            retryInterval: TimeSpan.Zero);

        var ctx = MakeConsumeContext(MakeJobCreatedEvent(jobId, orderId, orderItemId));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.LinkJobAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JobCreatedConsumer_WhenPartAlreadyLinked_DoesNotCallLinkJobAsync()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var existingJobId = Guid.NewGuid(); // already linked

        await using var db = await MakeDbAsync();
        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-1002",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "Linked job project",
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
                    OrderItemId = orderItemId,
                    JobId = existingJobId, // already has a job
                    Status = PartStatus.InProduction
                }
            ]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var logger = new Mock<ILogger<ProjectJobCreatedEventConsumer>>();
        var consumer = new ProjectJobCreatedEventConsumer(
            db,
            svcMock.Object,
            logger.Object,
            retryAttempts: 0,
            retryInterval: TimeSpan.Zero);

        var ctx = MakeConsumeContext(MakeJobCreatedEvent(Guid.NewGuid(), orderId, orderItemId));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.LinkJobAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JobCreatedConsumer_WhenOrderIdDoesNotMatch_DoesNotCallLinkJobAsync()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        await using var db = await MakeDbAsync();
        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-1003",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "Different order project",
            Status = ProjectStatus.QuotationAccepted,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    FileName = "part.stl",
                    OrderId = Guid.NewGuid(), // different order
                    OrderItemId = orderItemId,
                    JobId = null,
                    Status = PartStatus.Ordered
                }
            ]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var logger = new Mock<ILogger<ProjectJobCreatedEventConsumer>>();
        var consumer = new ProjectJobCreatedEventConsumer(
            db,
            svcMock.Object,
            logger.Object,
            retryAttempts: 0,
            retryInterval: TimeSpan.Zero);

        var ctx = MakeConsumeContext(MakeJobCreatedEvent(Guid.NewGuid(), orderId, orderItemId));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.LinkJobAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JobCreatedConsumer_WhenOrderLinkArrivesAfterJobEvent_RetriesAndCallsLinkJobAsync()
    {
        var jobId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var partId = Guid.NewGuid();

        await using var db = await MakeDbAsync();
        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-1004",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "Racing job project",
            Status = ProjectStatus.QuotationAccepted,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = partId,
                    FileName = "part.stl",
                    OrderId = null,
                    OrderItemId = null,
                    JobId = null,
                    Status = PartStatus.Configured
                }
            ]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var logger = new Mock<ILogger<ProjectJobCreatedEventConsumer>>();
        var consumer = new ProjectJobCreatedEventConsumer(
            db,
            svcMock.Object,
            logger.Object,
            retryAttempts: 20,
            retryInterval: TimeSpan.FromMilliseconds(25));

        var ctx = MakeConsumeContext(MakeJobCreatedEvent(jobId, orderId, orderItemId));
        var consumeTask = consumer.Consume(ctx.Object);

        await Task.Delay(TimeSpan.FromMilliseconds(75));
        await using (var updater = await MakeDbAsync())
        {
            var part = await updater.ProjectParts.SingleAsync(p => p.Id == partId);
            part.OrderId = orderId;
            part.OrderItemId = orderItemId;
            part.Status = PartStatus.Ordered;
            await updater.SaveChangesAsync();
        }

        await consumeTask;

        svcMock.Verify(s => s.LinkJobAsync(partId, jobId, CancellationToken.None), Times.Once);
    }

    // ── ProjectPaymentCompletedEventConsumer tests ───────────────────────────────────

    [Fact]
    public async Task PaymentCompletedConsumer_WhenMatchingProject_CallsUpdateStatusPaid()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using var db = await MakeDbAsync();

        var project = new Project
        {
            Id = projectId,
            ProjectNumber = "PRJ-2026-0001",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "Test Project",
            Status = ProjectStatus.Delivered,
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
                    JobId = Guid.NewGuid(),
                    Status = PartStatus.Delivered
                }
            ]
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var jobClientMock = new Mock<IJobServiceClient>();
        var logger = new Mock<ILogger<ProjectPaymentCompletedEventConsumer>>();
        var consumer = new ProjectPaymentCompletedEventConsumer(db, svcMock.Object, jobClientMock.Object, logger.Object);

        var ctx = MakeConsumeContext(MakePaymentCompletedEvent(orderId, paymentId));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.UpdateStatusAsync(projectId, "Paid", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PaymentCompletedConsumer_WhenProjectAlreadyPaid_DoesNotCallUpdateStatus()
    {
        var orderId = Guid.NewGuid();

        await using var db = await MakeDbAsync();

        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-0002",
            CustomerName = "Corp",
            Title = "Paid Project",
            Status = ProjectStatus.Paid, // already paid
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts = [new ProjectPart { Id = Guid.NewGuid(), FileName = "f.stl", OrderId = orderId, JobId = Guid.NewGuid(), Status = PartStatus.Delivered }]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var jobClientMock = new Mock<IJobServiceClient>();
        var logger = new Mock<ILogger<ProjectPaymentCompletedEventConsumer>>();
        var consumer = new ProjectPaymentCompletedEventConsumer(db, svcMock.Object, jobClientMock.Object, logger.Object);

        var ctx = MakeConsumeContext(MakePaymentCompletedEvent(orderId, Guid.NewGuid()));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentCompletedConsumer_WhenProjectCancelled_DoesNotCallUpdateStatus()
    {
        var orderId = Guid.NewGuid();

        await using var db = await MakeDbAsync();

        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-0003",
            CustomerName = "Corp",
            Title = "Cancelled Project",
            Status = ProjectStatus.Cancelled,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts = [new ProjectPart { Id = Guid.NewGuid(), FileName = "f.stl", OrderId = orderId, Status = PartStatus.Removed }]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var jobClientMock = new Mock<IJobServiceClient>();
        var logger = new Mock<ILogger<ProjectPaymentCompletedEventConsumer>>();
        var consumer = new ProjectPaymentCompletedEventConsumer(db, svcMock.Object, jobClientMock.Object, logger.Object);

        var ctx = MakeConsumeContext(MakePaymentCompletedEvent(orderId, Guid.NewGuid()));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentCompletedConsumer_WhenNoMatchingOrder_DoesNotCallUpdateStatus()
    {
        await using var db = await MakeDbAsync();
        // Empty DB — no projects

        var svcMock = new Mock<IProjectService>();
        var jobClientMock = new Mock<IJobServiceClient>();
        var logger = new Mock<ILogger<ProjectPaymentCompletedEventConsumer>>();
        var consumer = new ProjectPaymentCompletedEventConsumer(db, svcMock.Object, jobClientMock.Object, logger.Object);

        var ctx = MakeConsumeContext(MakePaymentCompletedEvent(Guid.NewGuid(), Guid.NewGuid()));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentCompletedConsumer_WhenJobServiceHasSourcePartJob_CallsLinkJobAsync()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await using var db = await MakeDbAsync();

        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = "PRJ-2026-0004",
            CustomerName = "Corp",
            Title = "Paid Project",
            Status = ProjectStatus.Paid,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts = [new ProjectPart { Id = partId, FileName = "f.stl", OrderId = orderId, OrderItemId = orderItemId, Status = PartStatus.Ordered }]
        });
        await db.SaveChangesAsync();

        var svcMock = new Mock<IProjectService>();
        var jobClientMock = new Mock<IJobServiceClient>();
        jobClientMock
            .Setup(client => client.GetJobsForOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProjectJobReference
                {
                    JobId = jobId,
                    OrderId = orderId,
                    OrderItemId = orderItemId,
                    SourceProjectPartId = partId
                }
            ]);
        var logger = new Mock<ILogger<ProjectPaymentCompletedEventConsumer>>();
        var consumer = new ProjectPaymentCompletedEventConsumer(db, svcMock.Object, jobClientMock.Object, logger.Object);

        var ctx = MakeConsumeContext(MakePaymentCompletedEvent(orderId, Guid.NewGuid()));
        await consumer.Consume(ctx.Object);

        svcMock.Verify(s => s.LinkJobAsync(partId, jobId, CancellationToken.None), Times.Once);
    }

    // ── Typed event record shape tests ────────────────────────────────────────
    // Verify that the typed contract records have the exact fields
    // that ProjectManagementService now populates.

    [Fact]
    public void ProjectCreatedEvent_CanBeConstructed_WithAllRequiredFields()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var evt = new ProjectCreatedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "ProjectCreatedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "ProjectService",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: projectId,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new ProjectCreatedEventPayload(
                ProjectId: projectId,
                ProjectNumber: "PRJ-2026-0001",
                CustomerId: customerId,
                CustomerName: "Acme Corp",
                CreatedBy: "user-abc",
                CreatedAt: DateTimeOffset.UtcNow
            )
        );

        Assert.Equal(projectId, evt.Payload.ProjectId);
        Assert.Equal(customerId, evt.Payload.CustomerId);
        Assert.Equal("Acme Corp", evt.Payload.CustomerName);
        Assert.Equal("ProjectService", evt.PublishedBy);
        Assert.Equal(MessageType.Event, evt.MessageType);
    }

    [Fact]
    public void ProjectQuotationAcceptedEvent_Parts_MapCorrectly()
    {
        var partId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        var evt = new ProjectQuotationAcceptedEvent(
            MessageId: Guid.NewGuid(), MessageName: "x", MessageType: MessageType.Event,
            MessageVersion: "1.0.0", PublishedBy: "ProjectService", ConsumedBy: Array.Empty<string>(),
            CorrelationId: Guid.NewGuid(), CausationId: null, OccurredAtUtc: DateTimeOffset.UtcNow, IsPublic: false,
            Payload: new ProjectQuotationAcceptedEventPayload(
                ProjectId: Guid.NewGuid(), ProjectNumber: "PRJ-001", QuotationId: null,
                CustomerId: Guid.NewGuid(), Currency: "THB",
                Parts:
                [
                    new ProjectQuotationAcceptedEventPayloadPartsItem(
                        PartId: partId, Description: "bracket.stl — FDM",
                        Quantity: 5, UnitPrice: 275.0, ProcessType: "FDM",
                        MaterialId: materialId, FileId: null)
                ],
                AcceptedAt: DateTimeOffset.UtcNow, AcceptedBy: "user-xyz"
            )
        );

        Assert.Single(evt.Payload.Parts);
        Assert.Equal(partId, evt.Payload.Parts[0].PartId);
        Assert.Equal(5, evt.Payload.Parts[0].Quantity);
        Assert.Equal(275.0, evt.Payload.Parts[0].UnitPrice);
        Assert.Equal(materialId, evt.Payload.Parts[0].MaterialId);
        Assert.Null(evt.Payload.Parts[0].FileId);
    }

    [Fact]
    public void ProjectStatusChangedEvent_CanBeConstructed_WithOldAndNewStatus()
    {
        var projectId = Guid.NewGuid();

        var evt = new ProjectStatusChangedEvent(
            MessageId: Guid.NewGuid(), MessageName: "ProjectStatusChangedEvent",
            MessageType: MessageType.Event, MessageVersion: "1.0.0",
            PublishedBy: "ProjectService", ConsumedBy: Array.Empty<string>(),
            CorrelationId: projectId, CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow, IsPublic: false,
            Payload: new ProjectStatusChangedEventPayload(
                ProjectId: projectId,
                ProjectNumber: "PRJ-2026-0001",
                OldStatus: "Delivered",
                NewStatus: "Paid",
                ChangedAt: DateTimeOffset.UtcNow
            )
        );

        Assert.Equal("Delivered", evt.Payload.OldStatus);
        Assert.Equal("Paid", evt.Payload.NewStatus);
        Assert.Equal(projectId, evt.Payload.ProjectId);
    }

    [Fact]
    public void JobCreatedEvent_CanBeConstructed_WithAllRequiredFields()
    {
        var jobId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var evt = MakeJobCreatedEvent(jobId, orderId, orderItemId);

        Assert.Equal(jobId, evt.Payload.JobId);
        Assert.Equal(orderId, evt.Payload.OrderId);
        Assert.Equal(orderItemId, evt.Payload.OrderItemId);
        Assert.Equal("FDM", evt.Payload.ProcessType);
        Assert.Equal("JOB-2026-0001", evt.Payload.JobNumber);
        Assert.Equal(MessageType.Event, evt.MessageType);
    }

    [Fact]
    public void PaymentCompletedEvent_CanBeConstructed_WithAllRequiredFields()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var evt = MakePaymentCompletedEvent(orderId, paymentId);

        Assert.Equal(orderId, evt.Payload.OrderId);
        Assert.Equal(paymentId, evt.Payload.PaymentId);
        Assert.Equal(5000.0, evt.Payload.Amount);
        Assert.Equal("THB", evt.Payload.Currency);
    }
}
