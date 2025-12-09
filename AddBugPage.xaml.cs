using Microsoft.Maui.Controls;

namespace EPBugTracker
{
    public partial class AddBugPage : ContentPage
    {
        public AddBugPage()
        {
            InitializeComponent();
            StatusPicker.SelectedIndex = 0;
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            var title = TitleEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                await DisplayAlert("Validation", "Title is required.", "OK");
                return;
            }

            var bug = new BugItem
            {
                Id = System.Guid.NewGuid().ToString(),
                Title = title,
                Description = DescriptionEditor.Text ?? string.Empty,
                Project = ProjectEntry.Text ?? string.Empty,
                AssigneeEmail = AssigneeEntry.Text ?? string.Empty,
                Status = (BugStatus)(StatusPicker.SelectedIndex == 0 ? BugStatus.New : (StatusPicker.SelectedIndex == 1 ? BugStatus.InProgress : BugStatus.Resolved))
            };

            var main = Application.Current?.MainPage as NavigationPage;
            var page = main?.CurrentPage as MainPage;
            if (page != null) page.AddToCollection(bug);

            await Navigation.PopAsync();
        }
    }
}