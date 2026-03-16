using Maliev.ProjectService.Infrastructure.Persistence;
using Maliev.ProjectService.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.ProjectService.Tests.Integration.TestFixtures;

/// <summary>
/// Shared test fixture for ProjectService integration tests.
/// Spins up Testcontainers: PostgreSQL, RabbitMQ, Redis.
/// Registers fake external service clients to isolate from PricingService and QuotationService.
/// </summary>
public class ProjectServiceTestFixture : BaseIntegrationTestFactory<Program, ProjectDbContext>
{
    /// <inheritdoc />
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Replace external HTTP clients with fakes to isolate from downstream services
        services.AddSingleton<Application.Abstractions.IPricingServiceClient, Fakes.FakePricingServiceClient>();
        services.AddSingleton<Application.Abstractions.IQuotationServiceClient, Fakes.FakeQuotationServiceClient>();
    }
}

/// <summary>Collection definition for ProjectService integration tests.</summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<ProjectServiceTestFixture> { }
