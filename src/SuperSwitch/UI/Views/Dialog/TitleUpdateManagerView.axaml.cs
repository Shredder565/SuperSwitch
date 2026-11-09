using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.Common.Models;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.ViewModels;
using SuperSwitch.Common.Helper;
using System.Threading.Tasks;

namespace SuperSwitch.Ava.UI.Views.Dialog
{
    public partial class TitleUpdateManagerView : SuperSwitchControl<TitleUpdateViewModel>
    {
        public TitleUpdateManagerView()
        {
            InitializeComponent();
        }

        public static async Task Show(ApplicationLibrary applicationLibrary, ApplicationData applicationData)
        {
            FAContentDialog contentDialog = new()
            {
                PrimaryButtonText = string.Empty,
                SecondaryButtonText = string.Empty,
                CloseButtonText = string.Empty,
                Title = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.GameUpdateWindowHeading, applicationData.Name, applicationData.IdBaseString),
                Content = new TitleUpdateManagerView
                {
                    ViewModel = new TitleUpdateViewModel(applicationLibrary, applicationData)
                }
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((FAContentDialog)Parent).Hide();
        }

        public void Save(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();

            ((FAContentDialog)Parent).Hide();
        }

        private void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: TitleUpdateModel model })
                OpenHelper.LocateFile(model.Path);
        }

        private void RemoveUpdate(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: TitleUpdateModel model })
                ViewModel.RemoveUpdate(model);
        }

        private void RemoveAll(object sender, RoutedEventArgs e)
        {
            ViewModel.TitleUpdates.Clear();

            ViewModel.SortUpdates();
        }
    }
}
