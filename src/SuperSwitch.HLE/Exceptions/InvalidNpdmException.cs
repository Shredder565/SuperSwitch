using System;

namespace SuperSwitch.HLE.Exceptions
{
    public class InvalidNpdmException : Exception
    {
        public InvalidNpdmException(string message) : base(message) { }
    }
}
