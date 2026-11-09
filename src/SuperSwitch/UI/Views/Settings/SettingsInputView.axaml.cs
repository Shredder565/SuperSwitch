using Avalonia;
using Avalonia.Controls;
using SuperSwitch.Ava.UI.Windows;

namespace SuperSwitch.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        private bool _inputUpdatesBlocked;

        public SettingsInputView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SetInputUpdatesBlocked(true);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            SetInputUpdatesBlocked(false);
            base.OnDetachedFromVisualTree(e);
        }

        public void Dispose()
        {
            try
            {
                InputView.Dispose();
            }
            finally
            {
                SetInputUpdatesBlocked(false);
            }
        }

        private void SetInputUpdatesBlocked(bool blocked)
        {
            if (_inputUpdatesBlocked == blocked)
            {
                return;
            }

            MainWindow mainWindow = SuperSwitchApp.MainWindow;
            if (mainWindow?.ViewModel?.AppHost?.NpadManager is not { } npadManager)
            {
                return;
            }

            if (blocked)
            {
                npadManager.BlockInputUpdates();
            }
            else
            {
                npadManager.UnblockInputUpdates();
            }

            _inputUpdatesBlocked = blocked;
        }
    }
}
