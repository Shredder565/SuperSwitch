using System.Collections.Generic;

namespace SuperSwitch.Common.Configuration
{
    public struct ModMetadata
    {
        public List<Mod> Mods { get; set; }

        public ModMetadata()
        {
            Mods = [];
        }
    }
}
