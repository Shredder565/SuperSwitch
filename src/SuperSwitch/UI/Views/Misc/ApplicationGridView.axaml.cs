using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Helpers;
using SuperSwitch.Ava.UI.ViewModels;
using System;

namespace SuperSwitch.Ava.UI.Views.Misc
{
    public partial class ApplicationGridView : SuperSwitchControl<MainWindowViewModel>
    {
        public static readonly RoutedEvent<ApplicationOpenedEventArgs> ApplicationOpenedEvent =
            RoutedEvent.Register<ApplicationGridView, ApplicationOpenedEventArgs>(nameof(ApplicationOpened), RoutingStrategies.Bubble);

        public event EventHandler<ApplicationOpenedEventArgs> ApplicationOpened
        {
            add => AddHandler(ApplicationOpenedEvent, value);
            remove => RemoveHandler(ApplicationOpenedEvent, value);
        }

        public ApplicationGridView() => InitializeComponent();

        public void GameList_DoubleTapped(object sender, TappedEventArgs args)
        {
            if (sender is ListBox { SelectedItem: ApplicationData selected })
                RaiseEvent(new ApplicationOpenedEventArgs(selected, ApplicationOpenedEvent));
        }
    }
}
