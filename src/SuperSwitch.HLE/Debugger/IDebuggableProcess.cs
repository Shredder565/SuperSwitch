using SuperSwitch.Cpu;
using SuperSwitch.HLE.HOS.Kernel.Threading;
using SuperSwitch.Memory;

namespace SuperSwitch.HLE.Debugger
{
    internal interface IDebuggableProcess
    {
        IVirtualMemoryManager CpuMemory { get; }
        ulong[] ThreadUids { get; }
        DebugState DebugState { get; }

        void DebugStop();
        void DebugContinue();
        void DebugContinue(KThread thread);
        bool DebugStep(KThread thread);
        KThread GetThread(ulong threadUid);
        bool IsThreadPaused(KThread thread);
        public void DebugInterruptHandler(IExecutionContext ctx);
        void InvalidateCacheRegion(ulong address, ulong size);
    }
}
