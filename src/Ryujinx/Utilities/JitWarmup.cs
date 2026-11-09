using Ryujinx.Common.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ryujinx.Ava.Utilities
{
    /// <summary>
    /// SuperSwitch: compiles the emulator's own code ahead of time on low-priority background threads.
    /// Ryubing disables QuickJit (and ReadyToRun), so every method is JIT-compiled with full optimization
    /// the first time it runs, which shows up as stutter the first time a game reaches new emulator code.
    /// </summary>
    internal static class JitWarmup
    {
        private static readonly string[] _assemblies =
        [
            "ARMeilleure",
            "Ryujinx.Cpu",
            "Ryujinx.Memory",
            "Ryujinx.Common",
            "Ryujinx.Graphics.Gpu",
            "Ryujinx.Graphics.GAL",
            "Ryujinx.Graphics.Vulkan",
            "Ryujinx.Graphics.Shader",
            "Ryujinx.Graphics.Texture",
            "Ryujinx.Graphics.Device",
            "Ryujinx.Graphics.Nvdec",
            "Ryujinx.Audio",
            "Ryujinx.HLE",
            "Ryujinx.Horizon",
            "Ryujinx.Horizon.Common",
            "Ryujinx.Input",
        ];

        public static void Start()
        {
            if (Environment.GetEnvironmentVariable("SUPERSWITCH_JIT_WARMUP") == "0")
            {
                return;
            }

            int workers = Math.Clamp(Environment.ProcessorCount / 4, 1, 4);
            int next = -1;
            int methods = 0;
            Stopwatch sw = Stopwatch.StartNew();
            int remaining = workers;

            for (int i = 0; i < workers; i++)
            {
                Thread thread = new(() =>
                {
                    int index;

                    while ((index = Interlocked.Increment(ref next)) < _assemblies.Length)
                    {
                        Interlocked.Add(ref methods, WarmAssembly(_assemblies[index]));
                    }

                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        Logger.Info?.Print(LogClass.Application, $"JIT warm-up compiled {methods} methods in {sw.Elapsed.TotalSeconds:F1} s.", "JitWarmup");
                    }
                })
                {
                    Name = $"SuperSwitch.JitWarmup.{i}",
                    IsBackground = true,
                    Priority = ThreadPriority.Lowest,
                };

                thread.Start();
            }
        }

        private static int WarmAssembly(string name)
        {
            Assembly assembly;

            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, name + ".dll");

                if (!File.Exists(path))
                {
                    return 0;
                }

                assembly = Assembly.Load(new AssemblyName(name));
            }
            catch (Exception)
            {
                return 0;
            }

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            int count = 0;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (Type type in types)
            {
                if (type.ContainsGenericParameters || type.IsInterface)
                {
                    continue;
                }

                MethodBase[] members;

                try
                {
                    members = type.GetMethods(Flags).Cast<MethodBase>().Concat(type.GetConstructors(Flags)).ToArray();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (MethodBase method in members)
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                    {
                        continue;
                    }

                    try
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                        count++;
                    }
                    catch (Exception)
                    {
                        // Some methods can't be prepared ahead of time; they are compiled on first use as usual.
                    }
                }
            }

            return count;
        }
    }
}
