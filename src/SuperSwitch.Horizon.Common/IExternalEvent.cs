namespace SuperSwitch.Horizon.Common
{
    public interface IExternalEvent
    {
        void Signal();
        void Clear();
    }
}
