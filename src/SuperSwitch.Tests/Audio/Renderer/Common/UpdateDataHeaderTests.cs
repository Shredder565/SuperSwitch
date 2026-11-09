using NUnit.Framework;
using SuperSwitch.Audio.Renderer.Common;
using System.Runtime.CompilerServices;

namespace SuperSwitch.Tests.Audio.Renderer.Common
{
    class UpdateDataHeaderTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.AreEqual(0x40, Unsafe.SizeOf<UpdateDataHeader>());
        }
    }
}
