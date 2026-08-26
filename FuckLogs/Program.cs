using System;
using System.Reflection;
using System.Windows.Forms;

namespace FuckLogs
{
    internal static class Program
    {
        public static readonly string AppVersion =
            (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "dev").Split('+')[0];

        [STAThread]
        static void Main(string[] args)
        {
            // --version / -v: quick check for which build is deployed
            if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
            {
                MessageBox.Show($"FuckLogs v{AppVersion}", "FuckLogs",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
