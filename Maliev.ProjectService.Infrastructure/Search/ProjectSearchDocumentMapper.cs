using System.Globalization;
using Maliev.MessagingContracts.Contracts.Search;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;

namespace Maliev.ProjectService.Infrastructure.Search;

/// <summary>
/// Maps project records and project parts to centralized global search documents.
/// </summary>
public static class ProjectSearchDocumentMapper
{
    private const string SourceService = "ProjectService";
    private const string ProjectResourceType = "project";
    private const string ProjectPartResourceType = "project-part";
    private const string RequiredPermission = "project.projects.read";

    /// <summary>
    /// Creates search upsert events for the project and each active part.
    /// </summary>
    /// <param name="project">Project aggregate to index.</param>
    /// <param name="occurredAtUtc">Timestamp for the source change.</param>
    /// <returns>Search upsert events for the project and its active parts.</returns>
    public static IReadOnlyList<SearchDocumentUpsertedEvent> ToUpsertEvents(Project project, DateTimeOffset occurredAtUtc)
    {
        var events = new List<SearchDocumentUpsertedEvent>
        {
            ToProjectUpsertEvent(project, occurredAtUtc)
        };

        events.AddRange(project.Parts
            .Where(part => part.Status != PartStatus.Removed)
            .OrderBy(part => part.PartNumber)
            .Select(part => ToPartUpsertEvent(project, part, occurredAtUtc)));

        return events;
    }

    /// <summary>
    /// Creates a search upsert event for a project.
    /// </summary>
    /// <param name="project">Project to index.</param>
    /// <param name="occurredAtUtc">Timestamp for the source change.</param>
    /// <returns>A centralized search upsert event.</returns>
    public static SearchDocumentUpsertedEvent ToProjectUpsertEvent(Project project, DateTimeOffset occurredAtUtc)
    {
        var activeParts = project.Parts
            .Where(part => part.Status != PartStatus.Removed)
            .OrderBy(part => part.PartNumber)
            .ToList();

        var title = string.IsNullOrWhiteSpace(project.ProjectNumber)
            ? project.Title
            : project.ProjectNumber;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = project.Id.ToString();
        }

        var subtitle = JoinKeywords(project.Title, project.CustomerName, project.QuotationNumber);
        var summary = JoinKeywords(
            project.Description,
            project.Status.ToString(),
            project.Currency,
            project.TotalEstimatedPrice.ToString(CultureInfo.InvariantCulture),
            activeParts.Count == 1 ? "1 part" : $"{activeParts.Count} parts",
            string.Join(" ", activeParts.Select(part => part.FileName)),
            string.Join(" ", activeParts.Select(part => part.MaterialName)),
            string.Join(" ", activeParts.Select(part => part.MaterialCode)),
            string.Join(" ", activeParts.Select(part => FormatProcess(part.ProcessType))));

        var keywords = new List<string?>
        {
            project.Id.ToString(),
            project.ProjectNumber,
            project.Title,
            project.Description,
            project.CustomerId.ToString(),
            project.CustomerName,
            project.QuotationId?.ToString(),
            project.QuotationNumber,
            project.Status.ToString(),
            project.Currency,
            project.CreatedBy,
            project.CreatedByName
        };
        keywords.AddRange(activeParts.SelectMany(ProjectPartKeywords));

        return CreateUpsertEvent(
            ProjectResourceType,
            project.Id.ToString(),
            title,
            subtitle,
            summary,
            CompactKeywords(keywords.ToArray()),
            FormatProjectStatus(project.Status),
            project.Id,
            occurredAtUtc);
    }

    /// <summary>
    /// Creates a search upsert event for a project part.
    /// </summary>
    /// <param name="project">Parent project.</param>
    /// <param name="part">Part to index.</param>
    /// <param name="occurredAtUtc">Timestamp for the source change.</param>
    /// <returns>A centralized search upsert event.</returns>
    public static SearchDocumentUpsertedEvent ToPartUpsertEvent(Project project, ProjectPart part, DateTimeOffset occurredAtUtc)
    {
        var dimensionText = FormatDimensions(part);
        var title = string.IsNullOrWhiteSpace(part.FileName) ? $"Part {part.PartNumber}" : part.FileName;
        var subtitle = JoinKeywords(
            project.ProjectNumber,
            FormatProcess(part.ProcessType),
            part.MaterialName ?? part.MaterialCode,
            dimensionText);
        var summary = JoinKeywords(
            project.Title,
            project.CustomerName,
            part.Status.ToString(),
            part.Quantity.ToString(CultureInfo.InvariantCulture),
            part.FinishType,
            part.Color,
            part.Tolerance,
            part.RoughnessCode,
            part.MarkingType,
            part.MarkingText,
            part.ThreadedHoleSpec,
            part.InsertType,
            part.InspectionLevel,
            string.Join(" ", part.Certificates),
            part.ThreadsInserts,
            part.CustomNotes,
            FormatPrice(part));

        var keywords = new List<string?>
        {
            BuildPartResourceId(project.Id, part.Id),
            project.Id.ToString(),
            project.ProjectNumber,
            project.Title,
            project.CustomerId.ToString(),
            project.CustomerName,
            project.QuotationId?.ToString(),
            project.QuotationNumber,
            part.Id.ToString(),
            part.PartNumber.ToString(CultureInfo.InvariantCulture),
            part.FileId?.ToString(),
            part.FileReference,
            part.FileName,
            FileNameWithoutExtension(part.FileName),
            Path.GetExtension(part.FileName),
            FormatProcess(part.ProcessType),
            part.ProcessType.ToString(),
            part.MaterialId?.ToString(),
            part.MaterialName,
            part.MaterialCode,
            part.Quantity.ToString(CultureInfo.InvariantCulture),
            part.FinishType,
            part.Color,
            part.Tolerance,
            part.RoughnessCode,
            part.MarkingType,
            part.MarkingText,
            part.ThreadedHoleSpec,
            part.ThreadedHoleCount.ToString(CultureInfo.InvariantCulture),
            part.InsertType,
            part.InsertCount.ToString(CultureInfo.InvariantCulture),
            part.InspectionLevel,
            dimensionText,
            part.BodyCount?.ToString(),
            part.SelectedBodyIndex?.ToString(),
            part.ThreadsInserts,
            part.CustomNotes,
            part.OrderId?.ToString(),
            part.OrderItemId?.ToString(),
            part.JobId?.ToString(),
            part.Status.ToString()
        };
        keywords.AddRange(part.Certificates);
        keywords.AddRange(part.ProcessConfig.SelectMany(pair => new[] { pair.Key, pair.Value, $"{pair.Key} {pair.Value}" }));
        keywords.AddRange(part.DrawingFiles.SelectMany(AttachmentKeywords));
        keywords.AddRange(part.SupplementaryFiles.SelectMany(AttachmentKeywords));

        return CreateUpsertEvent(
            ProjectPartResourceType,
            BuildPartResourceId(project.Id, part.Id),
            title,
            subtitle,
            summary,
            CompactKeywords(keywords.ToArray()),
            part.Status.ToString(),
            part.Id,
            occurredAtUtc);
    }

    /// <summary>
    /// Creates a search delete event for a project.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <param name="occurredAtUtc">Timestamp for the source change.</param>
    /// <returns>A centralized search delete event.</returns>
    public static SearchDocumentDeletedEvent ToProjectDeletedEvent(Guid projectId, DateTimeOffset occurredAtUtc)
    {
        return CreateDeletedEvent(ProjectResourceType, projectId.ToString(), projectId, occurredAtUtc);
    }

    /// <summary>
    /// Creates a search delete event for a project part.
    /// </summary>
    /// <param name="projectId">Parent project identifier.</param>
    /// <param name="partId">Part identifier.</param>
    /// <param name="occurredAtUtc">Timestamp for the source change.</param>
    /// <returns>A centralized search delete event.</returns>
    public static SearchDocumentDeletedEvent ToPartDeletedEvent(Guid projectId, Guid partId, DateTimeOffset occurredAtUtc)
    {
        return CreateDeletedEvent(ProjectPartResourceType, BuildPartResourceId(projectId, partId), partId, occurredAtUtc);
    }

    /// <summary>
    /// Builds the resource identifier used for project part search documents.
    /// </summary>
    /// <param name="projectId">Parent project identifier.</param>
    /// <param name="partId">Part identifier.</param>
    /// <returns>Composite project-part identifier.</returns>
    public static string BuildPartResourceId(Guid projectId, Guid partId) => $"{projectId}:{partId}";

    private static SearchDocumentUpsertedEvent CreateUpsertEvent(
        string resourceType,
        string resourceId,
        string title,
        string? subtitle,
        string? summary,
        IReadOnlyList<string> keywords,
        string status,
        Guid correlationId,
        DateTimeOffset occurredAtUtc)
    {
        return new SearchDocumentUpsertedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchDocumentUpsertedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: SourceService,
            ConsumedBy: ["SearchService"],
            CorrelationId: correlationId,
            CausationId: null,
            OccurredAtUtc: occurredAtUtc,
            IsPublic: false,
            Payload: new SearchDocumentUpsertedEventPayload(
                SourceService: SourceService,
                ResourceType: resourceType,
                ResourceId: resourceId,
                Title: title,
                Subtitle: subtitle,
                Summary: summary,
                Keywords: keywords,
                Status: status,
                RequiredPermission: RequiredPermission,
                OccurredAtUtc: occurredAtUtc));
    }

    private static SearchDocumentDeletedEvent CreateDeletedEvent(
        string resourceType,
        string resourceId,
        Guid correlationId,
        DateTimeOffset occurredAtUtc)
    {
        return new SearchDocumentDeletedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchDocumentDeletedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: SourceService,
            ConsumedBy: ["SearchService"],
            CorrelationId: correlationId,
            CausationId: null,
            OccurredAtUtc: occurredAtUtc,
            IsPublic: false,
            Payload: new SearchDocumentDeletedEventPayload(
                SourceService: SourceService,
                ResourceType: resourceType,
                ResourceId: resourceId,
                OccurredAtUtc: occurredAtUtc));
    }

    private static IEnumerable<string?> ProjectPartKeywords(ProjectPart part)
    {
        yield return part.Id.ToString();
        yield return part.FileId?.ToString();
        yield return part.FileName;
        yield return FileNameWithoutExtension(part.FileName);
        yield return part.FileReference;
        yield return FormatProcess(part.ProcessType);
        yield return part.ProcessType.ToString();
        yield return part.MaterialId?.ToString();
        yield return part.MaterialName;
        yield return part.MaterialCode;
        yield return FormatDimensions(part);
        yield return part.FinishType;
        yield return part.Color;
        yield return part.Tolerance;
        yield return part.RoughnessCode;
        yield return part.MarkingType;
        yield return part.MarkingText;
        yield return part.ThreadsInserts;
        yield return part.CustomNotes;
        yield return part.OrderId?.ToString();
        yield return part.JobId?.ToString();
    }

    private static IEnumerable<string?> AttachmentKeywords(ProjectPartAttachment attachment)
    {
        yield return attachment.FileId?.ToString();
        yield return attachment.FileName;
        yield return FileNameWithoutExtension(attachment.FileName);
        yield return attachment.StoragePath;
        yield return attachment.ContentType;
    }

    private static IReadOnlyList<string> CompactKeywords(params string?[] values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? JoinKeywords(params string?[] values)
    {
        var keywords = CompactKeywords(values);
        return keywords.Count == 0 ? null : string.Join(" - ", keywords);
    }

    private static string FormatProcess(ManufacturingProcess process)
    {
        return process.ToString().Replace("_", " ", StringComparison.Ordinal);
    }

    private static string FormatProjectStatus(ProjectStatus status)
    {
        return status switch
        {
            ProjectStatus.QuotationGenerated => "Generated",
            ProjectStatus.QuotationSent => "Sent",
            ProjectStatus.QuotationAccepted => "Accepted",
            _ => status.ToString()
        };
    }

    private static string? FormatDimensions(ProjectPart part)
    {
        if (part.BoundingBoxX is null || part.BoundingBoxY is null || part.BoundingBoxZ is null)
        {
            return null;
        }

        return $"{FormatNumber(part.BoundingBoxX.Value)} x {FormatNumber(part.BoundingBoxY.Value)} x {FormatNumber(part.BoundingBoxZ.Value)} mm";
    }

    private static string FormatNumber(decimal value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string? FormatPrice(ProjectPart part)
    {
        var unitPrice = part.ConfirmedUnitPrice ?? part.AiSuggestedPrice;
        if (unitPrice is null)
        {
            return null;
        }

        return unitPrice.Value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string? FileNameWithoutExtension(string? fileName)
    {
        return string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileNameWithoutExtension(fileName);
    }
}
