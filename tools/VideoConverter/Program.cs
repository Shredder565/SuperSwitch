using System;
using System.Windows.Forms;

namespace SuperSwitch.VideoConverter
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = new();
            form.AddFiles(args);

            Application.Run(form);
        }
    }
}
