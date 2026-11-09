using SuperSwitch.Graphics.GAL.Multithreading.Model;
using SuperSwitch.Graphics.GAL.Multithreading.Resources;

namespace SuperSwitch.Graphics.GAL.Multithreading.Commands
{
    struct SetProgramCommand : IGALCommand, IGALCommand<SetProgramCommand>
    {
        public readonly CommandType CommandType => CommandType.SetProgram;
        private TableRef<IProgram> _program;

        public void Set(TableRef<IProgram> program)
        {
            _program = program;
        }

        public static void Run(ref SetProgramCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedProgram program = command._program.GetAs<ThreadedProgram>(threaded);

            threaded.Programs.WaitForProgram(program);

            renderer.Pipeline.SetProgram(program.Base);
        }
    }
}
