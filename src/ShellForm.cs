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
        private const int SidebarMaxWidth = 560;
        private const int SidebarCollapseThreshold = 96;
        private const int SidebarResizeIntervalMs = 16;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
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
        private readonly RoundedLabel commandEmptyLabel;
        private readonly RoundedLabel siteEmptyLabel;
        private readonly Label webStateTitleLabel;
        private readonly Label webStateDetailLabel;
        private readonly ThemedButton webStateRetryButton;
        private readonly Panel webStateOverlay;
        private readonly BufferedScrollPanel commandListPanel;
        private readonly BufferedScrollPanel siteListPanel;
        private readonly TableLayoutPanel commandListContent;
        private readonly TableLayoutPanel siteListContent;
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
        private readonly Timer sidebarResizeTimer;
        private readonly Timer trayRestoreTimer;
        private readonly ToolStripMenuItem trayStartupMenuItem;
        private readonly Dictionary<string, CommandSidebarCard> commandCards;
        private readonly Dictionary<string, SiteSidebarCard> siteCards;
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
        private int sidebarFrozenContentWidth;
        private int sidebarFrozenWorkspaceContentWidth;
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
            commandCards = new Dictionary<string, CommandSidebarCard>(StringComparer.OrdinalIgnoreCase);
            siteCards = new Dictionary<string, SiteSidebarCard>(StringComparer.OrdinalIgnoreCase);
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

            sidebarContentPanel = new Panel();
            sidebarContentPanel.Dock = DockStyle.None;
            sidebarContentPanel.BackColor = UiTheme.SidebarBackground;
            sidebarContentPanel.Padding = new Padding(16, 16, 16, 14);

            sidebarSplitter = new SidebarSplitterPanel();
            sidebarSplitter.Dock = DockStyle.None;
            sidebarSplitter.MouseDown += OnSidebarSplitterMouseDown;
            sidebarSplitter.MouseMove += OnSidebarSplitterMouseMove;
            sidebarSplitter.MouseUp += OnSidebarSplitterMouseUp;

            sidebarResizeTimer = new Timer();
            sidebarResizeTimer.Interval = SidebarResizeIntervalMs;
            sidebarResizeTimer.Tick += OnSidebarResizeTimerTick;

            trayRestoreTimer = new Timer();
            trayRestoreTimer.Interval = 60;
            trayRestoreTimer.Tick += OnTrayRestoreTimerTick;

            brandPanel = new Panel();
            brandPanel.Dock = DockStyle.Top;
            brandPanel.Height = 164;
            brandPanel.BackColor = UiTheme.SidebarBackground;
            brandPanel.Padding = new Padding(18, 16, 18, 14);

            TableLayoutPanel brandLayout = new TableLayoutPanel();
            brandLayout.Dock = DockStyle.Fill;
            brandLayout.Margin = new Padding(0);
            brandLayout.Padding = new Padding(0);
            brandLayout.ColumnCount = 2;
            brandLayout.RowCount = 3;
            brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));

            appTitleLabel = new Label();
            appTitleLabel.Text = "Switch \u63a7\u5236\u53f0";
            appTitleLabel.Font = new Font("Microsoft YaHei UI", 13.5f, FontStyle.Bold);
            appTitleLabel.ForeColor = UiTheme.TextPrimary;
            appTitleLabel.AutoSize = false;
            appTitleLabel.Dock = DockStyle.Fill;
            appTitleLabel.Margin = new Padding(0);
            appTitleLabel.TextAlign = ContentAlignment.MiddleLeft;

            summaryLabel = new Label();
            summaryLabel.Text = "\u672c\u5730\u7f51\u9875\u3001\u547d\u4ee4\u4e0e\u65e5\u5fd7\u7edf\u4e00\u7ba1\u7406\u3002";
            summaryLabel.Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular);
            summaryLabel.ForeColor = UiTheme.TextSecondary;
            summaryLabel.AutoSize = false;
            summaryLabel.AutoEllipsis = true;
            summaryLabel.Dock = DockStyle.Fill;
            summaryLabel.Margin = new Padding(0, 10, 0, 0);
            summaryLabel.TextAlign = ContentAlignment.TopLeft;

            stopAllCommandsButton = CreateSecondaryButton("\u5168\u90e8\u505c\u6b62", 0, 0, 124);
            stopAllCommandsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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

            TableLayoutPanel brandActionPanel = new TableLayoutPanel();
            brandActionPanel.Dock = DockStyle.Fill;
            brandActionPanel.Margin = new Padding(0);
            brandActionPanel.Padding = new Padding(0);
            brandActionPanel.ColumnCount = 3;
            brandActionPanel.RowCount = 1;
            brandActionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            brandActionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            brandActionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            brandActionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            webViewModeButton.Dock = DockStyle.Fill;
            webViewModeButton.Location = Point.Empty;
            webViewModeButton.Margin = new Padding(0, 4, 8, 0);
            logsViewModeButton.Dock = DockStyle.Fill;
            logsViewModeButton.Location = Point.Empty;
            logsViewModeButton.Margin = new Padding(0, 4, 8, 0);
            reloadSiteButton.Dock = DockStyle.Fill;
            reloadSiteButton.Location = Point.Empty;
            reloadSiteButton.Margin = new Padding(0, 4, 0, 0);

            brandActionPanel.Controls.Add(webViewModeButton, 0, 0);
            brandActionPanel.Controls.Add(logsViewModeButton, 1, 0);
            brandActionPanel.Controls.Add(reloadSiteButton, 2, 0);

            brandLayout.Controls.Add(appTitleLabel, 0, 0);
            brandLayout.Controls.Add(stopAllCommandsButton, 1, 0);
            brandLayout.Controls.Add(summaryLabel, 0, 1);
            brandLayout.Controls.Add(brandActionPanel, 0, 2);
            brandLayout.SetColumnSpan(summaryLabel, 2);
            brandLayout.SetColumnSpan(brandActionPanel, 2);

            brandPanel.Controls.Add(brandLayout);

            commandSection = new Panel();
            commandSection.Dock = DockStyle.Top;
            commandSection.Height = 332;
            commandSection.Padding = new Padding(0, 14, 0, 0);
            commandSection.BackColor = UiTheme.SidebarBackground;

            commandSectionTitle = new Label();
            commandSectionTitle.Text = "\u547d\u4ee4";
            commandSectionTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            commandSectionTitle.ForeColor = UiTheme.TextPrimary;
            commandSectionTitle.Dock = DockStyle.Top;
            commandSectionTitle.Height = 24;
            commandSectionTitle.Margin = new Padding(0);
            commandSectionTitle.TextAlign = ContentAlignment.MiddleLeft;

            commandListPanel = new BufferedScrollPanel();
            commandListPanel.Dock = DockStyle.Fill;
            commandListPanel.BackColor = UiTheme.SidebarBackground;
            commandListPanel.BorderStyle = BorderStyle.None;
            commandListPanel.Padding = new Padding(0);
            commandListPanel.Margin = new Padding(0);
            commandListPanel.Resize += OnCommandListPanelResize;

            commandListContent = CreateSidebarListContent();
            commandListPanel.Controls.Add(commandListContent);

            commandEmptyLabel = CreateEmptyLabel("\u6682\u65e0\u547d\u4ee4\uff0c\u70b9\u51fb\u201c\u65b0\u589e\u201d\u521b\u5efa\u4e00\u4e2a\u672c\u5730\u670d\u52a1\u547d\u4ee4\u3002");
            commandEmptyLabel.Cursor = Cursors.Hand;
            commandEmptyLabel.Click += OnAddCommandClicked;
            commandListPanel.Controls.Add(commandEmptyLabel);

            commandActionPanel = new Panel();
            commandActionPanel.Dock = DockStyle.Bottom;
            commandActionPanel.Height = 40;
            commandActionPanel.Margin = new Padding(0);

            TableLayoutPanel commandActionLayout = new TableLayoutPanel();
            commandActionLayout.Dock = DockStyle.Fill;
            commandActionLayout.Margin = new Padding(0);
            commandActionLayout.Padding = new Padding(0);
            commandActionLayout.ColumnCount = 4;
            commandActionLayout.RowCount = 1;
            commandActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            commandActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            commandActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            commandActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            commandActionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            addCommandButton = CreatePrimaryButton("\u65b0\u589e", 0, 0, 80);
            addCommandButton.Click += OnAddCommandClicked;
            editCommandButton = CreateSecondaryButton("\u7f16\u8f91", 0, 0, 80);
            editCommandButton.Click += OnEditCommandClicked;
            deleteCommandButton = CreateSecondaryButton("\u5220\u9664", 0, 0, 80);
            deleteCommandButton.Click += OnDeleteCommandClicked;
            startStopCommandButton = CreatePrimaryButton("\u542f\u52a8", 0, 0, 80);
            startStopCommandButton.Click += OnStartStopCommandClicked;

            addCommandButton.Dock = DockStyle.Fill;
            addCommandButton.Location = Point.Empty;
            addCommandButton.Margin = new Padding(0, 0, 6, 0);
            editCommandButton.Dock = DockStyle.Fill;
            editCommandButton.Location = Point.Empty;
            editCommandButton.Margin = new Padding(6, 0, 6, 0);
            deleteCommandButton.Dock = DockStyle.Fill;
            deleteCommandButton.Location = Point.Empty;
            deleteCommandButton.Margin = new Padding(6, 0, 6, 0);
            startStopCommandButton.Dock = DockStyle.Fill;
            startStopCommandButton.Location = Point.Empty;
            startStopCommandButton.Margin = new Padding(6, 0, 0, 0);

            commandActionLayout.Controls.Add(addCommandButton, 0, 0);
            commandActionLayout.Controls.Add(editCommandButton, 1, 0);
            commandActionLayout.Controls.Add(deleteCommandButton, 2, 0);
            commandActionLayout.Controls.Add(startStopCommandButton, 3, 0);
            commandActionPanel.Controls.Add(commandActionLayout);

            commandSection.Controls.Add(commandListPanel);
            commandSection.Controls.Add(commandActionPanel);
            commandSection.Controls.Add(commandSectionTitle);

            siteSection = new Panel();
            siteSection.Dock = DockStyle.Fill;
            siteSection.Padding = new Padding(0, 14, 0, 0);
            siteSection.BackColor = UiTheme.SidebarBackground;

            siteSectionTitle = new Label();
            siteSectionTitle.Text = "\u7ad9\u70b9";
            siteSectionTitle.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            siteSectionTitle.ForeColor = UiTheme.TextPrimary;
            siteSectionTitle.Dock = DockStyle.Top;
            siteSectionTitle.Height = 24;
            siteSectionTitle.Margin = new Padding(0);
            siteSectionTitle.TextAlign = ContentAlignment.MiddleLeft;

            siteListPanel = new BufferedScrollPanel();
            siteListPanel.Dock = DockStyle.Fill;
            siteListPanel.BackColor = UiTheme.SidebarBackground;
            siteListPanel.BorderStyle = BorderStyle.None;
            siteListPanel.Padding = new Padding(0);
            siteListPanel.Margin = new Padding(0);
            siteListPanel.Resize += OnSiteListPanelResize;

            siteListContent = CreateSidebarListContent();
            siteListPanel.Controls.Add(siteListContent);

            siteEmptyLabel = CreateEmptyLabel("\u6682\u65e0\u7ad9\u70b9\uff0c\u8bf7\u5148\u65b0\u589e\u8981\u67e5\u770b\u7684\u672c\u5730\u7f51\u9875\u3002");
            siteEmptyLabel.Cursor = Cursors.Hand;
            siteEmptyLabel.Click += OnAddSiteClicked;
            siteListPanel.Controls.Add(siteEmptyLabel);

            siteActionPanel = new Panel();
            siteActionPanel.Dock = DockStyle.Bottom;
            siteActionPanel.Height = 40;
            siteActionPanel.Margin = new Padding(0);

            TableLayoutPanel siteActionLayout = new TableLayoutPanel();
            siteActionLayout.Dock = DockStyle.Fill;
            siteActionLayout.Margin = new Padding(0);
            siteActionLayout.Padding = new Padding(0);
            siteActionLayout.ColumnCount = 4;
            siteActionLayout.RowCount = 1;
            siteActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            siteActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            siteActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            siteActionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            siteActionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            addSiteButton = CreatePrimaryButton("\u65b0\u589e", 0, 0, 80);
            addSiteButton.Click += OnAddSiteClicked;
            editSiteButton = CreateSecondaryButton("\u7f16\u8f91", 0, 0, 80);
            editSiteButton.Click += OnEditSiteClicked;
            deleteSiteButton = CreateSecondaryButton("\u5220\u9664", 0, 0, 80);
            deleteSiteButton.Click += OnDeleteSiteClicked;
            openSiteButton = CreateSecondaryButton("\u6253\u5f00", 0, 0, 80);
            openSiteButton.Click += OnOpenSiteClicked;

            addSiteButton.Dock = DockStyle.Fill;
            addSiteButton.Location = Point.Empty;
            addSiteButton.Margin = new Padding(0, 0, 6, 0);
            editSiteButton.Dock = DockStyle.Fill;
            editSiteButton.Location = Point.Empty;
            editSiteButton.Margin = new Padding(6, 0, 6, 0);
            deleteSiteButton.Dock = DockStyle.Fill;
            deleteSiteButton.Location = Point.Empty;
            deleteSiteButton.Margin = new Padding(6, 0, 6, 0);
            openSiteButton.Dock = DockStyle.Fill;
            openSiteButton.Location = Point.Empty;
            openSiteButton.Margin = new Padding(6, 0, 0, 0);

            siteActionLayout.Controls.Add(addSiteButton, 0, 0);
            siteActionLayout.Controls.Add(editSiteButton, 1, 0);
            siteActionLayout.Controls.Add(deleteSiteButton, 2, 0);
            siteActionLayout.Controls.Add(openSiteButton, 3, 0);
            siteActionPanel.Controls.Add(siteActionLayout);

            siteSection.Controls.Add(siteListPanel);
            siteSection.Controls.Add(siteActionPanel);
            siteSection.Controls.Add(siteSectionTitle);

            sidebarContentPanel.Controls.Add(siteSection);
            sidebarContentPanel.Controls.Add(commandSection);
            sidebarContentPanel.Controls.Add(brandPanel);
            leftSidebar.Controls.Add(sidebarContentPanel);

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
            commandCards.Clear();
            commandListContent.SuspendLayout();

            try
            {
                commandListContent.Controls.Clear();
                commandListContent.RowStyles.Clear();
                commandListContent.RowCount = 0;

                for (int index = 0; index < commands.Count; index++)
                {
                    CommandEntry command = commands[index];
                    CommandSidebarCard card = new CommandSidebarCard();

                    card.Dock = DockStyle.Top;
                    card.Bind(command, commandManager.GetSnapshot(command.Id));
                    card.Selected = currentCommand != null &&
                        string.Equals(currentCommand.Id, command.Id, StringComparison.OrdinalIgnoreCase);
                    card.CardClicked += OnCommandCardClicked;

                    commandCards[command.Id] = card;
                    commandListContent.RowCount += 1;
                    commandListContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    commandListContent.Controls.Add(card, 0, commandListContent.RowCount - 1);
                }
            }
            finally
            {
                commandListContent.ResumeLayout();
            }

            ConstrainSidebarListWidth(commandListPanel, commandListContent);
            RefreshEmptyStates();
        }

        private void RefreshSiteList()
        {
            siteCards.Clear();
            siteListContent.SuspendLayout();

            try
            {
                siteListContent.Controls.Clear();
                siteListContent.RowStyles.Clear();
                siteListContent.RowCount = 0;

                for (int index = 0; index < sites.Count; index++)
                {
                    SiteEntry site = sites[index];
                    SiteSidebarCard card = new SiteSidebarCard();

                    card.Dock = DockStyle.Top;
                    card.Bind(site);
                    card.Selected = currentSite != null &&
                        string.Equals(currentSite.Id, site.Id, StringComparison.OrdinalIgnoreCase);
                    card.CardClicked += OnSiteCardClicked;

                    siteCards[site.Id] = card;
                    siteListContent.RowCount += 1;
                    siteListContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    siteListContent.Controls.Add(card, 0, siteListContent.RowCount - 1);
                }
            }
            finally
            {
                siteListContent.ResumeLayout();
            }

            ConstrainSidebarListWidth(siteListPanel, siteListContent);
            RefreshEmptyStates();
        }

        private void OnCommandCardClicked(object sender, EventArgs e)
        {
            CommandSidebarCard card = sender as CommandSidebarCard;

            if (card != null && card.Command != null)
            {
                SelectCommand(card.Command);
            }
        }

        private void OnSiteCardClicked(object sender, EventArgs e)
        {
            SiteSidebarCard card = sender as SiteSidebarCard;

            if (card != null && card.SiteEntry != null)
            {
                SelectSite(card.SiteEntry);
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
            if (!hasCommand)
            {
                startStopCommandButton.Text = "\u542f\u52a8";
                editCommandButton.Enabled = false;
                deleteCommandButton.Enabled = false;
                startStopCommandButton.Enabled = false;
                return;
            }

            startStopCommandButton.Text = isActive ? "\u505c\u6b62" : "\u542f\u52a8";
        }

        private void RefreshSiteButtons()
        {
            bool hasSite = currentSite != null;

            editSiteButton.Enabled = hasSite;
            deleteSiteButton.Enabled = hasSite && sites.Count > 1;
            openSiteButton.Enabled = hasSite;
            reloadSiteButton.Enabled = hasSite;
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
            FreezeSidebarContentForDrag();
            FreezeWorkspaceContentForSidebarDrag();
            sidebarResizeTimer.Start();
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
            sidebarResizeTimer.Stop();
            ApplyPendingSidebarResize();
            sidebarFrozenContentWidth = 0;
            sidebarFrozenWorkspaceContentWidth = 0;
            LayoutShellPanels(true);
            sidebarSplitter.Capture = false;
            sidebarSplitter.Active = false;
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

        private void OnSidebarResizeTimerTick(object sender, EventArgs e)
        {
            if (!resizingSidebar)
            {
                sidebarResizeTimer.Stop();
                return;
            }

            ApplyPendingSidebarResize();
        }

        private void ApplyPendingSidebarResize()
        {
            SetSidebarWidth(sidebarPendingWidth);
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
                LayoutShellPanels(!resizingSidebar);
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
            LayoutShellPanels(!resizingSidebar);
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
            LayoutShellPanels(!resizingSidebar);
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
            SetBoundsIfChanged(leftSidebar, 0, 0, sidebarWidth, height);
            SetBoundsIfChanged(sidebarSplitter, sidebarWidth, 0, splitterWidth, height);
            LayoutSidebarContent(resizeWorkspaceContent);
            if (resizeWorkspaceContent)
            {
                SetBoundsIfChanged(workspacePanel, workspaceX, 0, workspaceWidth, height);
            }
            rootPanel.ResumeLayout(false);
            if (resizeWorkspaceContent)
            {
                LayoutWorkspaceContent(true);
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

        private void FreezeSidebarContentForDrag()
        {
            sidebarFrozenContentWidth = sidebarContentPanel.Width > 0
                ? sidebarContentPanel.Width
                : Math.Max(SidebarMinExpandedWidth, expandedSidebarWidth);

            SetSidebarContentBounds(sidebarFrozenContentWidth);
        }

        private void LayoutSidebarContent(bool resizeContent)
        {
            int contentWidth = resizeContent
                ? (sidebarHidden ? 0 : expandedSidebarWidth)
                : sidebarFrozenContentWidth > 0
                    ? sidebarFrozenContentWidth
                    : sidebarContentPanel.Width;

            SetSidebarContentBounds(contentWidth);
        }

        private void SetSidebarContentBounds(int contentWidth)
        {
            SetBoundsIfChanged(
                sidebarContentPanel,
                0,
                0,
                Math.Max(0, contentWidth),
                leftSidebar.Height);
        }

        private void FreezeWorkspaceContentForSidebarDrag()
        {
            sidebarFrozenWorkspaceContentWidth = rightBody.Width > 0
                ? rightBody.Width
                : GetWorkspaceContentWidth();

            SetWorkspaceContentBounds(sidebarFrozenWorkspaceContentWidth);
        }

        private void LayoutWorkspaceContent(bool resizeContent)
        {
            int contentWidth = resizeContent
                ? GetWorkspaceContentWidth()
                : sidebarFrozenWorkspaceContentWidth > 0
                    ? sidebarFrozenWorkspaceContentWidth
                    : rightBody.Width;

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
            foreach (CommandEntry command in commands)
            {
                RefreshCommandCardState(command.Id);
            }
        }

        private void RefreshCommandCardState(string commandId)
        {
            CommandSidebarCard card;

            if (string.IsNullOrWhiteSpace(commandId) || !commandCards.TryGetValue(commandId, out card))
            {
                return;
            }

            card.Bind(card.Command, commandManager.GetSnapshot(commandId));
            card.Selected = currentCommand != null &&
                string.Equals(currentCommand.Id, commandId, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateCommandSelectionVisuals()
        {
            foreach (KeyValuePair<string, CommandSidebarCard> pair in commandCards)
            {
                pair.Value.Selected = currentCommand != null &&
                    string.Equals(currentCommand.Id, pair.Key, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void UpdateSiteSelectionVisuals()
        {
            foreach (KeyValuePair<string, SiteSidebarCard> pair in siteCards)
            {
                pair.Value.Selected = currentSite != null &&
                    string.Equals(currentSite.Id, pair.Key, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void OnCommandListPanelResize(object sender, EventArgs e)
        {
            ConstrainSidebarListWidth(commandListPanel, commandListContent);
            LayoutEmptyState(commandListPanel, commandEmptyLabel);
        }

        private void OnSiteListPanelResize(object sender, EventArgs e)
        {
            ConstrainSidebarListWidth(siteListPanel, siteListContent);
            LayoutEmptyState(siteListPanel, siteEmptyLabel);
        }

        private void ConstrainSidebarListWidth(BufferedScrollPanel scrollPanel, Control content)
        {
            int targetWidth;

            if (scrollPanel == null || content == null)
            {
                return;
            }

            targetWidth = Math.Max(
                180,
                scrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);

            if (content.Width != targetWidth)
            {
                content.Width = targetWidth;
            }
        }

        private TableLayoutPanel CreateSidebarListContent()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(8, 6, 8, 0);
            panel.ColumnCount = 1;
            panel.RowCount = 0;
            panel.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            panel.BackColor = UiTheme.SidebarBackground;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return panel;
        }

        private RoundedLabel CreateEmptyLabel(string text)
        {
            RoundedLabel label = new RoundedLabel();
            label.AutoSize = false;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
            label.ForeColor = UiTheme.TextMuted;
            label.BackColor = Color.FromArgb(233, 241, 249);
            label.TextAlign = ContentAlignment.TopLeft;
            label.Padding = new Padding(10, 10, 10, 10);
            label.Visible = false;
            label.CornerRadius = 6;
            return label;
        }

        private void RefreshEmptyStates()
        {
            commandListContent.Visible = commands.Count > 0;
            commandEmptyLabel.Visible = commands.Count == 0;
            siteListContent.Visible = sites.Count > 0;
            siteEmptyLabel.Visible = sites.Count == 0;
            LayoutEmptyState(commandListPanel, commandEmptyLabel);
            LayoutEmptyState(siteListPanel, siteEmptyLabel);
        }

        private void LayoutEmptyState(Control parent, Control label)
        {
            if (parent == null || label == null)
            {
                return;
            }

            label.Bounds = new Rectangle(
                8,
                8,
                Math.Max(160, parent.ClientSize.Width - 24),
                72);
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

            if (WindowState == FormWindowState.Minimized)
            {
                QueueHideToTray();
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
