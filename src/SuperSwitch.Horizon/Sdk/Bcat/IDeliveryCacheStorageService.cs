using LibHac.Bcat;
using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;
using System;

namespace SuperSwitch.Horizon.Sdk.Bcat
{
    internal interface IDeliveryCacheStorageService : IServiceObject
    {
        Result CreateDirectoryService(out IDeliveryCacheDirectoryService service);
        Result CreateFileService(out IDeliveryCacheFileService service);
        Result EnumerateDeliveryCacheDirectory(out int count, Span<DirectoryName> directoryNames);
    }
}
