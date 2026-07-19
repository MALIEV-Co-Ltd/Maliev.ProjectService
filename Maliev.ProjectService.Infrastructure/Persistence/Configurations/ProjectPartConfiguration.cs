using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Maliev.ProjectService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ProjectPart entity.
/// </summary>
public class ProjectPartConfiguration : IEntityTypeConfiguration<ProjectPart>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProjectPart> builder)
    {
        var dictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            (left, right) => left != null && right != null && left.Count == right.Count && !left.Except(right).Any(),
            value => value.OrderBy(pair => pair.Key).Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(), pair.Value.GetHashCode())),
            value => new Dictionary<string, string>(value));

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value.ToList());

        var attachmentListConverter = new ValueConverter<List<ProjectPartAttachment>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<ProjectPartAttachment>>(v, (JsonSerializerOptions?)null) ?? new List<ProjectPartAttachment>());

        var attachmentListComparer = new ValueComparer<List<ProjectPartAttachment>>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<List<ProjectPartAttachment>>(
                JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                (JsonSerializerOptions?)null) ?? new List<ProjectPartAttachment>());

        builder.ToTable("project_parts");

        builder.HasKey(pp => pp.Id);
        builder.Property(pp => pp.Id).HasColumnName("id");

        builder.Property(pp => pp.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pp => pp.PartNumber)
            .HasColumnName("part_number")
            .IsRequired();

        builder.Property(pp => pp.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(pp => pp.FileId)
            .HasColumnName("file_id");

        builder.Property(pp => pp.FileReference)
            .HasColumnName("file_reference")
            .HasMaxLength(1000);

        builder.Property(pp => pp.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(2000);

        builder.Property(pp => pp.ThumbnailSmallGcsPath)
            .HasColumnName("thumbnail_small_gcs_path")
            .HasMaxLength(1000);

        builder.Property(pp => pp.ThumbnailLargeGcsPath)
            .HasColumnName("thumbnail_large_gcs_path")
            .HasMaxLength(1000);

        builder.Property(pp => pp.GlbStoragePath)
            .HasColumnName("glb_storage_path")
            .HasMaxLength(1000);

        builder.Property(pp => pp.OverlayPaths)
            .HasColumnName("overlay_paths")
            .HasConversion(dictionaryConverter, dictionaryComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        // Manufacturing configuration
        builder.Property(pp => pp.ProcessType)
            .HasColumnName("process_type")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(pp => pp.MaterialId)
            .HasColumnName("material_id");

        builder.Property(pp => pp.MaterialName)
            .HasColumnName("material_name")
            .HasMaxLength(500);

        builder.Property(pp => pp.MaterialCode)
            .HasColumnName("material_code")
            .HasMaxLength(100);

        builder.Property(pp => pp.Quantity)
            .HasColumnName("quantity")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(pp => pp.FinishType)
            .HasColumnName("finish_type")
            .HasMaxLength(100);

        builder.Property(pp => pp.Color)
            .HasColumnName("color")
            .HasMaxLength(200);

        builder.Property(pp => pp.Tolerance)
            .HasColumnName("tolerance")
            .HasMaxLength(100);

        builder.Property(pp => pp.RoughnessCode)
            .HasColumnName("roughness_code")
            .HasMaxLength(100);

        builder.Property(pp => pp.MarkingType)
            .HasColumnName("marking_type")
            .HasMaxLength(100);

        builder.Property(pp => pp.MarkingText)
            .HasColumnName("marking_text")
            .HasMaxLength(500);

        builder.Property(pp => pp.DfmAcknowledged)
            .HasColumnName("dfm_acknowledged")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pp => pp.HasDfmWarnings)
            .HasColumnName("has_dfm_warnings")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pp => pp.HasThreadedHoles)
            .HasColumnName("has_threaded_holes")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pp => pp.ThreadedHoleSpec)
            .HasColumnName("threaded_hole_spec")
            .HasMaxLength(200);

        builder.Property(pp => pp.ThreadedHoleCount)
            .HasColumnName("threaded_hole_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(pp => pp.HasInserts)
            .HasColumnName("has_inserts")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pp => pp.InsertType)
            .HasColumnName("insert_type")
            .HasMaxLength(100);

        builder.Property(pp => pp.InsertCount)
            .HasColumnName("insert_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(pp => pp.BagAndTag)
            .HasColumnName("bag_and_tag")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(pp => pp.InspectionLevel)
            .HasColumnName("inspection_level")
            .HasMaxLength(100);

        builder.Property(pp => pp.Certificates)
            .HasColumnName("certificates")
            .HasConversion(stringListConverter, stringListComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(pp => pp.DrawingFiles)
            .HasColumnName("drawing_files")
            .HasConversion(attachmentListConverter, attachmentListComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(pp => pp.SupplementaryFiles)
            .HasColumnName("supplementary_files")
            .HasConversion(attachmentListConverter, attachmentListComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(pp => pp.ProcessConfig)
            .HasColumnName("process_config")
            .HasConversion(dictionaryConverter, dictionaryComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(pp => pp.BodyCount)
            .HasColumnName("body_count");

        builder.Property(pp => pp.BodiesJson)
            .HasColumnName("bodies_json")
            .HasColumnType("jsonb");

        builder.Property(pp => pp.SelectedBodyIndex)
            .HasColumnName("selected_body_index");

        builder.Property(pp => pp.ThreadsInserts)
            .HasColumnName("threads_inserts")
            .HasMaxLength(500);

        builder.Property(pp => pp.CustomNotes)
            .HasColumnName("custom_notes")
            .HasMaxLength(2000);

        // Geometry metrics
        builder.Property(pp => pp.VolumeCm3)
            .HasColumnName("volume_cm3")
            .HasPrecision(18, 6);

        builder.Property(pp => pp.SupportVolumeCm3)
            .HasColumnName("support_volume_cm3")
            .HasPrecision(18, 6);

        builder.Property(pp => pp.SurfaceAreaCm2)
            .HasColumnName("surface_area_cm2")
            .HasPrecision(18, 6);

        builder.Property(pp => pp.BoundingBoxX)
            .HasColumnName("bounding_box_x")
            .HasPrecision(18, 4);

        builder.Property(pp => pp.BoundingBoxY)
            .HasColumnName("bounding_box_y")
            .HasPrecision(18, 4);

        builder.Property(pp => pp.BoundingBoxZ)
            .HasColumnName("bounding_box_z")
            .HasPrecision(18, 4);

        builder.Property(pp => pp.IsManifold)
            .HasColumnName("is_manifold");

        // Pricing
        builder.Property(pp => pp.AiSuggestedPrice)
            .HasColumnName("ai_suggested_price")
            .HasPrecision(18, 4);

        builder.Property(pp => pp.ConfirmedUnitPrice)
            .HasColumnName("confirmed_unit_price")
            .HasPrecision(18, 4);

        builder.Property(pp => pp.PriceOverrideReason)
            .HasColumnName("price_override_reason")
            .HasMaxLength(500);

        builder.Property(pp => pp.PricingConfidence)
            .HasColumnName("pricing_confidence")
            .HasPrecision(5, 4);

        builder.Property(pp => pp.PricingStrategy)
            .HasColumnName("pricing_strategy");

        // TotalPrice is computed — ignored by EF Core (no column)
        builder.Ignore(pp => pp.TotalPrice);

        // Order / Job references
        builder.Property(pp => pp.OrderId)
            .HasColumnName("order_id");

        builder.Property(pp => pp.OrderItemId)
            .HasColumnName("order_item_id");

        builder.Property(pp => pp.JobId)
            .HasColumnName("job_id");

        // Status
        builder.Property(pp => pp.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Audit
        builder.Property(pp => pp.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(pp => pp.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(pp => pp.ProjectId)
            .HasDatabaseName("idx_project_parts_project_id");

        builder.HasIndex(pp => pp.OrderId)
            .HasDatabaseName("idx_project_parts_order_id");

        builder.HasIndex(pp => pp.JobId)
            .HasDatabaseName("idx_project_parts_job_id");

        builder.HasQueryFilter(pp => pp.Project == null || !pp.Project.IsDeleted);
    }
}
