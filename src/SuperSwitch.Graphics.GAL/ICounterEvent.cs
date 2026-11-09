using System;

namespace SuperSwitch.Graphics.GAL
{
    public interface ICounterEvent : IDisposable
    {
        bool Invalid { get; set; }

        bool ReserveForHostAccess();

        void Flush();
    }
}
