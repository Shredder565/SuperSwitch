using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter.Effect
{
    class BiquadFilterEffectParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x18, Unsafe.SizeOf<BiquadFilterEffectParameter1>());
            Assert.AreEqual(0x28, Unsafe.SizeOf<BiquadFilterEffectParameter2>());
        }
    }
}
