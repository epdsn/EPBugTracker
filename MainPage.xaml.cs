using System.Collections.ObjectModel;
using System.Text;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.IO;

namespace EPBugTracker
{
    public partial class MainPage : ContentPage
    {
        public ObservableCollection<BugItem> NewBugs { get; } = new();
        public ObservableCollection<BugItem> InProgressBugs { get; } = new();
        public ObservableCollection<BugItem> ResolvedBugs { get; } = new();

        readonly string dataFilePath;
        bool isLoading = false;

        public MainPage()
        {
            InitializeComponent();

            dataFilePath = Path.Combine(FileSystem.AppDataDirectory, "bugs.xml");

            var newCollection = this.FindByName<CollectionView>("NewCollection");
            var inProgressCollection = this.FindByName<CollectionView>("InProgressCollection");
            var resolvedCollection = this.FindByName<CollectionView>("ResolvedCollection");

            if (newCollection != null) newCollection.ItemsSource = NewBugs;
            if (inProgressCollection != null) inProgressCollection.ItemsSource = InProgressBugs;
            if (resolvedCollection != null) resolvedCollection.ItemsSource = ResolvedBugs;

            // Load persisted data
            LoadFromFile();
        }

        void LoadFromFile()
        {
            if (!File.Exists(dataFilePath)) return;

            try
            {
                isLoading = true;
                var xs = new XmlSerializer(typeof(List<BugItem>));
                using var fs = File.OpenRead(dataFilePath);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var items = (List<BugItem>?)xs.Deserialize(reader);
                if (items != null)
                {
                    foreach (var b in items)
                    {
                        // Ensure collections are cleared first
                        // We'll add to the appropriate collection without triggering save
                        switch (b.Status)
                        {
                            case BugStatus.New: NewBugs.Add(b); break;
                            case BugStatus.InProgress: InProgressBugs.Add(b); break;
                            case BugStatus.Resolved: ResolvedBugs.Add(b); break;
                            default: NewBugs.Add(b); break;
                        }
                    }

                    RefreshCollections();
                }
            }
            catch
            {
                // ignore load errors for now
            }
            finally
            {
                isLoading = false;
            }
        }

        void SaveAll()
        {
            try
            {
                var all = new List<BugItem>();
                all.AddRange(NewBugs);
                all.AddRange(InProgressBugs);
                all.AddRange(ResolvedBugs);

                var xs = new XmlSerializer(typeof(List<BugItem>));
                // ensure directory exists
                var dir = Path.GetDirectoryName(dataFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using var fs = File.Create(dataFilePath);
                using var writer = new StreamWriter(fs, Encoding.UTF8);
                xs.Serialize(writer, all);

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"SaveAll: wrote {all.Count} items to {dataFilePath}");
                try
                {
                    _ = Dispatcher.DispatchAsync(async () => await DisplayAlert("Saved", $"Wrote {all.Count} bug(s) to:\n{dataFilePath}", "OK"));
                }
                catch { }
#endif
            }
            catch (Exception ex)
            {
                // Log and show an alert so save errors aren't silently ignored
                System.Diagnostics.Debug.WriteLine($"SaveAll error: {ex}");
                try
                {
                    _ = Dispatcher.DispatchAsync(async () => await DisplayAlert("Save error", ex.Message, "OK"));
                }
                catch
                {
                    // ignore any UI dispatch errors
                }
            }
        }

        private async void OnImportClicked(object? sender, EventArgs e)
        {
            try
            {
                var pick = await FilePicker.Default.PickAsync();
                if (pick == null) return;

                using var stream = await pick.OpenReadAsync();
                using var sr = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
                var text = await sr.ReadToEndAsync();

                // Try JSON first
                try
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<BugItem>>(text, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (items != null)
                    {
                        foreach (var b in items) AddToCollection(b);
                        return;
                    }
                }
                catch { }

                // Try XML
                try
                {
                    var xs = new XmlSerializer(typeof(List<BugItem>));
                    using var reader = new StringReader(text);
                    var items = (List<BugItem>?)xs.Deserialize(reader);
                    if (items != null)
                    {
                        foreach (var b in items) AddToCollection(b);
                        return;
                    }
                }
                catch { }

                await DisplayAlertAsync("Import", "Could not parse file as JSON or XML of bug items.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", ex.Message, "OK");
            }
        }

        private async void OnAddBugClicked(object? sender, EventArgs e)
        {
            // Navigate to the AddBugPage which exposes all BugItem properties for entry
            if (Navigation != null)
            {
                await Navigation.PushAsync(new AddBugPage());
            }
        }

        public void AddToCollection(BugItem bug)
        {
            switch (bug.Status)
            {
                case BugStatus.New:
                    NewBugs.Add(bug);
                    break;
                case BugStatus.InProgress:
                    InProgressBugs.Add(bug);
                    break;
                case BugStatus.Resolved:
                    ResolvedBugs.Add(bug);
                    break;
                default:
                    NewBugs.Add(bug);
                    break;
            }

            RefreshCollections();

            if (!isLoading) SaveAll();
        }

        private async void OnAssignClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is BugItem bug)
            {
                var email = await DisplayPromptAsync("Assign", "Assignee email:");
                if (!string.IsNullOrWhiteSpace(email))
                {
                    bug.AssigneeEmail = email;
                    RefreshCollections();
                    if (!isLoading) SaveAll();
                }
            }
        }

        private void RefreshCollections()
        {
            var newCollection = this.FindByName<CollectionView>("NewCollection");
            var inProgressCollection = this.FindByName<CollectionView>("InProgressCollection");
            var resolvedCollection = this.FindByName<CollectionView>("ResolvedCollection");

            if (newCollection != null) { newCollection.ItemsSource = null; newCollection.ItemsSource = NewBugs; }
            if (inProgressCollection != null) { inProgressCollection.ItemsSource = null; inProgressCollection.ItemsSource = InProgressBugs; }
            if (resolvedCollection != null) { resolvedCollection.ItemsSource = null; resolvedCollection.ItemsSource = ResolvedBugs; }
        }

        private async void OnMoveClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is BugItem bug)
            {
                var choice = await DisplayActionSheetAsync("Move to", "Cancel", null, "New", "InProgress", "Resolved");
                if (choice == "Cancel" || string.IsNullOrEmpty(choice)) return;

                NewBugs.Remove(bug);
                InProgressBugs.Remove(bug);
                ResolvedBugs.Remove(bug);

                if (choice == "New") { bug.Status = BugStatus.New; NewBugs.Add(bug); }
                if (choice == "InProgress") { bug.Status = BugStatus.InProgress; InProgressBugs.Add(bug); }
                if (choice == "Resolved") { bug.Status = BugStatus.Resolved; ResolvedBugs.Add(bug); }

                RefreshCollections();
                if (!isLoading) SaveAll();
            }
        }

        private async void OnEmailClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is BugItem bug)
            {
                if (string.IsNullOrWhiteSpace(bug.AssigneeEmail))
                {
                    await DisplayAlertAsync("Email", "No assignee email set.", "OK");
                    return;
                }

                try
                {
                    var message = new EmailMessage {
                        Subject = $"Bug assigned: {bug.Title}",
                        Body = $"Bug: {bug.Title}\n\n{bug.Description}\n\nStatus: {bug.Status}",
                    };
                    message.To.Add(bug.AssigneeEmail);

                    await Email.ComposeAsync(message);
                }
                catch (FeatureNotSupportedException)
                {
                    await DisplayAlertAsync("Email", "Email is not supported on this device.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Email error", ex.Message, "OK");
                }
            }
        }

        private async void OnDiagnosticsClicked(object? sender, EventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"AppDataDirectory: {FileSystem.AppDataDirectory}");
                sb.AppendLine($"Bugs file: {dataFilePath}");

                // Try to locate the main executable
                string? exePath = null;
#if WINDOWS
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
#else
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
#endif
                sb.AppendLine($"ExePath: {exePath}");

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    // compute SHA256
                    try
                    {
                        using var fs = File.OpenRead(exePath);
                        using var sha = SHA256.Create();
                        var hash = sha.ComputeHash(fs);
                        var hex = BitConverter.ToString(hash).Replace("-", "");
                        sb.AppendLine($"SHA256: {hex}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"SHA256 error: {ex.Message}");
                    }

                    // Authenticode signature info (Windows only)
                    try
                    {
                        var sig = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(exePath);
                        if (sig != null)
                        {
                            sb.AppendLine($"Certificate Subject: {sig.Subject}");
                            sb.AppendLine($"Certificate Issuer: {sig.Issuer}");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"Signature info error: {ex.Message}");
                    }
                }

                // Try to read recent CodeIntegrity events via wevtutil (Windows)
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo("wevtutil", "qe Microsoft-Windows-CodeIntegrity/Operational /c:5 /f:text")
                        {
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null)
                        {
                            var outp = await proc.StandardOutput.ReadToEndAsync();
                            var err = await proc.StandardError.ReadToEndAsync();
                            if (!string.IsNullOrEmpty(outp)) sb.AppendLine("CodeIntegrity recent events:\n" + outp);
                            if (!string.IsNullOrEmpty(err)) sb.AppendLine("CodeIntegrity error output:\n" + err);
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Event read error: {ex.Message}");
                }

                var text = sb.ToString();

                // Copy to clipboard
                await Clipboard.SetTextAsync(text);

                // Also write to Desktop so you can inspect without relying on clipboard
                try
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    var outPath = Path.Combine(desktop ?? FileSystem.AppDataDirectory, "epbug_diagnostics.txt");
                    await File.WriteAllTextAsync(outPath, text, Encoding.UTF8);
                    await DisplayAlert("Diagnostics saved", $"Diagnostics copied to clipboard and written to:\n{outPath}", "OK");
                }
                catch (Exception ex)
                {
                    // If file write fails, still inform user that clipboard succeeded
                    await DisplayAlert("Diagnostics copied", $"Diagnostics copied to clipboard. Failed to write to Desktop: {ex.Message}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Diagnostics error", ex.Message, "OK");
            }
        }

        public void ReloadFromDisk()
        {
            // Clear existing collections and reload from the persisted file
            NewBugs.Clear();
            InProgressBugs.Clear();
            ResolvedBugs.Clear();
            LoadFromFile();
        }

        private void OnReloadClicked(object? sender, EventArgs e)
        {
            // Reload data from file and refresh UI
            NewBugs.Clear();
            InProgressBugs.Clear();
            ResolvedBugs.Clear();
            LoadFromFile();
        }
    }

    public enum BugStatus { New, InProgress, Resolved }

    public class BugItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BugStatus Status { get; set; } = BugStatus.New;
        public string AssigneeEmail { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        // New fields
        public string RepeatableSteps { get; set; } = string.Empty;
        public List<string> ImagePaths { get; set; } = new();
        public List<string> Steps { get; set; } = new();
    }
}
