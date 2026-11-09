using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter.Effect
{
    class Reverb3dParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x49, Unsafe.SizeOf<Reverb3dParameter>());
        }
    }
}
