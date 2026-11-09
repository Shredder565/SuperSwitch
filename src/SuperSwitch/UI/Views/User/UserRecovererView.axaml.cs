using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.ViewModels;
using NavigationEventArgs = FluentAvalonia.UI.Navigation.FANavigationEventArgs;

namespace SuperSwitch.Ava.UI.Views.User
{
    public partial class UserRecovererView : SuperSwitchControl<UserProfileViewModel>
    {
        private NavigationDialogHost _parent;

        public UserRecovererView()
        {
            InitializeComponent();
            AddHandler(FAFrame.NavigatedToEvent, (s, e) =>
            {
                NavigatedTo(e);
            }, RoutingStrategies.Direct);
        }

        private void NavigatedTo(NavigationEventArgs arg)
        {
            if (Program.PreviewerDetached)
            {
                switch (arg.NavigationMode)
                {
                    case FANavigationMode.New:
                        NavigationDialogHost parent = (NavigationDialogHost)arg.Parameter;

                        _parent = parent;

                        ((FAContentDialog)_parent.Parent).Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - {LocaleManager.Instance[LocaleKeys.UserProfilesRecoverHeading]}";

                        break;
                }
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent?.GoBack();
        }

        private void Recover(object sender, RoutedEventArgs e)
        {
            _parent?.RecoverLostAccounts();
        }
    }
}
