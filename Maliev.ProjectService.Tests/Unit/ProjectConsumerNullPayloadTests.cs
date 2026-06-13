using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Quotations;
using Maliev.MessagingContracts.Contracts.Search;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Infrastructure.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ProjectService.Tests.Unit;

public sealed class ProjectConsumerNullPayloadTests
{
    private readonly Mock<IProjectService> _projectService = new();

    [Fact]
    public async Task OrderCreatedEventConsumer_WithoutPayload_IsIgnored()
    {
        var consumer = new OrderCreatedEventConsumer(
            null!,
            _projectService.Object,
            Mock.Of<ILogger<OrderCreatedEventConsumer>>());

        await consumer.Consume(CreateContext(new OrderCreatedEvent { Payload = null! }).Object);

        _projectService.Verify(
            service => service.LinkOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<IEnumerable<(Guid PartId, Guid OrderItemId)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JobCreatedEventConsumer_WithoutPayload_IsIgnored()
    {
        var consumer = new JobCreatedEventConsumer(
            null!,
            _projectService.Object,
            Mock.Of<ILogger<JobCreatedEventConsumer>>());

        await consumer.Consume(CreateContext(new JobCreatedEvent { Payload = null! }).Object);

        _projectService.Verify(
            service => service.LinkJobAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JobStatusChangedEventConsumer_WithoutPayload_IsIgnored()
    {
        var consumer = new JobStatusChangedEventConsumer(
            null!,
            _projectService.Object,
            Mock.Of<ILogger<JobStatusChangedEventConsumer>>());

        await consumer.Consume(CreateContext(new JobStatusChangedEvent { Payload = null! }).Object);

        _projectService.Verify(
            service => service.UpdateStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PaymentCompletedEventConsumer_WithoutPayload_IsIgnored()
    {
        var consumer = new PaymentCompletedEventConsumer(
            null!,
            _projectService.Object,
            Mock.Of<ILogger<PaymentCompletedEventConsumer>>());

        await consumer.Consume(CreateContext(new PaymentCompletedEvent { Payload = null! }).Object);

        _projectService.Verify(
            service => service.UpdateStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task QuotationAcceptedEventConsumer_WithoutPayload_IsIgnored()
    {
        var consumer = new QuotationAcceptedEventConsumer(
            _projectService.Object,
            null!,
            Mock.Of<ILogger<QuotationAcceptedEventConsumer>>());

        await consumer.Consume(CreateContext(new QuotationAcceptedEvent { Payload = null! }).Object);

        _projectService.Verify(
            service => service.AcceptQuotationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchReindexRequestedConsumer_WithoutPayload_IsIgnored()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new SearchReindexRequestedConsumer(
            null!,
            publishEndpoint.Object,
            Mock.Of<ILogger<SearchReindexRequestedConsumer>>());

        await consumer.Consume(CreateContext(new SearchReindexRequestedCommand { Payload = null! }).Object);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<ConsumeContext<T>> CreateContext<T>(T message)
        where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.Setup(c => c.Message).Returns(message);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}
