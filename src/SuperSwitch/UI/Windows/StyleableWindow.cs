using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using FluentAvalonia.UI.Windowing;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.Systems.Configuration;
using SuperSwitch.Ava.UI.Controls;
using System.Threading.Tasks;

namespace SuperSwitch.Ava.UI.Windows
{
    public abstract class StyleableAppWindow : FAAppWindow
    {
        public static async Task ShowAsync(StyleableAppWindow appWindow, Window owner = null)
        {
/*
#if DEBUG
        appWindow.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Control));
#endif
*/
        await appWindow.ShowDialog(owner ?? SuperSwitchApp.MainWindow);
    }

        protected StyleableAppWindow(bool useCustomTitleBar = false, double? titleBarHeight = null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            TransparencyLevelHint = [WindowTransparencyLevel.None];

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();

            if (useCustomTitleBar)
            {
                TitleBar.ExtendsContentIntoTitleBar = !ConfigurationState.Instance.ShowOldUI;

                if (TitleBar.ExtendsContentIntoTitleBar && titleBarHeight != null)
                    TitleBar.Height = titleBarHeight.Value;
            }

            Icon = SuperSwitchLogo.Bitmap;
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        /* protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        } */
    }

    public abstract class StyleableWindow : Window
    {
        public static async Task ShowAsync(StyleableWindow window, Window owner = null)
        {
/*
#if DEBUG
            window.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Control));
#endif
*/
            await window.ShowDialog(owner ?? SuperSwitchApp.MainWindow);
        }

        protected StyleableWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            TransparencyLevelHint = [WindowTransparencyLevel.None];

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();

            Icon = new WindowIcon(SuperSwitchLogo.Bitmap);
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        /* protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        } */
    }
}
