using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter
{
    class MixInParameterDirtyOnlyUpdateTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x20, Unsafe.SizeOf<MixInParameterDirtyOnlyUpdate>());
        }
    }
}
