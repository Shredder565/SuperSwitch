using LibHac.Bcat;
using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf;
using System;

namespace SuperSwitch.Horizon.Sdk.Bcat
{
    internal interface IDeliveryCacheDirectoryService : IServiceObject
    {
        Result GetCount(out int count);
        Result Open(DirectoryName directoryName);
        Result Read(out int entriesRead, Span<DeliveryCacheDirectoryEntry> entriesBuffer);
    }
}
