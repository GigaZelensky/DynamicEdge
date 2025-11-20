using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

// Explicit alias to avoid ambiguity between System.Threading and WinForms timers
using Timer = System.Windows.Forms.Timer;

namespace DynamicEdge
{
    // Core logic engine handles Raw Input processing and cursor physics
    public class PhysicsWorker : Form
    {
        private Timer physicsTimer;
        private bool isActive = true;

        private readonly List<Rectangle> screenCache = new List<Rectangle>();
        private Rectangle currentScreen;
        private Rectangle virtualScreenBounds;

        private NativeMethods.RECT? activeClip = null;

        private int cooldownFrames = 0;
        private int inputVelocityX = 0;
        private int inputVelocityY = 0;
        private float membraneHealth;
        private int lastInputTick;
        private const int IdleStopGraceMs = 3000;

        private IntPtr rawInputBuffer;
        private int rawInputBufferSize;

        private AppSettings settings;

        private bool clipFailureLogged;
        private bool screenResolveFailureLogged;
        private bool physicsErrorLogged;

        public PhysicsWorker(AppSettings settings)
        {
            this.settings = PrepareSettings(settings);
            membraneHealth = this.settings.MaxHealth;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            CreateHandle();

            InitializeRawInput();
            RefreshScreenCache();

            SystemEvents.DisplaySettingsChanged += new EventHandler(OnDisplaySettingsChanged);

            physicsTimer = new Timer();
            physicsTimer.Interval = this.settings.PollRateIdle;
            physicsTimer.Tick += OnPhysicsTick;

            lastInputTick = Environment.TickCount;
            ResetBarrier();
        }

        public void UpdateSettings(AppSettings updated)
        {
            settings = PrepareSettings(updated);
            membraneHealth = settings.MaxHealth;
            cooldownFrames = 0;
            activeClip = null;
            physicsTimer.Interval = settings.PollRateIdle;
            lastInputTick = Environment.TickCount;
            ResetBarrier();
        }

        private AppSettings PrepareSettings(AppSettings incoming)
        {
            AppSettings result = incoming != null ? incoming.Clone() : AppSettings.CreateDefault();
            result.Clamp();
            return result;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            try
            {
                RefreshScreenCache();
                ResetBarrier();
            }
            catch (Exception ex)
            {
                Logger.LogError("Display change handling failed", ex);
            }
        }

        private void InitializeRawInput()
        {
            int headerSize = Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER));
            int mouseSize = Marshal.SizeOf(typeof(NativeMethods.RAWMOUSE));
            rawInputBufferSize = headerSize + mouseSize + 16;
            rawInputBuffer = Marshal.AllocHGlobal(rawInputBufferSize);

            NativeMethods.RAWINPUTDEVICE[] rid = new NativeMethods.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02;
            rid[0].dwFlags = 0x00000100;
            rid[0].hwndTarget = Handle;

            NativeMethods.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(rid[0]));
        }

        private void RefreshScreenCache()
        {
            screenCache.Clear();
            foreach (Screen s in Screen.AllScreens)
            {
                screenCache.Add(s.Bounds);
            }
            virtualScreenBounds = SystemInformation.VirtualScreen;

            Point cursorPosition;
            NativeMethods.GetCursorPos(out cursorPosition);
            currentScreen = ResolveScreen(cursorPosition);

            try
            {
                List<string> displayInfo = new List<string>();
                for (int i = 0; i < screenCache.Count; i++)
                {
                    Rectangle r = screenCache[i];
                    displayInfo.Add(string.Format("[{0}] {1},{2} {3}x{4}", i, r.Left, r.Top, r.Width, r.Height));
                }
                Logger.Log(string.Format("Detected {0} screen(s): {1}", screenCache.Count, string.Join(" | ", displayInfo.ToArray())));
            }
            catch
            {
                // Logging should not interfere with runtime
            }
        }

        private Rectangle ResolveScreen(Point p)
        {
            try
            {
                return Screen.FromPoint(p).Bounds;
            }
            catch (Exception ex)
            {
                if (!screenResolveFailureLogged)
                {
                    screenResolveFailureLogged = true;
                    Logger.LogError(string.Format("Failed to resolve screen for point {0}", p), ex);
                }

                return Screen.PrimaryScreen != null ? Screen.PrimaryScreen.Bounds : new Rectangle(0, 0, 1, 1);
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        public void SetEnabled(bool enabled)
        {
            isActive = enabled;
            if (isActive) ResetBarrier();
            else
            {
                physicsTimer.Stop();
                UnlockCursor();
            }
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
            if (!isActive || screenCache.Count <= 1)
            {
                inputVelocityX = 0;
                inputVelocityY = 0;
                return;
            }

            uint size = 0;
            NativeMethods.GetRawInputData(lParam, 0x10000003, IntPtr.Zero, ref size, 24);

            if (size > 0 && size <= rawInputBufferSize)
            {
                if (NativeMethods.GetRawInputData(lParam, 0x10000003, rawInputBuffer, ref size, 24) == size)
                {
                    int type = Marshal.ReadInt32(rawInputBuffer);
                    if (type == 0)
                    {
                        int headerSize = IntPtr.Size == 8 ? 24 : 16;
                        int offsetToX = headerSize + 12;
                        int offsetToY = headerSize + 16;

                        int relativeX = Marshal.ReadInt32(rawInputBuffer, offsetToX);
                        int relativeY = Marshal.ReadInt32(rawInputBuffer, offsetToY);

                        inputVelocityX += relativeX;
                        inputVelocityY += relativeY;
                        lastInputTick = Environment.TickCount;

                        if (!physicsTimer.Enabled)
                        {
                            physicsTimer.Interval = settings.PollRateActive;
                            physicsTimer.Start();
                        }
                    }
                }
            }
        }

        private void ResetBarrier()
        {
            if (!isActive) return;
            UnlockCursor();
            cooldownFrames = 0;

            if (screenCache.Count > 1)
            {
                Point p;
                NativeMethods.GetCursorPos(out p);
                currentScreen = ResolveScreen(p);
                lastInputTick = Environment.TickCount;
                physicsTimer.Start();
            }
            else
            {
                physicsTimer.Stop();
            }
        }

        private void OnPhysicsTick(object sender, EventArgs e)
        {
            if (!isActive) return;

            try
            {
                if (screenCache.Count <= 1)
                {
                    physicsTimer.Stop();
                    return;
                }

                int velX = inputVelocityX;
                int velY = inputVelocityY;
                inputVelocityX = 0;
                inputVelocityY = 0;

                Point position;
                NativeMethods.GetCursorPos(out position);

                if (!currentScreen.Contains(position))
                {
                    Rectangle realScreen = ResolveScreen(position);

                    if (realScreen != currentScreen)
                    {
                        currentScreen = realScreen;

                        if (cooldownFrames <= 0)
                        {
                            EngageBarrier(currentScreen);
                            membraneHealth = settings.MaxHealth;
                        }
                    }
                }

                if (cooldownFrames > 0)
                {
                    cooldownFrames--;
                    if (cooldownFrames == 0) EngageBarrier(currentScreen);
                    return;
                }

                int distX = Math.Min(Math.Abs(position.X - currentScreen.Left), Math.Abs(currentScreen.Right - position.X));
                int distY = Math.Min(Math.Abs(position.Y - currentScreen.Top), Math.Abs(currentScreen.Bottom - position.Y));
                int minDist = Math.Min(distX, distY);

                if (minDist > settings.IdleResetDistance)
                {
                    if (physicsTimer.Interval != settings.PollRateIdle) physicsTimer.Interval = settings.PollRateIdle;

                    if (membraneHealth < settings.MaxHealth) membraneHealth = settings.MaxHealth;

                    EngageBarrier(currentScreen);
                    MaybeStopForIdle(velX, velY, minDist);
                    return;
                }
                else
                {
                    if (physicsTimer.Interval != settings.PollRateActive) physicsTimer.Interval = settings.PollRateActive;
                }

                EngageBarrier(currentScreen);

                bool atRight = position.X >= currentScreen.Right - settings.EdgeProximityPx;
                bool atLeft = position.X <= currentScreen.Left + settings.EdgeProximityPx;
                bool atBottom = position.Y >= currentScreen.Bottom - settings.EdgeProximityPx;
                bool atTop = position.Y <= currentScreen.Top + settings.EdgeProximityPx;

                float force = 0;
                bool pushingOut = false;

                if (atRight && velX > 0) { force = Math.Abs(velX); pushingOut = true; }
                else if (atLeft && velX < 0) { force = Math.Abs(velX); pushingOut = true; }
                else if (atBottom && velY > 0) { force = Math.Abs(velY); pushingOut = true; }
                else if (atTop && velY < 0) { force = Math.Abs(velY); pushingOut = true; }

                if (pushingOut)
                {
                    if (force > 2)
                    {
                        float speed = (float)Math.Sqrt((double)(velX * velX + velY * velY));
                        float easeFactor = 1f + (speed / 50f) * settings.SpeedEaseMultiplier;
                        if (easeFactor > 10f) easeFactor = 10f;
                        force *= easeFactor;

                        if (force > settings.BreakThreshold)
                        {
                            BreakBarrier();
                            return;
                        }

                        membraneHealth -= (force * settings.DamageMultiplier);
                        if (membraneHealth <= 0)
                        {
                            BreakBarrier();
                            return;
                        }
                    }
                }
                else
                {
                    if (membraneHealth < settings.MaxHealth)
                    {
                        membraneHealth += settings.RegenRate;
                        if (membraneHealth > settings.MaxHealth) membraneHealth = settings.MaxHealth;
                    }
                }

                MaybeStopForIdle(velX, velY, minDist);
            }
            catch (Exception ex)
            {
                if (!physicsErrorLogged)
                {
                    physicsErrorLogged = true;
                    Logger.LogError("Physics loop error", ex);
                }
            }
        }

        private void BreakBarrier()
        {
            UnlockCursor();
            cooldownFrames = settings.CooldownFrames;
            membraneHealth = settings.MaxHealth;
            inputVelocityX = 0;
            inputVelocityY = 0;
        }

        private void EngageBarrier(Rectangle bounds)
        {
            Rectangle vs = virtualScreenBounds;
            if (vs.Width == 0 || vs.Height == 0)
            {
                vs = SystemInformation.VirtualScreen;
            }

            int left = Math.Max(bounds.Left, vs.Left);
            int right = Math.Min(bounds.Right, vs.Right);
            int top = Math.Max(bounds.Top, vs.Top);
            int bottom = Math.Min(bounds.Bottom, vs.Bottom);

            if (left >= right || top >= bottom)
            {
                UnlockCursor();
                if (!clipFailureLogged)
                {
                    clipFailureLogged = true;
                    Logger.LogError("Skipping ClipCursor because bounds collapsed", null);
                }
                return;
            }

            NativeMethods.RECT r;
            r.Left = left;
            r.Right = right;
            r.Top = top;
            r.Bottom = bottom;

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
                clipFailureLogged = false;
            }
            else if (!clipFailureLogged)
            {
                clipFailureLogged = true;
                Logger.LogError(string.Format("ClipCursor failed for rect {0},{1},{2},{3}", r.Left, r.Top, r.Right, r.Bottom), null);
            }
        }

        private void UnlockCursor()
        {
            NativeMethods.ClipCursor(IntPtr.Zero);
            activeClip = null;
        }

        private void MaybeStopForIdle(int velX, int velY, int minDist)
        {
            if (cooldownFrames > 0 || !physicsTimer.Enabled) return;

            int elapsed = unchecked(Environment.TickCount - lastInputTick);
            int idleThreshold = Math.Max(settings.PollRateIdle * 3, IdleStopGraceMs);
            if (elapsed < idleThreshold) return;
            if (velX != 0 || velY != 0) return;
            if (minDist <= settings.IdleResetDistance) return;

            physicsTimer.Stop();
        }

        public new void Dispose()
        {
            isActive = false;
            UnlockCursor();
            SystemEvents.DisplaySettingsChanged -= new EventHandler(OnDisplaySettingsChanged);
            if (rawInputBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rawInputBuffer);
                rawInputBuffer = IntPtr.Zero;
            }
            base.Dispose();
        }
    }
}
