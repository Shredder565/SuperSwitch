using ARMeilleure.Common;
using ARMeilleure.Memory;
using SuperSwitch.Cpu.LightningJit.Arm32.Target.Arm64;
using System;
using System.Runtime.InteropServices;

namespace SuperSwitch.Cpu.LightningJit.Arm32
{
    static class A32Compiler
    {
        public static CompiledFunction Compile(
            CpuPreset cpuPreset,
            IMemoryManager memoryManager,
            ulong address,
            IAddressTable<ulong> funcTable,
            nint dispatchStubPtr,
            bool isThumb,
            Architecture targetArch)
        {
            if (targetArch == Architecture.Arm64)
            {
                return Compiler.Compile(cpuPreset, memoryManager, address, funcTable, dispatchStubPtr, isThumb);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}
