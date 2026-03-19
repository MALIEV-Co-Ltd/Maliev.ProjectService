using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ProjectService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ProjectPart entity.
/// </summary>
public class ProjectPartConfiguration : IEntityTypeConfiguration<ProjectPart>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProjectPart> builder)
    {
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
