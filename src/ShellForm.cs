using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
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
        private const int SidebarBrandHeight = 164;
        private const int SidebarCommandSectionHeight = 332;
        private const int SidebarSectionPaddingTop = 14;
        private const int SidebarSectionTitleHeight = 24;
        private const int SidebarActionHeight = 40;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_SETREDRAW = 0x000B;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
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

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

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
        private readonly Panel sidebarContentPanel;
        private readonly SidebarSplitterPanel sidebarSplitter;
        private readonly Panel brandPanel;
        private readonly Panel commandSection;
        private readonly Panel siteSection;
        private readonly Panel workspacePanel;
        private readonly Panel commandActionPanel;
        private readonly Panel siteActionPanel;
        private readonly Panel rightBody;
        private readonly Panel webPanel;
        private readonly Panel logsPanel;
        private readonly Label appTitleLabel;
        private readonly Label summaryLabel;
        private readonly Label commandSectionTitle;
        private readonly Label siteSectionTitle;
        private readonly Label webStateTitleLabel;
        private readonly Label webStateDetailLabel;
        private readonly ThemedButton webStateRetryButton;
        private readonly Panel webStateOverlay;
        private readonly CommandSidebarListView commandListView;
        private readonly SiteSidebarListView siteListView;
        private readonly ThemedButton addCommandButton;
        private readonly ThemedButton editCommandButton;
        private readonly ThemedButton deleteCommandButton;
        private readonly ThemedButton startStopCommandButton;
        private readonly ThemedButton stopAllCommandsButton;
        private readonly ThemedButton addSiteButton;
        private readonly ThemedButton editSiteButton;
        private readonly ThemedButton deleteSiteButton;
        private readonly ThemedButton openSiteButton;
        private readonly ThemedButton webViewModeButton;
        private readonly ThemedButton logsViewModeButton;
        private readonly Label currentCommandLabel;
        private readonly RoundedLabel commandStatusBadge;
        private readonly ThemedButton reloadSiteButton;
        private readonly ThemedButton clearLogsButton;
        private readonly ThemedButton copyLogsButton;
        private readonly CheckBox autoScrollLogsCheckBox;
        private readonly TextBox logsTextBox;
        private readonly Panel webViewHost;
        private readonly Timer uiRefreshTimer;
        private readonly Timer trayRestoreTimer;
        private readonly ToolStripMenuItem trayStartupMenuItem;
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
        private bool sidebarHidden;
        private bool resizingSidebar;
        private bool hidingToTray;
        private bool parkedInTray;
        private bool restoringFromTray;
        private int sidebarDragStartX;
        private int sidebarDragStartWidth;
        private int sidebarPendingWidth;
        private DateTime statusSummaryHoldUntilUtc;
        private int expandedSidebarWidth;
        private Rectangle preTrayBounds;
        private FormWindowState preTrayWindowState;

        public ShellForm()
        {
            AppConfig config = AppConfigStore.Load();
            Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            commandManager = new CommandManager();
            commandManager.RuntimeChanged += OnCommandRuntimeChanged;
            siteViews = new Dictionary<string, SiteViewState>(StringComparer.OrdinalIgnoreCase);
            sites = new List<SiteEntry>(config.Sites ?? new SiteEntry[0]);
            commands = new List<CommandEntry>(config.Commands ?? new CommandEntry[0]);
            commandManager.SyncCommands(commands);

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
            preTrayBounds = Bounds;
            preTrayWindowState = WindowState;

            statusLabel = new ToolStripStatusLabel("\u6b63\u5728\u52a0\u8f7d\u5de5\u4f5c\u53f0...");
            statusStrip = new StatusStrip();
            statusStrip.Items.Add(statusLabel);

            titleBarPanel = new Panel();
            titleBarPanel.Dock = DockStyle.Top;
            titleBarPanel.Height = TitleBarHeight;
            titleBarPanel.BackColor = UiTheme.WindowBackground;
            titleBarPanel.MouseDown += OnTitleBarMouseDown;
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
            sidebarSurface.ReloadSiteClicked += OnReloadSiteClicked;
            sidebarSurface.WorkspaceModeRequested += OnSidebarWorkspaceModeRequested;
            sidebarSurface.CommandActivated += OnCommandListItemActivated;
            sidebarSurface.SiteActivated += OnSiteListItemActivated;
            sidebarSurface.CommandActionRequested += OnSidebarCommandActionRequested;
            sidebarSurface.SiteActionRequested += OnSidebarSiteActionRequested;

            sidebarContentPanel = new Panel();
            sidebarContentPanel.Dock = DockStyle.None;
            sidebarContentPanel.BackColor = UiTheme.SidebarBackground;
            sidebarContentPanel.Padding = new Padding(16, 16, 16, 14);
            sidebarContentPanel.Resize += OnSidebarContentPanelResize;

            sidebarSplitter = new SidebarSplitterPanel();
            sidebarSplitter.Dock = DockStyle.None;
            sidebarSplitter.MouseDown += OnSidebarSplitterMouseDown;
            sidebarSplitter.MouseMove += OnSidebarSplitterMouseMove;
            sidebarSplitter.MouseUp += OnSidebarSplitterMouseUp;

            trayRestoreTimer = new Timer();
            trayRestoreTimer.Interval = 60;
            trayRestoreTimer.Tick += OnTrayRestoreTimerTick;

            brandPanel = new Panel();
            brandPanel.Dock = DockStyle.None;
            brandPanel.Height = SidebarBrandHeight;
            brandPanel.BackColor = UiTheme.SidebarBackground;
            brandPanel.Padding = new Padding(18, 16, 18, 14);
            brandPanel.Resize += OnBrandPanelResize;

            appTitleLabel = new Label();
            appTitleLabel.Text = "Switch \u63a7\u5236\u53f0";
            appTitleLabel.Font = new Font("Microsoft YaHei UI", 13.5f, FontStyle.Bold);
            appTitleLabel.ForeColor = UiTheme.TextPrimary;
            appTitleLabel.AutoSize = false;
            appTitleLabel.Dock = DockStyle.None;
            appTitleLabel.Margin = new Padding(0);
            appTitleLabel.TextAlign = ContentAlignment.MiddleLeft;

            summaryLabel = new Label();
            summaryLabel.Text = "\u672c\u5730\u7f51\u9875\u3001\u547d\u4ee4\u4e0e\u65e5\u5fd7\u7edf\u4e00\u7ba1\u7406\u3002";
            summaryLabel.Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular);
            summaryLabel.ForeColor = UiTheme.TextSecondary;
            summaryLabel.AutoSize = false;
            summaryLabel.AutoEllipsis = true;
            summaryLabel.Dock = DockStyle.None;
            summaryLabel.Margin = new Padding(0, 10, 0, 0);
            summaryLabel.TextAlign = ContentAlignment.TopLeft;

            stopAllCommandsButton = CreateSecondaryButton("\u5168\u90e8\u505c\u6b62", 0, 0, 124);
            stopAllCommandsButton.Anchor = AnchorStyles.None;
            stopAllCommandsButton.Margin = new Padding(8, 2, 0, 0);
            stopAllCommandsButton.Click += OnStopAllCommandsClicked;

            webViewModeButton = CreateViewToggleButton("\u7f51\u9875", 0);
            webViewModeButton.Click += delegate { SetWorkspaceMode(WorkspaceMode.Web); };
            webViewModeButton.Location = new Point(0, 4);
            webViewModeButton.Size = new Size(76, 34);
            logsViewModeButton = CreateViewToggleButton("\u65e5\u5fd7", 1);
            logsViewModeButton.Click += delegate { SetWorkspaceMode(WorkspaceMode.Logs); };
            logsViewModeButton.Location = new Point(80, 4);
            logsViewModeButton.Size = new Size(76, 34);

            reloadSiteButton = CreateSecondaryButton("\u5237\u65b0\u9875\u9762", 160, 4, 98);
            reloadSiteButton.Click += OnReloadSiteClicked;

            webViewModeButton.Dock = DockStyle.None;
            webViewModeButton.Margin = new Padding(0, 4, 8, 0);
            logsViewModeButton.Dock = DockStyle.None;
            logsViewModeButton.Margin = new Padding(0, 4, 8, 0);
            reloadSiteButton.Dock = DockStyle.None;
            reloadSiteButton.Margin = new Padding(0, 4, 0, 0);

            brandPanel.Controls.Add(reloadSiteButton);
            brandPanel.Controls.Add(logsViewModeButton);
            brandPanel.Controls.Add(webViewModeButton);
            brandPanel.Controls.Add(stopAllCommandsButton);
            brandPanel.Controls.Add(summaryLabel);
            brandPanel.Controls.Add(appTitleLabel);

            commandSection = new Panel();
            commandSection.Dock = DockStyle.None;
            commandSection.Height = SidebarCommandSectionHeight;
            commandSection.Padding = new Padding(0, 14, 0, 0);
            commandSection.BackColor = UiTheme.SidebarBackground;
            commandSection.Resize += OnCommandSectionResize;

            commandSectionTitle = new Label();
            commandSectionTitle.Text = "\u547d\u4ee4";
            commandSectionTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            commandSectionTitle.ForeColor = UiTheme.TextPrimary;
            commandSectionTitle.Dock = DockStyle.None;
            commandSectionTitle.Height = SidebarSectionTitleHeight;
            commandSectionTitle.Margin = new Padding(0);
            commandSectionTitle.TextAlign = ContentAlignment.MiddleLeft;

            commandListView = new CommandSidebarListView();
            commandListView.Dock = DockStyle.None;
            commandListView.EmptyText = "\u6682\u65e0\u547d\u4ee4\uff0c\u70b9\u51fb\u201c\u65b0\u589e\u201d\u521b\u5efa\u4e00\u4e2a\u672c\u5730\u670d\u52a1\u547d\u4ee4\u3002";
            commandListView.SnapshotProvider = delegate(string commandId)
            {
                return commandManager.GetSnapshot(commandId);
            };
            commandListView.ItemActivated += OnCommandListItemActivated;
            commandListView.EmptyClicked += OnAddCommandClicked;

            commandActionPanel = new Panel();
            commandActionPanel.Dock = DockStyle.None;
            commandActionPanel.Height = SidebarActionHeight;
            commandActionPanel.Margin = new Padding(0);
            commandActionPanel.Resize += OnCommandActionPanelResize;

            addCommandButton = CreatePrimaryButton("\u65b0\u589e", 0, 0, 80);
            addCommandButton.Click += OnAddCommandClicked;
            editCommandButton = CreateSecondaryButton("\u7f16\u8f91", 0, 0, 80);
            editCommandButton.Click += OnEditCommandClicked;
            deleteCommandButton = CreateSecondaryButton("\u5220\u9664", 0, 0, 80);
            deleteCommandButton.Click += OnDeleteCommandClicked;
            startStopCommandButton = CreatePrimaryButton("\u542f\u52a8", 0, 0, 80);
            startStopCommandButton.Click += OnStartStopCommandClicked;

            addCommandButton.Dock = DockStyle.None;
            addCommandButton.Margin = new Padding(0, 0, 6, 0);
            editCommandButton.Dock = DockStyle.None;
            editCommandButton.Margin = new Padding(6, 0, 6, 0);
            deleteCommandButton.Dock = DockStyle.None;
            deleteCommandButton.Margin = new Padding(6, 0, 6, 0);
            startStopCommandButton.Dock = DockStyle.None;
            startStopCommandButton.Margin = new Padding(6, 0, 0, 0);

            commandActionPanel.Controls.Add(startStopCommandButton);
            commandActionPanel.Controls.Add(deleteCommandButton);
            commandActionPanel.Controls.Add(editCommandButton);
            commandActionPanel.Controls.Add(addCommandButton);

            commandSection.Controls.Add(commandListView);
            commandSection.Controls.Add(commandActionPanel);
            commandSection.Controls.Add(commandSectionTitle);

            siteSection = new Panel();
            siteSection.Dock = DockStyle.None;
            siteSection.Padding = new Padding(0, 14, 0, 0);
            siteSection.BackColor = UiTheme.SidebarBackground;
            siteSection.Resize += OnSiteSectionResize;

            siteSectionTitle = new Label();
            siteSectionTitle.Text = "\u7ad9\u70b9";
            siteSectionTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            siteSectionTitle.ForeColor = UiTheme.TextPrimary;
            siteSectionTitle.Dock = DockStyle.None;
            siteSectionTitle.Height = SidebarSectionTitleHeight;
            siteSectionTitle.Margin = new Padding(0);
            siteSectionTitle.TextAlign = ContentAlignment.MiddleLeft;

            siteListView = new SiteSidebarListView();
            siteListView.Dock = DockStyle.None;
            siteListView.EmptyText = "\u6682\u65e0\u7ad9\u70b9\uff0c\u8bf7\u5148\u65b0\u589e\u8981\u67e5\u770b\u7684\u672c\u5730\u7f51\u9875\u3002";
            siteListView.ItemActivated += OnSiteListItemActivated;
            siteListView.EmptyClicked += OnAddSiteClicked;

            siteActionPanel = new Panel();
            siteActionPanel.Dock = DockStyle.None;
            siteActionPanel.Height = SidebarActionHeight;
            siteActionPanel.Margin = new Padding(0);
            siteActionPanel.Resize += OnSiteActionPanelResize;

            addSiteButton = CreatePrimaryButton("\u65b0\u589e", 0, 0, 80);
            addSiteButton.Click += OnAddSiteClicked;
            editSiteButton = CreateSecondaryButton("\u7f16\u8f91", 0, 0, 80);
            editSiteButton.Click += OnEditSiteClicked;
            deleteSiteButton = CreateSecondaryButton("\u5220\u9664", 0, 0, 80);
            deleteSiteButton.Click += OnDeleteSiteClicked;
            openSiteButton = CreateSecondaryButton("\u6253\u5f00", 0, 0, 80);
            openSiteButton.Click += OnOpenSiteClicked;

            addSiteButton.Dock = DockStyle.None;
            addSiteButton.Margin = new Padding(0, 0, 6, 0);
            editSiteButton.Dock = DockStyle.None;
            editSiteButton.Margin = new Padding(6, 0, 6, 0);
            deleteSiteButton.Dock = DockStyle.None;
            deleteSiteButton.Margin = new Padding(6, 0, 6, 0);
            openSiteButton.Dock = DockStyle.None;
            openSiteButton.Margin = new Padding(6, 0, 0, 0);

            siteActionPanel.Controls.Add(openSiteButton);
            siteActionPanel.Controls.Add(deleteSiteButton);
            siteActionPanel.Controls.Add(editSiteButton);
            siteActionPanel.Controls.Add(addSiteButton);

            siteSection.Controls.Add(siteListView);
            siteSection.Controls.Add(siteActionPanel);
            siteSection.Controls.Add(siteSectionTitle);

            sidebarContentPanel.Controls.Add(siteSection);
            sidebarContentPanel.Controls.Add(commandSection);
            sidebarContentPanel.Controls.Add(brandPanel);
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
            trayMenu.Items.Add("\u5168\u90e8\u505c\u6b62\u547d\u4ee4", null, delegate { commandManager.StopAll(); });
            trayStartupMenuItem = new ToolStripMenuItem("\u5f00\u673a\u81ea\u542f");
            trayStartupMenuItem.CheckOnClick = true;
            trayStartupMenuItem.Click += OnTrayStartupMenuClicked;
            trayMenu.Items.Add(trayStartupMenuItem);
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
            RefreshLogsView();
            UpdateStatusSummary();
        }

        private void OnCommandRuntimeChanged(object sender, CommandRuntimeChangedEventArgs e)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new Action(
                delegate
                {
                    RefreshCommandCardState(e.CommandId);
                    RefreshCommandButtons();
                    RefreshLogsView();
                    UpdateStatusSummary();
                }));
        }

        private void RefreshCommandList()
        {
            commandListView.SetItems(commands);
            sidebarSurface.SetCommands(commands);
            UpdateCommandSelectionVisuals();
            RefreshEmptyStates();
        }

        private void RefreshSiteList()
        {
            siteListView.SetItems(sites);
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

            if (state != null && currentSite != null &&
                string.Equals(state.Site.Id, currentSite.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetTransientStatus("\u6b63\u5728\u52a0\u8f7d " + state.Site.Name + " - " + e.Uri, 1);
                SetWebState("\u6b63\u5728\u52a0\u8f7d " + state.Site.Name, e.Uri, false);
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            SiteViewState state = GetSiteState(sender);

            if (state == null || currentSite == null ||
                !string.Equals(state.Site.Id, currentSite.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetTransientStatus(e.IsSuccess
                ? "\u5df2\u52a0\u8f7d " + state.Site.Name
                : "\u65e0\u6cd5\u8bbf\u95ee " + state.Site.Url);
            SetWebState(
                e.IsSuccess ? string.Empty : "\u65e0\u6cd5\u8bbf\u95ee " + state.Site.Name,
                e.IsSuccess ? string.Empty : state.Site.Url,
                !e.IsSuccess);
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

            editCommandButton.Enabled = hasCommand;
            deleteCommandButton.Enabled = hasCommand && !isActive;
            startStopCommandButton.Enabled = hasCommand;
            sidebarSurface.EditCommandEnabled = editCommandButton.Enabled;
            sidebarSurface.DeleteCommandEnabled = deleteCommandButton.Enabled;
            sidebarSurface.StartStopCommandEnabled = startStopCommandButton.Enabled;
            sidebarSurface.StartStopCommandText = isActive ? "\u505c\u6b62" : "\u542f\u52a8";

            if (!hasCommand)
            {
                startStopCommandButton.Text = "\u542f\u52a8";
                editCommandButton.Enabled = false;
                deleteCommandButton.Enabled = false;
                startStopCommandButton.Enabled = false;
                sidebarSurface.EditCommandEnabled = false;
                sidebarSurface.DeleteCommandEnabled = false;
                sidebarSurface.StartStopCommandEnabled = false;
                sidebarSurface.StartStopCommandText = "\u542f\u52a8";
                sidebarSurface.Invalidate();
                return;
            }

            startStopCommandButton.Text = isActive ? "\u505c\u6b62" : "\u542f\u52a8";
            sidebarSurface.StartStopCommandText = startStopCommandButton.Text;
            sidebarSurface.Invalidate();
        }

        private void RefreshSiteButtons()
        {
            bool hasSite = currentSite != null;

            editSiteButton.Enabled = hasSite;
            deleteSiteButton.Enabled = hasSite && sites.Count > 1;
            openSiteButton.Enabled = hasSite;
            reloadSiteButton.Enabled = hasSite;
            sidebarSurface.EditSiteEnabled = editSiteButton.Enabled;
            sidebarSurface.DeleteSiteEnabled = deleteSiteButton.Enabled;
            sidebarSurface.OpenSiteEnabled = openSiteButton.Enabled;
            sidebarSurface.ReloadSiteEnabled = reloadSiteButton.Enabled;
            sidebarSurface.Invalidate();
        }

        private void RefreshLogsView()
        {
            CommandRuntimeSnapshot snapshot;
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
                return;
            }

            snapshot = commandManager.GetSnapshot(currentCommand.Id);
            lines = commandManager.GetLogs(currentCommand.Id);
            currentCommandLabel.Text = currentCommand.Name;
            commandStatusBadge.Text = snapshot.GetDisplayStatus();
            ApplyBadgeStyle(commandStatusBadge, snapshot.Status);
            clearLogsButton.Enabled = lines.Length > 0;
            copyLogsButton.Enabled = lines.Length > 0;
            bool shouldAutoScroll = autoScrollLogsCheckBox.Checked || IsNearBottom(logsTextBox);
            string newText = string.Join(Environment.NewLine, lines);

            if (!string.Equals(logsTextBox.Text, newText, StringComparison.Ordinal))
            {
                logsTextBox.Text = newText;
            }

            if (lines.Length > 0 && autoScrollLogsCheckBox.Checked && (shouldAutoScroll || lastLogAutoScrollEnabled))
            {
                logsTextBox.SelectionStart = logsTextBox.TextLength;
                logsTextBox.ScrollToCaret();
            }

            lastLogAutoScrollEnabled = autoScrollLogsCheckBox.Checked && shouldAutoScroll;
        }

        private void SetWorkspaceMode(WorkspaceMode mode)
        {
            workspaceMode = mode;
            webPanel.Visible = mode == WorkspaceMode.Web;
            logsPanel.Visible = mode == WorkspaceMode.Logs;
            ApplyViewToggleState(webViewModeButton, mode == WorkspaceMode.Web);
            ApplyViewToggleState(logsViewModeButton, mode == WorkspaceMode.Logs);
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

        private void OnSidebarContentPanelResize(object sender, EventArgs e)
        {
            LayoutSidebarInnerPanels();
        }

        private void OnBrandPanelResize(object sender, EventArgs e)
        {
            LayoutBrandPanel();
        }

        private void OnCommandSectionResize(object sender, EventArgs e)
        {
            LayoutCommandSection();
        }

        private void OnSiteSectionResize(object sender, EventArgs e)
        {
            LayoutSiteSection();
        }

        private void OnCommandActionPanelResize(object sender, EventArgs e)
        {
            LayoutFourActionButtons(
                commandActionPanel,
                addCommandButton,
                editCommandButton,
                deleteCommandButton,
                startStopCommandButton);
        }

        private void OnSiteActionPanelResize(object sender, EventArgs e)
        {
            LayoutFourActionButtons(
                siteActionPanel,
                addSiteButton,
                editSiteButton,
                deleteSiteButton,
                openSiteButton);
        }

        private void LayoutSidebarInnerPanels()
        {
            Rectangle contentBounds;
            int y;
            int siteHeight;

            if (brandPanel == null || commandSection == null || siteSection == null)
            {
                return;
            }

            contentBounds = new Rectangle(
                sidebarContentPanel.Padding.Left,
                sidebarContentPanel.Padding.Top,
                Math.Max(0, sidebarContentPanel.ClientSize.Width - sidebarContentPanel.Padding.Horizontal),
                Math.Max(0, sidebarContentPanel.ClientSize.Height - sidebarContentPanel.Padding.Vertical));

            y = contentBounds.Top;
            SetBoundsIfChanged(brandPanel, contentBounds.Left, y, contentBounds.Width, SidebarBrandHeight);
            y += SidebarBrandHeight;
            SetBoundsIfChanged(commandSection, contentBounds.Left, y, contentBounds.Width, SidebarCommandSectionHeight);
            y += SidebarCommandSectionHeight;
            siteHeight = Math.Max(0, contentBounds.Bottom - y);
            SetBoundsIfChanged(siteSection, contentBounds.Left, y, contentBounds.Width, siteHeight);
        }

        private void LayoutBrandPanel()
        {
            Padding padding;
            int x;
            int y;
            int width;
            int stopWidth;
            int titleWidth;
            int actionY;
            int firstWidth;
            int secondWidth;
            int thirdWidth;

            if (appTitleLabel == null || reloadSiteButton == null)
            {
                return;
            }

            padding = brandPanel.Padding;
            x = padding.Left;
            y = padding.Top;
            width = Math.Max(0, brandPanel.ClientSize.Width - padding.Horizontal);
            stopWidth = Math.Min(124, Math.Max(0, width / 2));
            titleWidth = Math.Max(0, width - stopWidth - 8);

            SetBoundsIfChanged(appTitleLabel, x, y, titleWidth, 42);
            SetBoundsIfChanged(stopAllCommandsButton, x + width - stopWidth, y + 2, stopWidth, 34);
            SetBoundsIfChanged(summaryLabel, x, y + 52, width, 34);

            actionY = y + 90;
            firstWidth = Math.Max(0, (width * 30) / 100);
            secondWidth = Math.Max(0, (width * 30) / 100);
            thirdWidth = Math.Max(0, width - firstWidth - secondWidth);

            SetBoundsIfChanged(webViewModeButton, x, actionY, Math.Max(0, firstWidth - 8), 34);
            SetBoundsIfChanged(logsViewModeButton, x + firstWidth, actionY, Math.Max(0, secondWidth - 8), 34);
            SetBoundsIfChanged(reloadSiteButton, x + firstWidth + secondWidth, actionY, thirdWidth, 34);
        }

        private void LayoutCommandSection()
        {
            LayoutSidebarSection(commandSection, commandSectionTitle, commandListView, commandActionPanel);
        }

        private void LayoutSiteSection()
        {
            LayoutSidebarSection(siteSection, siteSectionTitle, siteListView, siteActionPanel);
        }

        private void LayoutSidebarSection(Panel section, Control title, Control list, Panel actions)
        {
            int width;
            int top;
            int actionY;
            int listY;

            if (section == null || title == null || list == null || actions == null)
            {
                return;
            }

            width = Math.Max(0, section.ClientSize.Width);
            top = section.Padding.Top;
            actionY = Math.Max(top + SidebarSectionTitleHeight, section.ClientSize.Height - SidebarActionHeight);
            listY = top + SidebarSectionTitleHeight;

            SetBoundsIfChanged(title, 0, top, width, SidebarSectionTitleHeight);
            SetBoundsIfChanged(actions, 0, actionY, width, SidebarActionHeight);
            SetBoundsIfChanged(list, 0, listY, width, Math.Max(0, actionY - listY));
        }

        private void LayoutFourActionButtons(Panel panel, Control first, Control second, Control third, Control fourth)
        {
            int width;
            int height;
            int gap;
            int buttonHeight;
            int y;
            int availableWidth;
            int buttonWidth;
            int x;

            if (panel == null || first == null || second == null || third == null || fourth == null)
            {
                return;
            }

            width = Math.Max(0, panel.ClientSize.Width);
            height = Math.Max(0, panel.ClientSize.Height);
            gap = width < 260 ? 8 : 12;
            buttonHeight = Math.Min(34, height);
            y = Math.Max(0, (height - buttonHeight) / 2);
            availableWidth = Math.Max(0, width - (gap * 3));
            buttonWidth = availableWidth / 4;
            x = 0;

            SetBoundsIfChanged(first, x, y, buttonWidth, buttonHeight);
            x += buttonWidth + gap;
            SetBoundsIfChanged(second, x, y, buttonWidth, buttonHeight);
            x += buttonWidth + gap;
            SetBoundsIfChanged(third, x, y, buttonWidth, buttonHeight);
            x += buttonWidth + gap;
            SetBoundsIfChanged(fourth, x, y, Math.Max(0, width - x), buttonHeight);
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

            summaryLabel.Text =
                "\u547d\u4ee4 " + commands.Count + " \u4e2a\uff0c\u8fd0\u884c\u4e2d " +
                running + " \u4e2a\uff0c\u7b49\u5f85\u91cd\u8bd5 " +
                waitingRetry + " \u4e2a\uff0c\u7ad9\u70b9 " +
                sites.Count + " \u4e2a\uff0c" +
                startupText + "\u3002";
            sidebarSurface.SummaryText = summaryLabel.Text;
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
            commandListView.RefreshItems();
            sidebarSurface.Invalidate();
        }

        private void RefreshCommandCardState(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            commandListView.RefreshItem(commandId);
            sidebarSurface.RefreshCommand(commandId);
        }

        private void UpdateCommandSelectionVisuals()
        {
            commandListView.SelectedId = currentCommand == null ? null : currentCommand.Id;
            sidebarSurface.SelectedCommandId = currentCommand == null ? null : currentCommand.Id;
            sidebarSurface.Invalidate();
        }

        private void UpdateSiteSelectionVisuals()
        {
            siteListView.SelectedId = currentSite == null ? null : currentSite.Id;
            sidebarSurface.SelectedSiteId = currentSite == null ? null : currentSite.Id;
            sidebarSurface.Invalidate();
        }

        private void RefreshEmptyStates()
        {
            commandListView.RefreshItems();
            siteListView.RefreshItems();
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
                Commands = commands.ToArray()
            });
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

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
        }

        private void OnTitleBarMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleWindowMaximized();
            }
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
                trayRestoreTimer.Stop();
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
            if (parkedInTray)
            {
                return;
            }

            restoringFromTray = false;
            trayRestoreTimer.Stop();
            preTrayWindowState = WindowState;
            preTrayBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (preTrayBounds.Width <= 0 || preTrayBounds.Height <= 0)
            {
                preTrayBounds = Bounds;
            }

            parkedInTray = true;
            WindowState = FormWindowState.Normal;
            Bounds = GetTrayParkingBounds(preTrayBounds.Size);
            ShowInTaskbar = false;

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
            if (parkedInTray)
            {
                if (restoringFromTray)
                {
                    return;
                }

                restoringFromTray = true;
                ShowInTaskbar = true;
                trayRestoreTimer.Stop();
                trayRestoreTimer.Start();
                return;
            }

            if (!Visible)
            {
                Show();
            }

            ShowInTaskbar = true;
            WindowState = preTrayWindowState == FormWindowState.Minimized
                ? FormWindowState.Normal
                : preTrayWindowState;
            Activate();
        }

        private void OnTrayRestoreTimerTick(object sender, EventArgs e)
        {
            trayRestoreTimer.Stop();
            FinishRestoreFromTray();
        }

        private void FinishRestoreFromTray()
        {
            if (parkedInTray)
            {
                Bounds = preTrayBounds;
                parkedInTray = false;
            }

            if (!Visible)
            {
                Show();
            }

            ShowInTaskbar = true;
            WindowState = preTrayWindowState == FormWindowState.Minimized
                ? FormWindowState.Normal
                : preTrayWindowState;
            restoringFromTray = false;
            Activate();
        }

        private Rectangle GetTrayParkingBounds(Size size)
        {
            int width = Math.Max(MinimumSize.Width, size.Width);
            int height = Math.Max(MinimumSize.Height, size.Height);

            return new Rectangle(
                SystemInformation.VirtualScreen.Left - width - 80,
                SystemInformation.VirtualScreen.Top - height - 80,
                width,
                height);
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
            foreach (SiteEntry site in sites)
            {
                if (!string.Equals(site.Id, ignoredSiteId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(site.Url, url, StringComparison.OrdinalIgnoreCase))
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
                }
            };
        }

        private void CopyCommand(CommandEntry source, CommandEntry target)
        {
            target.Name = source.Name;
            target.Command = source.Command;
            target.RunMode = source.RunMode;
            target.EnabledOnStart = source.EnabledOnStart;
            target.AutoRetry = source.AutoRetry;
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

        private ThemedButton CreateViewToggleButton(string text, int columnIndex)
        {
            ThemedButton button = new ThemedButton();
            button.Text = text;
            button.Size = new Size(84, 34);
            button.Location = new Point(columnIndex * 88, 4);
            UiTheme.StyleSegmentButton(button);
            return button;
        }

        private void ApplyViewToggleState(ThemedButton button, bool active)
        {
            UiTheme.SetSegmentButtonState(button, active);
        }

        private bool IsNearBottom(TextBox textBox)
        {
            const int EM_GETFIRSTVISIBLELINE = 0x00CE;
            int firstVisibleLine = SendMessage(textBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            int lineHeight = textBox.Font.Height;
            int visibleLines = Math.Max(1, textBox.ClientSize.Height / Math.Max(1, lineHeight));
            int totalLines = Math.Max(0, textBox.GetLineFromCharIndex(textBox.TextLength) + 1);

            return firstVisibleLine + visibleLines >= totalLines - 1;
        }

        private sealed class SiteViewState
        {
            public SiteEntry Site { get; set; }

            public WebView2 WebView { get; set; }

            public bool IsInitialized { get; set; }

            public bool InitializationStarted { get; set; }

            public string LastNavigatedUrl { get; set; }
        }
    }
}
