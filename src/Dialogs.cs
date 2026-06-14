using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal sealed class SiteDialog : Form
    {
        private readonly TextBox nameTextBox;
        private readonly TextBox urlTextBox;
        private readonly ThemedButton saveButton;
        private readonly ThemedButton cancelButton;

        public SiteDialog(SiteEntry initial)
        {
            DialogUi.StyleForm(this, initial == null ? "\u65b0\u589e\u7ad9\u70b9" : "\u7f16\u8f91\u7ad9\u70b9", new Size(500, 250));

            TableLayoutPanel layout = DialogUi.CreateLayout();
            layout.RowCount = 6;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            nameTextBox = DialogUi.CreateTextBox(false);
            urlTextBox = DialogUi.CreateTextBox(false);

            saveButton = DialogUi.CreatePrimaryButton("\u4fdd\u5b58", OnSaveClicked);
            cancelButton = DialogUi.CreateCancelButton();

            layout.Controls.Add(DialogUi.CreateLabel("\u540d\u79f0"), 0, 0);
            layout.Controls.Add(DialogUi.CreateInputFrame(nameTextBox, false), 0, 1);
            layout.Controls.Add(DialogUi.CreateLabel("URL"), 0, 3);
            layout.Controls.Add(DialogUi.CreateInputFrame(urlTextBox, false), 0, 4);
            layout.Controls.Add(DialogUi.CreateFooter(saveButton, cancelButton), 0, 5);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            if (initial != null)
            {
                nameTextBox.Text = initial.Name;
                urlTextBox.Text = initial.Url;
                Result = new SiteEntry
                {
                    Id = initial.Id,
                    Name = initial.Name,
                    Url = initial.Url
                };
            }
        }

        public SiteEntry Result { get; private set; }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            Uri uri;
            string name = nameTextBox.Text == null ? string.Empty : nameTextBox.Text.Trim();
            string url = urlTextBox.Text == null ? string.Empty : urlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(
                    "\u8bf7\u8f93\u5165\u7ad9\u70b9 URL\u3002",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "\u8bf7\u8f93\u5165\u6709\u6548\u7684 http \u6216 https URL\u3002",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = uri.Host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port);
            }

            if (Result == null)
            {
                Result = new SiteEntry();
            }

            Result.Id = string.IsNullOrWhiteSpace(Result.Id)
                ? AppConfigStore.NewId("site")
                : Result.Id;
            Result.Name = name;
            Result.Url = uri.AbsoluteUri;

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class CommandDialog : Form
    {
        private readonly TextBox nameTextBox;
        private readonly TextBox commandTextBox;
        private readonly ComboBox runModeComboBox;
        private readonly CheckBox enabledOnStartCheckBox;
        private readonly CheckBox retryEnabledCheckBox;
        private readonly NumericUpDown maxAttemptsUpDown;
        private readonly NumericUpDown initialDelayUpDown;
        private readonly NumericUpDown maxDelayUpDown;
        private readonly NumericUpDown resetAfterUpDown;
        private readonly ThemedButton saveButton;
        private readonly ThemedButton cancelButton;

        public CommandDialog(CommandEntry initial, bool commandReadOnly)
        {
            AutoRetryConfig retry = initial == null || initial.AutoRetry == null
                ? AppConfigStore.CreateDefaultAutoRetry()
                : initial.AutoRetry;

            DialogUi.StyleForm(this, initial == null ? "\u65b0\u589e\u547d\u4ee4" : "\u7f16\u8f91\u547d\u4ee4", new Size(660, 610));

            TableLayoutPanel layout = DialogUi.CreateLayout();
            layout.RowCount = 8;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 178f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            nameTextBox = DialogUi.CreateTextBox(false);
            commandTextBox = DialogUi.CreateTextBox(true);
            commandTextBox.AcceptsReturn = true;
            commandTextBox.ScrollBars = ScrollBars.Vertical;

            runModeComboBox = DialogUi.CreateComboBox();
            runModeComboBox.Items.Add("\u76f4\u63a5");
            runModeComboBox.Items.Add("cmd");
            runModeComboBox.Items.Add("PowerShell");

            enabledOnStartCheckBox = DialogUi.CreateCheckBox("Switch \u6253\u5f00\u65f6\u81ea\u52a8\u542f\u52a8");

            retryEnabledCheckBox = DialogUi.CreateCheckBox("\u547d\u4ee4\u5f02\u5e38\u9000\u51fa\u65f6\u81ea\u52a8\u91cd\u8bd5");
            retryEnabledCheckBox.CheckedChanged += OnRetryCheckedChanged;

            maxAttemptsUpDown = DialogUi.CreateNumeric(0, 1000);
            initialDelayUpDown = DialogUi.CreateNumeric(1, 3600);
            maxDelayUpDown = DialogUi.CreateNumeric(1, 3600);
            resetAfterUpDown = DialogUi.CreateNumeric(1, 86400);

            saveButton = DialogUi.CreatePrimaryButton("\u4fdd\u5b58", OnSaveClicked);
            cancelButton = DialogUi.CreateCancelButton();

            layout.Controls.Add(DialogUi.CreateLabel("\u540d\u79f0"), 0, 0);
            layout.Controls.Add(DialogUi.CreateInputFrame(nameTextBox, false), 0, 1);
            layout.Controls.Add(DialogUi.CreateLabel("\u547d\u4ee4"), 0, 2);
            layout.Controls.Add(DialogUi.CreateInputFrame(commandTextBox, true), 0, 3);
            layout.Controls.Add(CreateCommandOptionsPanel(), 0, 5);
            layout.Controls.Add(CreateRetryPanel(), 0, 6);
            layout.Controls.Add(DialogUi.CreateFooter(saveButton, cancelButton), 0, 7);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            if (initial != null)
            {
                Result = new CommandEntry
                {
                    Id = initial.Id,
                    Name = initial.Name,
                    Command = initial.Command,
                    RunMode = initial.RunMode,
                    EnabledOnStart = initial.EnabledOnStart,
                    AutoRetry = initial.AutoRetry
                };
                nameTextBox.Text = initial.Name;
                commandTextBox.Text = initial.Command;
                enabledOnStartCheckBox.Checked = initial.EnabledOnStart;
            }

            if (RunModeCatalog.Normalize(initial == null ? null : initial.RunMode) == RunModeCatalog.Cmd)
            {
                runModeComboBox.SelectedIndex = 1;
            }
            else if (RunModeCatalog.Normalize(initial == null ? null : initial.RunMode) == RunModeCatalog.PowerShell)
            {
                runModeComboBox.SelectedIndex = 2;
            }
            else
            {
                runModeComboBox.SelectedIndex = 0;
            }

            retryEnabledCheckBox.Checked = retry.Enabled;
            maxAttemptsUpDown.Value = Clamp(retry.MaxAttempts, maxAttemptsUpDown.Minimum, maxAttemptsUpDown.Maximum);
            initialDelayUpDown.Value = Clamp(retry.InitialDelaySeconds, initialDelayUpDown.Minimum, initialDelayUpDown.Maximum);
            maxDelayUpDown.Value = Clamp(retry.MaxDelaySeconds, maxDelayUpDown.Minimum, maxDelayUpDown.Maximum);
            resetAfterUpDown.Value = Clamp(retry.ResetAfterSeconds, resetAfterUpDown.Minimum, resetAfterUpDown.Maximum);
            OnRetryCheckedChanged(this, EventArgs.Empty);

            if (commandReadOnly)
            {
                commandTextBox.ReadOnly = true;
                commandTextBox.BackColor = UiTheme.SecondaryDisabled;
                runModeComboBox.Enabled = false;
            }
        }

        public CommandEntry Result { get; private set; }

        private Control CreateCommandOptionsPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);
            panel.BackColor = UiTheme.WindowBackground;
            panel.ColumnCount = 2;
            panel.RowCount = 2;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            enabledOnStartCheckBox.Dock = DockStyle.Fill;
            enabledOnStartCheckBox.Margin = new Padding(18, 8, 0, 0);

            panel.Controls.Add(DialogUi.CreateLabel("\u542f\u52a8\u65b9\u5f0f"), 0, 0);
            panel.Controls.Add(DialogUi.CreateComboFrame(runModeComboBox), 0, 1);
            panel.Controls.Add(enabledOnStartCheckBox, 1, 1);
            return panel;
        }

        private Control CreateRetryPanel()
        {
            RoundedPanel shell = new RoundedPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(0, 0, 0, 12);
            shell.Padding = new Padding(16, 14, 16, 14);
            shell.BackColor = UiTheme.Surface;
            shell.BorderColor = UiTheme.BorderSoft;
            shell.CornerRadius = 8;

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);
            panel.BackColor = UiTheme.Surface;
            panel.ColumnCount = 4;
            panel.RowCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            retryEnabledCheckBox.Dock = DockStyle.Fill;
            retryEnabledCheckBox.Margin = new Padding(0, 0, 0, 4);
            retryEnabledCheckBox.BackColor = UiTheme.Surface;

            panel.Controls.Add(retryEnabledCheckBox, 0, 0);
            panel.SetColumnSpan(retryEnabledCheckBox, 4);
            AddRetryNumeric(panel, "\u6700\u5927\u91cd\u8bd5\u6b21\u6570", maxAttemptsUpDown, 0);
            AddRetryNumeric(panel, "\u521d\u59cb\u5ef6\u65f6(\u79d2)", initialDelayUpDown, 1);
            AddRetryNumeric(panel, "\u6700\u5927\u5ef6\u65f6(\u79d2)", maxDelayUpDown, 2);
            AddRetryNumeric(panel, "\u91cd\u7f6e\u8ba1\u6570(\u79d2)", resetAfterUpDown, 3);

            shell.Controls.Add(panel);
            return shell;
        }

        private void AddRetryNumeric(TableLayoutPanel panel, string labelText, NumericUpDown numeric, int column)
        {
            Label label = DialogUi.CreateSmallLabel(labelText);
            label.Margin = new Padding(column == 0 ? 0 : 8, 0, 0, 0);
            numeric.Margin = new Padding(column == 0 ? 0 : 8, 4, 0, 0);
            numeric.Dock = DockStyle.Top;

            panel.Controls.Add(label, column, 1);
            panel.Controls.Add(numeric, column, 2);
        }

        private void OnRetryCheckedChanged(object sender, EventArgs e)
        {
            bool enabled = retryEnabledCheckBox.Checked;

            maxAttemptsUpDown.Enabled = enabled;
            initialDelayUpDown.Enabled = enabled;
            maxDelayUpDown.Enabled = enabled;
            resetAfterUpDown.Enabled = enabled;
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            string name = nameTextBox.Text == null ? string.Empty : nameTextBox.Text.Trim();
            string command = commandTextBox.Text == null ? string.Empty : commandTextBox.Text.Trim();
            string runMode;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "\u8bf7\u8f93\u5165\u547d\u4ee4\u540d\u79f0\u3002",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                MessageBox.Show(
                    "\u8bf7\u8f93\u5165\u8981\u6267\u884c\u7684\u547d\u4ee4\u3002",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            runMode = RunModeCatalog.Direct;

            if (runModeComboBox.SelectedIndex == 1)
            {
                runMode = RunModeCatalog.Cmd;
            }
            else if (runModeComboBox.SelectedIndex == 2)
            {
                runMode = RunModeCatalog.PowerShell;
            }

            if (Result == null)
            {
                Result = new CommandEntry();
            }

            Result.Id = string.IsNullOrWhiteSpace(Result.Id)
                ? AppConfigStore.NewId("cmd")
                : Result.Id;
            Result.Name = name;
            Result.Command = command;
            Result.RunMode = runMode;
            Result.EnabledOnStart = enabledOnStartCheckBox.Checked;
            Result.AutoRetry = new AutoRetryConfig
            {
                Enabled = retryEnabledCheckBox.Checked,
                MaxAttempts = (int)maxAttemptsUpDown.Value,
                InitialDelaySeconds = (int)initialDelayUpDown.Value,
                MaxDelaySeconds = (int)maxDelayUpDown.Value,
                ResetAfterSeconds = (int)resetAfterUpDown.Value
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private static decimal Clamp(int value, decimal minimum, decimal maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal static class DialogUi
    {
        public static void StyleForm(Form form, string title, Size clientSize)
        {
            form.Text = title;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.ClientSize = clientSize;
            form.BackColor = UiTheme.WindowBackground;
            form.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }

        public static TableLayoutPanel CreateLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(20);
            layout.BackColor = UiTheme.WindowBackground;
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return layout;
        }

        public static Label CreateLabel(string text)
        {
            Label label = CreateSmallLabel(text);
            label.TextAlign = ContentAlignment.BottomLeft;
            return label;
        }

        public static Label CreateSmallLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.ForeColor = UiTheme.TextSecondary;
            label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            label.Margin = new Padding(0);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        public static TextBox CreateTextBox(bool multiline)
        {
            TextBox textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.Multiline = multiline;
            textBox.BackColor = UiTheme.Surface;
            textBox.ForeColor = UiTheme.TextPrimary;
            textBox.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            textBox.Dock = DockStyle.Fill;
            return textBox;
        }

        public static Control CreateInputFrame(TextBox textBox, bool multiline)
        {
            RoundedPanel frame = new RoundedPanel();
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(0, 0, 0, 6);
            frame.Padding = multiline ? new Padding(11, 10, 11, 10) : new Padding(11, 9, 11, 6);
            frame.BackColor = UiTheme.Surface;
            frame.BorderColor = UiTheme.BorderSoft;
            frame.CornerRadius = 8;
            frame.Controls.Add(textBox);
            return frame;
        }

        public static ComboBox CreateComboBox()
        {
            ComboBox comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = UiTheme.Surface;
            comboBox.ForeColor = UiTheme.TextPrimary;
            comboBox.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            comboBox.Dock = DockStyle.Fill;
            return comboBox;
        }

        public static Control CreateComboFrame(ComboBox comboBox)
        {
            RoundedPanel frame = new RoundedPanel();
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(0, 0, 0, 6);
            frame.Padding = new Padding(9, 6, 9, 4);
            frame.BackColor = UiTheme.Surface;
            frame.BorderColor = UiTheme.BorderSoft;
            frame.CornerRadius = 8;
            frame.Controls.Add(comboBox);
            return frame;
        }

        public static NumericUpDown CreateNumeric(int minimum, int maximum)
        {
            NumericUpDown control = new NumericUpDown();
            control.Minimum = minimum;
            control.Maximum = maximum;
            control.Width = 112;
            control.BorderStyle = BorderStyle.FixedSingle;
            control.BackColor = UiTheme.Surface;
            control.ForeColor = UiTheme.TextPrimary;
            control.Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular);
            return control;
        }

        public static CheckBox CreateCheckBox(string text)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.AutoSize = false;
            checkBox.ForeColor = UiTheme.TextSecondary;
            checkBox.BackColor = UiTheme.WindowBackground;
            checkBox.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            checkBox.TextAlign = ContentAlignment.MiddleLeft;
            return checkBox;
        }

        public static ThemedButton CreatePrimaryButton(string text, EventHandler clickHandler)
        {
            ThemedButton button = new ThemedButton();
            button.Text = text;
            button.Size = new Size(96, 34);
            button.Click += clickHandler;
            UiTheme.StylePrimaryButton(button);
            return button;
        }

        public static ThemedButton CreateCancelButton()
        {
            ThemedButton button = new ThemedButton();
            button.Text = "\u53d6\u6d88";
            button.Size = new Size(96, 34);
            button.DialogResult = DialogResult.Cancel;
            UiTheme.StyleSecondaryButton(button);
            return button;
        }

        public static Control CreateFooter(ThemedButton saveButton, ThemedButton cancelButton)
        {
            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.WrapContents = false;
            footer.Padding = new Padding(0, 8, 0, 0);
            footer.Margin = new Padding(0);
            footer.BackColor = UiTheme.WindowBackground;
            cancelButton.Margin = new Padding(8, 0, 0, 0);
            saveButton.Margin = new Padding(8, 0, 0, 0);
            footer.Controls.Add(cancelButton);
            footer.Controls.Add(saveButton);
            return footer;
        }
    }
}
