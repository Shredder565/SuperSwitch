using System;

namespace SuperSwitch.HLE.Exceptions
{
    class InvalidFirmwarePackageException : Exception
    {
        public InvalidFirmwarePackageException(string message) : base(message) { }
    }
}
