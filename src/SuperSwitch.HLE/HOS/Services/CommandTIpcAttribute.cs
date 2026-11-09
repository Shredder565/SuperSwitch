using System;

namespace SuperSwitch.HLE.HOS.Services
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class CommandTipcAttribute : Attribute
    {
        public readonly int Id;

        public CommandTipcAttribute(int id) => Id = id;
    }
}
