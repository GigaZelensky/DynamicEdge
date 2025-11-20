using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Explicit alias to avoid ambiguity between System.Threading and WinForms timers
using Timer = System.Windows.Forms.Timer;

namespace DynamicEdge
{
    static class Program
    {
        // Unique Mutex ID to prevent multiple instances of the application
        private const string AppMutexId = "DynamicEdge_Global_Mutex_9A2B3C";

        [STAThread]
        static void Main()
        {
            // Single instance check using a named system mutex
            using (Mutex mutex = new Mutex(false, AppMutexId))
            {
                // If we cannot acquire the mutex immediately, another instance is running
                if (!mutex.WaitOne(0, false))
                {
                    return;
                }

                // Set High DPI awareness to ensure coordinate calculations are accurate on 4K screens
                try { NativeMethods.SetProcessDpiAwareness(2); } catch { }

                // Apply system-level optimizations
                EfficiencyMode.Apply();
                WindowsSettings.EnforceCursorSettings();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var app = new EdgeAppContext())
                {
                    Application.Run(app);
                }
            }
        }
    }

    public class EdgeAppContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private PhysicsWorker worker;
        private bool isEnabled = true;
        private Icon iconActive;
        private Icon iconDisabled;
        private ToolStripMenuItem startupItem;

        public EdgeAppContext()
        {
            InitializeResources();
            InitializeTrayMenu();
            InitializeWorker();

            // Automatically enable startup on the very first run for convenience
            if (!startupItem.Checked)
            {
                StartupManager.SetStartup(true);
                startupItem.Checked = true;
            }
        }

        private void InitializeResources()
        {
            iconActive = SystemIcons.Shield;
            iconDisabled = IconHelper.CreateGrayscaleIcon(iconActive);
        }

        private void InitializeTrayMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            
            menu.Items.Add("Toggle Edge", null, OnToggleClick);
            menu.Items.Add(new ToolStripSeparator());
            
            startupItem = new ToolStripMenuItem("Start with Windows", null, OnStartupClick);
            startupItem.Checked = StartupManager.IsStartupEnabled();
            menu.Items.Add(startupItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, OnExitClick);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = iconActive;
            trayIcon.Text = "Dynamic Edge: ON";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            
            // Handle left-click to toggle protection quickly
            trayIcon.MouseClick += new MouseEventHandler(OnTrayIconMouseClick);
        }

        private void InitializeWorker()
        {
            worker = new PhysicsWorker();
        }

        private void OnTrayIconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OnToggleClick(sender, e);
            }
        }

        private void OnToggleClick(object sender, EventArgs e)
        {
            isEnabled = !isEnabled;
            worker.SetEnabled(isEnabled);
            
            trayIcon.Icon = isEnabled ? iconActive : iconDisabled;
            trayIcon.Text = isEnabled ? "Dynamic Edge: ON" : "Dynamic Edge: OFF";
        }

        private void OnStartupClick(object sender, EventArgs e)
        {
            bool newState = !startupItem.Checked;
            StartupManager.SetStartup(newState);
            startupItem.Checked = newState;
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (trayIcon != null) trayIcon.Dispose();
                if (worker != null) worker.Dispose();
                if (iconDisabled != null) iconDisabled.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Core logic engine handles Raw Input processing and cursor physics
    public class PhysicsWorker : Form
    {
        private Timer physicsTimer;
        private bool isActive = true;
        
        // Cache screen boundaries to minimize expensive API calls during the physics loop
        private List<Rectangle> screenCache = new List<Rectangle>();
        private Rectangle currentScreen;
        
        // Track the last applied clip rect to avoid redundant User32 calls
        private NativeMethods.RECT? activeClip = null;
        
        // Physics state
        private int cooldownFrames = 0;
        private int inputVelocityX = 0;
        private int inputVelocityY = 0;
        private float membraneHealth = 100f;
        
        // Unmanaged buffer for RawInput data
        private IntPtr rawInputBuffer;
        private int rawInputBufferSize;

        // Simulation constants
        private const float MaxHealth = 100f;
        private const float RegenRate = 15f;
        private const float DamageMultiplier = 0.6f;
        private const int BreakThreshold = 70;
        
        // Polling rates (ms)
        private const int PollRateActive = 10;
        private const int PollRateIdle = 200;

        public PhysicsWorker()
        {
            // Create an invisible window to receive WM_INPUT messages
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.CreateHandle();

            InitializeRawInput();
            RefreshScreenCache();
            
            // Listen for display changes (plugging/unplugging monitors)
            SystemEvents.DisplaySettingsChanged += new EventHandler(OnDisplaySettingsChanged);

            physicsTimer = new Timer();
            physicsTimer.Interval = PollRateIdle;
            physicsTimer.Tick += OnPhysicsTick;
            
            ResetBarrier();
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            RefreshScreenCache();
        }

        private void InitializeRawInput()
        {
            // Pre-allocate buffer to avoid allocations in the hot loop
            int headerSize = Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER));
            int mouseSize = Marshal.SizeOf(typeof(NativeMethods.RAWMOUSE));
            rawInputBufferSize = headerSize + mouseSize + 16;
            rawInputBuffer = Marshal.AllocHGlobal(rawInputBufferSize);

            // Register for High-Definition Mouse Input (Raw Input)
            // RIDEV_INPUTSINK enables receiving input even when not in foreground
            NativeMethods.RAWINPUTDEVICE[] rid = new NativeMethods.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; 
            rid[0].usUsage = 0x02; 
            rid[0].dwFlags = 0x00000100; 
            rid[0].hwndTarget = this.Handle;
            
            NativeMethods.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(rid[0]));
        }

        private void RefreshScreenCache()
        {
            screenCache.Clear();
            foreach (Screen s in Screen.AllScreens)
            {
                screenCache.Add(s.Bounds);
            }
            
            Point cursorPosition;
            NativeMethods.GetCursorPos(out cursorPosition);
            currentScreen = ResolveScreen(cursorPosition);
        }

        private Rectangle ResolveScreen(Point p)
        {
            // Use System.Windows.Forms.Screen.FromPoint to correctly identify the monitor
            // even with negative coordinates or complex vertical stacking.
            return Screen.FromPoint(p).Bounds;
        }

        protected override void SetVisibleCore(bool value) 
        { 
            base.SetVisibleCore(false); 
        }

        public void SetEnabled(bool enabled)
        {
            isActive = enabled;
            if (isActive) ResetBarrier();
            else UnlockCursor();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;
            if (m.Msg == WM_INPUT)
            {
                ProcessRawInput(m.LParam);
            }
            base.WndProc(ref m);
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            uint size = 0;
            NativeMethods.GetRawInputData(lParam, 0x10000003, IntPtr.Zero, ref size, 24);

            if (size > 0 && size <= rawInputBufferSize)
            {
                if (NativeMethods.GetRawInputData(lParam, 0x10000003, rawInputBuffer, ref size, 24) == size)
                {
                    // Read the header to confirm this is mouse input
                    int type = Marshal.ReadInt32(rawInputBuffer);
                    if (type == 0) // RIM_TYPEMOUSE
                    {
                        int headerSize = IntPtr.Size == 8 ? 24 : 16;
                        
                        // Offset 12 = lLastX, Offset 16 = lLastY
                        int offsetToX = headerSize + 12;
                        int offsetToY = headerSize + 16;
                        
                        int relativeX = Marshal.ReadInt32(rawInputBuffer, offsetToX);
                        int relativeY = Marshal.ReadInt32(rawInputBuffer, offsetToY);

                        inputVelocityX += relativeX;
                        inputVelocityY += relativeY;
                    }
                }
            }
        }

        private void ResetBarrier()
        {
            if (!isActive) return;
            UnlockCursor();
            cooldownFrames = 0;
            
            // Check cache count just to know if we have multi-monitor setup
            if (screenCache.Count > 1)
            {
                Point p;
                NativeMethods.GetCursorPos(out p);
                currentScreen = ResolveScreen(p);
                physicsTimer.Start();
            }
            else
            {
                physicsTimer.Stop();
            }
        }

        // Main Physics Loop
        private void OnPhysicsTick(object sender, EventArgs e)
        {
            if (!isActive) return;

            // 1. Capture and reset velocity accumulator
            int velX = inputVelocityX;
            int velY = inputVelocityY;
            inputVelocityX = 0;
            inputVelocityY = 0;
            
            Point position;
            NativeMethods.GetCursorPos(out position);

            // 2. Detect screen transitions
            if (!currentScreen.Contains(position))
            {
                Rectangle realScreen = ResolveScreen(position);
                
                // If we actually moved to a new monitor, reset logic
                if (realScreen != currentScreen)
                {
                    currentScreen = realScreen;
                    
                    // If not in cooldown, immediately re-engage barrier
                    if (cooldownFrames <= 0)
                    {
                        EngageBarrier(currentScreen);
                        membraneHealth = MaxHealth;
                    }
                }
            }

            // 3. Handle cooldown state (post-breakthrough)
            if (cooldownFrames > 0)
            {
                cooldownFrames--;
                if (cooldownFrames == 0) EngageBarrier(currentScreen);
                return;
            }

            // 4. Adaptive Polling
            int distX = Math.Min(Math.Abs(position.X - currentScreen.Left), Math.Abs(currentScreen.Right - position.X));
            int distY = Math.Min(Math.Abs(position.Y - currentScreen.Top), Math.Abs(currentScreen.Bottom - position.Y));
            int minDist = Math.Min(distX, distY);

            if (minDist > 50)
            {
                if (physicsTimer.Interval != PollRateIdle) physicsTimer.Interval = PollRateIdle;
                
                // Passively regenerate health when safe
                if (membraneHealth < MaxHealth) membraneHealth = MaxHealth;
                
                EngageBarrier(currentScreen);
                return;
            }
            else
            {
                if (physicsTimer.Interval != PollRateActive) physicsTimer.Interval = PollRateActive;
            }

            // 5. Physics Simulation
            EngageBarrier(currentScreen);

            // Check edge proximity (2px threshold)
            bool atRight = position.X >= currentScreen.Right - 2;
            bool atLeft = position.X <= currentScreen.Left + 2;
            bool atBottom = position.Y >= currentScreen.Bottom - 2;
            bool atTop = position.Y <= currentScreen.Top + 2;

            // Determine if pushing OUT against a specific edge
            float force = 0;
            bool pushingOut = false;

            if (atRight && velX > 0) { force = Math.Abs(velX); pushingOut = true; }
            else if (atLeft && velX < 0) { force = Math.Abs(velX); pushingOut = true; }
            else if (atBottom && velY > 0) { force = Math.Abs(velY); pushingOut = true; }
            else if (atTop && velY < 0) { force = Math.Abs(velY); pushingOut = true; }

            if (pushingOut)
            {
                // Ignore micro-movements
                if (force > 2)
                {
                    // Instant break threshold (Fast flick)
                    if (force > BreakThreshold)
                    {
                        BreakBarrier();
                        return;
                    }

                    // Standard damage calculation
                    membraneHealth -= (force * DamageMultiplier);
                    if (membraneHealth <= 0)
                    {
                        BreakBarrier();
                        return;
                    }
                }
            }
            else
            {
                // Rapid regeneration when pressure is released
                if (membraneHealth < MaxHealth)
                {
                    membraneHealth += RegenRate;
                    if (membraneHealth > MaxHealth) membraneHealth = MaxHealth;
                }
            }
        }

        private void BreakBarrier()
        {
            UnlockCursor();
            cooldownFrames = 25; // ~250ms cooldown before barrier reforms
            membraneHealth = MaxHealth;
            inputVelocityX = 0;
            inputVelocityY = 0;
        }

        private void EngageBarrier(Rectangle bounds)
        {
            // 1. Get the entire virtual desktop area (handles negative coords/stacked monitors)
            Rectangle vs = SystemInformation.VirtualScreen;

            // 2. Clamp the requested bounds to the valid virtual screen area
            // This prevents passing invalid rects to ClipCursor which would result in no resistance
            int left = Math.Max(bounds.Left, vs.Left);
            int right = Math.Min(bounds.Right, vs.Right);
            int top = Math.Max(bounds.Top, vs.Top);
            int bottom = Math.Min(bounds.Bottom, vs.Bottom);

            // 3. If clamping resulted in an invalid/degenerate rect, abort
            if (left >= right || top >= bottom)
            {
                UnlockCursor();
                return;
            }

            NativeMethods.RECT r;
            r.Left = left;
            r.Right = right;
            r.Top = top; 
            r.Bottom = bottom;

            // Optimization: Only call ClipCursor if the bounds have changed
            if (activeClip.HasValue && 
                activeClip.Value.Left == r.Left && 
                activeClip.Value.Right == r.Right &&
                activeClip.Value.Top == r.Top &&
                activeClip.Value.Bottom == r.Bottom)
            {
                return;
            }

            if (NativeMethods.ClipCursor(ref r))
            {
                activeClip = r;
            }
        }

        private void UnlockCursor()
        {
            NativeMethods.ClipCursor(IntPtr.Zero);
            activeClip = null;
        }

        public new void Dispose()
        {
            isActive = false;
            UnlockCursor();
            if (rawInputBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rawInputBuffer);
                rawInputBuffer = IntPtr.Zero;
            }
            base.Dispose();
        }
    }

    // --- UTILITY CLASSES ---

    public static class WindowsSettings
    {
        public static void EnforceCursorSettings()
        {
            try
            {
                // 1. Disable Windows 11 "Ease cursor movement between displays"
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", true))
                {
                    if (key != null) key.SetValue("CursorDeadzoneJumpingSetting", 0, RegistryValueKind.DWord);
                }

                // 2. Disable Windows 10 "Sticky Edges" speed check
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null) key.SetValue("MouseMonitorEscapeSpeed", 1, RegistryValueKind.DWord);
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ImmersiveShell\EdgeUI", true))
                {
                    if (key != null) key.SetValue("MouseMonitorEscapeSpeed", 1, RegistryValueKind.DWord);
                }

                // 3. Refresh System Parameters (SPI_SETCURSORS)
                NativeMethods.SystemParametersInfo(0x0057, 0, IntPtr.Zero, 0x01 | 0x02);

                // 4. Broadcast setting change to top-level windows
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
            catch { }
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
                
                var throttling = new NativeMethods.PROCESS_POWER_THROTTLING_STATE();
                throttling.Version = 1;
                throttling.ControlMask = 1;
                throttling.StateMask = 1;

                int size = Marshal.SizeOf(typeof(NativeMethods.PROCESS_POWER_THROTTLING_STATE));
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(throttling, ptr, false);
                    NativeMethods.SetProcessInformation(
                        Process.GetCurrentProcess().Handle, 
                        4, // ProcessPowerThrottling
                        ptr, 
                        (uint)size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch { }
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
            catch { return false; }
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
            catch { }
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

    internal static class NativeMethods
    {
        // --- User32 ---
        [DllImport("user32.dll", SetLastError = true)] 
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")] 
        public static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll")] 
        public static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll")] 
        public static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")] 
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")] 
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)] 
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] 
        public static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam);

        // --- Kernel32 ---
        [DllImport("kernel32.dll", SetLastError = true)] 
        public static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, IntPtr ProcessInformation, uint ProcessInformationSize);

        // --- Shcore ---
        [DllImport("shcore.dll")] 
        public static extern int SetProcessDpiAwareness(int value);

        // --- Structures ---
        [StructLayout(LayoutKind.Sequential)] 
        public struct RECT 
        { 
            public int Left, Top, Right, Bottom; 
        }

        [StructLayout(LayoutKind.Sequential)] 
        public struct RAWINPUTDEVICE 
        { 
            public ushort usUsagePage; 
            public ushort usUsage; 
            public uint dwFlags; 
            public IntPtr hwndTarget; 
        }

        [StructLayout(LayoutKind.Explicit)] 
        public struct RAWINPUTHEADER 
        { 
            [FieldOffset(0)] public uint dwType; 
            [FieldOffset(4)] public uint dwSize; 
            [FieldOffset(8)] public IntPtr hDevice; 
            [FieldOffset(16)] public IntPtr wParam; 
        }

        [StructLayout(LayoutKind.Explicit)] 
        public struct RAWMOUSE 
        { 
            [FieldOffset(0)] public ushort usFlags; 
            [FieldOffset(4)] public uint ulButtons; 
            [FieldOffset(12)] public int lLastX; 
            [FieldOffset(16)] public int lLastY; 
        }

        [StructLayout(LayoutKind.Sequential)] 
        public struct PROCESS_POWER_THROTTLING_STATE 
        { 
            public uint Version; 
            public uint ControlMask; 
            public uint StateMask; 
        }
    }
}