using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DynamicEdge
{
    public class ErrorDialog : Form
    {
        public ErrorDialog(string title, string message, string configDirectory)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);
            TopMost = true;

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };

            var lbl = new Label
            {
                Text = message,
                AutoSize = true,
                MaximumSize = new Size(500, 0)
            };

            var pathBox = new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Top,
                Width = 500,
                Text = string.IsNullOrEmpty(configDirectory) ? "Unknown config directory" : configDirectory
            };

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 8, 0, 0)
            };

            var closeBtn = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            var openBtn = new Button { Text = "Open Config Folder", AutoSize = true };

            closeBtn.Click += (s, e) => Close();
            openBtn.Click += (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(configDirectory))
                    {
                        Directory.CreateDirectory(configDirectory);
                        Process.Start("explorer.exe", configDirectory);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to open folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            buttons.Controls.Add(closeBtn);
            buttons.Controls.Add(openBtn);

            layout.Controls.Add(lbl);
            layout.Controls.Add(pathBox);
            layout.Controls.Add(buttons);

            Controls.Add(layout);
        }

        public static void ShowFatal(string message, string configDirectory)
        {
            try
            {
                using (var dlg = new ErrorDialog("DynamicEdge – Error", message, configDirectory))
                {
                    dlg.ShowDialog();
                }
            }
            catch
            {
                MessageBox.Show(message + Environment.NewLine + "Config: " + configDirectory, "DynamicEdge – Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
