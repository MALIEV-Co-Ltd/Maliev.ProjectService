using Maliev.ProjectService.Tests.Integration.TestFixtures;
using System.Net.Http.Headers;

namespace Maliev.ProjectService.Tests.Integration;

/// <summary>
/// Base class for all ProjectService integration tests.
/// Provides an authenticated HTTP client and per-test database cleanup.
/// </summary>
[Collection("IntegrationTests")]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    /// <summary>The shared test fixture (Testcontainers).</summary>
    protected readonly ProjectServiceTestFixture Fixture;

    /// <summary>HTTP client with all project permissions granted.</summary>
    protected readonly HttpClient Client;

    /// <summary>Initializes the base integration test.</summary>
    protected BaseIntegrationTest(ProjectServiceTestFixture fixture)
    {
        Fixture = fixture;
        Client = fixture.CreateClientWithPermissions(
            "project.projects.read",
            "project.projects.create",
            "project.projects.update",
            "project.projects.delete",
            "project.projects.quote",
            "project.projects.accept");
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync() => await Fixture.CleanDatabaseAsync();

    /// <summary>Creates an HTTP client with only read permission (for authorization tests).</summary>
    protected HttpClient CreateReadOnlyClient() =>
        Fixture.CreateClientWithPermissions("project.projects.read");
}
