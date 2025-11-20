using System;
using System.Threading;
using System.Windows.Forms;

namespace DynamicEdge
{
    static class Program
    {
        private const string AppMutexId = "DynamicEdge_Global_Mutex_9A2B3C";

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(false, AppMutexId))
            {
                if (!mutex.WaitOne(0, false))
                {
                    return;
                }

                Logger.Initialize();
                Application.ThreadException += (s, e) => Logger.LogError("Unhandled UI thread exception", e != null ? e.Exception : null);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.LogError("Unhandled domain exception", e.ExceptionObject as Exception);

                try { NativeMethods.SetProcessDpiAwareness(2); } catch (Exception ex) { Logger.LogError("Failed to set DPI awareness", ex); }

                EfficiencyMode.Apply();
                WindowsSettings.EnforceCursorSettings();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppSettings settings = AppSettingsStore.LoadOrDefault();

                using (var app = new EdgeAppContext(settings))
                {
                    Application.Run(app);
                }
            }
        }
    }
}
