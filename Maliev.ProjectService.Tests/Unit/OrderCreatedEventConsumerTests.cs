using Maliev.MessagingContracts.Contracts.Orders;
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

public sealed class OrderCreatedEventConsumerTests : IAsyncLifetime
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
    public async Task Consume_WithSourceProjectItems_LinksOrderToMatchingProjectParts()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        await using var db = await CreateDbAsync();
        db.Projects.Add(new Project
        {
            Id = projectId,
            ProjectNumber = "PRJ-2026-ORD1",
            CustomerId = customerId,
            CustomerName = "Make Studio Customer",
            Title = "Make Studio order project",
            Status = ProjectStatus.Draft,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = partId,
                    FileName = "fixture.step",
                    Status = PartStatus.Configured
                }
            ]
        });
        await db.SaveChangesAsync();

        var projectService = new Mock<IProjectService>();
        var consumer = new OrderCreatedEventConsumer(
            db,
            projectService.Object,
            Mock.Of<ILogger<OrderCreatedEventConsumer>>());

        await consumer.Consume(CreateConsumeContext(CreateOrderCreatedEvent(
            orderId,
            customerId,
            projectId,
            partId,
            orderItemId)).Object);

        projectService.Verify(
            service => service.LinkOrderAsync(
                projectId,
                orderId,
                It.Is<IEnumerable<(Guid PartId, Guid OrderItemId)>>(links =>
                    links.Single().PartId == partId &&
                    links.Single().OrderItemId == orderItemId),
                CancellationToken.None),
            Times.Once);
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

    private static OrderCreatedEvent CreateOrderCreatedEvent(
        Guid orderId,
        Guid customerId,
        Guid projectId,
        Guid partId,
        Guid orderItemId)
    {
        return new OrderCreatedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(OrderCreatedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "OrderService",
            ConsumedBy: ["ProjectService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new OrderCreatedEventPayload(
                OrderId: orderId,
                OrderNumber: $"ORD-{orderId:N}",
                CustomerId: customerId,
                TotalAmount: 1000,
                Currency: "THB",
                CreatedAt: DateTimeOffset.UtcNow,
                AssignedEmployeeId: null,
                Items:
                [
                    new OrderCreatedEventPayloadItemsItem(
                        ProductId: orderItemId,
                        ProductCode: "PLA",
                        ProductName: "FDM",
                        SourceProjectId: projectId,
                        SourceProjectPartId: partId,
                        Quantity: 1,
                        UnitPrice: 1000,
                        LineTotal: 1000)
                ]));
    }
}
