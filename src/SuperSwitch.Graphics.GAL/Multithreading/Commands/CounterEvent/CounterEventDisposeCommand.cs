using SuperSwitch.Graphics.GAL.Multithreading.Model;
using SuperSwitch.Graphics.GAL.Multithreading.Resources;

namespace SuperSwitch.Graphics.GAL.Multithreading.Commands.CounterEvent
{
    struct CounterEventDisposeCommand : IGALCommand, IGALCommand<CounterEventDisposeCommand>
    {
        public readonly CommandType CommandType => CommandType.CounterEventDispose;
        private TableRef<ThreadedCounterEvent> _event;

        public void Set(TableRef<ThreadedCounterEvent> evt)
        {
            _event = evt;
        }

        public static void Run(ref CounterEventDisposeCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            command._event.Get(threaded).Base.Dispose();
        }
    }
}
