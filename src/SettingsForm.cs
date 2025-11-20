using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicEdge
{
    public class SettingsForm : Form
    {
        public AppSettings Settings { get; private set; }

        private readonly Action<AppSettings> applyCallback;

        private NumericUpDown maxHealthInput;
        private NumericUpDown regenInput;
        private NumericUpDown damageInput;
        private NumericUpDown breakThresholdInput;
        private NumericUpDown cooldownInput;
        private NumericUpDown pollActiveInput;
        private NumericUpDown pollIdleInput;
        private NumericUpDown edgeProximityInput;
        private NumericUpDown idleResetInput;
        private NumericUpDown speedEaseInput;

        public SettingsForm(AppSettings current, Action<AppSettings> applyCallback)
        {
            this.applyCallback = applyCallback;

            Settings = current != null ? current.Clone() : AppSettings.CreateDefault();
            Settings.Clamp();

            Text = "DynamicEdge Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(380, 0);

            BuildLayout();
            PopulateValues();
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(10)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            maxHealthInput = CreateNumeric(1, 1000, 1);
            regenInput = CreateNumeric(0, 200, 1, 1);
            damageInput = CreateNumeric(0, 5, 0.05m, 2);
            breakThresholdInput = CreateNumeric(1, 500, 1);
            cooldownInput = CreateNumeric(1, 300, 1);
            pollActiveInput = CreateNumeric(1, 1000, 1);
            pollIdleInput = CreateNumeric(10, 5000, 10);
            edgeProximityInput = CreateNumeric(1, 20, 1);
            idleResetInput = CreateNumeric(5, 1000, 5);
            speedEaseInput = CreateNumeric(0, 1, 0.01m, 2);

            AddRow(layout, "Max health", maxHealthInput, 0);
            AddRow(layout, "Regen per tick", regenInput, 1);
            AddRow(layout, "Damage multiplier", damageInput, 2);
            AddRow(layout, "Break threshold", breakThresholdInput, 3);
            AddRow(layout, "Cooldown frames", cooldownInput, 4);
            AddRow(layout, "Poll rate (active ms)", pollActiveInput, 5);
            AddRow(layout, "Poll rate (idle ms)", pollIdleInput, 6);
            AddRow(layout, "Edge proximity px", edgeProximityInput, 7);
            AddRow(layout, "Idle reset distance", idleResetInput, 8);
            AddRow(layout, "Speed ease multiplier", speedEaseInput, 9);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false
            };

            var apply = new Button { Text = "Apply", AutoSize = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var reset = new Button { Text = "Reset", AutoSize = true };

            apply.Click += (s, e) => ApplySettingsInternal(true);
            ok.Click += (s, e) => { ApplySettingsInternal(true); Close(); };
            cancel.Click += (s, e) => Close();
            reset.Click += (s, e) => { ResetToDefaults(); };

            AcceptButton = ok;
            CancelButton = cancel;

            buttons.Controls.Add(apply);
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(reset);

            Controls.Add(layout);
            Controls.Add(buttons);
        }

        private NumericUpDown CreateNumeric(decimal min, decimal max, decimal step, int decimals = 0)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = step,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Width = 80
            };
        }

        private void AddRow(TableLayoutPanel layout, string label, Control control, int row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 0, 0)
            }, 0, row);

            layout.Controls.Add(control, 1, row);
        }

        private void PopulateValues()
        {
            maxHealthInput.Value = (decimal)Settings.MaxHealth;
            regenInput.Value = (decimal)Settings.RegenRate;
            damageInput.Value = (decimal)Settings.DamageMultiplier;
            breakThresholdInput.Value = Settings.BreakThreshold;
            cooldownInput.Value = Settings.CooldownFrames;
            pollActiveInput.Value = Settings.PollRateActive;
            pollIdleInput.Value = Settings.PollRateIdle;
            edgeProximityInput.Value = Settings.EdgeProximityPx;
            idleResetInput.Value = Settings.IdleResetDistance;
            speedEaseInput.Value = (decimal)Settings.SpeedEaseMultiplier;
        }

        private void ApplySettingsInternal(bool invokeApplyCallback)
        {
            Settings.MaxHealth = (float)maxHealthInput.Value;
            Settings.RegenRate = (float)regenInput.Value;
            Settings.DamageMultiplier = (float)damageInput.Value;
            Settings.BreakThreshold = (int)breakThresholdInput.Value;
            Settings.CooldownFrames = (int)cooldownInput.Value;
            Settings.PollRateActive = (int)pollActiveInput.Value;
            Settings.PollRateIdle = (int)pollIdleInput.Value;
            Settings.EdgeProximityPx = (int)edgeProximityInput.Value;
            Settings.IdleResetDistance = (int)idleResetInput.Value;
            Settings.SpeedEaseMultiplier = (float)speedEaseInput.Value;

            Settings.Clamp();

            if (invokeApplyCallback && applyCallback != null)
            {
                applyCallback(Settings.Clone());
            }
        }

        private void ResetToDefaults()
        {
            Settings = AppSettings.CreateDefault();
            PopulateValues();
        }
    }
}
