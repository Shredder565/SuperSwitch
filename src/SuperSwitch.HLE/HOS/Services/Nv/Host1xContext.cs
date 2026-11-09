using SuperSwitch.Graphics.Device;
using SuperSwitch.Graphics.Host1x;
using SuperSwitch.Graphics.Nvdec;
using SuperSwitch.Graphics.Vic;
using System;
using GpuContext = SuperSwitch.Graphics.Gpu.GpuContext;

namespace SuperSwitch.HLE.HOS.Services.Nv
{
    class Host1xContext : IDisposable
    {
        public DeviceMemoryManager Smmu { get; }
        public NvMemoryAllocator MemoryAllocator { get; }
        public Host1xDevice Host1x { get; }

        public Host1xContext(GpuContext gpu, ulong pid)
        {
            MemoryAllocator = new NvMemoryAllocator();
            Host1x = new Host1xDevice(gpu.Synchronization);
            Smmu = gpu.CreateDeviceMemoryManager(pid);
            NvdecDevice nvdec = new(Smmu);
            VicDevice vic = new(Smmu);
            Host1x.RegisterDevice(ClassId.Nvdec, nvdec);
            Host1x.RegisterDevice(ClassId.Vic, vic);
        }

        public void Dispose()
        {
            Host1x.Dispose();
        }
    }
}
