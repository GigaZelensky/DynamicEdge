using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicEdge
{
    public class EdgeAppContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private PhysicsWorker worker;
        private bool isEnabled = true;
        private Icon iconActive;
        private Icon iconDisabled;
        private ToolStripMenuItem startupItem;
        private AppSettings settings;

        public EdgeAppContext(AppSettings initialSettings)
        {
            settings = initialSettings ?? AppSettings.CreateDefault();

            InitializeResources();
            InitializeTrayMenu();
            InitializeWorker();

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

            menu.Items.Add(new ToolStripMenuItem("Settings...", null, OnSettingsClick));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, OnExitClick);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = iconActive;
            trayIcon.Text = "Dynamic Edge: ON";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;

            trayIcon.MouseClick += new MouseEventHandler(OnTrayIconMouseClick);
        }

        private void InitializeWorker()
        {
            worker = new PhysicsWorker(settings);
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

        private void OnSettingsClick(object sender, EventArgs e)
        {
            using (var form = new SettingsForm(settings, ApplySettings))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    ApplySettings(form.Settings);
                }
            }
        }

        private void ApplySettings(AppSettings updated)
        {
            settings = updated ?? AppSettings.CreateDefault();
            AppSettingsStore.Save(settings);
            worker.UpdateSettings(settings);
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
}
