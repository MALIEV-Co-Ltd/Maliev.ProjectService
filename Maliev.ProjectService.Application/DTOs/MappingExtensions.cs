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
            ThumbnailSmallGcsPath = part.ThumbnailSmallGcsPath,
            ThumbnailLargeGcsPath = part.ThumbnailLargeGcsPath,
            GlbStoragePath = part.GlbStoragePath,
            OverlayPaths = new Dictionary<string, string>(part.OverlayPaths),
            ProcessType = part.ProcessType.ToString(),
            MaterialId = part.MaterialId,
            MaterialName = part.MaterialName,
            MaterialCode = part.MaterialCode,
            Quantity = part.Quantity,
            FinishType = part.FinishType,
            Color = part.Color,
            Tolerance = part.Tolerance,
            RoughnessCode = part.RoughnessCode,
            MarkingType = part.MarkingType,
            MarkingText = part.MarkingText,
            DfmAcknowledged = part.DfmAcknowledged,
            HasThreadedHoles = part.HasThreadedHoles,
            ThreadedHoleSpec = part.ThreadedHoleSpec,
            ThreadedHoleCount = part.ThreadedHoleCount,
            HasInserts = part.HasInserts,
            InsertType = part.InsertType,
            InsertCount = part.InsertCount,
            BagAndTag = part.BagAndTag,
            InspectionLevel = part.InspectionLevel,
            Certificates = [.. part.Certificates],
            DrawingFiles = part.DrawingFiles.Select(ToAttachmentDto).ToList(),
            SupplementaryFiles = part.SupplementaryFiles.Select(ToAttachmentDto).ToList(),
            ProcessConfig = new Dictionary<string, string>(part.ProcessConfig),
            BodyCount = part.BodyCount,
            BodiesJson = part.BodiesJson,
            SelectedBodyIndex = part.SelectedBodyIndex,
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

    /// <summary>Maps a persisted attachment to its DTO representation.</summary>
    public static ProjectPartAttachmentDto ToAttachmentDto(this ProjectPartAttachment attachment)
    {
        return new ProjectPartAttachmentDto
        {
            FileId = attachment.FileId,
            FileName = attachment.FileName,
            StoragePath = attachment.StoragePath,
            SignedUrl = attachment.SignedUrl,
            SizeBytes = attachment.SizeBytes,
            ContentType = attachment.ContentType,
            UploadedAt = attachment.UploadedAt
        };
    }

    /// <summary>Maps an attachment DTO to a persisted attachment value.</summary>
    public static ProjectPartAttachment ToAttachment(this ProjectPartAttachmentDto attachment)
    {
        return new ProjectPartAttachment
        {
            FileId = attachment.FileId,
            FileName = attachment.FileName,
            StoragePath = attachment.StoragePath,
            SignedUrl = attachment.SignedUrl,
            SizeBytes = attachment.SizeBytes,
            ContentType = attachment.ContentType,
            UploadedAt = attachment.UploadedAt
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
