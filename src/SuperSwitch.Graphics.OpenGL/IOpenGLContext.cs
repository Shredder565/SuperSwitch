using System;

namespace SuperSwitch.Graphics.OpenGL
{
    public interface IOpenGLContext : IDisposable
    {
        void MakeCurrent();

        bool HasContext();
    }
}
