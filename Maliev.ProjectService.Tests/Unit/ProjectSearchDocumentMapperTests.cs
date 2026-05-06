using Maliev.MessagingContracts.Contracts.Search;
using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;
using Maliev.ProjectService.Infrastructure.Search;

namespace Maliev.ProjectService.Tests.Unit;

/// <summary>
/// Tests project search document mapping.
/// </summary>
public class ProjectSearchDocumentMapperTests
{
    /// <summary>
    /// Project search documents should include child part names and manufacturing terms.
    /// </summary>
    [Fact]
    public void ToUpsertEvents_WithProjectAndParts_IndexesProjectAndActivePartDocuments()
    {
        var project = CreateProject();
        var occurredAtUtc = DateTimeOffset.UtcNow;

        var events = ProjectSearchDocumentMapper.ToUpsertEvents(project, occurredAtUtc);

        Assert.Equal(2, events.Count);

        var projectDocument = Assert.Single(events, item => item.Payload.ResourceType == "project");
        Assert.Equal("ProjectService", projectDocument.Payload.SourceService);
        Assert.Equal(project.Id.ToString(), projectDocument.Payload.ResourceId);
        Assert.Equal("PRJ-2026-0001", projectDocument.Payload.Title);
        Assert.Equal("project.projects.read", projectDocument.Payload.RequiredPermission);
        Assert.Contains("d15-16.stp", projectDocument.Payload.Keywords);
        Assert.Contains("Polycarbonate", projectDocument.Payload.Keywords);

        var partDocument = Assert.Single(events, item => item.Payload.ResourceType == "project-part");
        Assert.Equal("d15-16.stp", partDocument.Payload.Title);
        Assert.Equal(ProjectSearchDocumentMapper.BuildPartResourceId(project.Id, project.Parts[0].Id), partDocument.Payload.ResourceId);
        Assert.Contains("38 x 22 x 38 mm", partDocument.Payload.Keywords);
        Assert.Contains("FDM", partDocument.Payload.Keywords);
        Assert.Contains("Standard FDM settings", partDocument.Payload.Keywords);
        Assert.DoesNotContain("removed.stl", partDocument.Payload.Keywords);
    }

    /// <summary>
    /// Project-part delete events should use the same composite key as upsert events.
    /// </summary>
    [Fact]
    public void ToPartDeletedEvent_WithPartId_UsesCompositeResourceId()
    {
        var projectId = Guid.NewGuid();
        var partId = Guid.NewGuid();

        var message = ProjectSearchDocumentMapper.ToPartDeletedEvent(projectId, partId, DateTimeOffset.UtcNow);

        Assert.IsType<SearchDocumentDeletedEvent>(message);
        Assert.Equal("ProjectService", message.Payload.SourceService);
        Assert.Equal("project-part", message.Payload.ResourceType);
        Assert.Equal($"{projectId}:{partId}", message.Payload.ResourceId);
    }

    private static Project CreateProject()
    {
        var projectId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        return new Project
        {
            Id = projectId,
            ProjectNumber = "PRJ-2026-0001",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Wanassriwilai Engineering",
            Title = "Project 2026-05-06",
            Status = ProjectStatus.QuotationGenerated,
            QuotationId = Guid.NewGuid(),
            QuotationNumber = "Q-0B4E8A0A",
            Currency = "THB",
            TotalEstimatedPrice = 9736.11m,
            CreatedBy = "employee:nat",
            CreatedByName = "Natthapol Wanasrivila",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Parts =
            [
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    PartNumber = 1,
                    FileName = "d15-16.stp",
                    FileId = Guid.NewGuid(),
                    FileReference = "customers/demo/projects/PRJ-2026-0001/d15-16.stp",
                    ProcessType = ManufacturingProcess.FDM,
                    MaterialId = materialId,
                    MaterialName = "Polycarbonate",
                    MaterialCode = "PC",
                    Quantity = 12,
                    FinishType = "As printed",
                    Color = "Natural",
                    Tolerance = "Standard FDM settings",
                    ProcessConfig = new Dictionary<string, string>
                    {
                        ["profile"] = "Standard FDM settings"
                    },
                    BoundingBoxX = 38,
                    BoundingBoxY = 22,
                    BoundingBoxZ = 38,
                    ConfirmedUnitPrice = 540.27m,
                    Status = PartStatus.Quoted,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ProjectPart
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    PartNumber = 2,
                    FileName = "removed.stl",
                    ProcessType = ManufacturingProcess.FDM,
                    Status = PartStatus.Removed,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
    }
}
