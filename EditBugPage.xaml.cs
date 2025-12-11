using Microsoft.Maui.Controls;
using System.Collections.Generic;
using Microsoft.Maui.Storage;
using System.IO;

namespace EPBugTracker
{
    public partial class EditBugPage : ContentPage
    {
        BugItem? current;
        readonly List<string> selectedImagePaths = new();

        public EditBugPage(BugItem bug)
        {
            InitializeComponent();
            current = bug;

            IdLabel.Text = bug.Id;
            TitleEntry.Text = bug.Title;
            DescriptionEditor.Text = bug.Description;
            RepeatableStepsEditor.Text = bug.RepeatableSteps;
            ProjectEntry.Text = bug.Project;
            AssigneeEntry.Text = bug.AssigneeEmail;
            StatusPicker.SelectedIndex = bug.Status == BugStatus.New ? 0 : (bug.Status == BugStatus.InProgress ? 1 : 2);

            if (bug.ImagePaths != null)
            {
                foreach (var p in bug.ImagePaths)
                {
                    if (File.Exists(p))
                    {
                        var img = new Image { Source = ImageSource.FromFile(p), WidthRequest = 100, HeightRequest = 100, Aspect = Aspect.AspectFill };
                        ImagesPanel.Children.Add(img);
                        selectedImagePaths.Add(p);
                    }
                }
            }
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
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

                var imagesDir = Path.Combine(FileSystem.AppDataDirectory, "attachments");
                if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);

                var ext = Path.GetExtension(pick.FileName);
                var destFile = Path.Combine(imagesDir, System.Guid.NewGuid().ToString() + ext);

                using var src = await pick.OpenReadAsync();
                using var dest = File.Create(destFile);
                await src.CopyToAsync(dest);

                selectedImagePaths.Add(destFile);
                var img = new Image { Source = ImageSource.FromFile(destFile), WidthRequest = 100, HeightRequest = 100, Aspect = Aspect.AspectFill };
                ImagesPanel.Children.Add(img);
            }
            catch (System.Exception ex)
            {
                await DisplayAlertAsync("Attachment error", ex.Message, "OK");
            }
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            if (current == null) return;

            current.Title = TitleEntry.Text ?? string.Empty;
            current.Description = DescriptionEditor.Text ?? string.Empty;
            current.RepeatableSteps = RepeatableStepsEditor.Text ?? string.Empty;
            current.Project = ProjectEntry.Text ?? string.Empty;
            current.AssigneeEmail = AssigneeEntry.Text ?? string.Empty;
            current.ImagePaths = new List<string>(selectedImagePaths);
            current.Status = (BugStatus)(StatusPicker.SelectedIndex == 0 ? BugStatus.New : (StatusPicker.SelectedIndex == 1 ? BugStatus.InProgress : BugStatus.Resolved));

            // After editing, save by calling MainPage.SaveAll through AddToCollection and Reload
            var main = Application.Current?.MainPage as NavigationPage;
            var page = main?.CurrentPage as MainPage;
            if (page != null)
            {
                // ensure collections are refreshed and persisted
                page.ReloadFromDisk();
            }

            await Navigation.PopAsync();
        }
    }
}