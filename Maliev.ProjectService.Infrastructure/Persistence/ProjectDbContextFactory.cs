using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.ProjectService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating <see cref="ProjectDbContext"/> instances for EF Core migrations.
/// Used by `dotnet ef migrations add` — not used in production.
/// Connection string is a placeholder; actual connection comes from Aspire service discovery at runtime.
/// </summary>
public class ProjectDbContextFactory : IDesignTimeDbContextFactory<ProjectDbContext>
{
    /// <inheritdoc />
    public ProjectDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProjectDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=project-app-db;Username=postgres;Password=postgres",
            o => o.MigrationsAssembly(typeof(ProjectDbContext).Assembly.FullName));

        return new ProjectDbContext(optionsBuilder.Options);
    }
}
