using Avalonia.Interactivity;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.ViewModels;
using System;

namespace SuperSwitch.Ava.UI.Views.Settings
{
    public partial class SettingsNetworkView : SuperSwitchControl<SettingsViewModel>
    {
        private readonly Random _random;

        public SettingsNetworkView()
        {
            _random = new Random();
            InitializeComponent();
        }

        private void GenLdnPassButton_OnClick(object sender, RoutedEventArgs e)
        {
            byte[] code = new byte[4];
            _random.NextBytes(code);
            ViewModel.LdnPassphrase = $"SuperSwitch-{BitConverter.ToUInt32(code):x8}";
        }

        private void ClearLdnPassButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.LdnPassphrase = string.Empty;
        }
    }
}
