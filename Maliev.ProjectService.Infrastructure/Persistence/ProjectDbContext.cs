using Maliev.ProjectService.Domain.Entities;
using Maliev.ProjectService.Infrastructure.Persistence.Configurations;
using Maliev.Aspire.ServiceDefaults.Database;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ProjectService.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the ProjectService. Owns Project, ProjectPart, and ProjectNote entities.
/// </summary>
public class ProjectDbContext : DbContext
{
    /// <inheritdoc />
    public ProjectDbContext(DbContextOptions<ProjectDbContext> options) : base(options) { }

    /// <summary>All projects.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>All project parts.</summary>
    public DbSet<ProjectPart> ProjectParts => Set<ProjectPart>();

    /// <summary>All project notes.</summary>
    public DbSet<ProjectNote> ProjectNotes => Set<ProjectNote>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectPartConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectNoteConfiguration());

        SnakeCaseNamingHelper.ApplySnakeCaseNaming(modelBuilder);

        // Apply UTC datetime converter to all DateTime properties
        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entityType.GetProperties())
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(dateTimeConverter);
    }
}
