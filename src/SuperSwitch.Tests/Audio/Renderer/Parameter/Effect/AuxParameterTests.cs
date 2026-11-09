using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter.Effect
{
    class AuxParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x6C, Unsafe.SizeOf<AuxiliaryBufferParameter>());
        }
    }
}
