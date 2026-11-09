using SuperSwitch.Horizon.Arp;
using SuperSwitch.Horizon.Audio;
using SuperSwitch.Horizon.Bcat;
using SuperSwitch.Horizon.Friends;
using SuperSwitch.Horizon.Hshl;
using SuperSwitch.Horizon.Ins;
using SuperSwitch.Horizon.Lbl;
using SuperSwitch.Horizon.LogManager;
using SuperSwitch.Horizon.MmNv;
using SuperSwitch.Horizon.Ngc;
using SuperSwitch.Horizon.Ovln;
using SuperSwitch.Horizon.Prepo;
using SuperSwitch.Horizon.Psc;
using SuperSwitch.Horizon.Ptm;
using SuperSwitch.Horizon.Sdk.Arp;
using SuperSwitch.Horizon.Srepo;
using SuperSwitch.Horizon.Usb;
using SuperSwitch.Horizon.Wlan;
using System.Collections.Generic;
using System.Threading;

namespace SuperSwitch.Horizon
{
    public class ServiceTable
    {
        private int _readyServices;
        private int _totalServices;

        private readonly ManualResetEvent _servicesReadyEvent = new(false);

        public IReader ArpReader { get; internal set; }
        public IWriter ArpWriter { get; internal set; }

        public IEnumerable<ServiceEntry> GetServices(HorizonOptions options)
        {
            List<ServiceEntry> entries = [];

            void RegisterService<T>() where T : IService
            {
                entries.Add(new ServiceEntry(T.Main, this, options, typeof(T).Name));
            }

            RegisterService<ArpMain>();
            RegisterService<AudioMain>();
            RegisterService<BcatMain>();
            RegisterService<FriendsMain>();
            RegisterService<HshlMain>();
            RegisterService<HwopusMain>(); // TODO: Merge with audio once we can start multiple threads.
            RegisterService<InsMain>();
            RegisterService<LblMain>();
            RegisterService<LmMain>();
            RegisterService<MmNvMain>();
            RegisterService<NgcMain>();
            RegisterService<OvlnMain>();
            RegisterService<PrepoMain>();
            RegisterService<PscMain>();
            RegisterService<SrepoMain>();
            RegisterService<TsMain>();
            RegisterService<UsbMain>();
            RegisterService<WlanMain>();

            _totalServices = entries.Count;

            return entries;
        }

        internal void SignalServiceReady()
        {
            if (Interlocked.Increment(ref _readyServices) == _totalServices)
            {
                _servicesReadyEvent.Set();
            }
        }

        public void WaitServicesReady()
        {
            _servicesReadyEvent.WaitOne();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _servicesReadyEvent.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
