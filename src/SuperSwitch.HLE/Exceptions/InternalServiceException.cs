using System;

namespace SuperSwitch.HLE.Exceptions
{
    class InternalServiceException : Exception
    {
        public InternalServiceException(string message) : base(message) { }
    }
}
