using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SuperSwitch.Graphics.Vulkan
{
    /// <summary>
    /// SuperSwitch: a small set of low-priority threads that compile graphics pipelines in the background.
    /// </summary>
    /// <remarks>
    /// Background compiles used to run on the shared thread pool, which lets dozens run at once at normal priority.
    /// When a new area needs many pipelines at the same moment, that took the processor away from the game itself
    /// and the frame rate dropped until the burst was over. A few below-normal threads compile just as much in the
    /// end, but the game's own threads always go first.
    /// </remarks>
    internal static class PipelineCompileWorkers
    {
        private static readonly BlockingCollection<Action> _work = new();
        private static int _started;

        /// <summary>
        /// Number of compile threads: a quarter of the logical processors, between 2 and 6.
        /// </summary>
        public static int WorkerCount { get; } = Math.Clamp(Environment.ProcessorCount / 4, 2, 6);

        /// <summary>
        /// Queues work for the compile threads. The returned task completes with its result.
        /// </summary>
        public static Task<T> Run<T>(Func<T> work)
        {
            EnsureStarted();

            TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            _work.Add(() =>
            {
                try
                {
                    completion.SetResult(work());
                }
                catch (Exception e)
                {
                    completion.SetException(e);
                }
            });

            return completion.Task;
        }

        private static void EnsureStarted()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                return;
            }

            for (int i = 0; i < WorkerCount; i++)
            {
                Thread thread = new(WorkerLoop)
                {
                    Name = $"SuperSwitch.PipelineCompile.{i}",
                    IsBackground = true,
                    Priority = ThreadPriority.BelowNormal,
                };

                thread.Start();
            }
        }

        private static void WorkerLoop()
        {
            foreach (Action work in _work.GetConsumingEnumerable())
            {
                work();
            }
        }
    }
}
