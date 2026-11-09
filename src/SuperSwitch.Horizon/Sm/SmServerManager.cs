using SuperSwitch.Horizon.Common;
using SuperSwitch.Horizon.Sdk.Sf.Hipc;
using SuperSwitch.Horizon.Sdk.Sm;
using SuperSwitch.Horizon.Sm.Impl;
using SuperSwitch.Horizon.Sm.Ipc;
using SuperSwitch.Horizon.Sm.Types;
using System;

namespace SuperSwitch.Horizon.Sm
{
    class SmServerManager : ServerManager
    {
        private readonly ServiceManager _serviceManager;

        public SmServerManager(ServiceManager serviceManager, HeapAllocator allocator, SmApi sm, int maxPorts, ManagerOptions options, int maxSessions) : base(allocator, sm, maxPorts, options, maxSessions)
        {
            _serviceManager = serviceManager;
        }

        protected override Result OnNeedsToAccept(int portIndex, Server server)
        {
            return (SmPortIndex)portIndex switch
            {
                SmPortIndex.User => AcceptImpl(server, new UserService(_serviceManager)),
                SmPortIndex.Manager => AcceptImpl(server, new ManagerService()),
                _ => throw new ArgumentOutOfRangeException(nameof(portIndex)),
            };
        }
    }
}
