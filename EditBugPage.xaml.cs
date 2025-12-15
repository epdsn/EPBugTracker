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
            PriorityPicker.SelectedIndex = bug.Priority == BugPriority.Low ? 0 : (bug.Priority == BugPriority.Medium ? 1 : 2);

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

        private MainPage? FindMainPageInstance()
        {
            // 1) Check current page's Navigation stack (INavigation)
            var nav = this.Navigation;
            if (nav != null)
            {
                try
                {
                    foreach (var p in nav.NavigationStack)
                    {
                        if (p is MainPage mp) return mp;
                    }

                    foreach (var m in nav.ModalStack)
                    {
                        if (m is MainPage mpm) return mpm;
                    }
                }
                catch { }
            }

            // 2) Inspect Application.Current.MainPage
            var appMain = Application.Current?.MainPage;
            if (appMain == null) return null;

            if (appMain is MainPage direct) return direct;

            if (appMain is NavigationPage navPage)
            {
                var curr = navPage.CurrentPage;
                if (curr is MainPage mpc) return mpc;

                var navInterface = navPage.Navigation; // INavigation
                if (navInterface != null)
                {
                    try
                    {
                        foreach (var p in navInterface.NavigationStack)
                        {
                            if (p is MainPage mp) return mp;
                        }

                        foreach (var m in navInterface.ModalStack)
                        {
                            if (m is MainPage mp) return mp;
                        }
                    }
                    catch { }
                }
            }

            // 3) Check Shell.Current.CurrentPage
            try
            {
                var shellCurrent = Shell.Current?.CurrentPage;
                if (shellCurrent is MainPage smp) return smp;
            }
            catch { }

            return null;
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
            current.Priority = (BugPriority)(PriorityPicker.SelectedIndex == 0 ? BugPriority.Low : (PriorityPicker.SelectedIndex == 1 ? BugPriority.Medium : BugPriority.High));

            System.Diagnostics.Debug.WriteLine($"EditBugPage: saving bug id={current.Id}");

            var main = FindMainPageInstance();
            if (main != null)
            {
                System.Diagnostics.Debug.WriteLine("EditBugPage: found MainPage instance, calling UpdateBug");
                main.UpdateBug(current);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("EditBugPage: MainPage instance not found � updating bugs.xml directly");

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

                    // Replace existing item by Id or add if not found
                    var replaced = false;
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].Id == current.Id)
                        {
                            items[i] = current;
                            replaced = true;
                            break;
                        }
                    }

                    if (!replaced) items.Add(current);

                    var xsw = new System.Xml.Serialization.XmlSerializer(typeof(List<BugItem>));
                    var dir = Path.GetDirectoryName(dataFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    using var outFs = File.Create(dataFilePath);
                    using var writer = new StreamWriter(outFs, System.Text.Encoding.UTF8);
                    xsw.Serialize(writer, items);

                    // try to find main again and request reload
                    var maybeMain = FindMainPageInstance();
                    maybeMain?.ReloadFromDisk();

                    System.Diagnostics.Debug.WriteLine($"EditBugPage: wrote {items.Count} items to {dataFilePath}");
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EditBugPage: error writing bugs.xml: {ex}");
                    await DisplayAlertAsync("Save error", ex.Message, "OK");
                }
            }

            await Navigation.PopAsync();
        }
    }
}