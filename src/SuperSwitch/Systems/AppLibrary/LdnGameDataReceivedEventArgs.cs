using SuperSwitch.Ava.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperSwitch.Ava.Systems.AppLibrary
{
    public class LdnGameDataReceivedEventArgs : EventArgs
    {
        public static new readonly LdnGameDataReceivedEventArgs Empty = new(null);

        public LdnGameDataReceivedEventArgs(LdnGameModel[] ldnData)
        {
            LdnData = ldnData ?? [];
        }
        
        public LdnGameDataReceivedEventArgs(IEnumerable<LdnGameModel> ldnData)
        {
            LdnData = ldnData?.ToArray() ?? [];
        }


        public LdnGameModel[] LdnData { get; }
    }
}
