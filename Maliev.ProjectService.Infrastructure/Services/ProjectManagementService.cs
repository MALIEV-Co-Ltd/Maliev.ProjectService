using Maliev.MessagingContracts.Contracts.Projects;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Application.DTOs;
using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;
using Maliev.ProjectService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Maliev.ProjectService.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="IProjectService"/>. Handles the full project lifecycle.
/// </summary>
public class ProjectManagementService : IProjectService
{
    private readonly ProjectDbContext _db;
    private readonly ProjectNumberGenerator _numberGenerator;
    private readonly IPricingServiceClient _pricingClient;
    private readonly IQuotationServiceClient _quotationClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProjectManagementService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static string CacheKey(Guid id) => $"project:{id}";

    /// <summary>Initializes a new instance of <see cref="ProjectManagementService"/>.</summary>
    public ProjectManagementService(
        ProjectDbContext db,
        ProjectNumberGenerator numberGenerator,
        IPricingServiceClient pricingClient,
        IQuotationServiceClient quotationClient,
        IPublishEndpoint publishEndpoint,
        IDistributedCache cache,
        ILogger<ProjectManagementService> logger)
    {
        _db = db;
        _numberGenerator = numberGenerator;
        _pricingClient = pricingClient;
        _quotationClient = quotationClient;
        _publishEndpoint = publishEndpoint;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProjectDetailResponse> CreateAsync(
        CreateProjectRequest request,
        string principalId,
        string principalName,
        CancellationToken ct = default)
    {
        var projectNumber = await _numberGenerator.GenerateAsync(ct);
        var now = DateTime.UtcNow;

        var project = new Project
        {
            Id = Guid.NewGuid(),
            ProjectNumber = projectNumber,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            Title = request.Title,
            Description = request.Description,
            Status = ProjectStatus.Draft,
            Currency = request.Currency,
            TotalEstimatedPrice = 0m,
            CreatedBy = principalId,
            CreatedByName = principalName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        await PublishEventSafeAsync(new ProjectCreatedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "ProjectCreatedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "ProjectService",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: project.Id,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new ProjectCreatedEventPayload(
                ProjectId: project.Id,
                ProjectNumber: project.ProjectNumber,
                CustomerId: project.CustomerId,
                CustomerName: project.CustomerName,
                CreatedBy: principalId,
                CreatedAt: project.CreatedAt
            )
        ), "ProjectCreated", ct);

        _logger.LogInformation("Project {ProjectNumber} created by {PrincipalId}", projectNumber, principalId);
        return project.ToDetailResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectDetailResponse?> GetByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.Parts)
            .Include(p => p.Notes)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        return project?.ToDetailResponse();
    }

    /// <inheritdoc />
    public async Task<PaginatedProjectResponse> SearchAsync(ProjectFilterRequest filter, CancellationToken ct = default)
    {
        var pageSize = Math.Min(filter.PageSize, 100);
        var page = Math.Max(filter.Page, 1);

        var query = _db.Projects
            .Include(p => p.Parts)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.ToLower();
            query = query.Where(p =>
                p.ProjectNumber.ToLower().Contains(q) ||
                p.Title.ToLower().Contains(q) ||
                p.CustomerName.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<ProjectStatus>(filter.Status, ignoreCase: true, out var statusEnum))
        {
            query = query.Where(p => p.Status == statusEnum);
        }

        if (filter.CustomerId.HasValue)
            query = query.Where(p => p.CustomerId == filter.CustomerId.Value);

        var total = await query.CountAsync(ct);
        var data = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedProjectResponse
        {
            Data = data.Select(p => p.ToSummaryResponse()).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    /// <inheritdoc />
    public async Task<ProjectDetailResponse> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        string principalId,
        CancellationToken ct = default)
    {
        var project = await GetProjectOrThrowAsync(projectId, ct);

        project.Title = request.Title;
        project.Description = request.Description;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        return project.ToDetailResponse();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid projectId, string principalId, CancellationToken ct = default)
    {
        var project = await GetProjectOrThrowAsync(projectId, ct);

        if (project.Status != ProjectStatus.Draft && project.Status != ProjectStatus.Configuring)
            throw new InvalidOperationException($"Project {project.ProjectNumber} cannot be deleted in {project.Status} status. Only Draft or Configuring projects may be deleted.");

        project.IsDeleted = true;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        _logger.LogInformation("Project {ProjectNumber} soft-deleted by {PrincipalId}", project.ProjectNumber, principalId);
    }

    /// <inheritdoc />
    public async Task<ProjectPartResponse> AddPartAsync(
        Guid projectId,
        AddProjectPartRequest request,
        string principalId,
        CancellationToken ct = default)
    {
        var project = await GetProjectOrThrowAsync(projectId, ct, includeParts: true);

        var nextPartNumber = project.Parts.Any()
            ? project.Parts.Max(p => p.PartNumber) + 1
            : 1;

        var now = DateTime.UtcNow;
        var part = new ProjectPart
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PartNumber = nextPartNumber,
            FileName = request.FileName,
            FileId = request.FileId,
            FileReference = request.FileReference,
            ThumbnailSmallGcsPath = request.ThumbnailSmallGcsPath,
            ThumbnailLargeGcsPath = request.ThumbnailLargeGcsPath,
            GlbStoragePath = request.GlbStoragePath,
            OverlayPaths = new Dictionary<string, string>(request.OverlayPaths),
            ProcessType = request.ProcessType,
            MaterialId = request.MaterialId,
            MaterialName = request.MaterialName,
            MaterialCode = request.MaterialCode,
            Quantity = request.Quantity,
            FinishType = request.FinishType,
            Color = request.Color,
            Tolerance = request.Tolerance,
            RoughnessCode = request.RoughnessCode,
            MarkingType = request.MarkingType,
            MarkingText = request.MarkingText,
            DfmAcknowledged = request.DfmAcknowledged,
            HasDfmWarnings = request.HasDfmWarnings,
            HasThreadedHoles = request.HasThreadedHoles,
            ThreadedHoleSpec = request.ThreadedHoleSpec,
            ThreadedHoleCount = request.ThreadedHoleCount,
            HasInserts = request.HasInserts,
            InsertType = request.InsertType,
            InsertCount = request.InsertCount,
            BagAndTag = request.BagAndTag,
            InspectionLevel = request.InspectionLevel,
            Certificates = [.. request.Certificates],
            DrawingFiles = request.DrawingFiles.Select(attachment => attachment.ToAttachment()).ToList(),
            SupplementaryFiles = request.SupplementaryFiles.Select(attachment => attachment.ToAttachment()).ToList(),
            ProcessConfig = new Dictionary<string, string>(request.ProcessConfig),
            BodyCount = request.BodyCount,
            BodiesJson = request.BodiesJson,
            SelectedBodyIndex = request.SelectedBodyIndex,
            ThreadsInserts = request.ThreadsInserts,
            CustomNotes = request.CustomNotes,
            VolumeCm3 = request.VolumeCm3,
            SupportVolumeCm3 = request.SupportVolumeCm3,
            SurfaceAreaCm2 = request.SurfaceAreaCm2,
            BoundingBoxX = request.BoundingBoxX,
            BoundingBoxY = request.BoundingBoxY,
            BoundingBoxZ = request.BoundingBoxZ,
            IsManifold = request.IsManifold,
            Status = PartStatus.Uploaded,
            CreatedAt = now,
            UpdatedAt = now
        };

        // If process, material are configured, advance to Configured status
        if (request.ProcessType != default && request.MaterialId.HasValue)
            part.Status = PartStatus.Configured;

        _db.ProjectParts.Add(part);

        // Advance project to Configuring if still Draft
        if (project.Status == ProjectStatus.Draft)
        {
            project.Status = ProjectStatus.Configuring;
        }
        project.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        return part.ToResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectPartResponse> UpdatePartAsync(
        Guid projectId,
        Guid partId,
        UpdateProjectPartRequest request,
        string principalId,
        CancellationToken ct = default)
    {
        var part = await GetPartOrThrowAsync(projectId, partId, ct);
        var now = DateTime.UtcNow;

        if (request.ProcessType.HasValue) part.ProcessType = request.ProcessType.Value;
        if (request.MaterialId.HasValue) part.MaterialId = request.MaterialId.Value;
        if (request.MaterialName is not null) part.MaterialName = request.MaterialName;
        if (request.MaterialCode is not null) part.MaterialCode = request.MaterialCode;
        if (request.Quantity.HasValue) part.Quantity = request.Quantity.Value;
        if (request.FinishType is not null) part.FinishType = request.FinishType;
        if (request.Color is not null) part.Color = request.Color;
        if (request.Tolerance is not null) part.Tolerance = request.Tolerance;
        if (request.RoughnessCode is not null) part.RoughnessCode = request.RoughnessCode;
        if (request.MarkingType is not null) part.MarkingType = request.MarkingType;
        if (request.MarkingText is not null) part.MarkingText = request.MarkingText;
        if (request.DfmAcknowledged.HasValue) part.DfmAcknowledged = request.DfmAcknowledged.Value;
        if (request.HasDfmWarnings.HasValue) part.HasDfmWarnings = request.HasDfmWarnings.Value;
        if (request.HasThreadedHoles.HasValue) part.HasThreadedHoles = request.HasThreadedHoles.Value;
        if (request.ThreadedHoleSpec is not null) part.ThreadedHoleSpec = request.ThreadedHoleSpec;
        if (request.ThreadedHoleCount.HasValue) part.ThreadedHoleCount = request.ThreadedHoleCount.Value;
        if (request.HasInserts.HasValue) part.HasInserts = request.HasInserts.Value;
        if (request.InsertType is not null) part.InsertType = request.InsertType;
        if (request.InsertCount.HasValue) part.InsertCount = request.InsertCount.Value;
        if (request.BagAndTag.HasValue) part.BagAndTag = request.BagAndTag.Value;
        if (request.InspectionLevel is not null) part.InspectionLevel = request.InspectionLevel;
        if (request.Certificates is not null) part.Certificates = [.. request.Certificates];
        if (request.DrawingFiles is not null) part.DrawingFiles = request.DrawingFiles.Select(attachment => attachment.ToAttachment()).ToList();
        if (request.SupplementaryFiles is not null) part.SupplementaryFiles = request.SupplementaryFiles.Select(attachment => attachment.ToAttachment()).ToList();
        if (request.ProcessConfig is not null) part.ProcessConfig = new Dictionary<string, string>(request.ProcessConfig);
        if (request.ThumbnailSmallGcsPath is not null) part.ThumbnailSmallGcsPath = request.ThumbnailSmallGcsPath;
        if (request.ThumbnailLargeGcsPath is not null) part.ThumbnailLargeGcsPath = request.ThumbnailLargeGcsPath;
        if (request.GlbStoragePath is not null) part.GlbStoragePath = request.GlbStoragePath;
        if (request.OverlayPaths is not null) part.OverlayPaths = new Dictionary<string, string>(request.OverlayPaths);
        if (request.BodyCount.HasValue) part.BodyCount = request.BodyCount.Value;
        if (request.BodiesJson is not null) part.BodiesJson = request.BodiesJson;
        if (request.SelectedBodyIndex.HasValue) part.SelectedBodyIndex = request.SelectedBodyIndex.Value;
        if (request.ThreadsInserts is not null) part.ThreadsInserts = request.ThreadsInserts;
        if (request.CustomNotes is not null) part.CustomNotes = request.CustomNotes;

        // If configuration is now complete, advance to Configured
        if (part.ProcessType != default && part.MaterialId.HasValue && part.Status == PartStatus.Uploaded)
            part.Status = PartStatus.Configured;

        // If configuration changed after pricing, reset to Configured (price may be stale)
        if (part.Status == PartStatus.Priced || part.Status == PartStatus.Confirmed)
        {
            part.AiSuggestedPrice = null;
            part.ConfirmedUnitPrice = null;
            part.PricingConfidence = null;
            part.Status = PartStatus.Configured;
        }

        part.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        return part.ToResponse();
    }

    /// <inheritdoc />
    public async Task RemovePartAsync(Guid projectId, Guid partId, string principalId, CancellationToken ct = default)
    {
        var part = await GetPartOrThrowAsync(projectId, partId, ct);

        if (part.Status >= PartStatus.Ordered)
            throw new InvalidOperationException($"Part '{part.FileName}' cannot be removed after an order has been placed.");

        part.Status = PartStatus.Removed;
        part.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);
    }

    /// <inheritdoc />
    public async Task<ProjectPartResponse> RequestPricingAsync(
        Guid projectId,
        Guid partId,
        string principalId,
        CancellationToken ct = default)
    {
        var part = await GetPartOrThrowAsync(projectId, partId, ct);
        var project = await GetProjectOrThrowAsync(projectId, ct);

        if (part.ProcessType == default)
            throw new InvalidOperationException("Part must have a manufacturing process selected before requesting pricing.");

        var pricingRequest = new CalculatePriceRequest
        {
            FileId = part.FileId,
            CustomerId = project.CustomerId,
            MaterialId = part.MaterialId,
            MaterialCode = part.MaterialCode ?? string.Empty,
            ManufacturingProcessId = (int)part.ProcessType,
            ManufacturingProcessName = part.ProcessType.ToString(),
            Quantity = part.Quantity,
            CorrelationId = $"{projectId}:{partId}",
            Geometry = part.VolumeCm3.HasValue ? new GeometryMetricsDto
            {
                VolumeCm3 = part.VolumeCm3.Value,
                SupportVolumeCm3 = part.SupportVolumeCm3 ?? 0,
                SurfaceAreaCm2 = part.SurfaceAreaCm2 ?? 0,
                BoundingBoxX = part.BoundingBoxX ?? 0,
                BoundingBoxY = part.BoundingBoxY ?? 0,
                BoundingBoxZ = part.BoundingBoxZ ?? 0,
                IsManifold = part.IsManifold ?? false
            } : null
        };

        var result = await _pricingClient.CalculateAsync(pricingRequest, ct);

        part.AiSuggestedPrice = result.TotalUnitPrice;
        part.PricingConfidence = result.ConfidenceLevel;
        part.PricingStrategy = result.PricingStrategyValue;
        part.Status = PartStatus.Priced;
        part.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        _logger.LogInformation("Pricing for part {PartId} in project {ProjectId}: {Price} THB (confidence: {Confidence:P0})",
            partId, projectId, result.TotalUnitPrice, result.ConfidenceLevel);

        return part.ToResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectPartResponse> ConfirmPriceAsync(
        Guid projectId,
        Guid partId,
        ConfirmPartPriceRequest request,
        string principalId,
        CancellationToken ct = default)
    {
        var part = await GetPartOrThrowAsync(projectId, partId, ct);

        if (part.AiSuggestedPrice is null && request.ConfirmedUnitPrice is null)
            throw new InvalidOperationException("Part must have an AI-suggested price before confirming. Request pricing first.");

        part.ConfirmedUnitPrice = request.ConfirmedUnitPrice ?? part.AiSuggestedPrice;
        part.PriceOverrideReason = request.PriceOverrideReason;
        part.Status = PartStatus.Confirmed;
        part.UpdatedAt = DateTime.UtcNow;

        // Recompute project total
        await _db.SaveChangesAsync(ct);
        await RecalculateProjectTotalAsync(projectId, ct);
        await InvalidateCacheAsync(projectId);

        return part.ToResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectDetailResponse> GenerateQuotationAsync(
        Guid projectId,
        GenerateQuotationRequest request,
        string principalId,
        CancellationToken ct = default)
    {
        var project = await GetProjectOrThrowAsync(projectId, ct, includeParts: true, includeNotes: true);

        var activeParts = project.Parts.Where(p => p.Status != PartStatus.Removed).ToList();
        var unconfirmed = activeParts.Where(p => p.Status < PartStatus.Confirmed).ToList();

        if (unconfirmed.Any())
            throw new InvalidOperationException($"{unconfirmed.Count} part(s) do not have confirmed prices. Confirm all part prices before generating a quotation.");

        if (!activeParts.Any())
            throw new InvalidOperationException("Project has no active parts. Add at least one part before generating a quotation.");

        var partsMissingMaterial = activeParts.Where(p => !p.MaterialId.HasValue).ToList();
        if (partsMissingMaterial.Any())
            throw new InvalidOperationException($"{partsMissingMaterial.Count} part(s) do not have a material selected. Select materials before generating a quotation.");

        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(request.ValidityDays);

        var quotationRequest = new CreateQuotationFromProjectRequest
        {
            CustomerId = project.CustomerId,
            ValidityPeriodStart = start,
            ValidityPeriodEnd = end,
            DeliveryExpectations = request.DeliveryExpectations,
            BulkDiscountAmount = Math.Max(0m, request.BulkDiscountAmount),
            InternalNote = $"Generated from project {project.ProjectNumber}",
            Items = activeParts.Select(p => new QuotationLineItemRequest
            {
                Description = $"{p.FileName} - {p.ProcessType} ({p.MaterialName ?? p.MaterialCode ?? "Unspecified material"})",
                MaterialServiceId = p.MaterialId!.Value,
                Quantity = p.Quantity,
                UnitOfMeasure = "pcs",
                UnitPrice = p.ConfirmedUnitPrice ?? p.AiSuggestedPrice ?? 0m,
                ManufacturingProcess = p.ProcessType.ToString(),
                Notes = p.MaterialCode
            }).ToList()
        };

        var created = await _quotationClient.CreateQuotationAsync(quotationRequest, ct);

        project.QuotationId = created.QuotationId;
        project.QuotationNumber = created.QuotationNumber;
        project.ValidUntil = end;
        project.Status = ProjectStatus.QuotationGenerated;
        var quotedSubtotal = activeParts.Sum(p => (p.ConfirmedUnitPrice ?? p.AiSuggestedPrice ?? 0m) * p.Quantity);
        project.TotalEstimatedPrice = Math.Max(0m, quotedSubtotal - Math.Max(0m, request.BulkDiscountAmount));
        project.UpdatedAt = DateTime.UtcNow;

        foreach (var part in activeParts)
        {
            part.Status = PartStatus.Quoted;
            part.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        await PublishEventSafeAsync(new ProjectQuotationGeneratedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "ProjectQuotationGeneratedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "ProjectService",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: project.Id,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new ProjectQuotationGeneratedEventPayload(
                ProjectId: project.Id,
                ProjectNumber: project.ProjectNumber,
                QuotationId: created.QuotationId,
                QuotationNumber: created.QuotationNumber,
                CustomerId: project.CustomerId,
                TotalAmount: (double)project.TotalEstimatedPrice,
                Currency: project.Currency,
                GeneratedAt: DateTimeOffset.UtcNow
            )
        ), "ProjectQuotationGenerated", ct);

        _logger.LogInformation("Quotation {QuotationNumber} generated for project {ProjectNumber}",
            created.QuotationNumber, project.ProjectNumber);

        return project.ToDetailResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectDetailResponse> MarkQuotationSentAsync(
        Guid projectId,
        string principalId,
        CancellationToken ct = default)
    {
        var project = await GetProjectOrThrowAsync(projectId, ct);

        if (project.Status != ProjectStatus.QuotationGenerated)
            throw new InvalidOperationException($"Project must be in QuotationGenerated status to mark as sent. Current: {project.Status}");

        project.Status = ProjectStatus.QuotationSent;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        return project.ToDetailResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectDetailResponse> AcceptQuotationAsync(
        Guid projectId,
        string principalId,
        CancellationToken ct = default)
    {
        var project = await GetProjectOrThrowAsync(projectId, ct, includeParts: true, includeNotes: true);

        if (project.Status != ProjectStatus.QuotationSent && project.Status != ProjectStatus.QuotationGenerated)
            throw new InvalidOperationException($"Project must have a sent quotation to be accepted. Current: {project.Status}");

        project.Status = ProjectStatus.QuotationAccepted;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        // Publish event — OrderService consumes this to create orders
        var activeParts = project.Parts
            .Where(p => p.Status != PartStatus.Removed)
            .ToList();

        await PublishEventSafeAsync(new ProjectQuotationAcceptedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "ProjectQuotationAcceptedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "ProjectService",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: project.Id,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new ProjectQuotationAcceptedEventPayload(
                ProjectId: project.Id,
                ProjectNumber: project.ProjectNumber,
                QuotationId: project.QuotationId,
                CustomerId: project.CustomerId,
                Currency: project.Currency,
                Parts: activeParts
                    .Select(p => new ProjectQuotationAcceptedEventPayloadPartsItem(
                        PartId: p.Id,
                        Description: $"{p.FileName} — {p.ProcessType}",
                        Quantity: p.Quantity,
                        UnitPrice: (double)(p.ConfirmedUnitPrice ?? p.AiSuggestedPrice ?? 0m),
                        ProcessType: p.ProcessType.ToString(),
                        MaterialId: p.MaterialId,
                        FileId: p.FileId
                    ))
                    .ToArray(),
                AcceptedAt: DateTimeOffset.UtcNow,
                AcceptedBy: principalId
            )
        ), "ProjectQuotationAccepted", ct);

        _logger.LogInformation("Quotation for project {ProjectNumber} accepted by {PrincipalId}",
            project.ProjectNumber, principalId);

        return project.ToDetailResponse();
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(Guid projectId, string newStatus, CancellationToken ct = default)
    {
        var project = await _db.Projects.FindAsync([projectId], ct);
        if (project is null) return;

        if (Enum.TryParse<ProjectStatus>(newStatus, ignoreCase: true, out var status))
        {
            var oldStatus = project.Status.ToString();
            project.Status = status;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await InvalidateCacheAsync(projectId);

            await PublishEventSafeAsync(new ProjectStatusChangedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "ProjectStatusChangedEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0.0",
                PublishedBy: "ProjectService",
                ConsumedBy: Array.Empty<string>(),
                CorrelationId: projectId,
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new ProjectStatusChangedEventPayload(
                    ProjectId: projectId,
                    ProjectNumber: project.ProjectNumber,
                    OldStatus: oldStatus,
                    NewStatus: newStatus,
                    ChangedAt: DateTimeOffset.UtcNow
                )
            ), "ProjectStatusChanged", ct);
        }
    }

    /// <inheritdoc />
    public async Task LinkOrderAsync(
        Guid projectId,
        Guid orderId,
        IEnumerable<(Guid PartId, Guid OrderItemId)> partLinks,
        CancellationToken ct = default)
    {
        var parts = await _db.ProjectParts
            .Where(p => p.ProjectId == projectId && p.Status != PartStatus.Removed)
            .ToListAsync(ct);

        foreach (var (partId, orderItemId) in partLinks)
        {
            var part = parts.FirstOrDefault(p => p.Id == partId);
            if (part is null) continue;

            part.OrderId = orderId;
            part.OrderItemId = orderItemId;
            part.Status = PartStatus.Ordered;
            part.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);
    }

    /// <inheritdoc />
    public async Task LinkJobAsync(Guid partId, Guid jobId, CancellationToken ct = default)
    {
        var part = await _db.ProjectParts.FindAsync([partId], ct);
        if (part is null) return;

        part.JobId = jobId;
        part.Status = PartStatus.InProduction;
        part.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(part.ProjectId);
    }

    /// <inheritdoc />
    public async Task<ProjectNoteResponse> AddNoteAsync(
        Guid projectId,
        AddProjectNoteRequest request,
        string principalId,
        string principalName,
        CancellationToken ct = default)
    {
        // Ensure project exists
        await GetProjectOrThrowAsync(projectId, ct);

        var note = new ProjectNote
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AuthorId = principalId,
            AuthorName = principalName,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProjectNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(projectId);

        return note.ToResponse();
    }

    /// <inheritdoc />
    public async Task<ProjectStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeCount = await _db.Projects.CountAsync(
            p => p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled, ct);
        var configuringCount = await _db.Projects.CountAsync(
            p => p.Status == ProjectStatus.Draft || p.Status == ProjectStatus.Configuring, ct);
        var quotedCount = await _db.Projects.CountAsync(
            p => p.Status == ProjectStatus.QuotationGenerated || p.Status == ProjectStatus.QuotationSent, ct);
        var inProductionCount = await _db.Projects.CountAsync(
            p => p.Status == ProjectStatus.InProduction || p.Status == ProjectStatus.QualityCheck, ct);
        var completedThisMonthCount = await _db.Projects.CountAsync(
            p => p.Status == ProjectStatus.Completed && p.UpdatedAt >= startOfMonth, ct);

        return new ProjectStatsResponse
        {
            ActiveCount = activeCount,
            ConfiguringCount = configuringCount,
            QuotedCount = quotedCount,
            InProductionCount = inProductionCount,
            CompletedThisMonth = completedThisMonthCount
        };
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────────

    private async Task<Project> GetProjectOrThrowAsync(
        Guid projectId,
        CancellationToken ct,
        bool includeParts = false,
        bool includeNotes = false)
    {
        var query = _db.Projects.AsQueryable();
        if (includeParts) query = query.Include(p => p.Parts);
        if (includeNotes) query = query.Include(p => p.Notes);
        if (includeParts && includeNotes) query = query.AsSplitQuery();

        var project = await query.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            throw new KeyNotFoundException($"Project {projectId} not found.");
        return project;
    }

    private async Task<ProjectPart> GetPartOrThrowAsync(Guid projectId, Guid partId, CancellationToken ct)
    {
        var part = await _db.ProjectParts
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.Id == partId, ct);
        if (part is null)
            throw new KeyNotFoundException($"Part {partId} not found in project {projectId}.");
        if (part.Status == PartStatus.Removed)
            throw new InvalidOperationException($"Part {partId} has been removed from the project.");
        return part;
    }

    private async Task RecalculateProjectTotalAsync(Guid projectId, CancellationToken ct)
    {
        var confirmedTotal = await _db.ProjectParts
            .Where(p => p.ProjectId == projectId && p.Status >= PartStatus.Confirmed && p.Status != PartStatus.Removed)
            .SumAsync(p => (p.ConfirmedUnitPrice ?? p.AiSuggestedPrice ?? 0m) * p.Quantity, ct);

        await _db.Projects
            .Where(p => p.Id == projectId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.TotalEstimatedPrice, confirmedTotal)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
    }

    private async Task InvalidateCacheAsync(Guid projectId)
    {
        try
        {
            await _cache.RemoveAsync(CacheKey(projectId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for project {ProjectId}", projectId);
        }
    }

    private async Task PublishEventSafeAsync(object eventData, string eventName, CancellationToken ct)
    {
        try
        {
            await _publishEndpoint.Publish(eventData, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish {EventName} event — continuing without messaging", eventName);
        }
    }
}
