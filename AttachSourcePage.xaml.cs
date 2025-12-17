using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Serialization;

namespace EPBugTracker
{
    public partial class AttachSourcePage : ContentPage
    {
        readonly string sourcesFile;

        public AttachSourcePage()
        {
            InitializeComponent();

            sourcesFile = Path.Combine(FileSystem.AppDataDirectory, "project_sources.xml");

            // sensible defaults
            LocalRadio.IsChecked = true;
            LocalPanel.IsVisible = true;
            GitHubPanel.IsVisible = false;
        }

        private void OnSourceTypeChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (LocalRadio.IsChecked)
            {
                LocalPanel.IsVisible = true;
                GitHubPanel.IsVisible = false;
            }
            else
            {
                LocalPanel.IsVisible = false;
                GitHubPanel.IsVisible = true;
            }
        }

        private async void OnValidateLocalClicked(object? sender, EventArgs e)
        {
            var path = LocalPathEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                await DisplayAlert("Validation", "Enter a local path.", "OK");
                return;
            }

            if (!Directory.Exists(path))
            {
                await DisplayAlert("Validation", "Directory does not exist.", "OK");
                return;
            }

            var gitDir = Path.Combine(path, ".git");
            if (!Directory.Exists(gitDir))
            {
                await DisplayAlert("Validation", "This directory does not appear to be a git repository (no .git folder found).", "OK");
                return;
            }

            await DisplayAlert("Validation", "Local repository looks valid.", "OK");
        }

        private async void OnListLocalBranchesClicked(object? sender, EventArgs e)
        {
            var path = LocalPathEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                await DisplayAlert("Error", "Enter a valid local path first.", "OK");
                return;
            }

            // Try to use 'git' executable to list branches. If not available, report failure.
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"-C \"{path}\" branch --all --no-color",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    await DisplayAlert("Branches", "Failed to start git process.", "OK");
                    return;
                }

                var outp = await proc.StandardOutput.ReadToEndAsync();
                var err = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(err))
                {
                    // Non-fatal: show to user
                    System.Diagnostics.Debug.WriteLine($"git error: {err}");
                }

                var branches = ParseGitBranchOutput(outp);
                LocalBranchesPicker.ItemsSource = branches;
                if (branches.Count > 0) LocalBranchesPicker.SelectedIndex = 0;

                if (branches.Count == 0)
                {
                    await DisplayAlert("Branches", "No branches found (git output was empty).", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not run git to list branches: {ex.Message}", "OK");
            }
        }

        private List<string> ParseGitBranchOutput(string output)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(output)) return list;

            using var sr = new StringReader(output);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                // lines look like: "* main" or "  remotes/origin/HEAD -> origin/main" etc.
                if (line.StartsWith("*")) line = line.Substring(1).Trim();
                if (line.StartsWith("remotes/")) line = line.Replace("remotes/", "");
                // ignore symbolic refs with ->
                if (line.Contains("->")) continue;
                if (!string.IsNullOrWhiteSpace(line) && !list.Contains(line)) list.Add(line);
            }

            return list;
        }

        private async void OnValidateRepoUrlClicked(object? sender, EventArgs e)
        {
            var url = RepoUrlEntry.Text?.Trim() ?? string.Empty;
            if (!TryParseGithubRepo(url, out var owner, out var repo))
            {
                await DisplayAlert("Validation", "Enter a GitHub repository URL like https://github.com/owner/repo", "OK");
                return;
            }

            await DisplayAlert("Validation", "GitHub URL looks valid.", "OK");
        }

        private async void OnListRemoteBranchesClicked(object? sender, EventArgs e)
        {
            var url = RepoUrlEntry.Text?.Trim() ?? string.Empty;
            if (!TryParseGithubRepo(url, out var owner, out var repo))
            {
                await DisplayAlert("Error", "Enter a valid GitHub repository URL first.", "OK");
                return;
            }

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("EPBugTracker/1.0");
                var api = $"https://api.github.com/repos/{owner}/{repo}/branches";
                var res = await http.GetAsync(api);
                if (!res.IsSuccessStatusCode)
                {
                    await DisplayAlert("Error", $"GitHub API returned {(int)res.StatusCode}: {res.ReasonPhrase}", "OK");
                    return;
                }

                var json = await res.Content.ReadAsStringAsync();
                var branches = JsonSerializer.Deserialize<List<GitHubBranchDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Select(b => b.Name).ToList() ?? new List<string>();

                RemoteBranchesPicker.ItemsSource = branches;
                if (branches.Count > 0) RemoteBranchesPicker.SelectedIndex = 0;

                if (branches.Count == 0)
                {
                    await DisplayAlert("Branches", "No branches found for this repository.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to query GitHub: {ex.Message}", "OK");
            }
        }

        private static bool TryParseGithubRepo(string url, out string owner, out string repo)
        {
            owner = string.Empty;
            repo = string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Accept forms:
            // https://github.com/owner/repo
            // git@github.com:owner/repo.git
            try
            {
                var u = url.Trim();
                if (u.StartsWith("git@"))
                {
                    // git@github.com:owner/repo.git
                    var parts = u.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) return false;
                    var path = parts[1].TrimEnd(".git".ToCharArray());
                    var seg = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (seg.Length < 2) return false;
                    owner = seg[0];
                    repo = seg[1];
                    return true;
                }

                var uri = new Uri(u);
                if (!uri.Host.Contains("github.com")) return false;
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2) return false;
                owner = segments[0];
                repo = segments[1].EndsWith(".git") ? segments[1][..^4] : segments[1];
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void OnAttachClicked(object? sender, EventArgs e)
        {
            var src = new ProjectSource();

            if (LocalRadio.IsChecked)
            {
                var path = LocalPathEntry.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    await DisplayAlert("Error", "Enter a valid local path.", "OK");
                    return;
                }

                src.Type = ProjectSourceType.LocalGit;
                src.LocalPath = path;
                src.Branch = LocalBranchesPicker.SelectedItem?.ToString() ?? string.Empty;
            }
            else
            {
                var url = RepoUrlEntry.Text?.Trim() ?? string.Empty;
                if (!TryParseGithubRepo(url, out _, out _))
                {
                    await DisplayAlert("Error", "Enter a valid GitHub repository URL.", "OK");
                    return;
                }

                src.Type = ProjectSourceType.GitHub;
                src.RepoUrl = url;
                src.Branch = RemoteBranchesPicker.SelectedItem?.ToString() ?? string.Empty;
            }

            // Persist to project_sources.xml (append)
            try
            {
                var list = new List<ProjectSource>();
                if (File.Exists(sourcesFile))
                {
                    var xs = new XmlSerializer(typeof(List<ProjectSource>));
                    using var fs = File.OpenRead(sourcesFile);
                    using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
                    var loaded = (List<ProjectSource>?)xs.Deserialize(reader);
                    if (loaded != null) list = loaded;
                }

                list.Add(src);

                var xsw = new XmlSerializer(typeof(List<ProjectSource>));
                var dir = Path.GetDirectoryName(sourcesFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using var outFs = File.Create(sourcesFile);
                using var writer = new StreamWriter(outFs, System.Text.Encoding.UTF8);
                xsw.Serialize(writer, list);

                await DisplayAlert("Attached", $"Project source saved.\n{src}", "OK");

                // Optionally: return to previous page
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save project source: {ex.Message}", "OK");
            }
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private class GitHubBranchDto
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}