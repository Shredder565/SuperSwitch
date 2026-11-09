using System;

namespace SuperSwitch.Ava.Systems.AppLibrary
{
    public class ApplicationCountUpdatedEventArgs : EventArgs
    {
        public int NumAppsFound { get; set; }
        public int NumAppsLoaded { get; set; }
    }
}
