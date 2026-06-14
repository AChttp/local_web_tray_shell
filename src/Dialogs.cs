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
            Text = initial == null ? "\u65b0\u589e\u7ad9\u70b9" : "\u7f16\u8f91\u7ad9\u70b9";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 460;
            Height = 214;
            BackColor = UiTheme.WindowBackground;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

            Label nameLabel = new Label();
            nameLabel.Text = "\u540d\u79f0";
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(16, 18);
            StyleLabel(nameLabel);

            nameTextBox = new TextBox();
            nameTextBox.Location = new Point(16, 40);
            nameTextBox.Width = 404;
            StyleInput(nameTextBox);

            Label urlLabel = new Label();
            urlLabel.Text = "URL";
            urlLabel.AutoSize = true;
            urlLabel.Location = new Point(16, 78);
            StyleLabel(urlLabel);

            urlTextBox = new TextBox();
            urlTextBox.Location = new Point(16, 100);
            urlTextBox.Width = 404;
            StyleInput(urlTextBox);

            saveButton = new ThemedButton();
            saveButton.Text = "\u4fdd\u5b58";
            saveButton.Width = 92;
            saveButton.Location = new Point(232, 138);
            saveButton.Click += OnSaveClicked;
            UiTheme.StylePrimaryButton(saveButton);

            cancelButton = new ThemedButton();
            cancelButton.Text = "\u53d6\u6d88";
            cancelButton.Width = 92;
            cancelButton.Location = new Point(328, 138);
            cancelButton.DialogResult = DialogResult.Cancel;
            UiTheme.StyleSecondaryButton(cancelButton);

            Controls.Add(nameLabel);
            Controls.Add(nameTextBox);
            Controls.Add(urlLabel);
            Controls.Add(urlTextBox);
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

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

        private void StyleLabel(Label label)
        {
            label.ForeColor = UiTheme.TextSecondary;
            label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
        }

        private void StyleInput(TextBox textBox)
        {
            textBox.BackColor = UiTheme.Surface;
            textBox.ForeColor = UiTheme.TextPrimary;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }

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

            Text = initial == null ? "\u65b0\u589e\u547d\u4ee4" : "\u7f16\u8f91\u547d\u4ee4";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 600;
            Height = 468;
            BackColor = UiTheme.WindowBackground;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

            Label nameLabel = new Label();
            nameLabel.Text = "\u540d\u79f0";
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(16, 16);
            StyleLabel(nameLabel);

            nameTextBox = new TextBox();
            nameTextBox.Location = new Point(16, 38);
            nameTextBox.Width = 548;
            StyleInput(nameTextBox);

            Label commandLabel = new Label();
            commandLabel.Text = "\u547d\u4ee4";
            commandLabel.AutoSize = true;
            commandLabel.Location = new Point(16, 74);
            StyleLabel(commandLabel);

            commandTextBox = new TextBox();
            commandTextBox.Location = new Point(16, 96);
            commandTextBox.Width = 548;
            commandTextBox.Height = 118;
            commandTextBox.Multiline = true;
            commandTextBox.ScrollBars = ScrollBars.Vertical;
            commandTextBox.AcceptsReturn = true;
            StyleInput(commandTextBox);

            Label runModeLabel = new Label();
            runModeLabel.Text = "\u542f\u52a8\u65b9\u5f0f";
            runModeLabel.AutoSize = true;
            runModeLabel.Location = new Point(16, 226);
            StyleLabel(runModeLabel);

            runModeComboBox = new ComboBox();
            runModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            runModeComboBox.Location = new Point(16, 248);
            runModeComboBox.Width = 180;
            runModeComboBox.BackColor = UiTheme.Surface;
            runModeComboBox.ForeColor = UiTheme.TextPrimary;
            runModeComboBox.Items.Add("\u76f4\u63a5");
            runModeComboBox.Items.Add("cmd");
            runModeComboBox.Items.Add("PowerShell");

            enabledOnStartCheckBox = new CheckBox();
            enabledOnStartCheckBox.Text = "Switch \u6253\u5f00\u65f6\u81ea\u52a8\u542f\u52a8";
            enabledOnStartCheckBox.AutoSize = true;
            enabledOnStartCheckBox.Location = new Point(220, 250);
            StyleCheckBox(enabledOnStartCheckBox);

            GroupBox retryGroup = new GroupBox();
            retryGroup.Text = "\u81ea\u52a8\u91cd\u8bd5";
            retryGroup.Location = new Point(16, 286);
            retryGroup.Size = new Size(548, 118);
            retryGroup.ForeColor = UiTheme.TextPrimary;
            retryGroup.BackColor = UiTheme.WindowBackground;

            retryEnabledCheckBox = new CheckBox();
            retryEnabledCheckBox.Text = "\u547d\u4ee4\u5f02\u5e38\u9000\u51fa\u65f6\u81ea\u52a8\u91cd\u8bd5";
            retryEnabledCheckBox.AutoSize = true;
            retryEnabledCheckBox.Location = new Point(16, 26);
            retryEnabledCheckBox.CheckedChanged += OnRetryCheckedChanged;
            StyleCheckBox(retryEnabledCheckBox);

            Label maxAttemptsLabel = new Label();
            maxAttemptsLabel.Text = "\u6700\u5927\u91cd\u8bd5\u6b21\u6570";
            maxAttemptsLabel.AutoSize = true;
            maxAttemptsLabel.Location = new Point(16, 56);
            StyleLabel(maxAttemptsLabel);

            maxAttemptsUpDown = new NumericUpDown();
            maxAttemptsUpDown.Location = new Point(16, 76);
            maxAttemptsUpDown.Width = 90;
            maxAttemptsUpDown.Minimum = 0;
            maxAttemptsUpDown.Maximum = 1000;
            StyleNumeric(maxAttemptsUpDown);

            Label initialDelayLabel = new Label();
            initialDelayLabel.Text = "\u521d\u59cb\u5ef6\u65f6(\u79d2)";
            initialDelayLabel.AutoSize = true;
            initialDelayLabel.Location = new Point(132, 56);
            StyleLabel(initialDelayLabel);

            initialDelayUpDown = new NumericUpDown();
            initialDelayUpDown.Location = new Point(132, 76);
            initialDelayUpDown.Width = 90;
            initialDelayUpDown.Minimum = 1;
            initialDelayUpDown.Maximum = 3600;
            StyleNumeric(initialDelayUpDown);

            Label maxDelayLabel = new Label();
            maxDelayLabel.Text = "\u6700\u5927\u5ef6\u65f6(\u79d2)";
            maxDelayLabel.AutoSize = true;
            maxDelayLabel.Location = new Point(248, 56);
            StyleLabel(maxDelayLabel);

            maxDelayUpDown = new NumericUpDown();
            maxDelayUpDown.Location = new Point(248, 76);
            maxDelayUpDown.Width = 90;
            maxDelayUpDown.Minimum = 1;
            maxDelayUpDown.Maximum = 3600;
            StyleNumeric(maxDelayUpDown);

            Label resetAfterLabel = new Label();
            resetAfterLabel.Text = "\u91cd\u7f6e\u8ba1\u6570(\u79d2)";
            resetAfterLabel.AutoSize = true;
            resetAfterLabel.Location = new Point(364, 56);
            StyleLabel(resetAfterLabel);

            resetAfterUpDown = new NumericUpDown();
            resetAfterUpDown.Location = new Point(364, 76);
            resetAfterUpDown.Width = 90;
            resetAfterUpDown.Minimum = 1;
            resetAfterUpDown.Maximum = 86400;
            StyleNumeric(resetAfterUpDown);

            retryGroup.Controls.Add(retryEnabledCheckBox);
            retryGroup.Controls.Add(maxAttemptsLabel);
            retryGroup.Controls.Add(maxAttemptsUpDown);
            retryGroup.Controls.Add(initialDelayLabel);
            retryGroup.Controls.Add(initialDelayUpDown);
            retryGroup.Controls.Add(maxDelayLabel);
            retryGroup.Controls.Add(maxDelayUpDown);
            retryGroup.Controls.Add(resetAfterLabel);
            retryGroup.Controls.Add(resetAfterUpDown);

            saveButton = new ThemedButton();
            saveButton.Text = "\u4fdd\u5b58";
            saveButton.Width = 92;
            saveButton.Location = new Point(376, 414);
            saveButton.Click += OnSaveClicked;
            UiTheme.StylePrimaryButton(saveButton);

            cancelButton = new ThemedButton();
            cancelButton.Text = "\u53d6\u6d88";
            cancelButton.Width = 92;
            cancelButton.Location = new Point(472, 414);
            cancelButton.DialogResult = DialogResult.Cancel;
            UiTheme.StyleSecondaryButton(cancelButton);

            Controls.Add(nameLabel);
            Controls.Add(nameTextBox);
            Controls.Add(commandLabel);
            Controls.Add(commandTextBox);
            Controls.Add(runModeLabel);
            Controls.Add(runModeComboBox);
            Controls.Add(enabledOnStartCheckBox);
            Controls.Add(retryGroup);
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

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
            maxAttemptsUpDown.Value = retry.MaxAttempts;
            initialDelayUpDown.Value = retry.InitialDelaySeconds;
            maxDelayUpDown.Value = retry.MaxDelaySeconds;
            resetAfterUpDown.Value = retry.ResetAfterSeconds;
            OnRetryCheckedChanged(this, EventArgs.Empty);

            if (commandReadOnly)
            {
                commandTextBox.ReadOnly = true;
                commandTextBox.BackColor = Color.FromArgb(245, 247, 250);
                runModeComboBox.Enabled = false;
            }
        }

        public CommandEntry Result { get; private set; }

        private void StyleLabel(Label label)
        {
            label.ForeColor = UiTheme.TextSecondary;
            label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
        }

        private void StyleInput(TextBox textBox)
        {
            textBox.BackColor = UiTheme.Surface;
            textBox.ForeColor = UiTheme.TextPrimary;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }

        private void StyleNumeric(NumericUpDown control)
        {
            control.BackColor = UiTheme.Surface;
            control.ForeColor = UiTheme.TextPrimary;
        }

        private void StyleCheckBox(CheckBox checkBox)
        {
            checkBox.ForeColor = UiTheme.TextSecondary;
            checkBox.BackColor = UiTheme.WindowBackground;
            checkBox.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
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
    }
}
