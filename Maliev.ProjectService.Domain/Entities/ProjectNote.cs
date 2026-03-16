namespace Maliev.ProjectService.Domain.Entities;

/// <summary>
/// An internal note added by an employee on a project.
/// </summary>
public class ProjectNote
{
    /// <summary>Unique identifier for the note.</summary>
    public Guid Id { get; set; }

    /// <summary>Parent project ID.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Display name of the note author.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Principal ID of the note author.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Note body content. Max 5000 characters.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the note was created.</summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation ---

    /// <summary>Parent project.</summary>
    public Project? Project { get; set; }
}
