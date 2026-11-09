namespace SuperSwitch.Memory
{
    public interface IRefCounted
    {
        void IncrementReferenceCount();
        void DecrementReferenceCount();
    }
}
