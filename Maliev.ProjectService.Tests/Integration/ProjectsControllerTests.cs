using Maliev.ProjectService.Application.DTOs;
using Maliev.ProjectService.Tests.Integration.TestFixtures;
using System.Net;
using System.Net.Http.Json;

namespace Maliev.ProjectService.Tests.Integration;

/// <summary>
/// Integration tests for the <c>ProjectsController</c>.
/// Tests the full HTTP stack against real PostgreSQL (Testcontainers).
/// </summary>
public class ProjectsControllerTests : BaseIntegrationTest
{
    /// <inheritdoc />
    public ProjectsControllerTests(ProjectServiceTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAll_WhenAuthenticated_ShouldReturnOk()
    {
        var response = await Client.GetAsync("/project/v1/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedProjectResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetAll_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var anonClient = Fixture.CreateAnonymousClient();
        var response = await anonClient.GetAsync("/project/v1/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNotFound()
    {
        var response = await Client.GetAsync($"/project/v1/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreated()
    {
        var request = new CreateProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Customer Co. Ltd.",
            Title = "Bracket Assembly — Prototype Run",
            Description = "3 CNC aluminium brackets for prototype testing",
            Currency = "THB"
        };

        var response = await Client.PostAsJsonAsync("/project/v1/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(project);
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.StartsWith("PRJ-", project.ProjectNumber);
        Assert.Equal(request.CustomerId, project.CustomerId);
        Assert.Equal(request.Title, project.Title);
        Assert.Equal("Draft", project.Status);
        Assert.Empty(project.Parts);
    }

    [Fact]
    public async Task Create_WithoutPermission_ShouldReturnForbidden()
    {
        var readClient = CreateReadOnlyClient();
        var request = new CreateProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Forbidden Customer",
            Title = "This should fail",
            Currency = "THB"
        };

        var response = await readClient.PostAsJsonAsync("/project/v1/projects", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ThenGetById_ShouldReturnSameProject()
    {
        var request = new CreateProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Integration Test Customer",
            Title = "Full CRUD Verification Project",
            Currency = "THB"
        };

        var createResponse = await Client.PostAsJsonAsync("/project/v1/projects", request);
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(created);

        var getResponse = await Client.GetAsync($"/project/v1/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.ProjectNumber, fetched.ProjectNumber);
        Assert.Equal(created.Title, fetched.Title);
    }

    [Fact]
    public async Task AddPart_ToExistingProject_ShouldReturnCreated()
    {
        // Arrange: create project
        var project = await CreateTestProjectAsync();

        // Act: add a part
        var partRequest = new AddProjectPartRequest
        {
            FileName = "bracket_v2.stl",
            ProcessType = Domain.Enums.ManufacturingProcess.FDM,
            MaterialId = Guid.NewGuid(),
            MaterialName = "PLA+ White",
            MaterialCode = "PLA-WH-175",
            Quantity = 5,
            FinishType = "Standard",
            VolumeCm3 = 12.5m,
            BoundingBoxX = 80m,
            BoundingBoxY = 40m,
            BoundingBoxZ = 20m,
            IsManifold = true
        };

        var response = await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/parts", partRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var part = await response.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(part);
        Assert.Equal("bracket_v2.stl", part.FileName);
        Assert.Equal(1, part.PartNumber);
        Assert.Equal(5, part.Quantity);
        Assert.Equal("FDM", part.ProcessType);
    }

    [Fact]
    public async Task RequestPricing_ForConfiguredPart_ShouldReturnPricedPart()
    {
        // Arrange
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);

        // Act
        var response = await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pricedPart = await response.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(pricedPart);
        Assert.NotNull(pricedPart.AiSuggestedPrice);
        Assert.True(pricedPart.AiSuggestedPrice > 0);
        Assert.Equal("Priced", pricedPart.Status);
    }

    [Fact]
    public async Task ConfirmPrice_AfterPricing_ShouldAdvanceToConfirmedStatus()
    {
        // Arrange
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);

        // Act: confirm with AI price
        var confirmRequest = new ConfirmPartPriceRequest
        {
            ConfirmedUnitPrice = null // use AI price
        };
        var response = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price", confirmRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmedPart = await response.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(confirmedPart);
        Assert.Equal("Confirmed", confirmedPart.Status);
        Assert.NotNull(confirmedPart.ConfirmedUnitPrice);
    }

    [Fact]
    public async Task GenerateQuotation_WithAllPartsConfirmed_ShouldReturnQuotationGeneratedProject()
    {
        // Arrange: create project, add part, price it, confirm it
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());

        // Act: generate quotation
        var quotationRequest = new GenerateQuotationRequest { ValidityDays = 30 };
        var response = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation", quotationRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedProject = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(updatedProject);
        Assert.Equal("QuotationGenerated", updatedProject.Status);
        Assert.NotNull(updatedProject.QuotationId);
        Assert.StartsWith("QUO-TEST-", updatedProject.QuotationNumber);
    }

    [Fact]
    public async Task Delete_DraftProject_ShouldReturnNoContent()
    {
        var project = await CreateTestProjectAsync();

        var response = await Client.DeleteAsync($"/project/v1/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's gone
        var getResponse = await Client.GetAsync($"/project/v1/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetStats_ShouldReturnStatsResponse()
    {
        var response = await Client.GetAsync("/project/v1/projects/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<ProjectStatsResponse>();
        Assert.NotNull(stats);
        Assert.True(stats.ActiveCount >= 0);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<ProjectDetailResponse> CreateTestProjectAsync()
    {
        var request = new CreateProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test Corp",
            Title = "Integration Test Project",
            Currency = "THB"
        };
        var response = await Client.PostAsJsonAsync("/project/v1/projects", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        return result!;
    }

    private async Task<ProjectPartResponse> AddTestPartAsync(Guid projectId)
    {
        var request = new AddProjectPartRequest
        {
            FileName = "test_part.stl",
            ProcessType = Domain.Enums.ManufacturingProcess.FDM,
            MaterialId = Guid.NewGuid(),
            MaterialName = "PLA",
            MaterialCode = "PLA-BK-175",
            Quantity = 1,
            VolumeCm3 = 10m,
            BoundingBoxX = 50m,
            BoundingBoxY = 30m,
            BoundingBoxZ = 20m,
            IsManifold = true
        };
        var response = await Client.PostAsJsonAsync($"/project/v1/projects/{projectId}/parts", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectPartResponse>();
        return result!;
    }
}
