using SuperSwitch.Graphics.Device;
using SuperSwitch.Graphics.Nvdec.Image;

namespace SuperSwitch.Graphics.Nvdec
{
    readonly struct ResourceManager
    {
        public DeviceMemoryManager MemoryManager { get; }
        public SurfaceCache Cache { get; }

        public ResourceManager(DeviceMemoryManager mm, SurfaceCache cache)
        {
            MemoryManager = mm;
            Cache = cache;
        }
    }
}
