using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter.Effect
{
    class ReverbParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x41, Unsafe.SizeOf<ReverbParameter>());
        }
    }
}
