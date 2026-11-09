using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Parameter
{
    class BehaviourErrorInfoOutStatusTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0xB0, Unsafe.SizeOf<BehaviourErrorInfoOutStatus>());
        }
    }
}
