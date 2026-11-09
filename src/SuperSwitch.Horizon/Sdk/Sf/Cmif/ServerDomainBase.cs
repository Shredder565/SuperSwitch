using SuperSwitch.Horizon.Common;
using System;

namespace SuperSwitch.Horizon.Sdk.Sf.Cmif
{
    abstract class ServerDomainBase
    {
        public abstract Result ReserveIds(Span<int> outIds);
        public abstract void UnreserveIds(ReadOnlySpan<int> ids);
        public abstract void RegisterObject(int id, ServiceObjectHolder obj);

        public abstract ServiceObjectHolder UnregisterObject(int id);
        public abstract ServiceObjectHolder GetObject(int id);
    }
}
