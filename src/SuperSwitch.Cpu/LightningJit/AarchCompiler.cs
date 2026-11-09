using ARMeilleure.Common;
using ARMeilleure.Memory;
using SuperSwitch.Cpu.LightningJit.Arm32;
using SuperSwitch.Cpu.LightningJit.Arm64;
using SuperSwitch.Cpu.LightningJit.State;
using System.Runtime.InteropServices;

namespace SuperSwitch.Cpu.LightningJit
{
    class AarchCompiler
    {
        public static CompiledFunction Compile(
            CpuPreset cpuPreset,
            IMemoryManager memoryManager,
            ulong address,
            IAddressTable<ulong> funcTable,
            nint dispatchStubPtr,
            ExecutionMode executionMode,
            Architecture targetArch)
        {
            if (executionMode == ExecutionMode.Aarch64)
            {
                return A64Compiler.Compile(cpuPreset, memoryManager, address, funcTable, dispatchStubPtr, targetArch);
            }
            else
            {
                return A32Compiler.Compile(cpuPreset, memoryManager, address, funcTable, dispatchStubPtr, executionMode == ExecutionMode.Aarch32Thumb, targetArch);
            }
        }
    }
}
