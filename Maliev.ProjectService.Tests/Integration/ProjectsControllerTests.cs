using Maliev.ProjectService.Application.DTOs;
using Maliev.ProjectService.Tests.Integration.TestFixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Maliev.ProjectService.Tests.Integration;

/// <summary>
/// Integration tests for the <c>ProjectsController</c>.
/// Tests the full HTTP stack against real PostgreSQL (Testcontainers).
/// Requires Docker — skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
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
    public async Task AddPart_UpdatePart_GetById_ReorderFields_RoundTripAndUseExpectedWireShape()
    {
        var project = await CreateTestProjectAsync();
        var fileId = Guid.NewGuid();
        var drawingFileId = Guid.NewGuid();
        var supplementaryFileId = Guid.NewGuid();

        var addRequest = new AddProjectPartRequest
        {
            FileName = "reorder-bracket.step",
            FileId = fileId,
            FileReference = $"customers/{project.CustomerId}/projects/{project.Id}/source/reorder-bracket.step",
            ProcessType = Domain.Enums.ManufacturingProcess.CNC_Milling,
            MaterialId = Guid.NewGuid(),
            MaterialName = "Aluminium 6061-T6",
            MaterialCode = "AL6061-T6",
            Quantity = 3,
            FinishType = "Anodized",
            Color = "Black",
            Tolerance = "ISO 2768-m",
            RoughnessCode = "Ra1.6",
            MarkingType = "Engraved",
            MarkingText = "PN-100",
            DfmAcknowledged = true,
            HasThreadedHoles = true,
            ThreadedHoleSpec = "M6 x 1.0",
            ThreadedHoleCount = 4,
            HasInserts = true,
            InsertType = "Helicoil",
            InsertCount = 2,
            BagAndTag = false,
            InspectionLevel = "Detailed",
            Certificates = ["MaterialCert", "CoC"],
            ThumbnailSmallGcsPath = "customers/c1/projects/p1/source/reorder-bracket_small.webp",
            ThumbnailLargeGcsPath = "customers/c1/projects/p1/source/reorder-bracket_large.webp",
            GlbStoragePath = "customers/c1/projects/p1/source/reorder-bracket_viewer.glb",
            OverlayPaths = new Dictionary<string, string>
            {
                ["CNC__tool_access"] = "customers/c1/projects/p1/source/reorder-bracket_tool-access.glb"
            },
            DrawingFiles =
            [
                new ProjectPartAttachmentDto
                {
                    FileId = drawingFileId,
                    FileName = "drawing.pdf",
                    StoragePath = "customers/c1/projects/p1/drawings/drawing.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 1024
                }
            ],
            SupplementaryFiles =
            [
                new ProjectPartAttachmentDto
                {
                    FileId = supplementaryFileId,
                    FileName = "photo.jpg",
                    StoragePath = "customers/c1/projects/p1/supp/photo.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 2048
                }
            ],
            ProcessConfig = new Dictionary<string, string>
            {
                ["anodizeColor"] = "Black",
                ["fixtureSide"] = "A"
            },
            BodyCount = 2,
            BodiesJson = """[{"index":0,"name":"Body_01"},{"index":1,"name":"Body_02"}]""",
            SelectedBodyIndex = 1,
            VolumeCm3 = 15.5m,
            SupportVolumeCm3 = 1.25m,
            SurfaceAreaCm2 = 84.4m,
            BoundingBoxX = 120m,
            BoundingBoxY = 80m,
            BoundingBoxZ = 35m,
            IsManifold = true
        };

        var addResponse = await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/parts", addRequest);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = await addResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(added);
        AssertReorderFields(addRequest, added);

        var updateRequest = new UpdateProjectPartRequest
        {
            Quantity = 5,
            RoughnessCode = "Ra0.8",
            MarkingType = "Laser",
            MarkingText = "PN-100-R2",
            DfmAcknowledged = false,
            HasThreadedHoles = true,
            ThreadedHoleSpec = "M8 x 1.25",
            ThreadedHoleCount = 6,
            HasInserts = true,
            InsertType = "PressFit",
            InsertCount = 6,
            BagAndTag = true,
            InspectionLevel = "Cmm",
            Certificates = ["MaterialCert", "CmmReport"],
            ProcessConfig = new Dictionary<string, string>
            {
                ["anodizeColor"] = "Clear",
                ["fixtureSide"] = "B"
            },
            OverlayPaths = new Dictionary<string, string>
            {
                ["CNC__tool_access"] = "customers/c1/projects/p2/copy/reorder-bracket_tool-access.glb",
                ["CNC__undercut"] = "customers/c1/projects/p2/copy/reorder-bracket_undercut.glb"
            },
            BodyCount = 3,
            BodiesJson = """[{"index":0,"name":"Body_01"},{"index":1,"name":"Body_02"},{"index":2,"name":"Body_03"}]""",
            SelectedBodyIndex = 2
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{added.Id}",
            updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await Client.GetAsync($"/project/v1/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var responseJson = await getResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseJson);
        var partJson = document.RootElement.GetProperty("parts")[0];
        Assert.True(partJson.TryGetProperty("thumbnailSmallGcsPath", out _));
        Assert.Equal("Ra0.8", partJson.GetProperty("roughnessCode").GetString());
        Assert.Equal("Laser", partJson.GetProperty("markingType").GetString());
        Assert.Equal("Cmm", partJson.GetProperty("inspectionLevel").GetString());
        Assert.Equal(2, partJson.GetProperty("overlayPaths").EnumerateObject().Count());
        Assert.Equal("Body_03", partJson.GetProperty("bodiesJson").GetString()!.Contains("Body_03", StringComparison.Ordinal) ? "Body_03" : "");

        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(fetched);
        var fetchedPart = Assert.Single(fetched.Parts);
        Assert.Equal("Ra0.8", fetchedPart.RoughnessCode);
        Assert.Equal("Laser", fetchedPart.MarkingType);
        Assert.Equal("PN-100-R2", fetchedPart.MarkingText);
        Assert.False(fetchedPart.DfmAcknowledged);
        Assert.Equal("M8 x 1.25", fetchedPart.ThreadedHoleSpec);
        Assert.Equal(6, fetchedPart.ThreadedHoleCount);
        Assert.Equal("PressFit", fetchedPart.InsertType);
        Assert.Equal(6, fetchedPart.InsertCount);
        Assert.True(fetchedPart.BagAndTag);
        Assert.Equal("Cmm", fetchedPart.InspectionLevel);
        Assert.Equal(["MaterialCert", "CmmReport"], fetchedPart.Certificates);
        Assert.Equal("Clear", fetchedPart.ProcessConfig["anodizeColor"]);
        Assert.Equal(2, fetchedPart.OverlayPaths.Count);
        Assert.Equal(3, fetchedPart.BodyCount);
        Assert.Equal(2, fetchedPart.SelectedBodyIndex);
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

    private static void AssertReorderFields(AddProjectPartRequest expected, ProjectPartResponse actual)
    {
        Assert.Equal(expected.FileId, actual.FileId);
        Assert.Equal(expected.FileReference, actual.FileReference);
        Assert.Equal(expected.RoughnessCode, actual.RoughnessCode);
        Assert.Equal(expected.MarkingType, actual.MarkingType);
        Assert.Equal(expected.MarkingText, actual.MarkingText);
        Assert.Equal(expected.DfmAcknowledged, actual.DfmAcknowledged);
        Assert.Equal(expected.ThreadedHoleSpec, actual.ThreadedHoleSpec);
        Assert.Equal(expected.ThreadedHoleCount, actual.ThreadedHoleCount);
        Assert.Equal(expected.InsertType, actual.InsertType);
        Assert.Equal(expected.InsertCount, actual.InsertCount);
        Assert.Equal(expected.BagAndTag, actual.BagAndTag);
        Assert.Equal(expected.InspectionLevel, actual.InspectionLevel);
        Assert.Equal(expected.Certificates, actual.Certificates);
        Assert.Equal(expected.ThumbnailSmallGcsPath, actual.ThumbnailSmallGcsPath);
        Assert.Equal(expected.ThumbnailLargeGcsPath, actual.ThumbnailLargeGcsPath);
        Assert.Equal(expected.GlbStoragePath, actual.GlbStoragePath);
        Assert.Equal(expected.OverlayPaths, actual.OverlayPaths);
        Assert.Equal(expected.DrawingFiles.Single().FileId, actual.DrawingFiles.Single().FileId);
        Assert.Equal(expected.SupplementaryFiles.Single().FileId, actual.SupplementaryFiles.Single().FileId);
        Assert.Equal(expected.ProcessConfig, actual.ProcessConfig);
        Assert.Equal(expected.BodyCount, actual.BodyCount);
        Assert.Equal(expected.BodiesJson, actual.BodiesJson);
        Assert.Equal(expected.SelectedBodyIndex, actual.SelectedBodyIndex);
    }
}
