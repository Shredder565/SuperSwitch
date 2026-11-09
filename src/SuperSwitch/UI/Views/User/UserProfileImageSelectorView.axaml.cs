using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Gommon;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Models;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Ava.Utilities;
using SuperSwitch.HLE.FileSystem;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using NavigationEventArgs = FluentAvalonia.UI.Navigation.FANavigationEventArgs;

namespace SuperSwitch.Ava.UI.Views.User
{
    public partial class UserProfileImageSelectorView : SuperSwitchControl<UserProfileImageSelectorViewModel>
    {
        private ContentManager _contentManager;
        private NavigationDialogHost _parent;
        private TempProfile _profile;

        public UserProfileImageSelectorView()
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
                switch (arg.NavigationMode)
                {
                    case FANavigationMode.New:
                        (_parent, _profile) = ((NavigationDialogHost, TempProfile))arg.Parameter;
                        _contentManager = _parent.ContentManager;

                        ((FAContentDialog)_parent.Parent)?.Title = $"{LocaleManager.Instance[LocaleKeys.UserProfileWindowTitle]} - {LocaleManager.Instance[LocaleKeys.ProfileImageSelectionHeader]}";

                        if (Program.PreviewerDetached)
                        {
                            DataContext = ViewModel = new UserProfileImageSelectorViewModel();
                            ViewModel.FirmwareFound = _contentManager.GetCurrentFirmwareVersion() != null;
                        }

                        break;
                    case FANavigationMode.Back:
                        if (_profile.Image != null)
                        {
                            _parent.GoBack();
                        }

                        break;
                }
            }
        }

        private async void Import_OnClick(object sender, RoutedEventArgs e)
        {
            Optional<IStorageFile> result = await ((Window)TopLevel.GetTopLevel(this))!.StorageProvider.OpenSingleFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocaleManager.Instance[LocaleKeys.Common_FilePicker_AllSupportedFormats])
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"],
                        AppleUniformTypeIdentifiers = ["public.jpeg", "public.png", "com.microsoft.bmp"],
                        MimeTypes = ["image/jpeg", "image/png", "image/bmp"],
                    },
                },
            });

            if (result.HasValue)
            {
                _profile.Image = ProcessProfileImage(await File.ReadAllBytesAsync(result.Value.Path.LocalPath));
                _parent.GoBack();
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            _parent.GoBack();
        }

        private void SelectFirmwareImage_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.FirmwareFound)
            {
                _parent.Navigate(typeof(UserFirmwareAvatarSelectorView), (_parent, _profile));
            }
        }

        private static byte[] ProcessProfileImage(byte[] buffer)
        {
            using SKBitmap bitmap = SKBitmap.Decode(buffer);

            SKBitmap resizedBitmap = bitmap.Resize(new SKImageInfo(256, 256), new SKSamplingOptions(SKFilterMode.Linear));

            using MemoryStream streamJpg = new();

            if (resizedBitmap != null)
            {
                using SKImage image = SKImage.FromBitmap(resizedBitmap);
                using SKData dataJpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100);

                dataJpeg.SaveTo(streamJpg);
            }

            return streamJpg.ToArray();
        }
    }
}
