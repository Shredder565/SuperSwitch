using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Helpers;
using SuperSwitch.Ava.UI.Models;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Common.Helper;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;

namespace SuperSwitch.Ava.UI.Views.Dialog
{
    public partial class ModManagerView : SuperSwitchControl<ModManagerViewModel>
    {
        public ModManagerView()
        {
            InitializeComponent();
        }

        public static async Task Show(ulong titleId, ulong titleIdBase, ApplicationLibrary appLibrary, string titleName)
        {
            FAContentDialog contentDialog = new()
            {
                PrimaryButtonText = string.Empty,
                SecondaryButtonText = string.Empty,
                CloseButtonText = string.Empty,
                Content = new ModManagerView
                {
                    ViewModel = new ModManagerViewModel(titleId, titleIdBase, appLibrary)
                },
                Title = string.Format(LocaleManager.Instance[LocaleKeys.ModWindowTitle], titleName, titleId.ToString("X16")),
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void SaveAndClose(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();
            ((FAContentDialog)Parent).Hide();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((FAContentDialog)Parent).Hide();
        }

        private async void DeleteMod(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is ModModel model)
                {
                    UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                        LocaleManager.Instance[LocaleKeys.DialogWarning],
                        LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogModManagerDeletionWarningMessage, model.Name),
                        LocaleManager.Instance[LocaleKeys.InputDialogYes],
                        LocaleManager.Instance[LocaleKeys.InputDialogNo],
                        LocaleManager.Instance[LocaleKeys.SuperSwitchConfirm]);

                    if (result == UserResult.Yes)
                    {
                        ViewModel.Delete(model);
                    }
                }
            }
        }

        private async void DeleteAll(object sender, RoutedEventArgs e)
        {
            UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                LocaleManager.Instance[LocaleKeys.DialogWarning],
                LocaleManager.Instance[LocaleKeys.DialogModManagerDeletionAllWarningMessage],
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                LocaleManager.Instance[LocaleKeys.SuperSwitchConfirm]);

            if (result == UserResult.Yes)
            {
                ViewModel.DeleteAll();
            }
        }

        private void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is ModModel model)
                {
                    OpenHelper.OpenFolder(model.Path);
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (object content in e.AddedItems)
            {
                if (content is ModModel model)
                {
                    int index = ViewModel.Mods.IndexOf(model);

                    if (index != -1)
                    {
                        ViewModel.Mods[index].Enabled = true;
                    }
                }
            }

            foreach (object content in e.RemovedItems)
            {
                if (content is ModModel model)
                {
                    int index = ViewModel.Mods.IndexOf(model);

                    if (index != -1)
                    {
                        ViewModel.Mods[index].Enabled = false;
                    }
                }
            }
        }
    }
}
