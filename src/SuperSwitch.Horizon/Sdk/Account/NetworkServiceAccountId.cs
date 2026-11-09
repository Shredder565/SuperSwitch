using System.Runtime.InteropServices;

namespace SuperSwitch.Horizon.Sdk.Account
{
    [StructLayout(LayoutKind.Sequential, Size = 0x8, Pack = 0x8)]
    readonly record struct NetworkServiceAccountId
    {
        public readonly ulong Id;

        public NetworkServiceAccountId(ulong id)
        {
            Id = id;
        }

        public override readonly string ToString()
        {
            return Id.ToString("x16");
        }
    }
}
