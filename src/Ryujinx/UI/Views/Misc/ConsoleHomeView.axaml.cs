using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;

namespace Ryujinx.Ava.UI.Views.Misc
{
    /// <summary>
    /// SuperSwitch: console-style home screen (one row of large game tiles, the selected game shown big).
    /// </summary>
    public partial class ConsoleHomeView : RyujinxControl<MainWindowViewModel>
    {
        public ConsoleHomeView() => InitializeComponent();

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            // Arrow keys and Enter work straight away, without clicking a game first.
            GameList.Focus();
        }

        private async void PlaySelected()
        {
            if (DataContext is not MainWindowViewModel viewModel || viewModel.IsGameRunning)
            {
                return;
            }

            ApplicationData application = viewModel.ConsoleSelectedApplication;

            if (application == null)
            {
                return;
            }

            viewModel.SelectedIcon = application.Icon;

            await viewModel.LoadApplication(application);
        }

        private void Play_Click(object sender, RoutedEventArgs e) => PlaySelected();

        private void GameList_DoubleTapped(object sender, TappedEventArgs e) => PlaySelected();

        private void GameList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                PlaySelected();
            }
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow window)
            {
                await window.OpenSettingsFromHome();
            }
        }

        private void GameOptions_Click(object sender, RoutedEventArgs e)
        {
            // The same per-game menu as a right-click on a tile (saves, mods, updates, game settings, ...).
            if (sender is Control anchor)
            {
                GameList.ContextFlyout?.ShowAt(anchor);
            }
        }
    }
}
