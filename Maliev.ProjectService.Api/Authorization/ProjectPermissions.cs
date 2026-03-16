namespace Maliev.ProjectService.Api.Authorization;

/// <summary>
/// Permission constants for the ProjectService.
/// Format: {service}.{resources}.{action} — resources must be plural.
/// </summary>
public static class ProjectPermissions
{
    /// <summary>Permissions for the projects resource.</summary>
    public static class Projects
    {
        /// <summary>Read (view) projects.</summary>
        public const string Read = "project.projects.read";

        /// <summary>Create new projects.</summary>
        public const string Create = "project.projects.create";

        /// <summary>Update project metadata and parts.</summary>
        public const string Update = "project.projects.update";

        /// <summary>Delete draft projects.</summary>
        public const string Delete = "project.projects.delete";

        /// <summary>Generate and send quotations.</summary>
        public const string Quote = "project.projects.quote";

        /// <summary>Accept quotations on behalf of a customer.</summary>
        public const string Accept = "project.projects.accept";
    }
}
