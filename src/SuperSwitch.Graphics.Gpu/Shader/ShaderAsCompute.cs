using SuperSwitch.Graphics.GAL;
using SuperSwitch.Graphics.Shader;
using SuperSwitch.Graphics.Shader.Translation;

namespace SuperSwitch.Graphics.Gpu.Shader
{
    class ShaderAsCompute
    {
        public IProgram HostProgram { get; }
        public ShaderProgramInfo Info { get; }
        public ResourceReservations Reservations { get; }

        public ShaderAsCompute(IProgram hostProgram, ShaderProgramInfo info, ResourceReservations reservations)
        {
            HostProgram = hostProgram;
            Info = info;
            Reservations = reservations;
        }
    }
}
