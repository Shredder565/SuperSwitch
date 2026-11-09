using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Models;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.HLE.FileSystem;
using SkiaSharp;
using System.IO;
using NavigationEventArgs = FluentAvalonia.UI.Navigation.FANavigationEventArgs;

namespace SuperSwitch.Ava.UI.Views.User
{
    public partial class UserFirmwareAvatarSelectorView : SuperSwitchControl<UserFirmwareAvatarSelectorViewModel>
    {
        private NavigationDialogHost _parent;
        private TempProfile _profile;

        public UserFirmwareAvatarSelectorView(ContentManager contentManager)
        {
            ContentManager = contentManager;

            InitializeComponent();
        }

        public UserFirmwareAvatarSelectorView()
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
                if (arg.NavigationMode == FANavigationMode.New)
                {
                    (_parent, _profile) = ((NavigationDialogHost, TempProfile))arg.Parameter;
                    ContentManager = _parent.ContentManager;
                    if (Program.PreviewerDetached)
                    {
                        ViewModel = new UserFirmwareAvatarSelectorViewModel();
                    }

                    DataContext = ViewModel;
                }
            }
        }

        public ContentManager ContentManager { get; private set; }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent.GoBack();
        }

        private void ChooseButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedImage != null)
            {
                using MemoryStream streamJpg = new();
                using SKBitmap bitmap = SKBitmap.Decode(ViewModel.SelectedImage);
                using SKBitmap newBitmap = new(bitmap.Width, bitmap.Height);

                using (SKCanvas canvas = new(newBitmap))
                {
                    canvas.Clear(new SKColor(
                        ViewModel.BackgroundColor.R,
                        ViewModel.BackgroundColor.G,
                        ViewModel.BackgroundColor.B,
                        ViewModel.BackgroundColor.A));
                    canvas.DrawBitmap(bitmap, 0, 0);
                }

                using (SKImage image = SKImage.FromBitmap(newBitmap))
                using (SKData dataJpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100))
                {
                    dataJpeg.SaveTo(streamJpg);
                }

                _profile.Image = streamJpg.ToArray();

                _parent.GoBack();
            }
        }
    }
}
