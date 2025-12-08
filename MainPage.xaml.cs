using System.Collections.ObjectModel;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

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
            }
            catch
            {
                // ignore save errors for now
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

                await DisplayAlert("Import", "Could not parse file as JSON or XML of bug items.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnAddBugClicked(object? sender, EventArgs e)
        {
            var title = await DisplayPromptAsync("New Bug", "Title:");
            if (string.IsNullOrWhiteSpace(title)) return;
            var desc = await DisplayPromptAsync("New Bug", "Description:");

            var bug = new BugItem { Id = Guid.NewGuid().ToString(), Title = title, Description = desc ?? string.Empty, Status = BugStatus.New };
            AddToCollection(bug);
        }

        private void AddToCollection(BugItem bug)
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
                var choice = await DisplayActionSheet("Move to", "Cancel", null, "New", "InProgress", "Resolved");
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
                    await DisplayAlert("Email", "No assignee email set.", "OK");
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
                    await DisplayAlert("Email", "Email is not supported on this device.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Email error", ex.Message, "OK");
                }
            }
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
    }
}
