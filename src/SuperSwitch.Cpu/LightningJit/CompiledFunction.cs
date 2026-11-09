using System;

namespace SuperSwitch.Cpu.LightningJit
{
    readonly ref struct CompiledFunction
    {
        public readonly ReadOnlySpan<byte> Code;
        public readonly int GuestCodeLength;

        public CompiledFunction(ReadOnlySpan<byte> code, int guestCodeLength)
        {
            Code = code;
            GuestCodeLength = guestCodeLength;
        }
    }
}
