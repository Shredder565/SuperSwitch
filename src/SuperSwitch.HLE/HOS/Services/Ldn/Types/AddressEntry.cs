using SuperSwitch.Common.Memory;
using System.Runtime.InteropServices;

namespace SuperSwitch.HLE.HOS.Services.Ldn.Types
{
    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    struct AddressEntry
    {
        public uint Ipv4Address;
        public Array6<byte> MacAddress;
        public ushort Reserved;
    }
}
