using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Helpers;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Common.Helper;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;

namespace SuperSwitch.Ava.UI.Views.Dialog
{
    public partial class AboutView : SuperSwitchControl<AboutWindowViewModel>
    {
        public AboutView()
        {
            InitializeComponent();
        }

        public static async Task Show()
        {
            using AboutWindowViewModel viewModel = new();

            FAContentDialog contentDialog = new()
            {
                PrimaryButtonText = string.Empty,
                SecondaryButtonText = string.Empty,
                CloseButtonText = LocaleManager.Instance[LocaleKeys.UserProfilesClose],
                Content = new AboutView { ViewModel = viewModel }
            };

            await ContentDialogHelper.ShowAsync(contentDialog.ApplyStyles());
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string url })
                OpenHelper.OpenUrl(url);
        }

        private void AmiiboLabel_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock)
            {
                OpenHelper.OpenUrl("https://amiiboapi.com");
            }
        }
    }
}
