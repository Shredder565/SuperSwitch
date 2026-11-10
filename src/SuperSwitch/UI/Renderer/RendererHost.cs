using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SuperSwitch.Ava.Systems.Configuration;
using SuperSwitch.Common.Configuration;
using System;

namespace SuperSwitch.Ava.UI.Renderer
{
    public class RendererHost : UserControl, IDisposable
    {
        public readonly EmbeddedWindow EmbeddedWindow;

        public event EventHandler<EventArgs> WindowCreated;
        public event Action<object, Size> BoundsChanged;

        public RendererHost()
        {
            Focusable = true;
            FlowDirection = FlowDirection.LeftToRight;

            // SuperSwitch: Vulkan is the only graphics backend.
            EmbeddedWindow = new EmbeddedWindowVulkan();

            Initialize();
        }

        public GraphicsBackend Backend =>
            EmbeddedWindow switch
            {
                EmbeddedWindowVulkan => GraphicsBackend.Vulkan,
                _ => throw new NotImplementedException()
            };


        private void Initialize()
        {
            EmbeddedWindow.WindowCreated += CurrentWindow_WindowCreated;
            EmbeddedWindow.BoundsChanged += CurrentWindow_BoundsChanged;

            Content = EmbeddedWindow;
        }
        public void Dispose()
        {
            if (EmbeddedWindow != null)
            {
                EmbeddedWindow.WindowCreated -= CurrentWindow_WindowCreated;
                EmbeddedWindow.BoundsChanged -= CurrentWindow_BoundsChanged;
            }

            GC.SuppressFinalize(this);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            Dispose();
        }

        private void CurrentWindow_BoundsChanged(object sender, Size e)
        {
            BoundsChanged?.Invoke(sender, e);
        }

        private void CurrentWindow_WindowCreated(object sender, nint e)
        {
            WindowCreated?.Invoke(this, EventArgs.Empty);
        }
    }
}
