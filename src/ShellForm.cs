using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal sealed class ShellForm : Form
    {
        private const string AppName = "Switch";
        private const int DefaultSidebarWidth = 390;
        private const int SidebarSplitterWidth = 20;
        private const int SidebarMinExpandedWidth = 260;
        private const int SidebarMaxWidth = 820;
        private const int SidebarCollapseThreshold = 96;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_HOTKEY = 0x0312;
        private const int ShowHotkeyId = 0x9001;
        private const int WM_SETREDRAW = 0x000B;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int TitleBarHeight = 44;
        private const int ResizeGripSize = 8;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly StatusStrip statusStrip;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly Panel titleBarPanel;
        private readonly TitleBarIconButton titleSidebarButton;
        private readonly Label titleBarLabel;
        private readonly TitleBarIconButton minimizeButton;
        private readonly TitleBarIconButton maximizeButton;
        private readonly TitleBarIconButton closeButton;
        private readonly Panel rootPanel;
        private readonly Panel leftSidebar;
        private readonly SidebarSurfaceControl sidebarSurface;
        private readonly SidebarSplitterPanel sidebarSplitter;
        private readonly Panel workspacePanel;
        private readonly Panel rightBody;
        private readonly Panel webPanel;
        private readonly Panel logsPanel;
        private readonly Label webStateTitleLabel;
        private readonly Label webStateDetailLabel;
        private readonly ThemedButton webStateRetryButton;
        private readonly Panel webStateOverlay;
        private readonly Label currentCommandLabel;
        private readonly RoundedLabel commandStatusBadge;
        private readonly ThemedButton clearLogsButton;
        private readonly ThemedButton copyLogsButton;
        private readonly CheckBox autoScrollLogsCheckBox;
        private readonly TextBox logsTextBox;
        private readonly Panel webViewHost;
        private readonly Timer uiRefreshTimer;
        private readonly Timer runtimeRefreshTimer;
        private readonly ToolStripMenuItem trayStartupMenuItem;
        private readonly object runtimeRefreshSync;
        private readonly HashSet<string> pendingRuntimeRefreshCommandIds;
        private readonly HashSet<string> pendingLogRefreshCommandIds;
        private readonly Dictionary<string, SiteViewState> siteViews;
        private readonly CommandManager commandManager;
        private readonly List<SiteEntry> sites;
        private readonly List<CommandEntry> commands;
        private CoreWebView2Environment webViewEnvironment;
        private WorkspaceMode workspaceMode;
        private SiteEntry currentSite;
        private CommandEntry currentCommand;
        private bool allowExit;
        private bool trayHintShown;
        private bool startupCommandsRequested;
        private bool updatingStartupToggle;
        private bool lastLogAutoScrollEnabled;
        private bool runtimeRefreshActive;
        private bool sidebarHidden;
        private bool resizingSidebar;
        private bool hidingToTray;
        private HotkeyConfig pendingHotkey;
        private bool hotkeyRegistered;
        private ToolStripMenuItem trayHotkeyMenuItem;
        private double pendingCommandSectionRatio;
        private readonly object siteHealthSync;
        private Dictionary<string, SiteHealth> siteHealth;
        private System.Threading.Timer siteHealthTimer;
        private bool titleBarDragPending;
        private int sidebarDragStartX;
        private int sidebarDragStartWidth;
        private int sidebarPendingWidth;
        private DateTime statusSummaryHoldUntilUtc;
        private DateTime lastTitleBarDoubleClickHandledUtc;
        private int expandedSidebarWidth;
        private Point titleBarDragStartScreen;
        private FormWindowState preTrayWindowState;
        private string renderedLogCommandId;
        private int renderedLogFirstSequence;
        private int renderedLogNextSequence;

        public ShellForm()
        {
            AppConfig config = AppConfigStore.Load();
            Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            commandManager = new CommandManager();
            commandManager.RuntimeChanged += OnCommandRuntimeChanged;
            runtimeRefreshSync = new object();
            siteHealthSync = new object();
            siteHealth = new Dictionary<string, SiteHealth>(StringComparer.OrdinalIgnoreCase);
            pendingRuntimeRefreshCommandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pendingLogRefreshCommandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            siteViews = new Dictionary<string, SiteViewState>(StringComparer.OrdinalIgnoreCase);
            sites = new List<SiteEntry>(config.Sites ?? new SiteEntry[0]);
            commands = new List<CommandEntry>(config.Commands ?? new CommandEntry[0]);
            commandManager.SyncCommands(commands);
            pendingHotkey = config.GlobalHotkey ?? HotkeyConstants.CreateDefault();
            pendingCommandSectionRatio = config.CommandSectionRatio;

            workspaceMode = WorkspaceMode.Web;

            SetWindowTitle(AppName);
            Width = 1540;
            Height = 930;
            MinimumSize = new Size(1240, 760);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = appIcon;
            BackColor = UiTheme.WindowBackground;
            preTrayWindowState = WindowState;

            statusLabel = new ToolStripStatusLabel("\u6b63\u5728\u52a0\u8f7d\u5de5\u4f5c\u53f0...");
            statusStrip = new StatusStrip();
            statusStrip.Items.Add(statusLabel);

            titleBarPanel = new Panel();
            titleBarPanel.Dock = DockStyle.Top;
            titleBarPanel.Height = TitleBarHeight;
            titleBarPanel.BackColor = UiTheme.WindowBackground;
            titleBarPanel.MouseDown += OnTitleBarMouseDown;
            titleBarPanel.MouseMove += OnTitleBarMouseMove;
            titleBarPanel.MouseUp += OnTitleBarMouseUp;
            titleBarPanel.MouseDoubleClick += OnTitleBarMouseDoubleClick;
            titleBarPanel.Resize += OnTitleBarResize;

            titleSidebarButton = new TitleBarIconButton(TitleBarButtonKind.Sidebar);
            titleSidebarButton.Location = new Point(10, 6);
            titleSidebarButton.SidebarCollapsed = sidebarHidden;
            titleSidebarButton.Click += OnSidebarToggleClicked;

            titleBarLabel = new Label();
            titleBarLabel.AutoSize = false;
            titleBarLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleBarLabel.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            titleBarLabel.ForeColor = UiTheme.TextSecondary;
            titleBarLabel.BackColor = UiTheme.WindowBackground;
            titleBarLabel.MouseDown += OnTitleBarMouseDown;
            titleBarLabel.MouseMove += OnTitleBarMouseMove;
            titleBarLabel.MouseUp += OnTitleBarMouseUp;
            titleBarLabel.MouseDoubleClick += OnTitleBarMouseDoubleClick;

            minimizeButton = new TitleBarIconButton(TitleBarButtonKind.Minimize);
            minimizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            minimizeButton.Click += OnTitleMinimizeClicked;

            maximizeButton = new TitleBarIconButton(TitleBarButtonKind.Maximize);
            maximizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            maximizeButton.Click += OnTitleMaximizeClicked;

            closeButton = new TitleBarIconButton(TitleBarButtonKind.Close);
            closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeButton.Click += OnTitleCloseClicked;

            titleBarPanel.Controls.Add(titleSidebarButton);
            titleBarPanel.Controls.Add(titleBarLabel);
            titleBarPanel.Controls.Add(minimizeButton);
            titleBarPanel.Controls.Add(maximizeButton);
            titleBarPanel.Controls.Add(closeButton);
            LayoutTitleBarControls();

            rootPanel = new Panel();
            rootPanel.Dock = DockStyle.Fill;
            rootPanel.BackColor = BackColor;
            rootPanel.Margin = new Padding(0);
            rootPanel.Padding = new Padding(0);
            rootPanel.Resize += OnRootPanelResize;

            leftSidebar = new Panel();
            leftSidebar.Dock = DockStyle.None;
            leftSidebar.Width = DefaultSidebarWidth;
            leftSidebar.BackColor = UiTheme.SidebarBackground;
            leftSidebar.Padding = new Padding(0);
            expandedSidebarWidth = leftSidebar.Width;

            sidebarSurface = new SidebarSurfaceControl();
            sidebarSurface.BackColor = UiTheme.SidebarBackground;
            sidebarSurface.SnapshotProvider = delegate(string commandId)
            {
                return commandManager.GetSnapshot(commandId);
            };
            sidebarSurface.StopAllCommandsClicked += OnStopAllCommandsClicked;
            sidebarSurface.BackSiteClicked += OnBackSiteClicked;
            sidebarSurface.HomeSiteClicked += OnHomeSiteClicked;
            sidebarSurface.ReloadSiteClicked += OnReloadSiteClicked;
            sidebarSurface.WorkspaceModeRequested += OnSidebarWorkspaceModeRequested;
            sidebarSurface.CommandActivated += OnCommandListItemActivated;
            sidebarSurface.SiteActivated += OnSiteListItemActivated;
            sidebarSurface.CommandActionRequested += OnSidebarCommandActionRequested;
            sidebarSurface.SiteActionRequested += OnSidebarSiteActionRequested;
            sidebarSurface.CommandReorderRequested += OnCommandReorderRequested;
            sidebarSurface.SiteReorderRequested += OnSiteReorderRequested;
            sidebarSurface.CommandSectionRatioChanged += OnSidebarRatioChanged;
            sidebarSurface.CommandSectionRatio = pendingCommandSectionRatio;
            sidebarSurface.SiteHealthProvider = GetSiteHealth;

            sidebarSplitter = new SidebarSplitterPanel();
            sidebarSplitter.Dock = DockStyle.None;
            sidebarSplitter.MouseDown += OnSidebarSplitterMouseDown;
            sidebarSplitter.MouseMove += OnSidebarSplitterMouseMove;
            sidebarSplitter.MouseUp += OnSidebarSplitterMouseUp;

            leftSidebar.Controls.Add(sidebarSurface);

            workspacePanel = new Panel();
            workspacePanel.Dock = DockStyle.None;
            workspacePanel.Padding = new Padding(14, 14, 14, 14);
            workspacePanel.BackColor = BackColor;

            rightBody = new Panel();
            rightBody.Dock = DockStyle.None;
            rightBody.Padding = new Padding(0);

            webPanel = new Panel();
            webPanel.Dock = DockStyle.Fill;
            webPanel.BackColor = UiTheme.Surface;
            webPanel.Padding = new Padding(12);

            webViewHost = new Panel();
            webViewHost.Dock = DockStyle.Fill;
            webViewHost.BackColor = Color.FromArgb(221, 232, 242);
            webViewHost.Padding = new Padding(0);

            webStateOverlay = new Panel();
            webStateOverlay.BackColor = Color.FromArgb(221, 232, 242);
            webStateOverlay.Dock = DockStyle.Fill;
            webStateOverlay.Visible = true;

            TableLayoutPanel webStateLayout = new TableLayoutPanel();
            webStateLayout.Dock = DockStyle.Fill;
            webStateLayout.ColumnCount = 3;
            webStateLayout.RowCount = 5;
            webStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            webStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420f));
            webStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            webStateTitleLabel = new Label();
            webStateTitleLabel.Dock = DockStyle.Fill;
            webStateTitleLabel.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            webStateTitleLabel.ForeColor = UiTheme.TextPrimary;
            webStateTitleLabel.TextAlign = ContentAlignment.BottomCenter;
            webStateTitleLabel.Text = "\u6b63\u5728\u51c6\u5907\u7f51\u9875\u5de5\u4f5c\u533a";

            webStateDetailLabel = new Label();
            webStateDetailLabel.Dock = DockStyle.Fill;
            webStateDetailLabel.Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular);
            webStateDetailLabel.ForeColor = UiTheme.TextSecondary;
            webStateDetailLabel.TextAlign = ContentAlignment.TopCenter;
            webStateDetailLabel.AutoEllipsis = true;
            webStateDetailLabel.Text = "\u7b49\u5f85 WebView2 \u521d\u59cb\u5316\u3002";

            webStateRetryButton = CreatePrimaryButton("\u91cd\u8bd5", 0, 0, 96);
            webStateRetryButton.Dock = DockStyle.Top;
            webStateRetryButton.Margin = new Padding(156, 2, 156, 0);
            webStateRetryButton.Click += OnReloadSiteClicked;

            webStateLayout.Controls.Add(webStateTitleLabel, 1, 1);
            webStateLayout.Controls.Add(webStateDetailLabel, 1, 2);
            webStateLayout.Controls.Add(webStateRetryButton, 1, 3);
            webStateOverlay.Controls.Add(webStateLayout);

            webPanel.Controls.Add(webViewHost);
            webViewHost.Controls.Add(webStateOverlay);

            logsPanel = new Panel();
            logsPanel.Dock = DockStyle.Fill;
            logsPanel.BackColor = UiTheme.Surface;
            logsPanel.Padding = new Padding(12);

            currentCommandLabel = new Label();
            currentCommandLabel.Text = "\u672a\u9009\u62e9\u547d\u4ee4";
            currentCommandLabel.Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
            currentCommandLabel.ForeColor = UiTheme.TextPrimary;
            currentCommandLabel.AutoSize = true;
            currentCommandLabel.Dock = DockStyle.Fill;
            currentCommandLabel.TextAlign = ContentAlignment.MiddleLeft;
            currentCommandLabel.AutoEllipsis = true;

            commandStatusBadge = UiTheme.CreateBadgeLabel();
            commandStatusBadge.Text = "\u5df2\u505c\u6b62";
            commandStatusBadge.Size = new Size(104, 30);
            commandStatusBadge.Dock = DockStyle.Right;
            commandStatusBadge.BackColor = UiTheme.BadgeNeutralBackground;
            commandStatusBadge.ForeColor = UiTheme.BadgeNeutralForeground;

            clearLogsButton = CreateSecondaryButton("\u6e05\u7a7a\u65e5\u5fd7", 0, 0, 96);
            clearLogsButton.Click += OnClearLogsClicked;
            copyLogsButton = CreateSecondaryButton("\u590d\u5236\u65e5\u5fd7", 108, 0, 96);
            copyLogsButton.Click += OnCopyLogsClicked;
            autoScrollLogsCheckBox = new CheckBox();
            autoScrollLogsCheckBox.Text = "\u81ea\u52a8\u6eda\u52a8";
            autoScrollLogsCheckBox.Checked = true;
            autoScrollLogsCheckBox.AutoSize = true;
            autoScrollLogsCheckBox.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            autoScrollLogsCheckBox.ForeColor = UiTheme.TextSecondary;
            autoScrollLogsCheckBox.Location = new Point(0, 8);
            autoScrollLogsCheckBox.CheckedChanged += OnAutoScrollLogsChanged;

            Panel logsToolbar = new Panel();
            logsToolbar.Dock = DockStyle.Top;
            logsToolbar.Height = 44;
            logsToolbar.BackColor = UiTheme.Surface;

            Panel logsTitlePanel = new Panel();
            logsTitlePanel.Dock = DockStyle.Left;
            logsTitlePanel.Width = 560;
            logsTitlePanel.Controls.Add(commandStatusBadge);
            logsTitlePanel.Controls.Add(currentCommandLabel);

            Panel logsActionPanel = new Panel();
            logsActionPanel.Dock = DockStyle.Right;
            logsActionPanel.Width = 330;
            logsActionPanel.Controls.Add(clearLogsButton);
            logsActionPanel.Controls.Add(copyLogsButton);
            logsActionPanel.Controls.Add(autoScrollLogsCheckBox);
            clearLogsButton.Location = new Point(126, 0);
            copyLogsButton.Location = new Point(228, 0);

            logsToolbar.Controls.Add(logsActionPanel);
            logsToolbar.Controls.Add(logsTitlePanel);

            logsTextBox = new TextBox();
            logsTextBox.Dock = DockStyle.Fill;
            logsTextBox.Multiline = true;
            logsTextBox.ReadOnly = true;
            logsTextBox.ScrollBars = ScrollBars.Both;
            logsTextBox.WordWrap = false;
            logsTextBox.BackColor = Color.FromArgb(14, 22, 32);
            logsTextBox.ForeColor = Color.FromArgb(228, 236, 246);
            logsTextBox.Font = new Font("Cascadia Mono", 10f, FontStyle.Regular);

            logsPanel.Controls.Add(logsTextBox);
            logsPanel.Controls.Add(logsToolbar);

            rightBody.Controls.Add(webPanel);
            rightBody.Controls.Add(logsPanel);

            workspacePanel.Controls.Add(rightBody);

            rootPanel.Controls.Add(leftSidebar);
            rootPanel.Controls.Add(sidebarSplitter);
            rootPanel.Controls.Add(workspacePanel);
            LayoutShellPanels();

            Controls.Add(rootPanel);
            Controls.Add(statusStrip);
            Controls.Add(titleBarPanel);

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("\u6253\u5f00\u4e3b\u754c\u9762", null, delegate { RestoreFromTray(); });
            trayMenu.Items.Add("\u663e\u793a\u63a7\u5236\u53f0", null, delegate { RestoreFromTray(); SetSidebarWidth(expandedSidebarWidth <= 0 ? DefaultSidebarWidth : expandedSidebarWidth); });
            trayMenu.Items.Add("\u5237\u65b0\u5f53\u524d\u9875\u9762", null, delegate { ReloadCurrentSite(); });
            trayMenu.Items.Add("\u5168\u90e8\u505c\u6b62\u547d\u4ee4", null, delegate { ConfirmAndStopAll(); });
            trayStartupMenuItem = new ToolStripMenuItem("\u5f00\u673a\u81ea\u542f");
            trayStartupMenuItem.CheckOnClick = true;
            trayStartupMenuItem.Click += OnTrayStartupMenuClicked;
            trayMenu.Items.Add(trayStartupMenuItem);

            trayHotkeyMenuItem = new ToolStripMenuItem();
            trayHotkeyMenuItem.Click += OnConfigureHotkeyClicked;
            trayMenu.Items.Add(trayHotkeyMenuItem);
            UpdateHotkeyMenuText();

            trayMenu.Items.Add("\u5bfc\u5165\u914d\u7f6e\u2026", null, delegate { ImportConfig(); });
            trayMenu.Items.Add("\u5bfc\u51fa\u914d\u7f6e\u2026", null, delegate { ExportConfig(); });

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("\u9000\u51fa", null, delegate { ExitApplication(); });

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = appIcon;
            notifyIcon.Text = AppName;
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += delegate { RestoreFromTray(); };

            uiRefreshTimer = new Timer();
            uiRefreshTimer.Interval = 1000;
            uiRefreshTimer.Tick += OnUiRefreshTimerTick;

            runtimeRefreshTimer = new Timer();
            runtimeRefreshTimer.Interval = 100;
            runtimeRefreshTimer.Tick += OnRuntimeRefreshTimerTick;

            updatingStartupToggle = true;
            trayStartupMenuItem.Checked = WindowsStartupManager.IsEnabled();
            updatingStartupToggle = false;

            Shown += OnShown;
            Resize += OnResize;
            FormClosing += OnFormClosing;

            RefreshCommandList();
            RefreshSiteList();
            SetWorkspaceMode(WorkspaceMode.Web);
            RefreshCommandButtons();
            RefreshSiteButtons();
            UpdateStatusSummary();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.Style |= WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                return createParams;
            }
        }

        private async void OnShown(object sender, EventArgs e)
        {
            try
            {
                webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                    null,
                    AppPaths.WebViewUserDataDirectory);

                SetTransientStatus("\u5de5\u4f5c\u53f0\u5df2\u5c31\u7eea\u3002");
                SetWebState("\u7f51\u9875\u5de5\u4f5c\u533a\u5df2\u5c31\u7eea", "\u8bf7\u9009\u62e9\u4e00\u4e2a\u7ad9\u70b9\u6216\u7b49\u5f85\u9ed8\u8ba4\u7ad9\u70b9\u52a0\u8f7d\u3002", false);
                uiRefreshTimer.Start();
                RestartSiteHealthProbe();

                if (commands.Count > 0)
                {
                    SelectCommand(commands[0], false);
                }

                if (sites.Count > 0)
                {
                    SelectSite(sites[0]);
                }

                if (!startupCommandsRequested)
                {
                    startupCommandsRequested = true;
                    commandManager.StartEnabledCommands(commands);
                }
            }
            catch (Exception ex)
            {
                SetTransientStatus("WebView2 \u521d\u59cb\u5316\u5931\u8d25\u3002");
                SetWebState("WebView2 \u521d\u59cb\u5316\u5931\u8d25", ex.Message, false);
                MessageBox.Show(
                    "\u65e0\u6cd5\u521d\u59cb\u5316 WebView2\u3002\r\n\r\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (!allowExit &&
                m.Msg == WM_SYSCOMMAND &&
                ((int)m.WParam & 0xFFF0) == SC_CLOSE)
            {
                QueueHideToTray();
                return;
            }

            if (m.Msg == WM_HOTKEY && (int)m.WParam == ShowHotkeyId)
            {
                ToggleFromHotkey();
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                HandleWindowHitTest(ref m);
                return;
            }

            base.WndProc(ref m);
        }

        private void HandleWindowHitTest(ref Message m)
        {
            Point clientPoint = PointToClient(new Point(
                unchecked((short)((long)m.LParam & 0xFFFF)),
                unchecked((short)(((long)m.LParam >> 16) & 0xFFFF))));
            bool left = clientPoint.X <= ResizeGripSize;
            bool right = clientPoint.X >= ClientSize.Width - ResizeGripSize;
            bool top = clientPoint.Y <= ResizeGripSize;
            bool bottom = clientPoint.Y >= ClientSize.Height - ResizeGripSize;

            if (left && top)
            {
                m.Result = new IntPtr(HTTOPLEFT);
                return;
            }

            if (right && top)
            {
                m.Result = new IntPtr(HTTOPRIGHT);
                return;
            }

            if (left && bottom)
            {
                m.Result = new IntPtr(HTBOTTOMLEFT);
                return;
            }

            if (right && bottom)
            {
                m.Result = new IntPtr(HTBOTTOMRIGHT);
                return;
            }

            if (left)
            {
                m.Result = new IntPtr(HTLEFT);
                return;
            }

            if (right)
            {
                m.Result = new IntPtr(HTRIGHT);
                return;
            }

            if (top)
            {
                m.Result = new IntPtr(HTTOP);
                return;
            }

            if (bottom)
            {
                m.Result = new IntPtr(HTBOTTOM);
                return;
            }

            if (clientPoint.Y >= 0 &&
                clientPoint.Y < TitleBarHeight &&
                !IsPointOverTitleBarControl(clientPoint))
            {
                m.Result = new IntPtr(HTCAPTION);
                return;
            }

            m.Result = new IntPtr(HTCLIENT);
        }

        private bool IsPointOverTitleBarControl(Point clientPoint)
        {
            Point titlePoint = titleBarPanel.PointToClient(PointToScreen(clientPoint));
            Control child = titleBarPanel.GetChildAtPoint(titlePoint);

            return child == titleSidebarButton ||
                child == minimizeButton ||
                child == maximizeButton ||
                child == closeButton;
        }

        private void OnUiRefreshTimerTick(object sender, EventArgs e)
        {
            RefreshCommandCardsState();
            RefreshCommandButtons();
            if (workspaceMode == WorkspaceMode.Logs)
            {
                RefreshLogsView();
            }
            UpdateStatusSummary();
        }

        private void OnCommandRuntimeChanged(object sender, CommandRuntimeChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            QueueRuntimeRefresh(e.CommandId, e.LogsOnly);
        }

        private void QueueRuntimeRefresh(string commandId, bool logsOnly)
        {
            bool shouldStartTimer = false;

            if (string.IsNullOrWhiteSpace(commandId) || !IsHandleCreated || IsDisposed)
            {
                return;
            }

            lock (runtimeRefreshSync)
            {
                if (logsOnly)
                {
                    pendingLogRefreshCommandIds.Add(commandId);
                }
                else
                {
                    pendingRuntimeRefreshCommandIds.Add(commandId);
                }

                if (!runtimeRefreshActive)
                {
                    runtimeRefreshActive = true;
                    shouldStartTimer = true;
                }
            }

            if (!shouldStartTimer)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(StartRuntimeRefreshTimer));
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeRefreshInactive();
            }
        }

        private void StartRuntimeRefreshTimer()
        {
            if (IsDisposed)
            {
                MarkRuntimeRefreshInactive();
                return;
            }

            if (!runtimeRefreshTimer.Enabled)
            {
                runtimeRefreshTimer.Start();
            }
        }

        private void OnRuntimeRefreshTimerTick(object sender, EventArgs e)
        {
            FlushPendingRuntimeRefresh();
        }

        private void FlushPendingRuntimeRefresh()
        {
            string[] runtimeCommandIds;
            string[] logCommandIds;
            bool hasPendingAfterFlush;

            lock (runtimeRefreshSync)
            {
                if (pendingRuntimeRefreshCommandIds.Count == 0 &&
                    pendingLogRefreshCommandIds.Count == 0)
                {
                    runtimeRefreshActive = false;
                    runtimeRefreshTimer.Stop();
                    return;
                }

                runtimeCommandIds = CopyAndClear(pendingRuntimeRefreshCommandIds);
                logCommandIds = CopyAndClear(pendingLogRefreshCommandIds);
            }

            for (int index = 0; index < runtimeCommandIds.Length; index++)
            {
                RefreshCommandCardState(runtimeCommandIds[index]);
            }

            if (runtimeCommandIds.Length > 0)
            {
                RefreshCommandButtons();
                UpdateStatusSummary();
            }

            if (ShouldRefreshCurrentLogs(runtimeCommandIds, logCommandIds))
            {
                RefreshLogsView();
            }

            lock (runtimeRefreshSync)
            {
                hasPendingAfterFlush =
                    pendingRuntimeRefreshCommandIds.Count > 0 ||
                    pendingLogRefreshCommandIds.Count > 0;

                if (!hasPendingAfterFlush)
                {
                    runtimeRefreshActive = false;
                }
            }

            if (!hasPendingAfterFlush)
            {
                runtimeRefreshTimer.Stop();
            }
        }

        private static string[] CopyAndClear(HashSet<string> source)
        {
            string[] values = new string[source.Count];
            source.CopyTo(values);
            source.Clear();
            return values;
        }

        private bool ShouldRefreshCurrentLogs(string[] runtimeCommandIds, string[] logCommandIds)
        {
            string currentCommandId;

            if (workspaceMode != WorkspaceMode.Logs || currentCommand == null)
            {
                return false;
            }

            currentCommandId = currentCommand.Id;
            return ContainsCommandId(runtimeCommandIds, currentCommandId) ||
                ContainsCommandId(logCommandIds, currentCommandId);
        }

        private static bool ContainsCommandId(string[] commandIds, string commandId)
        {
            if (commandIds == null || string.IsNullOrWhiteSpace(commandId))
            {
                return false;
            }

            for (int index = 0; index < commandIds.Length; index++)
            {
                if (string.Equals(commandIds[index], commandId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkRuntimeRefreshInactive()
        {
            lock (runtimeRefreshSync)
            {
                runtimeRefreshActive = false;
            }
        }

        private void RefreshCommandList()
        {
            sidebarSurface.SetCommands(commands);
            UpdateCommandSelectionVisuals();
            RefreshEmptyStates();
        }

        private void RefreshSiteList()
        {
            sidebarSurface.SetSites(sites);
            UpdateSiteSelectionVisuals();
            RefreshEmptyStates();
        }

        private void OnSidebarWorkspaceModeRequested(object sender, SidebarWorkspaceModeEventArgs e)
        {
            if (e != null)
            {
                SetWorkspaceMode(e.Mode);
            }
        }

        private void OnSidebarCommandActionRequested(object sender, SidebarCommandActionEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Action)
            {
                case SidebarCommandAction.Add:
                    OnAddCommandClicked(sender, EventArgs.Empty);
                    break;
                case SidebarCommandAction.Edit:
                    OnEditCommandClicked(sender, EventArgs.Empty);
                    break;
                case SidebarCommandAction.Delete:
                    OnDeleteCommandClicked(sender, EventArgs.Empty);
                    break;
                case SidebarCommandAction.Restart:
                    OnRestartCommandClicked(sender, EventArgs.Empty);
                    break;
                case SidebarCommandAction.StartStop:
                    OnStartStopCommandClicked(sender, EventArgs.Empty);
                    break;
            }
        }

        private void OnSidebarSiteActionRequested(object sender, SidebarSiteActionEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            switch (e.Action)
            {
                case SidebarSiteAction.Add:
                    OnAddSiteClicked(sender, EventArgs.Empty);
                    break;
                case SidebarSiteAction.Edit:
                    OnEditSiteClicked(sender, EventArgs.Empty);
                    break;
                case SidebarSiteAction.Delete:
                    OnDeleteSiteClicked(sender, EventArgs.Empty);
                    break;
                case SidebarSiteAction.Open:
                    OnOpenSiteClicked(sender, EventArgs.Empty);
                    break;
            }
        }

        private void OnCommandReorderRequested(object sender, SidebarReorderEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            int target = e.Index + e.Delta;

            if (e.Index < 0 || target < 0 || e.Index >= commands.Count || target >= commands.Count)
            {
                return;
            }

            CommandEntry temporary = commands[e.Index];
            commands[e.Index] = commands[target];
            commands[target] = temporary;

            PersistAndSyncCommands();
            RefreshCommandList();
            sidebarSurface.EnsureCommandVisible(target);
        }

        private void OnSiteReorderRequested(object sender, SidebarReorderEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            int target = e.Index + e.Delta;

            if (e.Index < 0 || target < 0 || e.Index >= sites.Count || target >= sites.Count)
            {
                return;
            }

            SiteEntry temporary = sites[e.Index];
            sites[e.Index] = sites[target];
            sites[target] = temporary;

            PersistConfig();
            RefreshSiteList();
            sidebarSurface.EnsureSiteVisible(target);
        }

        private void OnCommandListItemActivated(object sender, SidebarListItemEventArgs<CommandEntry> e)
        {
            if (e != null && e.Item != null)
            {
                SelectCommand(e.Item);
            }
        }

        private void OnSiteListItemActivated(object sender, SidebarListItemEventArgs<SiteEntry> e)
        {
            if (e != null && e.Item != null)
            {
                SelectSite(e.Item);
            }
        }

        private void SelectCommand(CommandEntry command)
        {
            SelectCommand(command, true);
        }

        private void SelectCommand(CommandEntry command, bool switchToLogs)
        {
            currentCommand = command;
            UpdateCommandSelectionVisuals();
            RefreshCommandButtons();
            RefreshLogsView();
            lastLogAutoScrollEnabled = true;

            if (switchToLogs)
            {
                SetWorkspaceMode(WorkspaceMode.Logs);
            }
        }

        private void SelectSite(SiteEntry site)
        {
            currentSite = site;
            UpdateSiteSelectionVisuals();
            RefreshSiteButtons();
            SetWorkspaceMode(WorkspaceMode.Web);
        }

        private async void ShowSite(SiteEntry site)
        {
            SiteViewState state;

            if (site == null)
            {
                return;
            }

            if (webViewEnvironment == null)
            {
                SetTransientStatus("\u7f51\u9875\u5de5\u4f5c\u533a\u4ecd\u5728\u542f\u52a8\u4e2d\u3002");
                SetWebState("\u7f51\u9875\u5de5\u4f5c\u533a\u4ecd\u5728\u542f\u52a8\u4e2d", site.Url, false);
                return;
            }

            state = GetOrCreateSiteView(site);

            foreach (Control control in webViewHost.Controls)
            {
                control.Visible = false;
            }

            state.WebView.Visible = true;
            state.WebView.BringToFront();
            SetWebState(
                state.IsInitialized ? string.Empty : "\u6b63\u5728\u6253\u5f00 " + site.Name,
                state.IsInitialized ? string.Empty : site.Url,
                false);
            SetTransientStatus(state.IsInitialized
                ? "\u5df2\u5207\u6362\u5230 " + site.Name
                : "\u6b63\u5728\u6253\u5f00 " + site.Name + "...");

            if (state.InitializationStarted)
            {
                if (state.IsInitialized &&
                    state.WebView.CoreWebView2 != null &&
                    !string.Equals(state.LastNavigatedUrl, site.Url, StringComparison.OrdinalIgnoreCase))
                {
                    state.LastNavigatedUrl = site.Url;
                    state.WebView.CoreWebView2.Navigate(site.Url);
                }

                if (state.IsInitialized)
                {
                    SetWebState(string.Empty, string.Empty, false);
                }

                return;
            }

            state.InitializationStarted = true;

            try
            {
                await state.WebView.EnsureCoreWebView2Async(webViewEnvironment);
                state.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                state.WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                state.WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                state.IsInitialized = true;
                state.LastNavigatedUrl = site.Url;
                state.WebView.CoreWebView2.Navigate(site.Url);
                SetWebState("\u6b63\u5728\u52a0\u8f7d " + site.Name, site.Url, false);
            }
            catch (Exception ex)
            {
                state.InitializationStarted = false;
                SetTransientStatus("\u65e0\u6cd5\u6253\u5f00 " + site.Name);
                SetWebState("\u65e0\u6cd5\u6253\u5f00 " + site.Name, ex.Message, true);
                MessageBox.Show(
                    "\u65e0\u6cd5\u6253\u5f00 " + site.Url + "\u3002\r\n\r\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private SiteViewState GetOrCreateSiteView(SiteEntry site)
        {
            SiteViewState state;

            if (siteViews.TryGetValue(site.Id, out state))
            {
                state.Site = site;
                return state;
            }

            state = new SiteViewState();
            state.Site = site;
            state.NavigationHistory = new List<string>();
            state.WebView = new WebView2();
            state.WebView.Dock = DockStyle.Fill;
            state.WebView.Visible = false;
            state.WebView.Margin = new Padding(0);
            state.WebView.Tag = state;
            state.WebView.NavigationStarting += OnNavigationStarting;
            state.WebView.NavigationCompleted += OnNavigationCompleted;

            webViewHost.Controls.Add(state.WebView);
            siteViews[site.Id] = state;
            return state;
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            SiteViewState state = GetSiteState(sender);

            if (state != null)
            {
                state.CurrentNavigationUrl = e.Uri;
            }

            if (state != null && currentSite != null &&
                string.Equals(state.Site.Id, currentSite.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetTransientStatus("\u6b63\u5728\u52a0\u8f7d " + state.Site.Name + " - " + e.Uri, 1);
                SetWebState("\u6b63\u5728\u52a0\u8f7d " + state.Site.Name, e.Uri, false);
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            CoreWebView2 webView = sender as CoreWebView2;
            SiteViewState state = FindSiteState(webView);

            e.Handled = true;

            if (state == null || string.IsNullOrWhiteSpace(e.Uri))
            {
                return;
            }

            webView.Navigate(e.Uri);

            if (currentSite != null &&
                string.Equals(state.Site.Id, currentSite.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetWorkspaceMode(WorkspaceMode.Web);
                SetTransientStatus("\u6b63\u5728\u5f53\u524d\u9875\u6253\u5f00\u94fe\u63a5...");
                RefreshSiteButtons();
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            SiteViewState state = GetSiteState(sender);
            string navigationUrl;

            if (state == null || currentSite == null ||
                !string.Equals(state.Site.Id, currentSite.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            navigationUrl = string.IsNullOrWhiteSpace(state.CurrentNavigationUrl)
                ? state.Site.Url
                : state.CurrentNavigationUrl;

            if (e.IsSuccess)
            {
                RecordSiteNavigation(state, navigationUrl);
            }
            else
            {
                state.SuppressNextHistoryEntry = false;
            }

            SetTransientStatus(e.IsSuccess
                ? "\u5df2\u52a0\u8f7d " + state.Site.Name
                : "\u65e0\u6cd5\u8bbf\u95ee " + navigationUrl);
            SetWebState(
                e.IsSuccess ? string.Empty : "\u65e0\u6cd5\u8bbf\u95ee\u9875\u9762",
                e.IsSuccess ? string.Empty : navigationUrl,
                !e.IsSuccess);
            RefreshSiteButtons();
        }

        private void RecordSiteNavigation(SiteViewState state, string url)
        {
            string lastUrl;

            if (state == null || string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (state.NavigationHistory == null)
            {
                state.NavigationHistory = new List<string>();
            }

            if (state.SuppressNextHistoryEntry)
            {
                state.SuppressNextHistoryEntry = false;
                return;
            }

            if (state.NavigationHistory.Count > 0)
            {
                lastUrl = state.NavigationHistory[state.NavigationHistory.Count - 1];
                if (string.Equals(lastUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            state.NavigationHistory.Add(url);

            if (state.NavigationHistory.Count > 64)
            {
                state.NavigationHistory.RemoveAt(0);
            }
        }

        private SiteViewState GetSiteState(object sender)
        {
            WebView2 webView = sender as WebView2;

            if (webView == null)
            {
                return null;
            }

            return webView.Tag as SiteViewState;
        }

        private SiteViewState FindSiteState(CoreWebView2 coreWebView)
        {
            if (coreWebView == null)
            {
                return null;
            }

            foreach (SiteViewState state in siteViews.Values)
            {
                if (state != null &&
                    state.WebView != null &&
                    state.WebView.CoreWebView2 == coreWebView)
                {
                    return state;
                }
            }

            return null;
        }

        private void SetWebState(string title, string detail, bool canRetry)
        {
            if (webStateOverlay == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(detail))
            {
                webStateOverlay.Visible = false;
                return;
            }

            webStateTitleLabel.Text = title ?? string.Empty;
            webStateDetailLabel.Text = detail ?? string.Empty;
            webStateRetryButton.Visible = canRetry;
            webStateOverlay.Visible = true;
            webStateOverlay.BringToFront();
        }

        private void OnAddCommandClicked(object sender, EventArgs e)
        {
            using (CommandDialog dialog = new CommandDialog(null, false))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
                {
                    return;
                }

                commands.Add(dialog.Result);
                PersistAndSyncCommands();
                RefreshCommandList();
                SelectCommand(dialog.Result);
            }
        }

        private void OnEditCommandClicked(object sender, EventArgs e)
        {
            CommandEntry selectedCommand = currentCommand;
            bool commandReadOnly = false;
            CommandRuntimeSnapshot snapshot;

            if (selectedCommand == null)
            {
                return;
            }

            snapshot = commandManager.GetSnapshot(selectedCommand.Id);
            commandReadOnly = snapshot.Status == CommandStatus.Running ||
                snapshot.Status == CommandStatus.Starting ||
                snapshot.Status == CommandStatus.Stopping;

            using (CommandDialog dialog = new CommandDialog(CloneCommand(selectedCommand), commandReadOnly))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
                {
                    return;
                }

                CopyCommand(dialog.Result, selectedCommand);
                PersistAndSyncCommands();
                RefreshCommandList();
                SelectCommand(selectedCommand);
            }
        }

        private void OnDeleteCommandClicked(object sender, EventArgs e)
        {
            CommandEntry selectedCommand = currentCommand;
            CommandRuntimeSnapshot snapshot;

            if (selectedCommand == null)
            {
                return;
            }

            snapshot = commandManager.GetSnapshot(selectedCommand.Id);

            if (snapshot.Status == CommandStatus.Running ||
                snapshot.Status == CommandStatus.Starting ||
                snapshot.Status == CommandStatus.Stopping)
            {
                MessageBox.Show(
                    "\u8bf7\u5148\u505c\u6b62\u8be5\u547d\u4ee4\uff0c\u518d\u6267\u884c\u5220\u9664\u3002",
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                    "\u786e\u8ba4\u5220\u9664\u547d\u4ee4\u201c" + selectedCommand.Name + "\u201d\uff1f",
                    AppName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            commands.RemoveAll(delegate(CommandEntry command)
            {
                return string.Equals(command.Id, selectedCommand.Id, StringComparison.OrdinalIgnoreCase);
            });

            currentCommand = null;
            PersistAndSyncCommands();
            RefreshCommandList();

            if (commands.Count > 0)
            {
                SelectCommand(commands[0]);
            }
            else
            {
                UpdateCommandSelectionVisuals();
                RefreshCommandButtons();
                RefreshLogsView();
            }
        }

        private void OnStartStopCommandClicked(object sender, EventArgs e)
        {
            CommandRuntimeSnapshot snapshot;

            if (currentCommand == null)
            {
                return;
            }

            snapshot = commandManager.GetSnapshot(currentCommand.Id);

            if (snapshot.Status == CommandStatus.Running ||
                snapshot.Status == CommandStatus.Starting ||
                snapshot.Status == CommandStatus.Stopping ||
                snapshot.Status == CommandStatus.WaitingRetry)
            {
                commandManager.Stop(currentCommand.Id);
            }
            else
            {
                commandManager.Start(currentCommand.Id);
            }
        }

        private void OnRestartCommandClicked(object sender, EventArgs e)
        {
            if (currentCommand == null)
            {
                return;
            }

            commandManager.Restart(currentCommand.Id);
        }

        private void OnAddSiteClicked(object sender, EventArgs e)
        {
            using (SiteDialog dialog = new SiteDialog(null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
                {
                    return;
                }

                if (ContainsSiteUrl(dialog.Result.Url, null))
                {
                    MessageBox.Show(
                        "\u8be5\u5730\u5740\u5df2\u7ecf\u5b58\u5728\u4e8e\u7ad9\u70b9\u5217\u8868\u4e2d\u3002",
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                sites.Add(dialog.Result);
                PersistConfig();
                RefreshSiteList();
                SelectSite(dialog.Result);
            }
        }

        private void OnEditSiteClicked(object sender, EventArgs e)
        {
            SiteEntry selectedSite = currentSite;

            if (selectedSite == null)
            {
                return;
            }

            using (SiteDialog dialog = new SiteDialog(CloneSite(selectedSite)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
                {
                    return;
                }

                if (ContainsSiteUrl(dialog.Result.Url, selectedSite.Id))
                {
                    MessageBox.Show(
                        "\u8be5\u5730\u5740\u5df2\u7ecf\u5b58\u5728\u4e8e\u7ad9\u70b9\u5217\u8868\u4e2d\u3002",
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                CopySite(dialog.Result, selectedSite);
                PersistConfig();
                RefreshSiteList();
                SelectSite(selectedSite);
            }
        }

        private void OnDeleteSiteClicked(object sender, EventArgs e)
        {
            SiteEntry selectedSite = currentSite;
            SiteViewState viewState;

            if (selectedSite == null)
            {
                return;
            }

            if (sites.Count <= 1)
            {
                MessageBox.Show(
                    "\u81f3\u5c11\u9700\u8981\u4fdd\u7559\u4e00\u4e2a\u7ad9\u70b9\u3002",
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                    "\u786e\u8ba4\u5220\u9664\u7ad9\u70b9\u201c" + selectedSite.Name + "\u201d\uff1f",
                    AppName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            sites.RemoveAll(delegate(SiteEntry site)
            {
                return string.Equals(site.Id, selectedSite.Id, StringComparison.OrdinalIgnoreCase);
            });

            if (siteViews.TryGetValue(selectedSite.Id, out viewState))
            {
                webViewHost.Controls.Remove(viewState.WebView);
                viewState.WebView.NavigationStarting -= OnNavigationStarting;
                viewState.WebView.NavigationCompleted -= OnNavigationCompleted;
                if (viewState.WebView.CoreWebView2 != null)
                {
                    viewState.WebView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
                }
                viewState.WebView.Dispose();
                siteViews.Remove(selectedSite.Id);
            }

            currentSite = null;
            PersistConfig();
            RefreshSiteList();

            if (sites.Count > 0)
            {
                SelectSite(sites[0]);
            }
            else
            {
                UpdateSiteSelectionVisuals();
                RefreshSiteButtons();
            }
        }

        private void OnOpenSiteClicked(object sender, EventArgs e)
        {
            if (currentSite != null)
            {
                SetWorkspaceMode(WorkspaceMode.Web);
                ShowSite(currentSite);
            }
        }

        private void OnReloadSiteClicked(object sender, EventArgs e)
        {
            ReloadCurrentSite();
        }

        private void OnBackSiteClicked(object sender, EventArgs e)
        {
            GoBackCurrentSite();
        }

        private void OnHomeSiteClicked(object sender, EventArgs e)
        {
            NavigateCurrentSiteHome();
        }

        private void GoBackCurrentSite()
        {
            SiteViewState state;
            string previousUrl;

            if (currentSite == null)
            {
                SetTransientStatus("\u5f53\u524d\u672a\u9009\u62e9\u7ad9\u70b9\u3002");
                return;
            }

            if (siteViews.TryGetValue(currentSite.Id, out state) &&
                state.IsInitialized &&
                state.WebView.CoreWebView2 != null)
            {
                SetWorkspaceMode(WorkspaceMode.Web);

                if (state.WebView.CoreWebView2.CanGoBack)
                {
                    PopCurrentSiteHistory(state);
                    state.WebView.CoreWebView2.GoBack();
                    SetTransientStatus("\u6b63\u5728\u8fd4\u56de\u4e0a\u4e00\u9875...");
                    RefreshSiteButtons();
                    return;
                }

                if (TryGetPreviousSiteUrl(state, out previousUrl))
                {
                    PopCurrentSiteHistory(state);
                    state.SuppressNextHistoryEntry = true;
                    state.WebView.CoreWebView2.Navigate(previousUrl);
                    SetTransientStatus("\u6b63\u5728\u8fd4\u56de\u4e0a\u4e00\u9875...");
                    SetWebState("\u6b63\u5728\u8fd4\u56de\u4e0a\u4e00\u9875", previousUrl, false);
                    RefreshSiteButtons();
                    return;
                }
            }

            SetTransientStatus("\u5f53\u524d\u9875\u9762\u6ca1\u6709\u53ef\u8fd4\u56de\u7684\u5386\u53f2\u3002");
        }

        private void NavigateCurrentSiteHome()
        {
            SiteViewState state;

            if (currentSite == null)
            {
                SetTransientStatus("\u5f53\u524d\u672a\u9009\u62e9\u7ad9\u70b9\u3002");
                return;
            }

            if (siteViews.TryGetValue(currentSite.Id, out state) &&
                state.IsInitialized &&
                state.WebView.CoreWebView2 != null)
            {
                SetWorkspaceMode(WorkspaceMode.Web);
                state.LastNavigatedUrl = currentSite.Url;
                state.WebView.CoreWebView2.Navigate(currentSite.Url);
                SetTransientStatus("\u6b63\u5728\u56de\u5230 " + currentSite.Name + " \u4e3b\u9875...");
                SetWebState("\u6b63\u5728\u6253\u5f00 " + currentSite.Name, currentSite.Url, false);
                RefreshSiteButtons();
                return;
            }

            ShowSite(currentSite);
        }

        private static bool TryGetPreviousSiteUrl(SiteViewState state, out string previousUrl)
        {
            previousUrl = null;

            if (state == null ||
                state.NavigationHistory == null ||
                state.NavigationHistory.Count < 2)
            {
                return false;
            }

            previousUrl = state.NavigationHistory[state.NavigationHistory.Count - 2];
            return !string.IsNullOrWhiteSpace(previousUrl);
        }

        private static void PopCurrentSiteHistory(SiteViewState state)
        {
            if (state == null ||
                state.NavigationHistory == null ||
                state.NavigationHistory.Count < 2)
            {
                return;
            }

            state.NavigationHistory.RemoveAt(state.NavigationHistory.Count - 1);
            state.SuppressNextHistoryEntry = true;
        }

        private void ReloadCurrentSite()
        {
            SiteViewState state;

            if (currentSite == null)
            {
                SetTransientStatus("\u5f53\u524d\u672a\u9009\u62e9\u7ad9\u70b9\u3002");
                return;
            }

            if (siteViews.TryGetValue(currentSite.Id, out state) &&
                state.IsInitialized &&
                state.WebView.CoreWebView2 != null)
            {
                if (!string.Equals(state.LastNavigatedUrl, currentSite.Url, StringComparison.OrdinalIgnoreCase))
                {
                    state.LastNavigatedUrl = currentSite.Url;
                    state.WebView.CoreWebView2.Navigate(currentSite.Url);
                }
                else
                {
                    state.WebView.CoreWebView2.Reload();
                }

                SetTransientStatus("\u6b63\u5728\u5237\u65b0 " + currentSite.Name + "...");
                SetWebState("\u6b63\u5728\u5237\u65b0 " + currentSite.Name, currentSite.Url, false);
                return;
            }

            ShowSite(currentSite);
        }

        private void OnClearLogsClicked(object sender, EventArgs e)
        {
            if (currentCommand != null)
            {
                commandManager.ClearLogs(currentCommand.Id);
                RefreshLogsView();
            }
        }

        private void OnCopyLogsClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(logsTextBox.Text))
            {
                try
                {
                    Clipboard.SetText(logsTextBox.Text);
                    SetTransientStatus("\u65e5\u5fd7\u5df2\u590d\u5236\u5230\u526a\u8d34\u677f\u3002");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "\u65e0\u6cd5\u590d\u5236\u65e5\u5fd7\u3002\r\n\r\n" + ex.Message,
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void OnAutoScrollLogsChanged(object sender, EventArgs e)
        {
            if (!autoScrollLogsCheckBox.Checked)
            {
                lastLogAutoScrollEnabled = false;
                return;
            }

            lastLogAutoScrollEnabled = true;
            logsTextBox.SelectionStart = logsTextBox.TextLength;
            logsTextBox.ScrollToCaret();
        }

        private void RefreshCommandButtons()
        {
            CommandRuntimeSnapshot snapshot = currentCommand == null
                ? null
                : commandManager.GetSnapshot(currentCommand.Id);
            bool hasCommand = currentCommand != null;
            bool isBusy = snapshot != null &&
                (snapshot.Status == CommandStatus.Starting || snapshot.Status == CommandStatus.Stopping);
            bool isActive = snapshot != null &&
                (snapshot.Status == CommandStatus.Running ||
                 snapshot.Status == CommandStatus.Starting ||
                 snapshot.Status == CommandStatus.Stopping ||
                 snapshot.Status == CommandStatus.WaitingRetry);

            sidebarSurface.EditCommandEnabled = hasCommand;
            sidebarSurface.DeleteCommandEnabled = hasCommand && !isActive;
            sidebarSurface.RestartCommandEnabled = hasCommand && !isBusy;
            sidebarSurface.StartStopCommandEnabled = hasCommand;
            sidebarSurface.StartStopCommandText = isActive ? "\u505c\u6b62" : "\u542f\u52a8";

            if (!hasCommand)
            {
                sidebarSurface.EditCommandEnabled = false;
                sidebarSurface.DeleteCommandEnabled = false;
                sidebarSurface.RestartCommandEnabled = false;
                sidebarSurface.StartStopCommandEnabled = false;
                sidebarSurface.StartStopCommandText = "\u542f\u52a8";
                sidebarSurface.Invalidate();
                return;
            }

            sidebarSurface.RestartCommandEnabled = hasCommand && !isBusy;
            sidebarSurface.StartStopCommandText = isActive ? "\u505c\u6b62" : "\u542f\u52a8";
            sidebarSurface.Invalidate();
        }

        private void RefreshSiteButtons()
        {
            bool hasSite = currentSite != null;

            sidebarSurface.EditSiteEnabled = hasSite;
            sidebarSurface.DeleteSiteEnabled = hasSite && sites.Count > 1;
            sidebarSurface.OpenSiteEnabled = hasSite;
            sidebarSurface.BackSiteEnabled = CanCurrentSiteGoBack();
            sidebarSurface.HomeSiteEnabled = hasSite;
            sidebarSurface.ReloadSiteEnabled = hasSite;
            sidebarSurface.Invalidate();
        }

        private bool CanCurrentSiteGoBack()
        {
            SiteViewState state;

            return currentSite != null &&
                siteViews.TryGetValue(currentSite.Id, out state) &&
                state.IsInitialized &&
                state.WebView.CoreWebView2 != null &&
                (state.WebView.CoreWebView2.CanGoBack ||
                 (state.NavigationHistory != null && state.NavigationHistory.Count > 1));
        }

        private void RefreshLogsView()
        {
            CommandRuntimeSnapshot snapshot;
            CommandLogSnapshot logSnapshot;
            string[] lines;

            if (currentCommand == null)
            {
                currentCommandLabel.Text = "\u672a\u9009\u62e9\u547d\u4ee4";
                commandStatusBadge.Text = "\u5df2\u505c\u6b62";
                ApplyBadgeStyle(commandStatusBadge, CommandStatus.Stopped);
                logsTextBox.Text = string.Empty;
                clearLogsButton.Enabled = false;
                copyLogsButton.Enabled = false;
                lastLogAutoScrollEnabled = true;
                ResetRenderedLogState();
                return;
            }

            snapshot = commandManager.GetSnapshot(currentCommand.Id);
            logSnapshot = commandManager.GetLogSnapshot(currentCommand.Id);
            lines = logSnapshot.Lines ?? new string[0];
            currentCommandLabel.Text = currentCommand.Name;
            commandStatusBadge.Text = snapshot.GetDisplayStatus();
            ApplyBadgeStyle(commandStatusBadge, snapshot.Status);
            clearLogsButton.Enabled = lines.Length > 0;
            copyLogsButton.Enabled = lines.Length > 0;
            bool shouldAutoScroll = autoScrollLogsCheckBox.Checked || IsNearBottom(logsTextBox);
            UpdateLogsText(currentCommand.Id, logSnapshot, shouldAutoScroll);

            if (lines.Length > 0 && autoScrollLogsCheckBox.Checked && (shouldAutoScroll || lastLogAutoScrollEnabled))
            {
                logsTextBox.SelectionStart = logsTextBox.TextLength;
                logsTextBox.ScrollToCaret();
            }

            lastLogAutoScrollEnabled = autoScrollLogsCheckBox.Checked && shouldAutoScroll;
        }

        private void UpdateLogsText(string commandId, CommandLogSnapshot snapshot, bool shouldAutoScroll)
        {
            string[] lines = snapshot == null || snapshot.Lines == null
                ? new string[0]
                : snapshot.Lines;

            if (snapshot != null &&
                string.Equals(renderedLogCommandId, commandId, StringComparison.OrdinalIgnoreCase) &&
                renderedLogFirstSequence == snapshot.FirstSequence &&
                renderedLogNextSequence == snapshot.NextSequence)
            {
                return;
            }

            int firstVisibleLine = shouldAutoScroll ? 0 : GetFirstVisibleLine(logsTextBox);
            int selectionStart = shouldAutoScroll ? 0 : logsTextBox.SelectionStart;
            int selectionLength = shouldAutoScroll ? 0 : logsTextBox.SelectionLength;

            if (CanAppendLogLines(commandId, snapshot, lines))
            {
                AppendLogLines(snapshot, lines);
            }
            else
            {
                string newText = string.Join(Environment.NewLine, lines);

                if (!string.Equals(logsTextBox.Text, newText, StringComparison.Ordinal))
                {
                    logsTextBox.Text = newText;
                }
            }

            renderedLogCommandId = commandId;
            renderedLogFirstSequence = snapshot == null ? 0 : snapshot.FirstSequence;
            renderedLogNextSequence = snapshot == null ? 0 : snapshot.NextSequence;

            if (!shouldAutoScroll)
            {
                selectionStart = Math.Min(selectionStart, logsTextBox.TextLength);
                selectionLength = Math.Min(selectionLength, logsTextBox.TextLength - selectionStart);
                logsTextBox.Select(selectionStart, selectionLength);
                ScrollTextBoxToFirstVisibleLine(logsTextBox, firstVisibleLine);
            }
        }

        private bool CanAppendLogLines(string commandId, CommandLogSnapshot snapshot, string[] lines)
        {
            int startIndex;

            if (snapshot == null ||
                lines == null ||
                lines.Length == 0 ||
                string.IsNullOrWhiteSpace(commandId) ||
                !string.Equals(renderedLogCommandId, commandId, StringComparison.OrdinalIgnoreCase) ||
                snapshot.FirstSequence != renderedLogFirstSequence ||
                snapshot.NextSequence < renderedLogNextSequence)
            {
                return false;
            }

            startIndex = renderedLogNextSequence - snapshot.FirstSequence;
            return startIndex >= 0 && startIndex < lines.Length;
        }

        private void AppendLogLines(CommandLogSnapshot snapshot, string[] lines)
        {
            int startIndex = renderedLogNextSequence - snapshot.FirstSequence;
            StringBuilder builder = new StringBuilder();

            for (int index = startIndex; index < lines.Length; index++)
            {
                if (logsTextBox.TextLength > 0 || builder.Length > 0)
                {
                    builder.Append(Environment.NewLine);
                }

                builder.Append(lines[index]);
            }

            if (builder.Length > 0)
            {
                logsTextBox.AppendText(builder.ToString());
            }
        }

        private void ResetRenderedLogState()
        {
            renderedLogCommandId = null;
            renderedLogFirstSequence = 0;
            renderedLogNextSequence = 0;
        }

        private void SetWorkspaceMode(WorkspaceMode mode)
        {
            workspaceMode = mode;
            webPanel.Visible = mode == WorkspaceMode.Web;
            logsPanel.Visible = mode == WorkspaceMode.Logs;
            sidebarSurface.WorkspaceMode = mode;
            sidebarSurface.Invalidate();
            SetWindowTitle(mode == WorkspaceMode.Web
                ? currentSite == null ? AppName : AppName + " - " + currentSite.Name
                : currentCommand == null ? AppName : AppName + " - " + currentCommand.Name);

            if (mode == WorkspaceMode.Web && currentSite != null)
            {
                ShowSite(currentSite);
            }
            else if (mode == WorkspaceMode.Logs)
            {
                RefreshLogsView();
            }
        }

        private void SetWindowTitle(string title)
        {
            string displayTitle = title ?? AppName;

            Text = displayTitle;

            if (titleBarLabel != null)
            {
                titleBarLabel.Text = displayTitle;
            }
        }

        private void OnSidebarSplitterMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            resizingSidebar = true;
            sidebarDragStartX = PointToClient(sidebarSplitter.PointToScreen(e.Location)).X;
            sidebarDragStartWidth = GetCurrentSidebarLayoutWidth();
            sidebarPendingWidth = sidebarDragStartWidth;
            sidebarSplitter.Active = true;
            sidebarSplitter.Capture = true;
            workspacePanel.SendToBack();
            leftSidebar.BringToFront();
            sidebarSplitter.BringToFront();
        }

        private void OnSidebarSplitterMouseMove(object sender, MouseEventArgs e)
        {
            int currentX;
            int delta;
            int targetWidth;

            if (!resizingSidebar)
            {
                return;
            }

            currentX = PointToClient(sidebarSplitter.PointToScreen(e.Location)).X;
            delta = currentX - sidebarDragStartX;
            targetWidth = sidebarDragStartWidth + delta;

            sidebarPendingWidth = targetWidth;
            ApplyPendingSidebarResize();
        }

        private void OnSidebarSplitterMouseUp(object sender, MouseEventArgs e)
        {
            int currentX;
            int delta;

            if (!resizingSidebar)
            {
                return;
            }

            currentX = PointToClient(sidebarSplitter.PointToScreen(e.Location)).X;
            delta = currentX - sidebarDragStartX;
            sidebarPendingWidth = sidebarDragStartWidth + delta;
            resizingSidebar = false;
            sidebarSplitter.Capture = false;
            sidebarSplitter.Active = false;
            CommitPendingSidebarResize();
            SnapSidebarWidth();
        }

        private void OnSidebarToggleClicked(object sender, EventArgs e)
        {
            if (sidebarHidden)
            {
                SetSidebarWidth(expandedSidebarWidth <= 0 ? DefaultSidebarWidth : expandedSidebarWidth);
                SetTransientStatus("\u5de6\u4fa7\u9762\u677f\u5df2\u5c55\u5f00\u3002", 2);
                return;
            }

            SetSidebarWidth(0);
            SetTransientStatus("\u5de6\u4fa7\u9762\u677f\u5df2\u6298\u53e0\u3002", 2);
        }

        private void ApplyPendingSidebarResize()
        {
            SetSidebarWidth(sidebarPendingWidth);
        }

        private void CommitPendingSidebarResize()
        {
            SuspendRedraw(rootPanel);

            try
            {
                ApplyPendingSidebarResize();
                LayoutShellPanels(true);
            }
            finally
            {
                ResumeRedraw(rootPanel);
            }
        }

        private int GetCurrentSidebarLayoutWidth()
        {
            return sidebarHidden ? 0 : expandedSidebarWidth;
        }

        private void SetSidebarWidth(int requestedWidth)
        {
            int width;
            int currentWidth;
            bool needsFinalWorkspaceLayout;

            width = GetEffectiveSidebarWidth(requestedWidth);
            needsFinalWorkspaceLayout = !resizingSidebar && rightBody.Width != GetWorkspaceContentWidth();

            if (width <= 0)
            {
                if (sidebarHidden && !leftSidebar.Visible)
                {
                    if (needsFinalWorkspaceLayout)
                    {
                        LayoutShellPanels(true);
                    }

                    return;
                }

                sidebarHidden = true;
                leftSidebar.Visible = false;
                sidebarSplitter.Collapsed = true;
                titleSidebarButton.SidebarCollapsed = true;
                LayoutShellPanels(true);
                return;
            }

            currentWidth = GetCurrentSidebarLayoutWidth();

            if (!sidebarHidden &&
                leftSidebar.Visible &&
                Math.Abs(currentWidth - width) < 2 &&
                !needsFinalWorkspaceLayout)
            {
                return;
            }

            sidebarHidden = false;
            expandedSidebarWidth = width;
            leftSidebar.Visible = true;
            sidebarSplitter.Collapsed = false;
            titleSidebarButton.SidebarCollapsed = false;
            LayoutShellPanels(true);
        }

        private int GetEffectiveSidebarWidth(int requestedWidth)
        {
            if (requestedWidth <= SidebarCollapseThreshold)
            {
                return 0;
            }

            return Math.Max(SidebarMinExpandedWidth, Math.Min(SidebarMaxWidth, requestedWidth));
        }

        private void OnRootPanelResize(object sender, EventArgs e)
        {
            LayoutShellPanels(true);
        }

        private void LayoutShellPanels()
        {
            LayoutShellPanels(true);
        }

        private void LayoutShellPanels(bool resizeWorkspaceContent)
        {
            int sidebarWidth = sidebarHidden ? 0 : expandedSidebarWidth;
            int splitterWidth = Math.Min(SidebarSplitterWidth, rootPanel.ClientSize.Width);
            int workspaceX = Math.Min(rootPanel.ClientSize.Width, sidebarWidth + splitterWidth);
            int workspaceWidth = Math.Max(0, rootPanel.ClientSize.Width - workspaceX);
            int height = rootPanel.ClientSize.Height;

            rootPanel.SuspendLayout();
            workspacePanel.SuspendLayout();
            leftSidebar.SuspendLayout();

            try
            {
                SetBoundsIfChanged(leftSidebar, 0, 0, sidebarWidth, height);
                SetBoundsIfChanged(sidebarSplitter, sidebarWidth, 0, splitterWidth, height);
                LayoutSidebarContent();

                if (resizeWorkspaceContent)
                {
                    SetBoundsIfChanged(workspacePanel, workspaceX, 0, workspaceWidth, height);
                    LayoutWorkspaceContent(true);
                }
            }
            finally
            {
                leftSidebar.ResumeLayout(false);
                workspacePanel.ResumeLayout(false);
                rootPanel.ResumeLayout(false);
            }
        }

        private static void SetBoundsIfChanged(Control control, int x, int y, int width, int height)
        {
            Rectangle bounds = new Rectangle(x, y, width, height);

            if (control.Bounds == bounds)
            {
                return;
            }

            control.Bounds = bounds;
        }

        private static void SuspendRedraw(Control control)
        {
            if (control != null && control.IsHandleCreated)
            {
                SendMessage(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private static void ResumeRedraw(Control control)
        {
            if (control == null)
            {
                return;
            }

            if (control.IsHandleCreated)
            {
                SendMessage(control.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            }

            control.Invalidate(true);
            control.Update();
        }

        private void LayoutSidebarContent()
        {
            int contentWidth = sidebarHidden ? 0 : expandedSidebarWidth;

            SetSidebarContentBounds(contentWidth);
        }

        private void SetSidebarContentBounds(int contentWidth)
        {
            SetBoundsIfChanged(
                sidebarSurface,
                0,
                0,
                Math.Max(0, contentWidth),
                leftSidebar.Height);
        }

        private void LayoutWorkspaceContent(bool resizeContent)
        {
            int contentWidth = GetWorkspaceContentWidth();

            SetWorkspaceContentBounds(contentWidth);
        }

        private int GetWorkspaceContentWidth()
        {
            return Math.Max(0, workspacePanel.ClientSize.Width - workspacePanel.Padding.Horizontal);
        }

        private void SetWorkspaceContentBounds(int contentWidth)
        {
            int contentHeight = Math.Max(0, workspacePanel.ClientSize.Height - workspacePanel.Padding.Vertical);

            SetBoundsIfChanged(
                rightBody,
                workspacePanel.Padding.Left,
                workspacePanel.Padding.Top,
                Math.Max(0, contentWidth),
                contentHeight);
        }

        private void SnapSidebarWidth()
        {
            if (sidebarHidden)
            {
                SetTransientStatus("\u5de6\u4fa7\u63a7\u5236\u53f0\u5df2\u6298\u53e0\u3002");
                return;
            }

            SetTransientStatus("\u5de6\u4fa7\u63a7\u5236\u53f0\u5bbd\u5ea6\u5df2\u8c03\u6574\u3002", 2);
        }

        private void UpdateStatusSummary()
        {
            int running = commandManager.GetRunningCount();
            int waitingRetry = commandManager.GetWaitingRetryCount();
            string startupText = WindowsStartupManager.IsEnabled()
                ? "\u5df2\u542f\u7528\u81ea\u542f"
                : "\u672a\u542f\u7528\u81ea\u542f";

            sidebarSurface.SummaryText =
                "\u547d\u4ee4 " + commands.Count + " \u4e2a\uff0c\u8fd0\u884c\u4e2d " +
                running + " \u4e2a\uff0c\u7b49\u5f85\u91cd\u8bd5 " +
                waitingRetry + " \u4e2a\uff0c\u7ad9\u70b9 " +
                sites.Count + " \u4e2a\uff0c" +
                startupText + "\u3002";
            sidebarSurface.Invalidate();
            notifyIcon.Text = AppName + " - \u8fd0\u884c\u4e2d " + running + "/" + commands.Count;

            if (DateTime.UtcNow >= statusSummaryHoldUntilUtc)
            {
                statusLabel.Text = "\u8fd0\u884c\u4e2d " + running + "/" + commands.Count +
                    "\uff0c\u7b49\u5f85\u91cd\u8bd5 " + waitingRetry +
                    "\uff0c\u7ad9\u70b9 " + sites.Count + "\u3002";
            }
        }

        private void SetTransientStatus(string message)
        {
            SetTransientStatus(message, 3);
        }

        private void SetTransientStatus(string message, int holdSeconds)
        {
            statusLabel.Text = message ?? string.Empty;
            statusSummaryHoldUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, holdSeconds));
        }

        private void RefreshCommandCardsState()
        {
            sidebarSurface.Invalidate();
        }

        private void RefreshCommandCardState(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            sidebarSurface.RefreshCommand(commandId);
        }

        private void UpdateCommandSelectionVisuals()
        {
            sidebarSurface.SelectedCommandId = currentCommand == null ? null : currentCommand.Id;
            sidebarSurface.Invalidate();
        }

        private void UpdateSiteSelectionVisuals()
        {
            sidebarSurface.SelectedSiteId = currentSite == null ? null : currentSite.Id;
            sidebarSurface.Invalidate();
        }

        private void RefreshEmptyStates()
        {
            sidebarSurface.Invalidate();
        }

        private void PersistAndSyncCommands()
        {
            PersistConfig();
            commandManager.SyncCommands(commands);
        }

        private void PersistConfig()
        {
            AppConfigStore.Save(new AppConfig
            {
                Sites = sites.ToArray(),
                Commands = commands.ToArray(),
                GlobalHotkey = pendingHotkey,
                CommandSectionRatio = sidebarSurface.CommandSectionRatio
            });
        }

        private void ExportConfig()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "导出配置";
                dialog.Filter = "Switch 配置 (*.json)|*.json";
                dialog.FileName = "switch-config.json";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    AppConfigStore.SaveTo(dialog.FileName, new AppConfig
                    {
                        Sites = sites.ToArray(),
                        Commands = commands.ToArray(),
                        GlobalHotkey = pendingHotkey,
                        CommandSectionRatio = sidebarSurface.CommandSectionRatio
                    });
                    MessageBox.Show("配置已导出。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出配置失败。\r\n\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportConfig()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "导入配置";
                dialog.Filter = "Switch 配置 (*.json)|*.json";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                AppConfig loaded = AppConfigStore.LoadFrom(dialog.FileName);

                if (loaded == null)
                {
                    MessageBox.Show("无法读取该配置文件。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show(
                        "导入将覆盖当前的站点、命令、快捷键和布局，确定继续吗？",
                        AppName,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    sites.Clear();
                    if (loaded.Sites != null)
                    {
                        sites.AddRange(loaded.Sites);
                    }

                    commands.Clear();
                    if (loaded.Commands != null)
                    {
                        commands.AddRange(loaded.Commands);
                    }

                    commandManager.SyncCommands(commands);
                    pendingHotkey = loaded.GlobalHotkey ?? HotkeyConstants.CreateDefault();
                    sidebarSurface.CommandSectionRatio = loaded.CommandSectionRatio;

                    PersistConfig();
                    TryRegisterHotkey();
                    UpdateHotkeyMenuText();
                    RefreshCommandList();
                    RefreshSiteList();
                    RestartSiteHealthProbe();
                    SetTransientStatus("配置已导入。");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导入配置失败。\r\n\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyBadgeStyle(Label badge, CommandStatus status)
        {
            if (status == CommandStatus.Running)
            {
                badge.BackColor = UiTheme.SuccessBackground;
                badge.ForeColor = UiTheme.SuccessForeground;
                badge.Invalidate();
                return;
            }

            if (status == CommandStatus.Error)
            {
                badge.BackColor = UiTheme.DangerBackground;
                badge.ForeColor = UiTheme.DangerForeground;
                badge.Invalidate();
                return;
            }

            if (status == CommandStatus.Starting ||
                status == CommandStatus.Stopping ||
                status == CommandStatus.WaitingRetry)
            {
                badge.BackColor = UiTheme.WarningBackground;
                badge.ForeColor = UiTheme.WarningForeground;
                badge.Invalidate();
                return;
            }

            badge.BackColor = UiTheme.BadgeNeutralBackground;
            badge.ForeColor = UiTheme.BadgeNeutralForeground;
            badge.Invalidate();
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (maximizeButton != null)
            {
                maximizeButton.Maximized = WindowState == FormWindowState.Maximized;
            }
        }

        private void OnTitleBarResize(object sender, EventArgs e)
        {
            LayoutTitleBarControls();
        }

        private void LayoutTitleBarControls()
        {
            const int windowButtonWidth = 50;
            int right = Math.Max(0, titleBarPanel.ClientSize.Width);
            int labelRight;

            closeButton.SetBounds(right - windowButtonWidth, 0, windowButtonWidth, TitleBarHeight);
            maximizeButton.SetBounds(closeButton.Left - windowButtonWidth, 0, windowButtonWidth, TitleBarHeight);
            minimizeButton.SetBounds(maximizeButton.Left - windowButtonWidth, 0, windowButtonWidth, TitleBarHeight);
            titleSidebarButton.SetBounds(10, 6, 36, 32);

            labelRight = Math.Max(58, minimizeButton.Left - 8);
            titleBarLabel.SetBounds(58, 0, Math.Max(0, labelRight - 58), TitleBarHeight);
        }

        private void OnTitleBarMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (e.Clicks > 1)
            {
                titleBarDragPending = false;
                HandleTitleBarDoubleClick();
                return;
            }

            titleBarDragPending = true;
            titleBarDragStartScreen = GetTitleBarMouseScreenPoint(sender, e.Location);
        }

        private void OnTitleBarMouseMove(object sender, MouseEventArgs e)
        {
            Point currentScreen;
            Size dragSize;
            Rectangle dragBounds;

            if (!titleBarDragPending || e.Button != MouseButtons.Left)
            {
                return;
            }

            currentScreen = GetTitleBarMouseScreenPoint(sender, e.Location);
            dragSize = SystemInformation.DragSize;
            dragBounds = new Rectangle(
                titleBarDragStartScreen.X - (dragSize.Width / 2),
                titleBarDragStartScreen.Y - (dragSize.Height / 2),
                Math.Max(1, dragSize.Width),
                Math.Max(1, dragSize.Height));

            if (dragBounds.Contains(currentScreen))
            {
                return;
            }

            titleBarDragPending = false;

            if (WindowState == FormWindowState.Maximized)
            {
                RestoreMaximizedForDrag(titleBarDragStartScreen);
            }

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
        }

        // Dragging a maximized borderless window does nothing on its own -- the system
        // will not move a maximized window. Restore it first and reposition it under the
        // cursor (keeping the same horizontal grip point) so the drag continues smoothly,
        // matching how standard Windows chrome behaves.
        private void RestoreMaximizedForDrag(Point dragStartScreen)
        {
            Size restoreSize = RestoreBounds.Size;

            if (restoreSize.Width <= 0 || restoreSize.Height <= 0)
            {
                Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                restoreSize = new Size(
                    Math.Min(workingArea.Width - 80, 1280),
                    Math.Min(workingArea.Height - 80, 820));
            }

            Rectangle maxBounds = Bounds;
            float ratio = maxBounds.Width > 0
                ? (float)(dragStartScreen.X - maxBounds.Left) / maxBounds.Width
                : 0.5f;
            ratio = Math.Max(0f, Math.Min(1f, ratio));

            Point cursor = Cursor.Position;
            int newX = cursor.X - (int)(restoreSize.Width * ratio);
            int newY = cursor.Y - (TitleBarHeight / 2);

            WindowState = FormWindowState.Normal;
            Bounds = new Rectangle(newX, newY, restoreSize.Width, restoreSize.Height);
            maximizeButton.Maximized = false;
        }

        private void OnTitleBarMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                titleBarDragPending = false;
            }
        }

        private void OnTitleBarMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                HandleTitleBarDoubleClick();
            }
        }

        private Point GetTitleBarMouseScreenPoint(object sender, Point location)
        {
            Control control = sender as Control;

            return control == null
                ? PointToScreen(location)
                : control.PointToScreen(location);
        }

        private void HandleTitleBarDoubleClick()
        {
            DateTime now = DateTime.UtcNow;

            if ((now - lastTitleBarDoubleClickHandledUtc).TotalMilliseconds <= SystemInformation.DoubleClickTime)
            {
                return;
            }

            lastTitleBarDoubleClickHandledUtc = now;
            titleBarDragPending = false;
            ToggleWindowMaximized();
        }

        private void OnTitleMinimizeClicked(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void OnTitleMaximizeClicked(object sender, EventArgs e)
        {
            ToggleWindowMaximized();
        }

        private void OnTitleCloseClicked(object sender, EventArgs e)
        {
            QueueHideToTray();
        }

        private void ToggleWindowMaximized()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            maximizeButton.Maximized = WindowState == FormWindowState.Maximized;
        }

        private void OnTrayStartupMenuClicked(object sender, EventArgs e)
        {
            if (updatingStartupToggle)
            {
                return;
            }

            try
            {
                WindowsStartupManager.SetEnabled(trayStartupMenuItem.Checked);
                UpdateStatusSummary();
                SetTransientStatus(trayStartupMenuItem.Checked
                    ? "\u5df2\u5f00\u542f\u5f00\u673a\u81ea\u542f\u3002"
                    : "\u5df2\u5173\u95ed\u5f00\u673a\u81ea\u542f\u3002");
            }
            catch (Exception ex)
            {
                updatingStartupToggle = true;
                trayStartupMenuItem.Checked = WindowsStartupManager.IsEnabled();
                updatingStartupToggle = false;
                MessageBox.Show(
                    "\u65e0\u6cd5\u66f4\u65b0\u5f00\u673a\u81ea\u542f\u8bbe\u7f6e\u3002\r\n\r\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnStopAllCommandsClicked(object sender, EventArgs e)
        {
            ConfirmAndStopAll();
        }

        private void ConfirmAndStopAll()
        {
            if (!commandManager.HasActiveOrPendingCommands())
            {
                return;
            }

            if (MessageBox.Show(
                    "\u786e\u5b9a\u505c\u6b62\u6240\u6709\u6b63\u5728\u8fd0\u884c\u7684\u547d\u4ee4\u5417\uff1f",
                    AppName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            commandManager.StopAll();
            SetTransientStatus("\u6b63\u5728\u505c\u6b62\u6240\u6709\u547d\u4ee4...");
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (allowExit ||
                e.CloseReason == CloseReason.ApplicationExitCall ||
                e.CloseReason == CloseReason.WindowsShutDown ||
                e.CloseReason == CloseReason.TaskManagerClosing)
            {
                notifyIcon.Visible = false;
                uiRefreshTimer.Stop();
                runtimeRefreshTimer.Stop();
                commandManager.Dispose();
                return;
            }

            e.Cancel = true;
            QueueHideToTray();
        }

        private void QueueHideToTray()
        {
            if (hidingToTray || !IsHandleCreated)
            {
                return;
            }

            hidingToTray = true;
            BeginInvoke(new Action(
                delegate
                {
                    try
                    {
                        HideToTray();
                    }
                    finally
                    {
                        hidingToTray = false;
                    }
                }));
        }

        private void HideToTray()
        {
            if (!Visible)
            {
                return;
            }

            // A hidden top-level window has no taskbar button, so we hide via
            // Visible instead of toggling ShowInTaskbar. Toggling ShowInTaskbar
            // recreates the window handle, which stalls the UI for ~1-2s while a
            // live WebView2 re-attaches -- that was the restore delay.
            preTrayWindowState = WindowState == FormWindowState.Minimized
                ? FormWindowState.Normal
                : WindowState;
            Hide();

            if (trayHintShown)
            {
                return;
            }

            notifyIcon.BalloonTipTitle = AppName;
            notifyIcon.BalloonTipText =
                "Switch \u5df2\u7f29\u5c0f\u5230\u7cfb\u7edf\u6258\u76d8\uff0c\u53cc\u51fb\u56fe\u6807\u53ef\u4ee5\u6062\u590d\u4e3b\u754c\u9762\u3002";
            notifyIcon.ShowBalloonTip(2500);
            trayHintShown = true;
        }

        private void RestoreFromTray()
        {
            hidingToTray = false;
            if (!Visible)
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = preTrayWindowState;
                }
                Show();
            }
            Activate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryRegisterHotkey();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotkey();
            base.OnHandleDestroyed(e);
        }

        private void TryRegisterHotkey()
        {
            UnregisterHotkey();

            if (pendingHotkey == null || !pendingHotkey.Enabled)
            {
                return;
            }

            int modifiers = pendingHotkey.Modifiers | HotkeyConstants.ModNoRepeat;

            if (RegisterHotKey(Handle, ShowHotkeyId, modifiers, pendingHotkey.Key))
            {
                hotkeyRegistered = true;
                return;
            }

            hotkeyRegistered = false;
            SetTransientStatus("\u5168\u5c40\u5feb\u6377\u952e\u6ce8\u518c\u5931\u8d25\uff1a\u53ef\u80fd\u5df2\u88ab\u5176\u4ed6\u7a0b\u5e8f\u5360\u7528\u3002");

            if (notifyIcon != null && notifyIcon.Visible)
            {
                notifyIcon.BalloonTipTitle = AppName;
                notifyIcon.BalloonTipText =
                    "\u5f53\u524d\u5feb\u6377\u952e\u53ef\u80fd\u5df2\u88ab\u5176\u4ed6\u7a0b\u5e8f\u5360\u7528\uff0c\u8bf7\u5728\u8bbe\u7f6e\u4e2d\u6362\u4e00\u4e2a\u7ec4\u5408\u3002";
                notifyIcon.ShowBalloonTip(2500);
            }
        }

        private void UnregisterHotkey()
        {
            if (hotkeyRegistered)
            {
                UnregisterHotKey(Handle, ShowHotkeyId);
                hotkeyRegistered = false;
            }
        }

        // Refined toggle: hidden -> show; focused -> hide; visible-but-not-focused -> bring to front.
        private void ToggleFromHotkey()
        {
            hidingToTray = false;

            if (!Visible)
            {
                RestoreFromTray();
                return;
            }

            if (ContainsFocus)
            {
                QueueHideToTray();
                return;
            }

            RestoreFromTray();
        }

        private void OnConfigureHotkeyClicked(object sender, EventArgs e)
        {
            HotkeyConfig result;

            using (HotkeyDialog dialog = new HotkeyDialog(pendingHotkey))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
                {
                    return;
                }

                result = dialog.Result;
            }

            pendingHotkey = result;
            TryRegisterHotkey();
            PersistConfig();
            UpdateHotkeyMenuText();
        }

        private void UpdateHotkeyMenuText()
        {
            if (trayHotkeyMenuItem == null)
            {
                return;
            }

            trayHotkeyMenuItem.Text = pendingHotkey != null && pendingHotkey.Enabled
                ? "\u5feb\u6377\u952e\u8bbe\u7f6e\u2026 (" + pendingHotkey.ToDisplayString() + ")"
                : "\u5feb\u6377\u952e\u8bbe\u7f6e\u2026";
        }

        private void OnSidebarRatioChanged(object sender, EventArgs e)
        {
            PersistConfig();
        }

        private SiteHealth GetSiteHealth(string siteId)
        {
            if (string.IsNullOrEmpty(siteId))
            {
                return SiteHealth.Unknown;
            }

            lock (siteHealthSync)
            {
                SiteHealth health;
                siteHealth.TryGetValue(siteId, out health);
                return health;
            }
        }

        private void RestartSiteHealthProbe()
        {
            if (siteHealthTimer != null)
            {
                siteHealthTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            }

            ProbeSiteHealth(null);
            siteHealthTimer = new System.Threading.Timer(
                ProbeSiteHealth,
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
        }

        private void ProbeSiteHealth(object state)
        {
            // Capture the site list on the UI thread (it's only mutated there), then probe off-thread.
            BeginInvoke(new Action(delegate
            {
                SiteEntry[] snapshot = sites.ToArray();

                for (int index = 0; index < snapshot.Length; index++)
                {
                    ProbeSingleSite(snapshot[index]);
                }
            }));
        }

        private void ProbeSingleSite(SiteEntry site)
        {
            if (site == null || string.IsNullOrWhiteSpace(site.Url))
            {
                return;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                SiteHealth health = ProbeUrl(site.Url);

                lock (siteHealthSync)
                {
                    siteHealth[site.Id] = health;
                }

                try
                {
                    BeginInvoke(new Action(delegate { sidebarSurface.Invalidate(); }));
                }
                catch
                {
                }
            });
        }

        // Lightweight HTTP probe using WebRequest (available via System.dll), so no extra
        // assembly reference is needed. Treat any HTTP response (even non-2xx) as "up" --
        // the server is listening. Connection refused / timeout / DNS => "down".
        private static SiteHealth ProbeUrl(string url)
        {
            try
            {
                System.Net.WebRequest request = System.Net.WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = 3000;
                request.Proxy = null;

                using (System.Net.WebResponse response = request.GetResponse())
                {
                    return SiteHealth.Up;
                }
            }
            catch (System.Net.WebException ex)
            {
                System.Net.WebResponse response = ex.Response;
                if (response != null)
                {
                    response.Close();
                    return SiteHealth.Up;
                }

                return SiteHealth.Down;
            }
            catch
            {
                return SiteHealth.Down;
            }
        }

        private void ExitApplication()
        {
            if (commandManager.HasActiveOrPendingCommands())
            {
                if (MessageBox.Show(
                        "\u4ecd\u6709\u547d\u4ee4\u6b63\u5728\u8fd0\u884c\uff0c\u662f\u5426\u505c\u6b62\u540e\u9000\u51fa\uff1f",
                        AppName,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                commandManager.StopAll();
            }

            allowExit = true;
            notifyIcon.Visible = false;
            Close();
        }

        private bool ContainsSiteUrl(string url, string ignoredSiteId)
        {
            string normalizedUrl = AppConfigStore.NormalizeUrl(url);

            foreach (SiteEntry site in sites)
            {
                if (string.Equals(site.Id, ignoredSiteId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(AppConfigStore.NormalizeUrl(site.Url), normalizedUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private SiteEntry CloneSite(SiteEntry site)
        {
            return new SiteEntry
            {
                Id = site.Id,
                Name = site.Name,
                Url = site.Url
            };
        }

        private void CopySite(SiteEntry source, SiteEntry target)
        {
            target.Name = source.Name;
            target.Url = source.Url;
        }

        private CommandEntry CloneCommand(CommandEntry command)
        {
            return new CommandEntry
            {
                Id = command.Id,
                Name = command.Name,
                Command = command.Command,
                RunMode = command.RunMode,
                EnabledOnStart = command.EnabledOnStart,
                AutoRetry = new AutoRetryConfig
                {
                    Enabled = command.AutoRetry == null ? false : command.AutoRetry.Enabled,
                    MaxAttempts = command.AutoRetry == null ? 0 : command.AutoRetry.MaxAttempts,
                    InitialDelaySeconds = command.AutoRetry == null ? 3 : command.AutoRetry.InitialDelaySeconds,
                    MaxDelaySeconds = command.AutoRetry == null ? 60 : command.AutoRetry.MaxDelaySeconds,
                    ResetAfterSeconds = command.AutoRetry == null ? 300 : command.AutoRetry.ResetAfterSeconds
                },
                WorkingDirectory = command.WorkingDirectory,
                EnvironmentVariables = CloneEnvironmentVariables(command.EnvironmentVariables)
            };
        }

        private void CopyCommand(CommandEntry source, CommandEntry target)
        {
            target.Name = source.Name;
            target.Command = source.Command;
            target.RunMode = source.RunMode;
            target.EnabledOnStart = source.EnabledOnStart;
            target.AutoRetry = source.AutoRetry;
            target.WorkingDirectory = source.WorkingDirectory;
            target.EnvironmentVariables = CloneEnvironmentVariables(source.EnvironmentVariables);
        }

        private static EnvironmentVariableEntry[] CloneEnvironmentVariables(EnvironmentVariableEntry[] variables)
        {
            if (variables == null || variables.Length == 0)
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

        private ThemedButton CreatePrimaryButton(string text, int x, int y, int width)
        {
            ThemedButton button = new ThemedButton();
            button.Text = text;
            button.Size = new Size(width, 34);
            button.Location = new Point(x, y);
            UiTheme.StylePrimaryButton(button);
            return button;
        }

        private ThemedButton CreateSecondaryButton(string text, int x, int y, int width)
        {
            ThemedButton button = new ThemedButton();
            button.Text = text;
            button.Size = new Size(width, 34);
            button.Location = new Point(x, y);
            UiTheme.StyleSecondaryButton(button);
            return button;
        }

        private bool IsNearBottom(TextBox textBox)
        {
            int firstVisibleLine = GetFirstVisibleLine(textBox);
            int lineHeight = textBox.Font.Height;
            int visibleLines = Math.Max(1, textBox.ClientSize.Height / Math.Max(1, lineHeight));
            int totalLines = Math.Max(0, textBox.GetLineFromCharIndex(textBox.TextLength) + 1);

            return firstVisibleLine + visibleLines >= totalLines - 1;
        }

        private int GetFirstVisibleLine(TextBox textBox)
        {
            if (textBox == null || !textBox.IsHandleCreated)
            {
                return 0;
            }

            return SendMessage(textBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
        }

        private void ScrollTextBoxToFirstVisibleLine(TextBox textBox, int firstVisibleLine)
        {
            int delta;

            if (textBox == null || !textBox.IsHandleCreated)
            {
                return;
            }

            delta = firstVisibleLine - GetFirstVisibleLine(textBox);

            if (delta != 0)
            {
                SendMessage(textBox.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Tear down cached WebView2 controls explicitly: unhook the navigation
                // handlers and dispose each control so the embedded browser process can
                // release the user-data folder. The default Form dispose does not do this.
                foreach (SiteViewState state in siteViews.Values)
                {
                    if (state.WebView == null)
                    {
                        continue;
                    }

                    state.WebView.NavigationStarting -= OnNavigationStarting;
                    state.WebView.NavigationCompleted -= OnNavigationCompleted;

                    if (state.WebView.CoreWebView2 != null)
                    {
                        state.WebView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
                    }

                    try
                    {
                        state.WebView.Dispose();
                    }
                    catch
                    {
                    }
                }

                siteViews.Clear();

                if (siteHealthTimer != null)
                {
                    siteHealthTimer.Dispose();
                    siteHealthTimer = null;
                }
            }

            base.Dispose(disposing);
        }

        private sealed class SiteViewState
        {
            public SiteEntry Site { get; set; }

            public WebView2 WebView { get; set; }

            public bool IsInitialized { get; set; }

            public bool InitializationStarted { get; set; }

            public string LastNavigatedUrl { get; set; }

            public string CurrentNavigationUrl { get; set; }

            public List<string> NavigationHistory { get; set; }

            public bool SuppressNextHistoryEntry { get; set; }
        }
    }
}
