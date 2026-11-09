using LibHac.Loader;
using SuperSwitch.Common;

namespace SuperSwitch.HLE.Loaders.Processes.Extensions
{
    public static class MetaLoaderExtensions
    {
        public static void LoadDefault(this MetaLoader metaLoader)
        {
            byte[] npdmBuffer = EmbeddedResources.Read("SuperSwitch.HLE/Homebrew.npdm");

            metaLoader.Load(npdmBuffer).ThrowIfFailure();
        }
    }
}
