using Microsoft.Maui.Controls;

namespace EPBugTracker
{
    public partial class UsersPage : ContentPage
    {
        public UsersPage()
        {
            InitializeComponent();
            UsersCollection.ItemsSource = UserStore.Users;
        }

        private void OnAddUpdateClicked(object? sender, EventArgs e)
        {
            var name = NameEntry.Text?.Trim() ?? string.Empty;
            var email = EmailEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email)) return;

            var user = new UserItem { Name = name, Email = email };
            UserStore.AddOrUpdate(user);
            NameEntry.Text = string.Empty;
            EmailEntry.Text = string.Empty;
        }

        private void OnRemoveClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is UserItem user)
            {
                UserStore.Remove(user);
            }
        }
    }
}
