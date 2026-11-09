using SuperSwitch.Memory;

namespace SuperSwitch.HLE.HOS.Kernel.Process
{
    interface IProcessContextFactory
    {
        IProcessContext Create(KernelContext context, ulong pid, ulong addressSpaceSize, InvalidAccessHandler invalidAccessHandler, bool for64Bit);
    }
}
