using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal sealed class SiteDialog : Form
    {
        private readonly TextBox nameTextBox;
        private readonly TextBox urlTextBox;
        private readonly CheckBox proxyCheckBox;
        private readonly TextBox proxyServerTextBox;
        private readonly ThemedButton saveButton;
        private readonly ThemedButton cancelButton;
        private readonly Label errorLabel;

        public SiteDialog(SiteEntry initial)
        {
            string title = initial == null ? "新增站点" : "编辑站点";
            DialogUi.StyleForm(this, title, new Size(540, 392));

            TableLayoutPanel layout = DialogUi.CreateLayout();
            layout.RowCount = 9;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(58))); // 0: header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22))); // 1: name label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44))); // 2: name input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22))); // 3: url label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44))); // 4: url input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32))); // 5: proxy check
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44))); // 6: proxy server input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(24))); // 7: proxy hint
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(56))); // 8: footer

            nameTextBox = DialogUi.CreateTextBox(false);
            urlTextBox = DialogUi.CreateTextBox(false);
            proxyCheckBox = DialogUi.CreateCheckBox("使用自定义代理（未勾选时为直连）");
            proxyCheckBox.Dock = DockStyle.Fill;

            proxyServerTextBox = DialogUi.CreateTextBox(false);

            saveButton = DialogUi.CreatePrimaryButton("保存", OnSaveClicked);
            cancelButton = DialogUi.CreateCancelButton();
            errorLabel = DialogUi.CreateErrorLabel();

            layout.Controls.Add(DialogUi.CreateModalHeader(title, "配置 Web 服务的访问地址及独立网络代理"), 0, 0);
            layout.Controls.Add(DialogUi.CreateLabel("名称"), 0, 1);
            layout.Controls.Add(DialogUi.CreateInputFrame(nameTextBox, false), 0, 2);
            layout.Controls.Add(DialogUi.CreateLabel("URL"), 0, 3);
            layout.Controls.Add(DialogUi.CreateInputFrame(urlTextBox, false), 0, 4);
            layout.Controls.Add(proxyCheckBox, 0, 5);
            layout.Controls.Add(DialogUi.CreateInputFrame(proxyServerTextBox, false), 0, 6);
            layout.Controls.Add(DialogUi.CreateSmallLabel("支持 HTTP、HTTPS 与 SOCKS5，例如 http://127.0.0.1:7890 或 socks5://127.0.0.1:1080"), 0, 7);
            layout.Controls.Add(DialogUi.CreateFooter(saveButton, cancelButton, errorLabel), 0, 8);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            Action updateProxyUi = delegate
            {
                bool enabled = proxyCheckBox.Checked;
                proxyServerTextBox.Enabled = enabled;
                proxyServerTextBox.ForeColor = enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
            };

            proxyCheckBox.CheckedChanged += delegate { updateProxyUi(); };

            if (initial != null)
            {
                nameTextBox.Text = initial.Name;
                urlTextBox.Text = initial.Url;
                proxyCheckBox.Checked = initial.ProxyEnabled;
                proxyServerTextBox.Text = initial.ProxyServer ?? string.Empty;
                updateProxyUi();

                Result = new SiteEntry
                {
                    Id = initial.Id,
                    Name = initial.Name,
                    Url = initial.Url,
                    ProxyEnabled = initial.ProxyEnabled,
                    ProxyServer = initial.ProxyServer
                };
            }
            else
            {
                updateProxyUi();
            }
        }

        private static int S(int value)
        {
            return UiTheme.Scale(value);
        }

        public SiteEntry Result { get; private set; }

        private void ShowError(string message, Control focusControl)
        {
            errorLabel.Text = message ?? string.Empty;

            if (focusControl != null)
            {
                focusControl.Focus();
            }
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            Uri uri;
            string name = nameTextBox.Text == null ? string.Empty : nameTextBox.Text.Trim();
            string url = urlTextBox.Text == null ? string.Empty : urlTextBox.Text.Trim();
            string proxyServer = proxyServerTextBox.Text == null ? string.Empty : proxyServerTextBox.Text.Trim();
            bool proxyEnabled = proxyCheckBox.Checked;

            errorLabel.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowError("请输入站点 URL。", urlTextBox);
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError("请输入有效的 http 或 https URL。", urlTextBox);
                return;
            }

            if (proxyEnabled && string.IsNullOrWhiteSpace(proxyServer))
            {
                ShowError("已启用自定义代理，请输入代理服务器地址（例如 http://127.0.0.1:7890）。", proxyServerTextBox);
                return;
            }

            if (proxyEnabled && !string.IsNullOrEmpty(proxyServer))
            {
                if (!proxyServer.Contains("://"))
                {
                    proxyServer = "http://" + proxyServer;
                }

                Uri proxyUri;
                if (!Uri.TryCreate(proxyServer, UriKind.Absolute, out proxyUri))
                {
                    ShowError("代理地址格式无效，请输入如 http://127.0.0.1:7890 或 socks5://127.0.0.1:1080。", proxyServerTextBox);
                    return;
                }
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
            Result.ProxyEnabled = proxyEnabled && !string.IsNullOrEmpty(proxyServer);
            Result.ProxyServer = Result.ProxyEnabled ? proxyServer : string.Empty;

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class HotkeyDialog : Form
    {
        private readonly CheckBox enableCheckBox;
        private readonly Label comboLabel;
        private readonly ThemedButton saveButton;
        private readonly ThemedButton cancelButton;
        private readonly ThemedButton clearButton;
        private readonly Label errorLabel;

        private int capturedModifiers;
        private int capturedKey;
        private bool hasCapture;

        public HotkeyDialog(HotkeyConfig initial)
        {
            DialogUi.StyleForm(this, "快捷键设置", new Size(460, 300));
            KeyPreview = true;
            KeyDown += OnCaptureKeyDown;

            TableLayoutPanel layout = DialogUi.CreateLayout();
            layout.RowCount = 6;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(58)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(64)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(24)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(56)));

            comboLabel = new Label();
            comboLabel.Dock = DockStyle.Fill;
            comboLabel.BackColor = Color.Transparent;
            comboLabel.ForeColor = UiTheme.Primary;
            comboLabel.Font = UiTheme.CreateFont(15f, FontStyle.Bold);
            comboLabel.TextAlign = ContentAlignment.MiddleCenter;

            RoundedPanel comboCard = new RoundedPanel();
            comboCard.Dock = DockStyle.Fill;
            comboCard.BackColor = UiTheme.CardBackground;
            comboCard.BorderColor = UiTheme.Border;
            comboCard.BorderWidth = 1.5f;
            comboCard.CornerRadius = S(8);
            comboCard.Margin = new Padding(0, 0, 0, S(6));
            comboCard.Controls.Add(comboLabel);

            clearButton = new ThemedButton();
            clearButton.Text = "清除";
            clearButton.Size = new Size(S(64), S(34));
            clearButton.CornerRadius = S(7);
            clearButton.Margin = new Padding(S(8), 0, 0, S(6));
            UiTheme.StyleSecondaryButton(clearButton);
            clearButton.Click += delegate
            {
                capturedModifiers = 0;
                capturedKey = 0;
                hasCapture = false;
                errorLabel.Text = string.Empty;
                UpdateComboLabel();
            };

            TableLayoutPanel captureRow = new TableLayoutPanel();
            captureRow.Dock = DockStyle.Fill;
            captureRow.Margin = new Padding(0);
            captureRow.Padding = new Padding(0);
            captureRow.BackColor = UiTheme.WindowBackground;
            captureRow.ColumnCount = 2;
            captureRow.RowCount = 1;
            captureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            captureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(76)));
            captureRow.Controls.Add(comboCard, 0, 0);
            captureRow.Controls.Add(clearButton, 1, 0);

            enableCheckBox = DialogUi.CreateCheckBox("启用全局快捷键（托盘后台也生效）");
            enableCheckBox.Dock = DockStyle.Fill;

            saveButton = DialogUi.CreatePrimaryButton("保存", OnSaveClicked);
            cancelButton = DialogUi.CreateCancelButton();
            errorLabel = DialogUi.CreateErrorLabel();

            layout.Controls.Add(DialogUi.CreateModalHeader("快捷键设置", "设置呼出或隐藏主窗口的全局热键"), 0, 0);
            layout.Controls.Add(DialogUi.CreateLabel("请直接按下组合键（如 Ctrl + `）"), 0, 1);
            layout.Controls.Add(captureRow, 0, 2);
            layout.Controls.Add(DialogUi.CreateSmallLabel("需包含 Ctrl 或 Alt 之一；Win 键暂不支持"), 0, 3);
            layout.Controls.Add(enableCheckBox, 0, 4);
            layout.Controls.Add(DialogUi.CreateFooter(saveButton, cancelButton, errorLabel), 0, 5);

            Controls.Add(layout);
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            if (initial != null)
            {
                enableCheckBox.Checked = initial.Enabled;

                if (initial.Key != 0)
                {
                    capturedModifiers = initial.Modifiers;
                    capturedKey = initial.Key;
                    hasCapture = true;
                }
            }

            UpdateComboLabel();
        }

        private static int S(int value)
        {
            return UiTheme.Scale(value);
        }

        public HotkeyConfig Result { get; private set; }

        private void OnCaptureKeyDown(object sender, KeyEventArgs e)
        {
            Keys code = e.KeyCode;

            // Let Enter/Escape reach the Accept/Cancel buttons when no modifier is held.
            if (e.Modifiers == Keys.None && (code == Keys.Return || code == Keys.Escape))
            {
                return;
            }

            // Ignore bare modifier presses; wait for an actual key.
            if (code == Keys.ControlKey || code == Keys.Menu || code == Keys.ShiftKey ||
                code == Keys.LControlKey || code == Keys.RControlKey ||
                code == Keys.LMenu || code == Keys.RMenu ||
                code == Keys.LShiftKey || code == Keys.RShiftKey ||
                code == Keys.LWin || code == Keys.RWin)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            int modifiers = 0;

            if ((e.Modifiers & Keys.Control) == Keys.Control)
            {
                modifiers |= HotkeyConstants.ModControl;
            }

            if ((e.Modifiers & Keys.Alt) == Keys.Alt)
            {
                modifiers |= HotkeyConstants.ModAlt;
            }

            if ((e.Modifiers & Keys.Shift) == Keys.Shift)
            {
                modifiers |= HotkeyConstants.ModShift;
            }

            capturedModifiers = modifiers;
            capturedKey = e.KeyValue;
            hasCapture = true;
            errorLabel.Text = string.Empty;
            UpdateComboLabel();
        }

        private void UpdateComboLabel()
        {
            if (!hasCapture)
            {
                comboLabel.Text = "（请按下组合键）";
                comboLabel.ForeColor = UiTheme.TextMuted;
                return;
            }

            comboLabel.Text = HotkeyConfig.ToDisplayString(capturedModifiers, capturedKey);
            comboLabel.ForeColor = UiTheme.Primary;
        }

        private bool IsCaptureValid()
        {
            if (!hasCapture)
            {
                return false;
            }

            int required = HotkeyConstants.ModControl | HotkeyConstants.ModAlt;

            return (capturedModifiers & required) != 0;
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            if (enableCheckBox.Checked)
            {
                if (!hasCapture)
                {
                    errorLabel.Text = "请先按下要设置的组合键。";
                    return;
                }

                if (!IsCaptureValid())
                {
                    errorLabel.Text = "组合键需包含 Ctrl 或 Alt（Shift 单独无效）。";
                    return;
                }

                Result = new HotkeyConfig
                {
                    Enabled = true,
                    Modifiers = capturedModifiers,
                    Key = capturedKey
                };
            }
            else
            {
                Result = new HotkeyConfig
                {
                    Enabled = false,
                    Modifiers = capturedModifiers,
                    Key = capturedKey
                };
            }

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
        private readonly TextBox workingDirectoryTextBox;
        private readonly ThemedButton browseButton;
        private readonly TextBox environmentTextBox;
        private readonly ThemedButton saveButton;
        private readonly ThemedButton cancelButton;
        private readonly Label errorLabel;

        public CommandDialog(CommandEntry initial, bool commandReadOnly)
        {
            AutoRetryConfig retry = initial == null || initial.AutoRetry == null
                ? AppConfigStore.CreateDefaultAutoRetry()
                : initial.AutoRetry;

            string title = initial == null ? "新增命令" : "编辑命令";
            DialogUi.StyleForm(this, title, new Size(680, 748));

            TableLayoutPanel layout = DialogUi.CreateLayout();
            layout.RowCount = 13;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(58)));   // 0  header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));   // 1  name label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44)));   // 2  name input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));   // 3  command label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(116)));  // 4  command input (multiline)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(12)));   // 5  spacer
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(68)));   // 6  options panel
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));   // 7  working dir label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(42)));   // 8  working dir input + browse
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));   // 9  environment label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(96)));   // 10 environment input (multiline)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(146)));  // 11 retry panel
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(56)));   // 12 footer

            nameTextBox = DialogUi.CreateTextBox(false);
            commandTextBox = DialogUi.CreateMonospaceTextBox(true);
            commandTextBox.AcceptsReturn = true;
            commandTextBox.ScrollBars = ScrollBars.Vertical;

            runModeComboBox = DialogUi.CreateComboBox();
            runModeComboBox.Items.Add("直接");
            runModeComboBox.Items.Add("cmd");
            runModeComboBox.Items.Add("PowerShell");

            enabledOnStartCheckBox = DialogUi.CreateCheckBox("Switch 打开时自动启动");

            retryEnabledCheckBox = DialogUi.CreateCheckBox("命令异常退出时自动重试");
            retryEnabledCheckBox.CheckedChanged += OnRetryCheckedChanged;

            maxAttemptsUpDown = DialogUi.CreateNumeric(0, 1000);
            initialDelayUpDown = DialogUi.CreateNumeric(1, 3600);
            maxDelayUpDown = DialogUi.CreateNumeric(1, 3600);
            resetAfterUpDown = DialogUi.CreateNumeric(1, 86400);

            workingDirectoryTextBox = DialogUi.CreateTextBox(false);
            browseButton = new ThemedButton();
            browseButton.Text = "浏览...";
            browseButton.Size = new Size(S(84), S(32));
            browseButton.CornerRadius = S(7);
            UiTheme.StyleSecondaryButton(browseButton);
            browseButton.Click += OnBrowseWorkingDirectoryClicked;

            environmentTextBox = DialogUi.CreateMonospaceTextBox(true);
            environmentTextBox.AcceptsReturn = true;
            environmentTextBox.ScrollBars = ScrollBars.Vertical;

            saveButton = DialogUi.CreatePrimaryButton("保存", OnSaveClicked);
            cancelButton = DialogUi.CreateCancelButton();
            errorLabel = DialogUi.CreateErrorLabel();

            layout.Controls.Add(DialogUi.CreateModalHeader(title, "管理常驻后台进程、启动参数及异常自愈策略"), 0, 0);
            layout.Controls.Add(DialogUi.CreateLabel("名称"), 0, 1);
            layout.Controls.Add(DialogUi.CreateInputFrame(nameTextBox, false), 0, 2);
            layout.Controls.Add(DialogUi.CreateLabel("命令"), 0, 3);
            layout.Controls.Add(DialogUi.CreateInputFrame(commandTextBox, true), 0, 4);
            layout.Controls.Add(CreateCommandOptionsPanel(), 0, 6);
            layout.Controls.Add(DialogUi.CreateLabel("工作目录"), 0, 7);
            layout.Controls.Add(CreateWorkingDirectoryPanel(), 0, 8);
            layout.Controls.Add(DialogUi.CreateLabel("环境变量（每行 KEY=VALUE）"), 0, 9);
            layout.Controls.Add(DialogUi.CreateInputFrame(environmentTextBox, true), 0, 10);
            layout.Controls.Add(CreateRetryPanel(), 0, 11);
            layout.Controls.Add(DialogUi.CreateFooter(saveButton, cancelButton, errorLabel), 0, 12);

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
                    AutoRetry = initial.AutoRetry,
                    WorkingDirectory = initial.WorkingDirectory,
                    EnvironmentVariables = CloneEnvironmentVariables(initial.EnvironmentVariables)
                };
                nameTextBox.Text = initial.Name;
                commandTextBox.Text = initial.Command;
                enabledOnStartCheckBox.Checked = initial.EnabledOnStart;
                workingDirectoryTextBox.Text = initial.WorkingDirectory ?? string.Empty;
                environmentTextBox.Text = FormatEnvironmentVariables(initial.EnvironmentVariables);
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
                workingDirectoryTextBox.ReadOnly = true;
                workingDirectoryTextBox.BackColor = UiTheme.SecondaryDisabled;
                browseButton.Enabled = false;
                environmentTextBox.ReadOnly = true;
                environmentTextBox.BackColor = UiTheme.SecondaryDisabled;
            }
        }

        private static int S(int value)
        {
            return UiTheme.Scale(value);
        }

        private Control CreateWorkingDirectoryPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);
            panel.BackColor = UiTheme.WindowBackground;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(92)));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Control frame = DialogUi.CreateInputFrame(workingDirectoryTextBox, false);
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(0);

            browseButton.Dock = DockStyle.Fill;
            browseButton.Margin = new Padding(S(8), 0, 0, 0);

            panel.Controls.Add(frame, 0, 0);
            panel.Controls.Add(browseButton, 1, 0);
            return panel;
        }

        private void OnBrowseWorkingDirectoryClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择命令的工作目录";
                string current = workingDirectoryTextBox.Text;

                if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current.Trim()))
                {
                    dialog.SelectedPath = current.Trim();
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    workingDirectoryTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private static EnvironmentVariableEntry[] CloneEnvironmentVariables(EnvironmentVariableEntry[] variables)
        {
            if (variables == null)
            {
                return new EnvironmentVariableEntry[0];
            }

            EnvironmentVariableEntry[] clone = new EnvironmentVariableEntry[variables.Length];

            for (int index = 0; index < variables.Length; index++)
            {
                EnvironmentVariableEntry entry = variables[index];
                clone[index] = new EnvironmentVariableEntry
                {
                    Key = entry == null ? null : entry.Key,
                    Value = entry == null ? null : entry.Value
                };
            }

            return clone;
        }

        private static string FormatEnvironmentVariables(EnvironmentVariableEntry[] variables)
        {
            if (variables == null || variables.Length == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            for (int index = 0; index < variables.Length; index++)
            {
                EnvironmentVariableEntry entry = variables[index];

                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                builder.Append(entry.Key).Append('=').Append(entry.Value ?? string.Empty)
                    .Append(Environment.NewLine);
            }

            return builder.ToString();
        }

        private static EnvironmentVariableEntry[] ParseEnvironmentVariables(string text, out int invalidLineCount)
        {
            List<EnvironmentVariableEntry> results = new List<EnvironmentVariableEntry>();
            string[] lines = (text ?? string.Empty).Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries);
            invalidLineCount = 0;

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue;
                }

                int separator = line.IndexOf('=');

                if (separator <= 0)
                {
                    invalidLineCount += 1;
                    continue;
                }

                results.Add(new EnvironmentVariableEntry
                {
                    Key = line.Substring(0, separator).Trim(),
                    Value = line.Substring(separator + 1)
                });
            }

            return results.ToArray();
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
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(220)));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            enabledOnStartCheckBox.Dock = DockStyle.Fill;
            enabledOnStartCheckBox.Margin = new Padding(S(18), S(6), 0, 0);

            panel.Controls.Add(DialogUi.CreateLabel("启动方式"), 0, 0);
            panel.Controls.Add(DialogUi.CreateComboFrame(runModeComboBox), 0, 1);
            panel.Controls.Add(enabledOnStartCheckBox, 1, 1);
            return panel;
        }

        private Control CreateRetryPanel()
        {
            RoundedPanel shell = new RoundedPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(0, 0, 0, S(10));
            shell.Padding = new Padding(S(16), S(12), S(16), S(12));
            shell.BackColor = UiTheme.CardBackground;
            shell.BorderColor = UiTheme.Border;
            shell.BorderWidth = 1f;
            shell.CornerRadius = S(8);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);
            panel.BackColor = UiTheme.CardBackground;
            panel.ColumnCount = 4;
            panel.RowCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            retryEnabledCheckBox.Dock = DockStyle.Fill;
            retryEnabledCheckBox.Margin = new Padding(0, 0, 0, S(2));
            retryEnabledCheckBox.BackColor = UiTheme.CardBackground;
            retryEnabledCheckBox.Font = UiTheme.CreateFont(9f, FontStyle.Bold);

            panel.Controls.Add(retryEnabledCheckBox, 0, 0);
            panel.SetColumnSpan(retryEnabledCheckBox, 4);
            AddRetryNumeric(panel, "最大重试次数", maxAttemptsUpDown, 0);
            AddRetryNumeric(panel, "初始延时(秒)", initialDelayUpDown, 1);
            AddRetryNumeric(panel, "最大延时(秒)", maxDelayUpDown, 2);
            AddRetryNumeric(panel, "重置计数(秒)", resetAfterUpDown, 3);

            shell.Controls.Add(panel);
            return shell;
        }

        private void AddRetryNumeric(TableLayoutPanel panel, string labelText, NumericUpDown numeric, int column)
        {
            Label label = DialogUi.CreateSmallLabel(labelText);
            label.Margin = new Padding(column == 0 ? 0 : S(8), 0, 0, 0);
            numeric.Margin = new Padding(column == 0 ? 0 : S(8), S(4), 0, 0);
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

        private void ShowError(string message, Control focusControl)
        {
            errorLabel.Text = message ?? string.Empty;

            if (focusControl != null)
            {
                focusControl.Focus();
            }
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            string name = nameTextBox.Text == null ? string.Empty : nameTextBox.Text.Trim();
            string command = commandTextBox.Text == null ? string.Empty : commandTextBox.Text.Trim();
            string runMode;

            errorLabel.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("请输入命令名称。", nameTextBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                ShowError("请输入要执行的命令。", commandTextBox);
                return;
            }

            int invalidEnvLines;
            EnvironmentVariableEntry[] environmentVariables =
                ParseEnvironmentVariables(environmentTextBox.Text, out invalidEnvLines);

            if (invalidEnvLines > 0)
            {
                ShowError("环境变量有 " + invalidEnvLines + " 行格式无效（应为 KEY=VALUE），请修正或删除。", environmentTextBox);
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
            Result.WorkingDirectory = workingDirectoryTextBox.Text == null
                ? null
                : workingDirectoryTextBox.Text.Trim();
            Result.EnvironmentVariables = environmentVariables;

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
            Size scaled = new Size(UiTheme.Scale(clientSize.Width), UiTheme.Scale(clientSize.Height));
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            int maxWidth = Math.Max(UiTheme.Scale(360), workingArea.Width - UiTheme.Scale(48));
            int maxHeight = Math.Max(UiTheme.Scale(320), workingArea.Height - UiTheme.Scale(48));

            form.Text = title;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.ClientSize = new Size(Math.Min(scaled.Width, maxWidth), Math.Min(scaled.Height, maxHeight));
            form.BackColor = UiTheme.WindowBackground;
            form.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            form.AutoScaleMode = AutoScaleMode.None;
            // If the screen is too small for the content (e.g. 768p with the command
            // editor), the dialog scrolls instead of pushing the footer off-screen.
            form.AutoScroll = true;
        }

        public static Control CreateModalHeader(string title, string subtitle)
        {
            Panel headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Fill;
            headerPanel.Margin = new Padding(0, 0, 0, UiTheme.Scale(12));
            headerPanel.Height = UiTheme.Scale(56);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = UiTheme.CreateFont(13f, FontStyle.Bold);
            titleLabel.ForeColor = UiTheme.TextPrimary;
            titleLabel.AutoSize = false;
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = UiTheme.Scale(28);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;

            Label subtitleLabel = new Label();
            subtitleLabel.Text = subtitle;
            subtitleLabel.Font = UiTheme.CreateFont(8.75f, FontStyle.Regular);
            subtitleLabel.ForeColor = UiTheme.TextMuted;
            subtitleLabel.AutoSize = false;
            subtitleLabel.Dock = DockStyle.Top;
            subtitleLabel.Height = UiTheme.Scale(22);
            subtitleLabel.TextAlign = ContentAlignment.MiddleLeft;

            headerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(UiTheme.BorderSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            headerPanel.Controls.Add(subtitleLabel);
            headerPanel.Controls.Add(titleLabel);
            return headerPanel;
        }

        public static TableLayoutPanel CreateLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            // Docked top + AutoSize so the owning form's AutoScroll can take over when
            // the dialog is height-capped on small screens.
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.Margin = new Padding(0);
            layout.Padding = UiTheme.ScalePadding(24, 18, 24, 20);
            layout.BackColor = UiTheme.WindowBackground;
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return layout;
        }

        public static Label CreateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.ForeColor = UiTheme.TextPrimary;
            label.Font = UiTheme.CreateFont(9f, FontStyle.Bold);
            label.Margin = new Padding(0);
            label.TextAlign = ContentAlignment.BottomLeft;
            return label;
        }

        public static Label CreateSmallLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.ForeColor = UiTheme.TextMuted;
            label.Font = UiTheme.CreateFont(8.5f, FontStyle.Regular);
            label.Margin = new Padding(0);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        public static Label CreateErrorLabel()
        {
            Label label = new Label();
            label.Text = string.Empty;
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.ForeColor = UiTheme.DangerForeground;
            label.Font = UiTheme.CreateFont(8.75f, FontStyle.Regular);
            label.Margin = new Padding(0, UiTheme.Scale(14), UiTheme.Scale(12), 0);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            return label;
        }

        public static TextBox CreateTextBox(bool multiline)
        {
            TextBox textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.Multiline = multiline;
            textBox.BackColor = UiTheme.Surface;
            textBox.ForeColor = UiTheme.TextPrimary;
            textBox.Font = UiTheme.CreateFont(9.5f, FontStyle.Regular);
            textBox.Dock = DockStyle.Fill;
            return textBox;
        }

        public static TextBox CreateMonospaceTextBox(bool multiline)
        {
            TextBox textBox = CreateTextBox(multiline);
            textBox.Font = UiTheme.CreateMonospaceFont(9.5f, FontStyle.Regular);
            return textBox;
        }

        public static Control CreateInputFrame(TextBox textBox, bool multiline)
        {
            RoundedPanel frame = new RoundedPanel();
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(0, 0, 0, UiTheme.Scale(8));
            frame.Padding = multiline
                ? UiTheme.ScalePadding(12, 10, 12, 10)
                : UiTheme.ScalePadding(12, 9, 12, 6);
            frame.BackColor = UiTheme.Surface;
            frame.BorderColor = UiTheme.Border;
            frame.BorderWidth = 1f;
            frame.CornerRadius = UiTheme.Scale(7);
            frame.Controls.Add(textBox);

            textBox.Enter += delegate
            {
                frame.BorderColor = UiTheme.FocusRing;
                frame.BorderWidth = 1.5f;
            };

            textBox.Leave += delegate
            {
                frame.BorderColor = UiTheme.Border;
                frame.BorderWidth = 1f;
            };

            return frame;
        }

        public static ComboBox CreateComboBox()
        {
            ComboBox comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = UiTheme.Surface;
            comboBox.ForeColor = UiTheme.TextPrimary;
            comboBox.Font = UiTheme.CreateFont(9.5f, FontStyle.Regular);
            comboBox.Dock = DockStyle.Fill;
            return comboBox;
        }

        public static Control CreateComboFrame(ComboBox comboBox)
        {
            RoundedPanel frame = new RoundedPanel();
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(0, 0, 0, UiTheme.Scale(8));
            frame.Padding = UiTheme.ScalePadding(10, 7, 10, 5);
            frame.BackColor = UiTheme.Surface;
            frame.BorderColor = UiTheme.Border;
            frame.BorderWidth = 1f;
            frame.CornerRadius = UiTheme.Scale(7);
            frame.Controls.Add(comboBox);

            comboBox.Enter += delegate
            {
                frame.BorderColor = UiTheme.FocusRing;
                frame.BorderWidth = 1.5f;
            };

            comboBox.Leave += delegate
            {
                frame.BorderColor = UiTheme.Border;
                frame.BorderWidth = 1f;
            };

            return frame;
        }

        public static NumericUpDown CreateNumeric(int minimum, int maximum)
        {
            NumericUpDown control = new NumericUpDown();
            control.Minimum = minimum;
            control.Maximum = maximum;
            control.Width = UiTheme.Scale(112);
            control.BorderStyle = BorderStyle.FixedSingle;
            control.BackColor = UiTheme.Surface;
            control.ForeColor = UiTheme.TextPrimary;
            control.Font = UiTheme.CreateFont(9.25f, FontStyle.Regular);
            return control;
        }

        public static CheckBox CreateCheckBox(string text)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.AutoSize = false;
            checkBox.ForeColor = UiTheme.TextPrimary;
            checkBox.BackColor = UiTheme.WindowBackground;
            checkBox.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            checkBox.TextAlign = ContentAlignment.MiddleLeft;
            return checkBox;
        }

        public static ThemedButton CreatePrimaryButton(string text, EventHandler clickHandler)
        {
            ThemedButton button = new ThemedButton();
            button.Text = text;
            button.Size = new Size(UiTheme.Scale(100), UiTheme.Scale(36));
            button.Font = UiTheme.CreateFont(9.5f, FontStyle.Bold);
            button.CornerRadius = UiTheme.Scale(8);
            button.Click += clickHandler;
            UiTheme.StylePrimaryButton(button);
            return button;
        }

        public static ThemedButton CreateCancelButton()
        {
            ThemedButton button = new ThemedButton();
            button.Text = "取消";
            button.Size = new Size(UiTheme.Scale(100), UiTheme.Scale(36));
            button.Font = UiTheme.CreateFont(9.5f, FontStyle.Regular);
            button.CornerRadius = UiTheme.Scale(8);
            button.DialogResult = DialogResult.Cancel;
            UiTheme.StyleSecondaryButton(button);
            return button;
        }

        // Footer with an inline error area (left) so validation feedback stays in the
        // dialog instead of stacking modal message boxes.
        public static Control CreateFooter(ThemedButton saveButton, ThemedButton cancelButton, Label errorLabel)
        {
            Panel footer = new Panel();
            footer.Dock = DockStyle.Fill;
            footer.Margin = new Padding(0);
            footer.BackColor = UiTheme.WindowBackground;

            Panel buttons = new Panel();
            buttons.Dock = DockStyle.Right;
            buttons.Width = UiTheme.Scale(212);
            buttons.Margin = new Padding(0);
            buttons.BackColor = UiTheme.WindowBackground;

            saveButton.Location = new Point(0, UiTheme.Scale(10));
            saveButton.Margin = new Padding(0);
            cancelButton.Location = new Point(UiTheme.Scale(112), UiTheme.Scale(10));
            cancelButton.Margin = new Padding(0);
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);

            if (errorLabel != null)
            {
                footer.Controls.Add(errorLabel);
            }

            footer.Controls.Add(buttons);
            return footer;
        }
    }
}
