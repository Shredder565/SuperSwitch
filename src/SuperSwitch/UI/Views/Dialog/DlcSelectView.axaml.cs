using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using SuperSwitch.Ava.Common.Models;
using SuperSwitch.Ava.Systems.AppLibrary;
using SuperSwitch.Ava.UI.Controls;
using SuperSwitch.Ava.UI.Helpers;
using SuperSwitch.Ava.UI.ViewModels;
using System.Threading.Tasks;

namespace SuperSwitch.Ava.UI.Views.Dialog
{
    public partial class DlcSelectView : SuperSwitchControl<DlcSelectViewModel>
    {
        public DlcSelectView()
        {
            InitializeComponent();
        }

#nullable enable
        public static async Task<DownloadableContentModel?> Show(ulong selectedTitleId, ApplicationLibrary appLibrary)
#nullable disable
        {
            DlcSelectViewModel viewModel = new(selectedTitleId, appLibrary);

            FAContentDialog contentDialog = new()
            {
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.Continue],
                SecondaryButtonText = string.Empty,
                CloseButtonText = string.Empty,
                Content = new DlcSelectView { ViewModel = viewModel }
            };

            await ContentDialogHelper.ShowAsync(contentDialog.ApplyStyles());

            return viewModel.SelectedDlc;
        }
    }
}
