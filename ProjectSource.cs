using System.Xml.Serialization;

namespace EPBugTracker
{
    public enum ProjectSourceType { LocalGit, GitHub }

    public class ProjectSource
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public ProjectSourceType Type { get; set; } = ProjectSourceType.LocalGit;

        // Local
        public string LocalPath { get; set; } = string.Empty;

        // Remote
        public string RepoUrl { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;

        public override string ToString() =>
            Type == ProjectSourceType.LocalGit
                ? $"Local: {LocalPath} (branch: {Branch})"
                : $"GitHub: {RepoUrl} (branch: {Branch})";
    }
}