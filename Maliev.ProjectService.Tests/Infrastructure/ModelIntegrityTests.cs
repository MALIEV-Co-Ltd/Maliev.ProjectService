using Maliev.ProjectService.Infrastructure.Persistence;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ProjectService.Tests.Infrastructure;

public sealed class ModelIntegrityTests
{
    [Fact]
    public void Model_ShouldIncludeMassTransitOutboxEntities()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseNpgsql("Host=localhost;Database=project_model_test;Username=postgres;Password=postgres")
            .Options;

        using var dbContext = new ProjectDbContext(options);

        Assert.NotNull(dbContext.Model.FindEntityType(typeof(InboxState)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(OutboxMessage)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(OutboxState)));
    }
}
