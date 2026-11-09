using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Helpers;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Ava.UI.Windows;
using System.Linq;
using System.Threading.Tasks;

namespace SuperSwitch.Ava.UI.Views.Dialog
{
    public partial class ApplicationDataView : SuperSwitchControl<ApplicationDataViewModel>
    {
        public static async Task Show(ApplicationData appData)
        {
            FAContentDialog contentDialog = new()
            {
                Title = appData.Name,
                PrimaryButtonText = string.Empty,
                SecondaryButtonText = string.Empty,
                CloseButtonText = LocaleManager.Instance[LocaleKeys.SettingsButtonClose],
                MinWidth = 256,
                Content = new ApplicationDataView { ViewModel = new ApplicationDataViewModel(appData) }
            };

            await ContentDialogHelper.ShowAsync(contentDialog.ApplyStyles(160, HorizontalAlignment.Center));
        }

        public ApplicationDataView()
        {
            InitializeComponent();
        }

        private async void PlayabilityStatus_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Content: TextBlock playabilityLabel })
                return;

            if (SuperSwitchApp.AppLifetime.Windows.TryGetFirst(x => x is ContentDialogOverlayWindow, out Window window))
                window.Close(FAContentDialogResult.None);

            await CompatibilityListWindow.Show((string)playabilityLabel.Tag);
        }

        private async void IdString_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Content: TextBlock idText })
                return;

            if (!SuperSwitchApp.IsClipboardAvailable(out IClipboard clipboard))
                return;

            ApplicationData appData = SuperSwitchApp.MainWindow.ViewModel.Applications.FirstOrDefault(it => it.IdString == idText.Text);
            if (appData is null)
                return;

            await clipboard.SetTextAsync(appData.IdString);

            NotificationHelper.ShowInformation(
                "Copied Title ID",
                $"{appData.Name} ({appData.IdString})");
        }
    }
}

