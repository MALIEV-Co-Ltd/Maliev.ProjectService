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
    public async Task Create_WithCustomerScopeForDifferentCustomer_ShouldReturnForbidden()
    {
        var scopedCustomerId = Guid.NewGuid();
        var scopedClient = Fixture.CreateClientWithPermissionsAndClaims(
            new Dictionary<string, string> { ["customer_id"] = scopedCustomerId.ToString() },
            "project.projects.create");
        var request = new CreateProjectRequest
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Wrong Customer",
            Title = "Cross-customer create attempt",
            Currency = "THB"
        };

        var response = await scopedClient.PostAsJsonAsync("/project/v1/projects", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithCustomerScope_ShouldReturnOnlyScopedCustomerProjects()
    {
        var scopedCustomerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var scopedProject = await CreateTestProjectAsync(scopedCustomerId);
        var otherProject = await CreateTestProjectAsync(otherCustomerId);
        var scopedClient = Fixture.CreateClientWithPermissionsAndClaims(
            new Dictionary<string, string> { ["customer_id"] = scopedCustomerId.ToString() },
            "project.projects.read");

        var listResponse = await scopedClient.GetAsync("/project/v1/projects");
        var ownResponse = await scopedClient.GetAsync($"/project/v1/projects/{scopedProject.Id}");
        var otherResponse = await scopedClient.GetAsync($"/project/v1/projects/{otherProject.Id}");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var result = await listResponse.Content.ReadFromJsonAsync<PaginatedProjectResponse>();
        Assert.NotNull(result);
        var match = Assert.Single(result.Data);
        Assert.Equal(scopedProject.Id, match.Id);
        Assert.Equal(scopedCustomerId, match.CustomerId);
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
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
    public async Task PinAndArchive_Project_ShouldPersistCustomerFlags()
    {
        var project = await CreateTestProjectAsync();

        var pinResponse = await Client.PostAsync($"/project/v1/projects/{project.Id}/pin", null);
        pinResponse.EnsureSuccessStatusCode();
        var pinned = await pinResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();

        Assert.NotNull(pinned);
        Assert.True(pinned.IsPinned);
        Assert.False(pinned.IsArchived);

        var archiveResponse = await Client.PostAsync($"/project/v1/projects/{project.Id}/archive", null);
        archiveResponse.EnsureSuccessStatusCode();
        var archived = await archiveResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();

        Assert.NotNull(archived);
        Assert.True(archived.IsPinned);
        Assert.True(archived.IsArchived);

        var listResponse = await Client.GetAsync($"/project/v1/projects?customerId={project.CustomerId:D}");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<PaginatedProjectResponse>();

        Assert.NotNull(list);
        var listed = Assert.Single(list.Data, item => item.Id == project.Id);
        Assert.True(listed.IsPinned);
        Assert.True(listed.IsArchived);
    }

    [Fact]
    public async Task GetAll_WithPartFileQuery_ReturnsProjectContainingPart()
    {
        var project = await CreateTestProjectAsync();
        var partRequest = new AddProjectPartRequest
        {
            FileName = "d15-16.stp",
            FileReference = $"customers/{project.CustomerId}/projects/{project.Id}/source/d15-16.stp",
            ProcessType = Domain.Enums.ManufacturingProcess.FDM,
            MaterialId = Guid.NewGuid(),
            MaterialName = "Polycarbonate",
            MaterialCode = "PC",
            Quantity = 12,
            BoundingBoxX = 38m,
            BoundingBoxY = 22m,
            BoundingBoxZ = 38m,
            IsManifold = true
        };
        var addResponse = await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/parts", partRequest);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var response = await Client.GetAsync("/project/v1/projects?query=d15-16");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedProjectResponse>();
        Assert.NotNull(result);
        var match = Assert.Single(result.Data);
        Assert.Equal(project.Id, match.Id);
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
            HasDfmWarnings = true,
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
            HasDfmWarnings = false,
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
        Assert.False(partJson.GetProperty("hasDfmWarnings").GetBoolean());
        Assert.Equal(2, partJson.GetProperty("overlayPaths").EnumerateObject().Count());
        Assert.Equal("Body_03", partJson.GetProperty("bodiesJson").GetString()!.Contains("Body_03", StringComparison.Ordinal) ? "Body_03" : "");

        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(fetched);
        var fetchedPart = Assert.Single(fetched.Parts);
        Assert.Equal("Ra0.8", fetchedPart.RoughnessCode);
        Assert.Equal("Laser", fetchedPart.MarkingType);
        Assert.Equal("PN-100-R2", fetchedPart.MarkingText);
        Assert.False(fetchedPart.DfmAcknowledged);
        Assert.False(fetchedPart.HasDfmWarnings);
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
        var beforeQuoteResponse = await Client.GetAsync($"/project/v1/projects/{project.Id}");
        beforeQuoteResponse.EnsureSuccessStatusCode();
        var beforeQuoteProject = await beforeQuoteResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(beforeQuoteProject);

        var quotationRequest = new GenerateQuotationRequest { ValidityDays = 30, BulkDiscountAmount = 5m };
        var response = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation", quotationRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedProject = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(updatedProject);
        Assert.Equal("QuotationGenerated", updatedProject.Status);
        Assert.NotNull(updatedProject.QuotationId);
        Assert.StartsWith("QUO-TEST-", updatedProject.QuotationNumber);
        Assert.NotNull(updatedProject.CurrentQuotationVersionId);
        Assert.Equal(1, updatedProject.CurrentQuotationVersionNumber);
        Assert.Equal(beforeQuoteProject.TotalEstimatedPrice - 5m, updatedProject.TotalEstimatedPrice);
    }

    [Fact]
    public async Task GenerateQuotation_ExistingQuotation_ShouldCreateNextVersionOnSameQuotation()
    {
        // Arrange
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());

        var firstResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation",
            new GenerateQuotationRequest { ValidityDays = 30, ChangeSummary = "Initial version" });
        firstResponse.EnsureSuccessStatusCode();
        var firstProject = await firstResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(firstProject);

        // Act
        var secondResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation",
            new GenerateQuotationRequest { ValidityDays = 30, ChangeSummary = "Regenerated after commercial review" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondProject = await secondResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(secondProject);
        Assert.Equal(firstProject.QuotationId, secondProject.QuotationId);
        Assert.Equal(2, secondProject.CurrentQuotationVersionNumber);
        Assert.NotEqual(firstProject.CurrentQuotationVersionId, secondProject.CurrentQuotationVersionId);
    }

    [Fact]
    public async Task AcceptQuotation_WithStaleExpectedVersion_ShouldReturnConflictAndLeaveProjectQuoted()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());

        var firstResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation",
            new GenerateQuotationRequest { ValidityDays = 30, ChangeSummary = "Initial version" });
        firstResponse.EnsureSuccessStatusCode();
        var firstProject = await firstResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(firstProject);
        Assert.NotNull(firstProject.CurrentQuotationVersionId);

        var secondResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation",
            new GenerateQuotationRequest { ValidityDays = 30, ChangeSummary = "Regenerated after commercial review" });
        secondResponse.EnsureSuccessStatusCode();
        var secondProject = await secondResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(secondProject);
        Assert.Equal(2, secondProject.CurrentQuotationVersionNumber);

        var staleAcceptResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/accept-quotation",
            new AcceptQuotationRequest
            {
                ExpectedQuotationVersionId = firstProject.CurrentQuotationVersionId,
                ExpectedQuotationVersionNumber = firstProject.CurrentQuotationVersionNumber
            });

        Assert.Equal(HttpStatusCode.Conflict, staleAcceptResponse.StatusCode);
        var afterConflictResponse = await Client.GetAsync($"/project/v1/projects/{project.Id}");
        afterConflictResponse.EnsureSuccessStatusCode();
        var afterConflictProject = await afterConflictResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(afterConflictProject);
        Assert.Equal("QuotationGenerated", afterConflictProject.Status);
        Assert.Equal(secondProject.CurrentQuotationVersionId, afterConflictProject.CurrentQuotationVersionId);
        Assert.Equal(2, afterConflictProject.CurrentQuotationVersionNumber);
    }

    [Fact]
    public async Task RequestReview_DraftProject_ShouldSetCustomerReviewStatusAndAddNote()
    {
        var project = await CreateTestProjectAsync();
        var request = new RequestProjectReviewRequest
        {
            Note = "Please review the thin wall before quoting."
        };

        var response = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/request-review",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reviewedProject = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(reviewedProject);
        Assert.Equal("CustomerReview", reviewedProject.Status);
        Assert.Contains(reviewedProject.Notes, note =>
            note.Content.Contains("Customer requested employee review from Make Studio", StringComparison.Ordinal) &&
            note.Content.Contains(request.Note, StringComparison.Ordinal));

        var queueResponse = await Client.GetAsync("/project/v1/projects?status=CustomerReview");
        queueResponse.EnsureSuccessStatusCode();
        var queue = await queueResponse.Content.ReadFromJsonAsync<PaginatedProjectResponse>();
        Assert.NotNull(queue);
        Assert.Contains(queue.Data, item => item.Id == project.Id && item.Status == "CustomerReview");
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

    [Fact]
    public async Task GetStats_AfterCustomerReviewRequest_ShouldCountReviewSeparatelyFromConfiguring()
    {
        var baselineResponse = await Client.GetAsync("/project/v1/projects/stats");
        baselineResponse.EnsureSuccessStatusCode();
        var baseline = await baselineResponse.Content.ReadFromJsonAsync<ProjectStatsResponse>();
        Assert.NotNull(baseline);

        var project = await CreateTestProjectAsync();

        var draftStatsResponse = await Client.GetAsync("/project/v1/projects/stats");
        draftStatsResponse.EnsureSuccessStatusCode();
        var draftStats = await draftStatsResponse.Content.ReadFromJsonAsync<ProjectStatsResponse>();
        Assert.NotNull(draftStats);
        Assert.Equal(baseline.ConfiguringCount + 1, draftStats.ConfiguringCount);

        var reviewResponse = await Client.PostAsJsonAsync($"/project/v1/projects/{project.Id}/request-review", new RequestProjectReviewRequest
        {
            Note = "Customer requested DFM review from Make Studio."
        });
        reviewResponse.EnsureSuccessStatusCode();

        var reviewedStatsResponse = await Client.GetAsync("/project/v1/projects/stats");
        reviewedStatsResponse.EnsureSuccessStatusCode();
        var reviewedStats = await reviewedStatsResponse.Content.ReadFromJsonAsync<ProjectStatsResponse>();
        Assert.NotNull(reviewedStats);
        Assert.Equal(baseline.ConfiguringCount, reviewedStats.ConfiguringCount);
        Assert.Equal(baseline.CustomerReviewCount + 1, reviewedStats.CustomerReviewCount);
    }

    [Fact]
    public async Task GetById_AfterPartMutation_ShouldExposeAdvancedProjectVersionOnProjectAndPart()
    {
        var project = await CreateTestProjectAsync();

        var part = await AddTestPartAsync(project.Id);

        Assert.NotEqual(0u, project.Version);
        Assert.NotEqual(project.Version, part.ProjectVersion);

        var response = await Client.GetAsync($"/project/v1/projects/{project.Id}");
        response.EnsureSuccessStatusCode();
        var fetched = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();

        Assert.NotNull(fetched);
        Assert.Equal(part.ProjectVersion, fetched.Version);
        Assert.Equal(fetched.Version, Assert.Single(fetched.Parts).ProjectVersion);
    }

    [Fact]
    public async Task Update_WithExpectedVersion_ShouldRoundTripLeadTimeAndRejectStaleWriteWithoutMutation()
    {
        var project = await CreateTestProjectAsync();
        var originalVersion = project.Version;

        var firstResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = "Priority enclosure",
                Description = "First accepted edit",
                LeadTimeCode = "EXPRESS",
                ExpectedVersion = originalVersion
            });
        firstResponse.EnsureSuccessStatusCode();
        var updated = await firstResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();

        Assert.NotNull(updated);
        Assert.Equal("EXPRESS", updated.LeadTimeCode);
        Assert.NotEqual(originalVersion, updated.Version);

        var staleResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = "Stale overwrite",
                Description = "Must not persist",
                LeadTimeCode = "STANDARD",
                ExpectedVersion = originalVersion
            });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        var fetchedResponse = await Client.GetAsync($"/project/v1/projects/{project.Id}");
        fetchedResponse.EnsureSuccessStatusCode();
        var fetched = await fetchedResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();

        Assert.NotNull(fetched);
        Assert.Equal("Priority enclosure", fetched.Title);
        Assert.Equal("First accepted edit", fetched.Description);
        Assert.Equal("EXPRESS", fetched.LeadTimeCode);
        Assert.Equal(updated.Version, fetched.Version);
    }

    [Fact]
    public async Task UpdatePart_PricingRelevantChange_ShouldInvalidatePriceAndRecalculateAggregate()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var before = await GetTestProjectAsync(project.Id);
        var confirmed = Assert.Single(before.Parts);
        Assert.Equal("Confirmed", confirmed.Status);
        Assert.True(before.TotalEstimatedPrice > 0m);

        var updateResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}",
            new UpdateProjectPartRequest
            {
                Quantity = confirmed.Quantity + 1,
                ExpectedVersion = before.Version
            });

        updateResponse.EnsureSuccessStatusCode();
        var updatedPart = await updateResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(updatedPart);
        Assert.Equal("Configured", updatedPart.Status);
        Assert.Null(updatedPart.AiSuggestedPrice);
        Assert.Null(updatedPart.ConfirmedUnitPrice);
        Assert.NotEqual(before.Version, updatedPart.ProjectVersion);

        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal(0m, after.TotalEstimatedPrice);
        Assert.Equal(updatedPart.ProjectVersion, after.Version);
    }

    [Fact]
    public async Task UpdatePart_ExplicitOptionalFieldClears_ShouldRoundTripAndInvalidatePricing()
    {
        var project = await CreateTestProjectAsync();
        var addResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts",
            new AddProjectPartRequest
            {
                FileName = "clearable-options.step",
                ProcessType = Domain.Enums.ManufacturingProcess.CNC_Milling,
                MaterialId = Guid.NewGuid(),
                MaterialName = "Aluminium 6061",
                MaterialCode = "AL6061",
                Quantity = 2,
                FinishType = "Anodized",
                Color = "Black",
                Tolerance = "ISO-2768-f",
                RoughnessCode = "RA_1_6",
                ThreadedHoleSpec = "M4",
                ThreadedHoleCount = 4,
                InsertType = "HELICOIL_M4",
                InsertCount = 2,
                InspectionLevel = "CMM",
                CustomNotes = "Protect cosmetic face",
                VolumeCm3 = 10m,
                BoundingBoxX = 50m,
                BoundingBoxY = 30m,
                BoundingBoxZ = 20m,
                IsManifold = true
            });
        addResponse.EnsureSuccessStatusCode();
        var part = await addResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(part);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var before = await GetTestProjectAsync(project.Id);

        var updateResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}",
            new UpdateProjectPartRequest
            {
                ExpectedVersion = before.Version,
                ClearFinishType = true,
                ClearColor = true,
                ClearTolerance = true,
                ClearRoughnessCode = true,
                ClearThreadedHoleSpec = true,
                ClearInsertType = true,
                ClearInspectionLevel = true,
                ClearCustomNotes = true
            });

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(updated);
        Assert.Null(updated.FinishType);
        Assert.Null(updated.Color);
        Assert.Null(updated.Tolerance);
        Assert.Null(updated.RoughnessCode);
        Assert.Null(updated.ThreadedHoleSpec);
        Assert.Null(updated.InsertType);
        Assert.Null(updated.InspectionLevel);
        Assert.Null(updated.CustomNotes);
        Assert.Equal("Configured", updated.Status);
        Assert.Null(updated.AiSuggestedPrice);
        Assert.Null(updated.ConfirmedUnitPrice);
        Assert.NotEqual(before.Version, updated.ProjectVersion);
        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal(0m, after.TotalEstimatedPrice);
    }

    [Fact]
    public async Task UpdatePart_PreviewDfmAndDrawingOnly_ShouldPreservePricingAndAdvanceVersion()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var before = await GetTestProjectAsync(project.Id);
        var confirmed = Assert.Single(before.Parts);

        var updateResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}",
            new UpdateProjectPartRequest
            {
                DfmAcknowledged = true,
                HasDfmWarnings = true,
                ThumbnailSmallGcsPath = "customers/c1/projects/p1/preview.webp",
                DrawingFiles =
                [
                    new ProjectPartAttachmentDto
                    {
                        FileId = Guid.NewGuid(),
                        FileName = "inspection.pdf",
                        StoragePath = "customers/c1/projects/p1/drawings/inspection.pdf",
                        ContentType = "application/pdf"
                    }
                ],
                ExpectedVersion = before.Version
            });

        updateResponse.EnsureSuccessStatusCode();
        var updatedPart = await updateResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(updatedPart);
        Assert.Equal("Confirmed", updatedPart.Status);
        Assert.Equal(confirmed.AiSuggestedPrice, updatedPart.AiSuggestedPrice);
        Assert.Equal(confirmed.ConfirmedUnitPrice, updatedPart.ConfirmedUnitPrice);
        Assert.NotEqual(before.Version, updatedPart.ProjectVersion);

        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal(before.TotalEstimatedPrice, after.TotalEstimatedPrice);
        Assert.Equal("Confirmed", Assert.Single(after.Parts).Status);
    }

    [Fact]
    public async Task UpdatePart_WithStaleVersion_ShouldReturnConflictAndLeavePartUnchanged()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        var before = await GetTestProjectAsync(project.Id);

        var firstResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}",
            new UpdateProjectPartRequest
            {
                DfmAcknowledged = true,
                ExpectedVersion = before.Version
            });
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(first);

        var staleResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}",
            new UpdateProjectPartRequest
            {
                DfmAcknowledged = false,
                HasDfmWarnings = true,
                ExpectedVersion = before.Version
            });

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        var after = await GetTestProjectAsync(project.Id);
        var unchanged = Assert.Single(after.Parts);
        Assert.True(unchanged.DfmAcknowledged);
        Assert.False(unchanged.HasDfmWarnings);
        Assert.Equal(first.ProjectVersion, after.Version);
    }

    [Fact]
    public async Task Update_LeadTimeChange_ShouldInvalidatePartPricingAndPreserveGeometry()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var before = await GetTestProjectAsync(project.Id);
        var beforePart = Assert.Single(before.Parts);

        var response = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = before.Title,
                Description = before.Description,
                LeadTimeCode = "EXPRESS",
                ExpectedVersion = before.Version
            });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(updated);
        Assert.Equal("EXPRESS", updated.LeadTimeCode);
        Assert.Equal(0m, updated.TotalEstimatedPrice);
        Assert.NotEqual(before.Version, updated.Version);
        var updatedPart = Assert.Single(updated.Parts);
        Assert.Equal("Configured", updatedPart.Status);
        Assert.Null(updatedPart.AiSuggestedPrice);
        Assert.Null(updatedPart.ConfirmedUnitPrice);
        Assert.Equal(beforePart.VolumeCm3, updatedPart.VolumeCm3);
        Assert.Equal(beforePart.BoundingBoxX, updatedPart.BoundingBoxX);
    }

    [Fact]
    public async Task Update_WhenQuotationGenerated_ShouldRejectProjectAndPartEdits()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var quoteResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation",
            new GenerateQuotationRequest());
        quoteResponse.EnsureSuccessStatusCode();
        var quoted = await quoteResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(quoted);

        var projectResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = "Forbidden edit",
                ExpectedVersion = quoted.Version
            });
        var partResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}",
            new UpdateProjectPartRequest
            {
                DfmAcknowledged = true,
                ExpectedVersion = quoted.Version
            });

        Assert.Equal(HttpStatusCode.Conflict, projectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, partResponse.StatusCode);
        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal(quoted.Title, after.Title);
        Assert.False(Assert.Single(after.Parts).DfmAcknowledged);
        Assert.Equal(quoted.Version, after.Version);
    }

    [Fact]
    public async Task Update_WithoutExpectedVersion_ShouldRemainBackwardCompatible()
    {
        var project = await CreateTestProjectAsync();

        var response = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = "Legacy client edit",
                Description = "No expectedVersion field"
            });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Legacy client edit", updated.Title);
        Assert.NotEqual(project.Version, updated.Version);
    }

    [Fact]
    public async Task RequestReview_WhenQuotationGenerated_ShouldReturnConflictAndPreserveIssuedState()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var quoteResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/generate-quotation",
            new GenerateQuotationRequest());
        quoteResponse.EnsureSuccessStatusCode();
        var quoted = await quoteResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(quoted);

        var reviewResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/request-review",
            new RequestProjectReviewRequest { Note = "Reopen issued quote" });

        Assert.Equal(HttpStatusCode.Conflict, reviewResponse.StatusCode);
        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal("QuotationGenerated", after.Status);
        Assert.Equal(quoted.QuotationId, after.QuotationId);
        Assert.Equal(quoted.CurrentQuotationVersionId, after.CurrentQuotationVersionId);
        Assert.Equal(quoted.Version, after.Version);
    }

    [Fact]
    public async Task RequestPricing_WhenPartWasConfirmed_ShouldClearConfirmationAndAggregateTotal()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        await Client.PostAsync($"/project/v1/projects/{project.Id}/parts/{part.Id}/price", null);
        await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/confirm-price",
            new ConfirmPartPriceRequest());
        var confirmedProject = await GetTestProjectAsync(project.Id);
        var confirmedPart = Assert.Single(confirmedProject.Parts);
        Assert.NotNull(confirmedPart.ConfirmedUnitPrice);
        Assert.True(confirmedProject.TotalEstimatedPrice > 0m);

        var repriceResponse = await Client.PostAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}/price",
            null);

        repriceResponse.EnsureSuccessStatusCode();
        var repriced = await repriceResponse.Content.ReadFromJsonAsync<ProjectPartResponse>();
        Assert.NotNull(repriced);
        Assert.Equal("Priced", repriced.Status);
        Assert.Null(repriced.ConfirmedUnitPrice);
        Assert.Null(repriced.PriceOverrideReason);
        Assert.NotEqual(confirmedProject.Version, repriced.ProjectVersion);
        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal(0m, after.TotalEstimatedPrice);
        Assert.Equal(repriced.ProjectVersion, after.Version);
    }

    [Fact]
    public async Task AddAndRemovePart_WithStaleExpectedVersion_ShouldReturnConflictWithoutMutation()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        var current = await GetTestProjectAsync(project.Id);
        var updateResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = "Advance aggregate version",
                ExpectedVersion = current.Version
            });
        updateResponse.EnsureSuccessStatusCode();

        var staleAddResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts",
            new AddProjectPartRequest
            {
                FileName = "stale-add.stl",
                ProcessType = Domain.Enums.ManufacturingProcess.FDM,
                MaterialId = Guid.NewGuid(),
                MaterialName = "PLA",
                Quantity = 1,
                ExpectedVersion = current.Version
            });
        var staleRemoveResponse = await Client.DeleteAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}?expectedVersion={current.Version}");

        Assert.Equal(HttpStatusCode.Conflict, staleAddResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleRemoveResponse.StatusCode);
        var after = await GetTestProjectAsync(project.Id);
        Assert.Equal("Advance aggregate version", after.Title);
        Assert.Equal(part.Id, Assert.Single(after.Parts).Id);
    }

    [Fact]
    public async Task ArchivedDraftProject_UpdateAddAndRemoveWrites_ShouldReturnConflict()
    {
        var project = await CreateTestProjectAsync();
        var part = await AddTestPartAsync(project.Id);
        var archiveResponse = await Client.PostAsync($"/project/v1/projects/{project.Id}/archive", null);
        archiveResponse.EnsureSuccessStatusCode();
        var archived = await archiveResponse.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.NotNull(archived);

        var updateResponse = await Client.PutAsJsonAsync(
            $"/project/v1/projects/{project.Id}",
            new UpdateProjectRequest
            {
                Title = "Forbidden archived update",
                ExpectedVersion = archived.Version
            });
        var addResponse = await Client.PostAsJsonAsync(
            $"/project/v1/projects/{project.Id}/parts",
            new AddProjectPartRequest
            {
                FileName = "archived-add.stl",
                ProcessType = Domain.Enums.ManufacturingProcess.FDM,
                MaterialId = Guid.NewGuid(),
                MaterialName = "PLA",
                Quantity = 1,
                ExpectedVersion = archived.Version
            });
        var removeResponse = await Client.DeleteAsync(
            $"/project/v1/projects/{project.Id}/parts/{part.Id}?expectedVersion={archived.Version}");

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, addResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removeResponse.StatusCode);
        var after = await GetTestProjectAsync(project.Id);
        Assert.True(after.IsArchived);
        Assert.Equal(project.Title, after.Title);
        Assert.Equal(part.Id, Assert.Single(after.Parts).Id);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<ProjectDetailResponse> CreateTestProjectAsync(Guid? customerId = null)
    {
        var request = new CreateProjectRequest
        {
            CustomerId = customerId ?? Guid.NewGuid(),
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

    private async Task<ProjectDetailResponse> GetTestProjectAsync(Guid projectId)
    {
        var response = await Client.GetAsync($"/project/v1/projects/{projectId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
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
        Assert.Equal(expected.HasDfmWarnings, actual.HasDfmWarnings);
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
