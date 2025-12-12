using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace EPBugTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // update link states initially and when navigation occurs
            UpdateRouteLinkStates();
            this.Navigated += OnShellNavigated;
        }

        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            UpdateRouteLinkStates();
        }

        private void UpdateRouteLinkStates()
        {
            try
            {
                // determine current location / route
                var location = Shell.Current?.CurrentState?.Location?.ToString() ?? string.Empty;

                // decide active state by route name presence
                var isHome = location.IndexOf("MainPage", StringComparison.OrdinalIgnoreCase) >= 0;
                var isUsers = location.IndexOf("UsersPage", StringComparison.OrdinalIgnoreCase) >= 0;

                var active = (Color)Application.Current.Resources["White"];
                var routeLink = (Color)Application.Current.Resources["RouteLink"];

                if (HomeLinkBtn != null) HomeLinkBtn.TextColor = isHome ? active : routeLink;
                if (UsersLinkBtn != null) UsersLinkBtn.TextColor = isUsers ? active : routeLink;
            }
            catch
            {
                // ignore resource lookup errors
            }
        }

        private async void OnNavigateHome(object? sender, System.EventArgs e)
        {
            // Navigate to the Home tab (absolute route ensures tab is selected)
            await Shell.Current.GoToAsync("//MainPage");
        }

        private async void OnNavigateUsers(object? sender, System.EventArgs e)
        {
            await Shell.Current.GoToAsync("//UsersPage");
        }

        private async void OnAddBugClicked(object? sender, System.EventArgs e)
        {
            // Push AddBugPage on the current navigation stack
            await Shell.Current.Navigation.PushAsync(new AddBugPage());
        }

        private void OnReloadClicked(object? sender, System.EventArgs e)
        {
            // Try to find MainPage instance and call its reload method
            var current = Shell.Current?.CurrentPage;
            if (current is MainPage main)
            {
                main.ReloadFromDisk();
                return;
            }

            // Fallback: search navigation stack
            try
            {
                var nav = Shell.Current?.Navigation;
                if (nav != null)
                {
                    foreach (var p in nav.NavigationStack)
                    {
                        if (p is MainPage mp)
                        {
                            mp.ReloadFromDisk();
                            return;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
