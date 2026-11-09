using CommunityToolkit.Mvvm.ComponentModel;
using SuperSwitch.HLE.HOS.Services.Account.Acc;
using System.Collections.ObjectModel;

namespace SuperSwitch.Ava.UI.ViewModels
{
    public partial class ProfileSelectorDialogViewModel : BaseModel
    {

        [ObservableProperty]
        public partial UserId SelectedUserId { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<BaseModel> Profiles { get; set; } = [];
    }
}
