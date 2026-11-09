using System;

namespace SuperSwitch.HLE.Exceptions
{
    public class TamperExecutionException : Exception
    {
        public TamperExecutionException(string message) : base(message) { }
    }
}
