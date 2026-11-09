using System;

namespace SuperSwitch.HLE.Exceptions
{
    public class InvalidSystemResourceException : Exception
    {
        public InvalidSystemResourceException(string message) : base(message) { }
    }
}
