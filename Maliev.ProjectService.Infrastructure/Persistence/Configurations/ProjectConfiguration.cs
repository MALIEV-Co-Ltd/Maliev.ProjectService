using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ProjectService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Project aggregate root.
/// </summary>
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.ProjectNumber)
            .HasColumnName("project_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(p => p.ProjectNumber)
            .IsUnique()
            .HasDatabaseName("idx_projects_project_number");

        builder.Property(p => p.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.HasIndex(p => p.CustomerId)
            .HasDatabaseName("idx_projects_customer_id");

        builder.Property(p => p.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("idx_projects_status");

        builder.Property(p => p.QuotationId)
            .HasColumnName("quotation_id");

        builder.Property(p => p.QuotationNumber)
            .HasColumnName("quotation_number")
            .HasMaxLength(50);

        builder.Property(p => p.TotalEstimatedPrice)
            .HasColumnName("total_estimated_price")
            .HasPrecision(18, 4)
            .IsRequired()
            .HasDefaultValue(0m);

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("THB");

        builder.Property(p => p.ValidUntil)
            .HasColumnName("valid_until");

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.CreatedByName)
            .HasColumnName("created_by_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // xmin shadow property for optimistic concurrency (mandatory pattern)
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Soft-delete global query filter
        builder.HasQueryFilter(p => !p.IsDeleted);

        // Navigation: parts
        builder.HasMany(p => p.Parts)
            .WithOne(pp => pp.Project)
            .HasForeignKey(pp => pp.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: notes
        builder.HasMany(p => p.Notes)
            .WithOne(n => n.Project)
            .HasForeignKey(n => n.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
