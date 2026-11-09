using NUnit.Framework;
using SuperSwitch.HLE.HOS.Services.Time.TimeZone;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Time
{
    internal class TimeZoneRuleTests
    {
        class EffectInfoParameterTests
        {
            [Test]
            public void EnsureTypeSize()
            {
                Assert.AreEqual(0x4000, Unsafe.SizeOf<TimeZoneRule>());
            }
        }
    }
}
