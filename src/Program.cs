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
            try
            {
                using (Mutex mutex = new Mutex(false, AppMutexId))
                {
                    if (!mutex.WaitOne(0, false))
                    {
                        return;
                    }

                    Logger.Initialize();
                    Application.ThreadException += (s, e) =>
                    {
                        Logger.LogError("Unhandled UI thread exception", e != null ? e.Exception : null);
                        if (e != null) ErrorDialog.ShowFatal("A UI error occurred:\n" + e.Exception, AppSettingsStore.ConfigDirectory);
                    };
                    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    {
                        Logger.LogError("Unhandled domain exception", e.ExceptionObject as Exception);
                        ErrorDialog.ShowFatal("A fatal error occurred:\n" + e.ExceptionObject, AppSettingsStore.ConfigDirectory);
                    };

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
            catch (Exception ex)
            {
                Logger.LogError("Startup failure", ex);
                ErrorDialog.ShowFatal(
                    "DynamicEdge failed to start:\n" + ex.Message + "\n\nConfig folder: " + AppSettingsStore.ConfigDirectory,
                    AppSettingsStore.ConfigDirectory);
            }
        }
    }
}
