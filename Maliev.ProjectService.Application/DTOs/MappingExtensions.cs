using Maliev.ProjectService.Application.DTOs;
using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;

namespace Maliev.ProjectService.Application.DTOs;

/// <summary>
/// Manual mapping extension methods between domain entities and DTOs.
/// No AutoMapper — per Maliev Constitution.
/// </summary>
public static class MappingExtensions
{
    /// <summary>Maps a <see cref="Project"/> entity to a <see cref="ProjectDetailResponse"/>.</summary>
    public static ProjectDetailResponse ToDetailResponse(this Project project)
    {
        return new ProjectDetailResponse
        {
            Id = project.Id,
            ProjectNumber = project.ProjectNumber,
            CustomerId = project.CustomerId,
            CustomerName = project.CustomerName,
            Title = project.Title,
            Description = project.Description,
            Status = project.Status.ToString(),
            QuotationId = project.QuotationId,
            QuotationNumber = project.QuotationNumber,
            TotalEstimatedPrice = project.TotalEstimatedPrice,
            Currency = project.Currency,
            ValidUntil = project.ValidUntil,
            CreatedBy = project.CreatedBy,
            CreatedByName = project.CreatedByName,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            Parts = project.Parts
                .Where(p => p.Status != PartStatus.Removed)
                .OrderBy(p => p.PartNumber)
                .Select(p => p.ToResponse())
                .ToList(),
            Notes = project.Notes
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => n.ToResponse())
                .ToList()
        };
    }

    /// <summary>Maps a <see cref="Project"/> entity to a <see cref="ProjectSummaryResponse"/>.</summary>
    public static ProjectSummaryResponse ToSummaryResponse(this Project project)
    {
        var activeParts = project.Parts.Where(p => p.Status != PartStatus.Removed).ToList();
        return new ProjectSummaryResponse
        {
            Id = project.Id,
            ProjectNumber = project.ProjectNumber,
            CustomerId = project.CustomerId,
            CustomerName = project.CustomerName,
            Title = project.Title,
            Status = project.Status.ToString(),
            PartsCount = activeParts.Count,
            ConfirmedPartsCount = activeParts.Count(p => p.Status >= PartStatus.Confirmed),
            TotalEstimatedPrice = project.TotalEstimatedPrice,
            Currency = project.Currency,
            QuotationNumber = project.QuotationNumber,
            CreatedAt = project.CreatedAt,
            CreatedByName = project.CreatedByName
        };
    }

    /// <summary>Maps a <see cref="ProjectPart"/> entity to a <see cref="ProjectPartResponse"/>.</summary>
    public static ProjectPartResponse ToResponse(this ProjectPart part)
    {
        return new ProjectPartResponse
        {
            Id = part.Id,
            ProjectId = part.ProjectId,
            PartNumber = part.PartNumber,
            FileName = part.FileName,
            FileId = part.FileId,
            FileReference = part.FileReference,
            ThumbnailUrl = part.ThumbnailUrl,
            ProcessType = part.ProcessType.ToString(),
            MaterialId = part.MaterialId,
            MaterialName = part.MaterialName,
            MaterialCode = part.MaterialCode,
            Quantity = part.Quantity,
            FinishType = part.FinishType,
            Color = part.Color,
            Tolerance = part.Tolerance,
            ThreadsInserts = part.ThreadsInserts,
            CustomNotes = part.CustomNotes,
            VolumeCm3 = part.VolumeCm3,
            BoundingBoxX = part.BoundingBoxX,
            BoundingBoxY = part.BoundingBoxY,
            BoundingBoxZ = part.BoundingBoxZ,
            IsManifold = part.IsManifold,
            AiSuggestedPrice = part.AiSuggestedPrice,
            ConfirmedUnitPrice = part.ConfirmedUnitPrice,
            PriceOverrideReason = part.PriceOverrideReason,
            PricingConfidence = part.PricingConfidence,
            PricingStrategy = part.PricingStrategy,
            OrderId = part.OrderId,
            OrderItemId = part.OrderItemId,
            JobId = part.JobId,
            Status = part.Status.ToString(),
            CreatedAt = part.CreatedAt,
            UpdatedAt = part.UpdatedAt
        };
    }

    /// <summary>Maps a <see cref="ProjectNote"/> entity to a <see cref="ProjectNoteResponse"/>.</summary>
    public static ProjectNoteResponse ToResponse(this ProjectNote note)
    {
        return new ProjectNoteResponse
        {
            Id = note.Id,
            ProjectId = note.ProjectId,
            AuthorName = note.AuthorName,
            AuthorId = note.AuthorId,
            Content = note.Content,
            CreatedAt = note.CreatedAt
        };
    }
}
