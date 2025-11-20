using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DynamicEdge
{
    public static class Logger
    {
        private static readonly object Sync = new object();
        private static string logPath;

        public static void Initialize()
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DynamicEdge",
                    "logs");
                Directory.CreateDirectory(directory);
                logPath = Path.Combine(directory, "dynamicedge.log");
                WriteLine("Logger initialized");
            }
            catch
            {
                logPath = null;
            }
        }

        public static void Log(string message)
        {
            WriteLine(message);
        }

        public static void LogError(string message, Exception ex = null)
        {
            string content = ex == null ? message : string.Format("{0}: {1}", message, ex);
            WriteLine(content);
        }

        private static void WriteLine(string message)
        {
            if (string.IsNullOrEmpty(logPath) || string.IsNullOrEmpty(message)) return;
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(logPath, string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} {1}{2}", DateTime.Now, message, Environment.NewLine));
                }
            }
            catch
            {
                // Logging failures should never break the app
            }
        }
    }

    public static class WindowsSettings
    {
        public static void EnforceCursorSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", true))
                {
                    if (key != null) key.SetValue("CursorDeadzoneJumpingSetting", 0, RegistryValueKind.DWord);
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null) key.SetValue("MouseMonitorEscapeSpeed", 1, RegistryValueKind.DWord);
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ImmersiveShell\EdgeUI", true))
                {
                    if (key != null) key.SetValue("MouseMonitorEscapeSpeed", 1, RegistryValueKind.DWord);
                }

                NativeMethods.SystemParametersInfo(0x0057, 0, IntPtr.Zero, 0x01 | 0x02);

                IntPtr strPtr = Marshal.StringToHGlobalUni(@"Control Panel\Cursors");
                try
                {
                    NativeMethods.SendNotifyMessage((IntPtr)0xFFFF, 0x001A, UIntPtr.Zero, strPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(strPtr);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to enforce cursor settings", ex);
            }
        }
    }

    public static class EfficiencyMode
    {
        public static void Apply()
        {
            try
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    p.PriorityClass = ProcessPriorityClass.Idle;
                }

                var throttling = new NativeMethods.PROCESS_POWER_THROTTLING_STATE
                {
                    Version = 1,
                    ControlMask = 1,
                    StateMask = 1
                };

                int size = Marshal.SizeOf(typeof(NativeMethods.PROCESS_POWER_THROTTLING_STATE));
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(throttling, ptr, false);
                    NativeMethods.SetProcessInformation(
                        Process.GetCurrentProcess().Handle,
                        4,
                        ptr,
                        (uint)size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply efficiency mode", ex);
            }
        }
    }

    public static class StartupManager
    {
        private const string AppName = "DynamicEdge";
        private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath, false))
                {
                    return key != null && key.GetValue(AppName) != null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read startup state", ex);
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                            key.SetValue(AppName, Application.ExecutablePath);
                        else
                            key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set startup state", ex);
            }
        }
    }

    public static class IconHelper
    {
        public static Icon CreateGrayscaleIcon(Icon original)
        {
            using (Bitmap bmp = original.ToBitmap())
            using (Bitmap grayBmp = new Bitmap(bmp.Width, bmp.Height))
            {
                using (Graphics g = Graphics.FromImage(grayBmp))
                {
                    ColorMatrix colorMatrix = new ColorMatrix(
                        new float[][] {
                            new float[] {.3f, .3f, .3f, 0, 0},
                            new float[] {.59f, .59f, .59f, 0, 0},
                            new float[] {.11f, .11f, .11f, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {0, 0, 0, 0, 1}
                        });
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(colorMatrix);
                        g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                            0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
                    }
                }

                IntPtr hIcon = grayBmp.GetHicon();
                try
                {
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        return (Icon)tempIcon.Clone();
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(hIcon);
                }
            }
        }
    }
}
