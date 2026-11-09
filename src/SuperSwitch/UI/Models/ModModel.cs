using CommunityToolkit.Mvvm.ComponentModel;
using SuperSwitch.Ava.UI.ViewModels;
using System.Globalization;

namespace SuperSwitch.Ava.UI.Models
{
    public partial class ModModel : BaseModel
    {
        [ObservableProperty]
        public partial bool Enabled { get; set; }
        public bool InSd { get; }
        public string Path { get; }
        public string Name { get; }

        public string FormattedName =>
            InSd && ulong.TryParse(Name, NumberStyles.HexNumber, null, out ulong applicationId)
                ? $"Atmosphère: {SuperSwitchApp.MainWindow.ApplicationLibrary.GetNameForApplicationId(applicationId)}"
                : Name;

        public ModModel(string path, string name, bool enabled, bool inSd)
        {
            Path = path;
            Name = name;
            Enabled = enabled;
            InSd = inSd;
        }
    }
}
