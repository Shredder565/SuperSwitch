using System;

namespace SuperSwitch.Horizon.Sdk.Sf
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class CmifCommandAttribute : Attribute
    {
        public uint CommandId { get; }

        public CmifCommandAttribute(uint commandId)
        {
            CommandId = commandId;
        }
    }
}
