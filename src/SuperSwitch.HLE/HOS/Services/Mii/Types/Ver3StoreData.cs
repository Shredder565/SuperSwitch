using SuperSwitch.Common.Memory;
using System;
using System.Runtime.InteropServices;

namespace SuperSwitch.HLE.HOS.Services.Mii.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = Size)]
    struct Ver3StoreData
    {
        public const int Size = 0x60;

        private Array96<byte> _storage;

        public Span<byte> Storage => _storage.AsSpan();

        // TODO: define all getters/setters
    }
}
