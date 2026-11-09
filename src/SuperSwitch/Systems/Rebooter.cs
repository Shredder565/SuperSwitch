using FluentAvalonia.UI.Controls;
using SuperSwitch.Ava.Common.Locale;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SuperSwitch.Ava.Systems
{
    internal static class Rebooter
    {

        private static readonly string _updateDir = Path.Combine(Path.GetTempPath(), "SuperSwitch", "update");

        public static void RebootAppWithGame(string gamePath, List<string> args)
        {
            _ = Reboot(gamePath, args);

        }

        private static async Task Reboot(string gamePath, List<string> args)
        {

            bool shouldRestart = true;

            FATaskDialog taskDialog = new()
            {
                Header = LocaleManager.Instance[LocaleKeys.SuperSwitchRebooter],
                SubHeader = LocaleManager.Instance[LocaleKeys.DialogRebooterMessage],
                IconSource = new FASymbolIconSource { Symbol = FASymbol.Games },
                XamlRoot = SuperSwitchApp.MainWindow,
            };

            if (shouldRestart)
            {
                string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;

                _ = taskDialog.ShowAsync(true);
                await Task.Delay(500);

                // Find the process name.
                string ryuName = Path.GetFileName(Environment.ProcessPath) ?? string.Empty;

                // Fallback if the executable could not be found.
                if (ryuName.Length == 0 || !Path.Exists(Path.Combine(executableDirectory, ryuName)))
                {
                    ryuName = OperatingSystem.IsWindows() ? "SuperSwitch.exe" : "SuperSwitch";
                }

                ProcessStartInfo processStart = new(ryuName)
                {
                    UseShellExecute = true,
                    WorkingDirectory = executableDirectory,
                };

                foreach (string arg in args)
                {
                    processStart.ArgumentList.Add(arg);
                }

                processStart.ArgumentList.Add(gamePath);

                Process.Start(processStart);

                Environment.Exit(0);
            }
        }
    }
}
