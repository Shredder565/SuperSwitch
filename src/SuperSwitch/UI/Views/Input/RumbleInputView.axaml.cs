using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Models.Input;
using SuperSwitch.Ava.UI.ViewModels.Input;
using System.Threading.Tasks;

namespace SuperSwitch.Ava.UI.Views.Input
{
    public partial class RumbleInputView : SuperSwitchControl<RumbleInputViewModel>
    {
        public RumbleInputView()
        {
            InitializeComponent();
        }

        public RumbleInputView(ControllerInputViewModel viewModel)
        {
            GamepadInputConfig config = viewModel.Config;

            ViewModel = new RumbleInputViewModel
            {
                StrongRumble = config.StrongRumble,
                WeakRumble = config.WeakRumble,
                EnableHDRumble = config.UseHDRumble
            };

            InitializeComponent();
        }

        public static async Task Show(ControllerInputViewModel viewModel)
        {
            RumbleInputView content = new(viewModel);

            FAContentDialog contentDialog = new()
            {
                Title = LocaleManager.Instance[LocaleKeys.ControllerRumbleTitle],
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsSave],
                SecondaryButtonText = string.Empty,
                CloseButtonText = LocaleManager.Instance[LocaleKeys.ControllerSettingsClose],
                Content = content,
            };

            contentDialog.PrimaryButtonClick += (_, _) =>
            {
                GamepadInputConfig config = viewModel.Config;
                config.StrongRumble = content.ViewModel.StrongRumble;
                config.WeakRumble = content.ViewModel.WeakRumble;
                config.UseHDRumble = content.ViewModel.EnableHDRumble;
            };

            await contentDialog.ShowAsync();
        }
    }
}
