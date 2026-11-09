using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SuperSwitch.Ava.Systems.Configuration;
using SuperSwitch.Ava.UI.ViewModels;
using System.Reflection;

namespace SuperSwitch.Ava.UI.Controls
{
    public class SuperSwitchLogo : Image
    {
        // The UI specifically uses a thicker bordered variant of the icon to avoid crunching out the border at lower resolutions.
        // For an example of this, download canary 1.2.95, then open the settings menu, and look at the icon in the top-left.
        // The border gets reduced to colored pixels in the 4 corners.
        public static readonly Bitmap Bitmap =
            new(Assembly.GetAssembly(typeof(MainWindowViewModel))!
                .GetManifestResourceStream("SuperSwitch.Assets.UIImages.Logo_SuperSwitch_AntiAlias.png")!);

        public SuperSwitchLogo()
        {
            Margin = new Thickness(7, 7, 7, 0);
            Height = 25;
            Width = 25;
            Source = Bitmap;
            // SuperSwitch: the old emulator logo is no longer shown in window headers and the menu bar.
            IsVisible = false;
        }
    }
}
