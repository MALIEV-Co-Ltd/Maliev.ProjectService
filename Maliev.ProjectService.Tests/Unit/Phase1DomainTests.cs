using Maliev.ProjectService.Application.DTOs;
using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;

namespace Maliev.ProjectService.Tests.Unit;

/// <summary>
/// Unit tests for Phase 1: ProjectService domain entities, value objects, enums, and DTO mappings.
/// Zero I/O — no database, no network.
/// </summary>
public class Phase1DomainTests
{
    // ── Project entity defaults ────────────────────────────────────────────

    [Fact]
    public void Project_DefaultStatus_IsDraft()
    {
        var project = new Project();
        Assert.Equal(ProjectStatus.Draft, project.Status);
    }

    [Fact]
    public void Project_DefaultCurrency_IsTHB()
    {
        var project = new Project();
        Assert.Equal("THB", project.Currency);
    }

    [Fact]
    public void Project_DefaultParts_IsEmpty()
    {
        var project = new Project();
        Assert.Empty(project.Parts);
    }

    [Fact]
    public void Project_DefaultNotes_IsEmpty()
    {
        var project = new Project();
        Assert.Empty(project.Notes);
    }

    [Fact]
    public void Project_IsNotDeleted_ByDefault()
    {
        var project = new Project();
        Assert.False(project.IsDeleted);
    }

    // ── ProjectPart entity ─────────────────────────────────────────────────

    [Fact]
    public void ProjectPart_DefaultStatus_IsUploaded()
    {
        var part = new ProjectPart();
        Assert.Equal(PartStatus.Uploaded, part.Status);
    }

    [Fact]
    public void ProjectPart_DefaultQuantity_IsOne()
    {
        var part = new ProjectPart();
        Assert.Equal(1, part.Quantity);
    }

    [Fact]
    public void ProjectPart_TotalPrice_WhenNoPricing_IsZero()
    {
        var part = new ProjectPart { Quantity = 5 };
        Assert.Equal(0m, part.TotalPrice);
    }

    [Fact]
    public void ProjectPart_TotalPrice_UsesAiPriceWhenNoConfirmed()
    {
        var part = new ProjectPart
        {
            AiSuggestedPrice = 100m,
            Quantity = 3
        };
        Assert.Equal(300m, part.TotalPrice);
    }

    [Fact]
    public void ProjectPart_TotalPrice_PrefersConfirmedOverAi()
    {
        var part = new ProjectPart
        {
            AiSuggestedPrice = 100m,
            ConfirmedUnitPrice = 120m,
            Quantity = 2
        };
        Assert.Equal(240m, part.TotalPrice);
    }

    // ── ProjectStatus enum completeness ───────────────────────────────────

    [Theory]
    [InlineData("Draft")]
    [InlineData("Configuring")]
    [InlineData("QuotationGenerated")]
    [InlineData("QuotationSent")]
    [InlineData("QuotationAccepted")]
    [InlineData("InProduction")]
    [InlineData("QualityCheck")]
    [InlineData("ReadyToShip")]
    [InlineData("Delivered")]
    [InlineData("Invoiced")]
    [InlineData("Paid")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void ProjectStatus_AllExpectedValuesExist(string statusName)
    {
        var exists = Enum.TryParse<ProjectStatus>(statusName, out _);
        Assert.True(exists, $"ProjectStatus.{statusName} should exist");
    }

    // ── PartStatus enum completeness ──────────────────────────────────────

    [Theory]
    [InlineData("Uploaded")]
    [InlineData("Configured")]
    [InlineData("Priced")]
    [InlineData("Confirmed")]
    [InlineData("Quoted")]
    [InlineData("Ordered")]
    [InlineData("InProduction")]
    [InlineData("QualityCheck")]
    [InlineData("Approved")]
    [InlineData("Delivered")]
    [InlineData("Removed")]
    public void PartStatus_AllExpectedValuesExist(string statusName)
    {
        var exists = Enum.TryParse<PartStatus>(statusName, out _);
        Assert.True(exists, $"PartStatus.{statusName} should exist");
    }

    // ── ManufacturingProcess enum completeness ────────────────────────────

    [Theory]
    [InlineData("FDM")]
    [InlineData("SLA")]
    [InlineData("SLS")]
    [InlineData("CNC_Milling")]
    [InlineData("CNC_Turning")]
    [InlineData("SheetMetal_Cutting")]
    [InlineData("Assembly")]
    public void ManufacturingProcess_AllExpectedValuesExist(string processName)
    {
        var exists = Enum.TryParse<ManufacturingProcess>(processName, out _);
        Assert.True(exists, $"ManufacturingProcess.{processName} should exist");
    }

    // ── MappingExtensions: Project → DetailResponse ───────────────────────

    [Fact]
    public void ToDetailResponse_MapsAllScalarFields()
    {
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = id,
            ProjectNumber = "PRJ-2026-0001",
            CustomerId = customerId,
            CustomerName = "Acme Corp",
            Title = "Test Project",
            Description = "Description",
            Status = ProjectStatus.Configuring,
            Currency = "THB",
            TotalEstimatedPrice = 9999.50m,
            CreatedBy = "user-123",
            CreatedByName = "John Doe",
            CreatedAt = now,
            UpdatedAt = now,
            Parts = [],
            Notes = []
        };

        var dto = project.ToDetailResponse(42);

        Assert.Equal(id, dto.Id);
        Assert.Equal("PRJ-2026-0001", dto.ProjectNumber);
        Assert.Equal(customerId, dto.CustomerId);
        Assert.Equal("Acme Corp", dto.CustomerName);
        Assert.Equal("Test Project", dto.Title);
        Assert.Equal("Configuring", dto.Status);
        Assert.Equal("THB", dto.Currency);
        Assert.Equal(9999.50m, dto.TotalEstimatedPrice);
        Assert.Empty(dto.Parts);
        Assert.Empty(dto.Notes);
    }

    [Fact]
    public void ToDetailResponse_ExcludesRemovedParts()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Parts =
            [
                new ProjectPart { Id = Guid.NewGuid(), PartNumber = 1, Status = PartStatus.Confirmed, FileName = "a.stl" },
                new ProjectPart { Id = Guid.NewGuid(), PartNumber = 2, Status = PartStatus.Removed, FileName = "b.stl" },
                new ProjectPart { Id = Guid.NewGuid(), PartNumber = 3, Status = PartStatus.Priced, FileName = "c.stl" }
            ],
            Notes = []
        };

        var dto = project.ToDetailResponse(42);

        Assert.Equal(2, dto.Parts.Count);
        Assert.DoesNotContain(dto.Parts, p => p.FileName == "b.stl");
    }

    [Fact]
    public void ToDetailResponse_OrdersPartsByPartNumber()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Parts =
            [
                new ProjectPart { Id = Guid.NewGuid(), PartNumber = 3, Status = PartStatus.Uploaded, FileName = "c.stl" },
                new ProjectPart { Id = Guid.NewGuid(), PartNumber = 1, Status = PartStatus.Uploaded, FileName = "a.stl" },
                new ProjectPart { Id = Guid.NewGuid(), PartNumber = 2, Status = PartStatus.Uploaded, FileName = "b.stl" }
            ],
            Notes = []
        };

        var dto = project.ToDetailResponse(42);

        Assert.Equal("a.stl", dto.Parts[0].FileName);
        Assert.Equal("b.stl", dto.Parts[1].FileName);
        Assert.Equal("c.stl", dto.Parts[2].FileName);
    }

    // ── MappingExtensions: Project → SummaryResponse ──────────────────────

    [Fact]
    public void ToSummaryResponse_ConfirmedPartsCount_OnlyCountsConfirmedOrHigher()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Parts =
            [
                new ProjectPart { Status = PartStatus.Uploaded },
                new ProjectPart { Status = PartStatus.Priced },
                new ProjectPart { Status = PartStatus.Confirmed },
                new ProjectPart { Status = PartStatus.Ordered },
                new ProjectPart { Status = PartStatus.Removed }   // excluded
            ],
            Notes = []
        };

        var dto = project.ToSummaryResponse();

        Assert.Equal(4, dto.PartsCount); // excludes Removed
        Assert.Equal(2, dto.ConfirmedPartsCount); // Confirmed + Ordered
    }

    [Fact]
    public void ToSummaryResponse_PartPreviews_ReturnsActivePartThumbnailsInPartOrder()
    {
        var firstPartId = Guid.NewGuid();
        var secondPartId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Parts =
            [
                new ProjectPart
                {
                    Id = secondPartId,
                    PartNumber = 2,
                    FileName = "bracket.stl",
                    FileReference = "customers/c1/projects/p1/source/bracket.stl",
                    ThumbnailUrl = "https://signed.example/bracket.webp",
                    ThumbnailSmallGcsPath = "customers/c1/projects/p1/source/bracket_small.webp",
                    ThumbnailLargeGcsPath = "customers/c1/projects/p1/source/bracket_large.webp",
                    ProcessType = ManufacturingProcess.FDM,
                    MaterialName = "PLA",
                    Quantity = 3,
                    Status = PartStatus.Uploaded
                },
                new ProjectPart
                {
                    Id = firstPartId,
                    PartNumber = 1,
                    FileName = "fixture.step",
                    FileReference = "customers/c1/projects/p1/source/fixture.step",
                    ThumbnailSmallGcsPath = "customers/c1/projects/p1/source/fixture_small.webp",
                    ProcessType = ManufacturingProcess.CNC_Milling,
                    MaterialName = "Aluminium 6061",
                    Quantity = 1,
                    Status = PartStatus.Confirmed
                },
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    PartNumber = 3,
                    FileName = "removed.stl",
                    ThumbnailUrl = "https://signed.example/removed.webp",
                    Status = PartStatus.Removed
                }
            ],
            Notes = []
        };

        var dto = project.ToSummaryResponse();

        Assert.Equal(2, dto.PartPreviews.Count);
        Assert.Equal(firstPartId, dto.PartPreviews[0].Id);
        Assert.Equal("fixture.step", dto.PartPreviews[0].FileName);
        Assert.Equal("customers/c1/projects/p1/source/fixture_small.webp", dto.PartPreviews[0].ThumbnailSmallGcsPath);
        Assert.Equal("CNC_Milling", dto.PartPreviews[0].ProcessType);
        Assert.Equal("Aluminium 6061", dto.PartPreviews[0].MaterialName);
        Assert.Equal(1, dto.PartPreviews[0].Quantity);
        Assert.Equal(secondPartId, dto.PartPreviews[1].Id);
        Assert.Equal("https://signed.example/bracket.webp", dto.PartPreviews[1].ThumbnailUrl);
        Assert.DoesNotContain(dto.PartPreviews, preview => preview.FileName == "removed.stl");
    }

    // ── MappingExtensions: ProjectPart → PartResponse ─────────────────────

    [Fact]
    public void ToResponse_MapsAllPartFields()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var part = new ProjectPart
        {
            Id = id,
            ProjectId = projectId,
            PartNumber = 2,
            FileName = "bracket.stl",
            MaterialId = materialId,
            MaterialName = "PLA+ White",
            MaterialCode = "PLA-WH-175",
            ProcessType = ManufacturingProcess.FDM,
            Quantity = 5,
            FinishType = "Standard",
            AiSuggestedPrice = 250m,
            ConfirmedUnitPrice = 275m,
            PricingConfidence = 0.92m,
            Status = PartStatus.Confirmed,
            VolumeCm3 = 18.5m,
            BoundingBoxX = 100m,
            BoundingBoxY = 50m,
            BoundingBoxZ = 30m,
            IsManifold = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var dto = part.ToResponse(42);

        Assert.Equal(id, dto.Id);
        Assert.Equal(projectId, dto.ProjectId);
        Assert.Equal(2, dto.PartNumber);
        Assert.Equal("bracket.stl", dto.FileName);
        Assert.Equal(materialId, dto.MaterialId);
        Assert.Equal("PLA+ White", dto.MaterialName);
        Assert.Equal("FDM", dto.ProcessType);
        Assert.Equal(5, dto.Quantity);
        Assert.Equal(250m, dto.AiSuggestedPrice);
        Assert.Equal(275m, dto.ConfirmedUnitPrice);
        Assert.Equal(0.92m, dto.PricingConfidence);
        Assert.Equal("Confirmed", dto.Status);
        Assert.Equal(18.5m, dto.VolumeCm3);
        Assert.True(dto.IsManifold);
    }

    // ── ProjectNote mapping ────────────────────────────────────────────────

    [Fact]
    public void NoteToResponse_MapsCorrectly()
    {
        var noteId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string authorId = "user-abc-123";
        var now = DateTime.UtcNow;
        var note = new ProjectNote
        {
            Id = noteId,
            ProjectId = projectId,
            AuthorId = authorId,
            AuthorName = "Jane Smith",
            Content = "Customer wants matte finish.",
            CreatedAt = now
        };

        var dto = note.ToResponse();

        Assert.Equal(noteId, dto.Id);
        Assert.Equal(projectId, dto.ProjectId);
        Assert.Equal(authorId, dto.AuthorId);
        Assert.Equal("Jane Smith", dto.AuthorName);
        Assert.Equal("Customer wants matte finish.", dto.Content);
        Assert.Equal(now, dto.CreatedAt);
    }
}
