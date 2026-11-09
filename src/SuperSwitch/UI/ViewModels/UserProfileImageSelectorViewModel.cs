using CommunityToolkit.Mvvm.ComponentModel;

namespace SuperSwitch.Ava.UI.ViewModels
{
    public partial class UserProfileImageSelectorViewModel : BaseModel
    {
        [ObservableProperty]
        public partial bool FirmwareFound { get; set; }
    }
}
