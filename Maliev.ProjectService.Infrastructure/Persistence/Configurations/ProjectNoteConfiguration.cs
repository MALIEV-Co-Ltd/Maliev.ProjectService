using Maliev.ProjectService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ProjectService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ProjectNote entity.
/// </summary>
public class ProjectNoteConfiguration : IEntityTypeConfiguration<ProjectNote>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProjectNote> builder)
    {
        builder.ToTable("project_notes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");

        builder.Property(n => n.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(n => n.AuthorName)
            .HasColumnName("author_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.AuthorId)
            .HasColumnName("author_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Content)
            .HasColumnName("content")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(n => n.ProjectId)
            .HasDatabaseName("idx_project_notes_project_id");

        builder.HasQueryFilter(n => n.Project == null || !n.Project.IsDeleted);
    }
}
