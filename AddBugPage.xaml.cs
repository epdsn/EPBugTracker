using Microsoft.Maui.Controls;
using System.Collections.Generic;
using Microsoft.Maui.Storage;
using System.IO;

namespace EPBugTracker
{
    public partial class AddBugPage : ContentPage
    {
        // store selected image file paths
        readonly List<string> selectedImagePaths = new();
        readonly List<string> steps = new();

        public AddBugPage()
        {
            InitializeComponent();
            StatusPicker.SelectedIndex = 0;
            PriorityPicker.SelectedIndex = 1;

            // bind steps collection
            StepsCollection.ItemsSource = steps;
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private void RefreshSteps()
        {
            StepsCollection.ItemsSource = null;
            StepsCollection.ItemsSource = steps;
        }

        private void OnAddStepClicked(object? sender, EventArgs e)
        {
            var text = StepEntry.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                steps.Add(text);
                StepEntry.Text = string.Empty;
                RefreshSteps();
            }
        }

        private void OnRemoveStepClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string step)
            {
                steps.Remove(step);
                RefreshSteps();
            }
        }

        private async void OnAddImageClicked(object? sender, EventArgs e)
        {
            try
            {
                var pick = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select attachment",
                    FileTypes = FilePickerFileType.Images
                });

                if (pick == null) return;

                // copy to app data attachments folder for persisted reference
                var imagesDir = Path.Combine(FileSystem.AppDataDirectory, "attachments");
                if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);

                var ext = Path.GetExtension(pick.FileName);
                var destFile = Path.Combine(imagesDir, System.Guid.NewGuid().ToString() + ext);

                using var src = await pick.OpenReadAsync();
                using var dest = File.Create(destFile);
                await src.CopyToAsync(dest);

                selectedImagePaths.Add(destFile);

                // add thumbnail to ImagesPanel if present in XAML (use FindByName to avoid generated field dependency)
                try
                {
                    var img = new Image { Source = ImageSource.FromFile(destFile), WidthRequest = 100, HeightRequest = 100, Aspect = Aspect.AspectFill };
                    var panel = this.FindByName<HorizontalStackLayout>("ImagesPanel");
                    if (panel != null)
                    {
                        panel.Children.Add(img);
                    }
                }
                catch { }
            }
            catch (System.Exception ex)
            {
                await DisplayAlertAsync("Attachment error", ex.Message, "OK");
            }
        }

        private MainPage? FindMainPageInstance()
        {
            // Check navigation stack
            if (Navigation?.NavigationStack != null)
            {
                for (int i = Navigation.NavigationStack.Count - 1; i >= 0; i--)
                {
                    if (Navigation.NavigationStack[i] is MainPage mp) return mp;
                }
            }

            // Check Application.MainPage
            var appMain = Application.Current?.MainPage;
            if (appMain is MainPage mp2) return mp2;
            if (appMain is NavigationPage nav && nav.CurrentPage is MainPage mp3) return mp3;
            if (Shell.Current?.CurrentPage is MainPage mp4) return mp4;

            return null;
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            var title = TitleEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                await DisplayAlertAsync("Validation", "Title is required.", "OK");
                return;
            }

            // read repeatable steps via FindByName to avoid dependency on generated field
            var repeatable = this.FindByName<Editor>("RepeatableStepsEditor")?.Text ?? string.Empty;

            var bug = new BugItem
            {
                Id = System.Guid.NewGuid().ToString(),
                Title = title,
                Description = DescriptionEditor.Text ?? string.Empty,
                Project = ProjectEntry.Text ?? string.Empty,
                AssigneeEmail = AssigneeEntry.Text ?? string.Empty,
                RepeatableSteps = repeatable,
                Steps = new List<string>(steps),
                ImagePaths = new List<string>(selectedImagePaths),
                Status = (BugStatus)(StatusPicker.SelectedIndex == 0 ? BugStatus.New : (StatusPicker.SelectedIndex == 1 ? BugStatus.InProgress : BugStatus.Resolved)),
                Priority = (BugPriority)(PriorityPicker.SelectedIndex == 0 ? BugPriority.Low : (PriorityPicker.SelectedIndex == 1 ? BugPriority.Medium : BugPriority.High))
            };

            // Try to call AddToCollection on the previous page in the navigation stack (should be MainPage)
            var added = false;
            var mainInstance = FindMainPageInstance();
            if (mainInstance != null)
            {
                mainInstance.AddToCollection(bug);
                // ensure UI and persisted file are reloaded so main view is up to date
                try { mainInstance.ReloadFromDisk(); } catch { }
                added = true;
            }

            if (!added)
            {
                // final fallback: append to file directly
                try
                {
                    var dataFilePath = Path.Combine(FileSystem.AppDataDirectory, "bugs.xml");
                    List<BugItem> items = new();
                    if (File.Exists(dataFilePath))
                    {
                        var xsr = new System.Xml.Serialization.XmlSerializer(typeof(List<BugItem>));
                        using var fs = File.OpenRead(dataFilePath);
                        using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
                        var loaded = (List<BugItem>?)xsr.Deserialize(reader);
                        if (loaded != null) items = loaded;
                    }

                    items.Add(bug);

                    var xsw = new System.Xml.Serialization.XmlSerializer(typeof(List<BugItem>));
                    var dir = Path.GetDirectoryName(dataFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    using var outFs = File.Create(dataFilePath);
                    using var writer = new StreamWriter(outFs, System.Text.Encoding.UTF8);
                    xsw.Serialize(writer, items);

                    // attempt to notify main page by finding it and calling reload
                    var maybeMain = FindMainPageInstance();
                    maybeMain?.ReloadFromDisk();
                }
                catch (System.Exception ex)
                {
                    await DisplayAlertAsync("Save error", ex.Message, "OK");
                }
            }

            await Navigation.PopAsync();
        }
    }
}