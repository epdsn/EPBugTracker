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
                await DisplayAlertAsync("Validation", "Title is required.", "OK");
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

            // Try to call AddToCollection on the previous page in the navigation stack (should be MainPage)
            if (Navigation != null)
            {
                var stack = Navigation.NavigationStack;
                if (stack != null && stack.Count >= 2 && stack[stack.Count - 2] is MainPage prev)
                {
                    prev.AddToCollection(bug);
                }
                else
                {
                    // Fallback: try Application.Current.MainPage as NavigationPage
                    var mainNav = Application.Current?.MainPage as NavigationPage;
                    var page = mainNav?.CurrentPage as MainPage;
                    if (page != null)
                    {
                        page.AddToCollection(bug);
                    }
                }
            }

            await Navigation.PopAsync();
        }
    }
}