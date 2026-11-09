using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Helpers;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Ava.UI.Windows;
using System;
using System.Linq;

namespace SuperSwitch.Ava.UI.Views.Misc
{
    public partial class ApplicationListView : SuperSwitchControl<MainWindowViewModel>
    {
        public static readonly RoutedEvent<ApplicationOpenedEventArgs> ApplicationOpenedEvent =
            RoutedEvent.Register<ApplicationListView, ApplicationOpenedEventArgs>(nameof(ApplicationOpened), RoutingStrategies.Bubble);

        public event EventHandler<ApplicationOpenedEventArgs> ApplicationOpened
        {
            add => AddHandler(ApplicationOpenedEvent, value);
            remove => RemoveHandler(ApplicationOpenedEvent, value);
        }

        public ApplicationListView() => InitializeComponent();

        public void GameList_DoubleTapped(object sender, TappedEventArgs args)
        {
            if (sender is ListBox { SelectedItem: ApplicationData selected })
                RaiseEvent(new ApplicationOpenedEventArgs(selected, ApplicationOpenedEvent));
        }

        private async void PlayabilityStatus_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Content: TextBlock playabilityLabel })
                return;

            await CompatibilityListWindow.Show((string)playabilityLabel.Tag);
        }
        
        private async void LdnGames_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Content: TextBlock ldnGamesLabel })
                return;

            await LdnGamesListWindow.Show((string)ldnGamesLabel.Tag);
        }

        private async void IdString_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Content: TextBlock idText })
                return;

            if (!SuperSwitchApp.IsClipboardAvailable(out IClipboard clipboard))
                return;

            ApplicationData appData = ViewModel.Applications.FirstOrDefault(it => it.IdString == idText.Text);
            if (appData is null)
                return;

            await clipboard.SetTextAsync(appData.IdString);

            NotificationHelper.ShowInformation(
                "Copied Title ID",
                $"{appData.Name} ({appData.IdString})");
        }
    }
}
