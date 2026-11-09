using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SuperSwitch.Ava.UI.Controls
{
    public class ApplicationContextMenu : MenuFlyout
    {
        public ApplicationContextMenu()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
