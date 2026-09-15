using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_DPICHANGED = 0x02E0;

        // Design metrics at 96 DPI; everything reaches the screen through S().
        private static int S(int value)
        {
            return UiTheme.Scale(value);
        }

        private static int TitleBarHeight
        {
            get { return S(44); }
        }

        private static int ResizeGripSize
        {
            get { return S(8); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        // Must match the Win32 MINMAXINFO layout exactly:
        // ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize.
        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public NativePoint Reserved;
            public NativePoint MaxSize;
            public NativePoint MaxPosition;
            public NativePoint MinTrackSize;
            public NativePoint MaxTrackSize;
        }

        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_CLIPCHILDREN = 0x02000000;
        private const int WS_CLIPSIBLINGS = 0x04000000;
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private const int EM_SETCUEBANNER = 0x1501;

        private static void SetCueBanner(TextBox textBox, string cue)
        {
            if (textBox == null || string.IsNullOrEmpty(cue))
            {
                return;
            }

            SendMessage(textBox.Handle, EM_SETCUEBANNER, new IntPtr(1), cue);
        }

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
        private readonly DoubleBufferedPanel titleBarPanel;
        private readonly TitleBarIconButton titleSidebarButton;
        private readonly Label titleBarLabel;
        private readonly TitleBarIconButton minimizeButton;
        private readonly TitleBarIconButton maximizeButton;
        private readonly TitleBarIconButton closeButton;
        private readonly DoubleBufferedPanel rootPanel;
        private readonly DoubleBufferedPanel leftSidebar;
        private readonly SidebarSurfaceControl sidebarSurface;
        private readonly SidebarSplitterPanel sidebarSplitter;
        private readonly DoubleBufferedPanel workspacePanel;
        private readonly DoubleBufferedPanel rightBody;
        private readonly DoubleBufferedPanel webPanel;
        private readonly DoubleBufferedPanel logsPanel;
        private readonly Label webStateTitleLabel;
        private readonly Label webStateDetailLabel;
        private readonly ThemedButton webStateRetryButton;
        private readonly DoubleBufferedPanel webStateOverlay;
        private readonly Label currentCommandLabel;
        private readonly RoundedLabel commandStatusBadge;
        private readonly ThemedButton clearLogsButton;
        private readonly ThemedButton copyLogsButton;
        private readonly CheckBox autoScrollLogsCheckBox;
        private readonly CheckBox wrapLogsCheckBox;
        private readonly TextBox logFilterTextBox;
        private readonly RichTextBox logsTextBox;
        private readonly DoubleBufferedPanel webViewHost;
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
        private readonly Dictionary<string, Task<CoreWebView2Environment>> webViewEnvironments;
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
        private string cachedStartupEnabledText;
        private string lastTrayTooltipText;
        private readonly DoubleBufferedPanel webNavBar;
        private readonly ThemedButton webBackButton;
        private readonly ThemedButton webForwardButton;
        private readonly ThemedButton webReloadButton;
        private readonly ThemedButton webHomeButton;
        private readonly TextBox webUrlTextBox;
        private readonly ThemedButton webCopyUrlButton;
        private readonly ThemedButton webOpenBrowserButton;
        private readonly WorkspaceSplitterPanel workspaceSplitter;
        private readonly Panel navLeftPanel;
        private readonly Panel navRightPanel;
        private readonly ThemedButton titleMenuButton;
        private readonly ContextMenuStrip titleMenu;
        private ToolStripMenuItem titleStartupMenuItem;
        private ContextMenuStrip commandContextMenu;
        private ContextMenuStrip siteContextMenu;
        private double workspaceSplitRatio = AppConfigStore.DefaultWorkspaceSplitRatio;
        private bool draggingWorkspaceSplitter;
        private string pendingSelectedSiteId;
        private string pendingSelectedCommandId;
        private string renderedLogFilter = string.Empty;
        private float formDpiScale = 1.0f;

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
            webViewEnvironments = new Dictionary<string, Task<CoreWebView2Environment>>(StringComparer.OrdinalIgnoreCase);
            siteViews = new Dictionary<string, SiteViewState>(StringComparer.OrdinalIgnoreCase);
            sites = new List<SiteEntry>(config.Sites ?? new SiteEntry[0]);
            commands = new List<CommandEntry>(config.Commands ?? new CommandEntry[0]);
            commandManager.SyncCommands(commands);
            pendingHotkey = config.GlobalHotkey ?? HotkeyConstants.CreateDefault();
            pendingCommandSectionRatio = config.CommandSectionRatio;
            pendingSelectedSiteId = config.SelectedSiteId;
            pendingSelectedCommandId = config.SelectedCommandId;

            workspaceMode = WorkspaceModeCatalog.Parse(config.WorkspaceMode);
            workspaceSplitRatio = config.WorkspaceSplitRatio;
            sidebarHidden = config.SidebarHidden;

            SetWindowTitle(AppName);
            AutoScaleMode = AutoScaleMode.None;
            Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            formDpiScale = UiTheme.DpiScale;
            MinimumSize = new Size(S(980), S(640));
            StartPosition = FormStartPosition.CenterScreen;
            RestoreWindowPlacement(config);
            FormBorderStyle = FormBorderStyle.None;
            Icon = appIcon;
            BackColor = UiTheme.WindowBackground;
            preTrayWindowState = WindowState;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            DoubleBuffered = true;

            statusLabel = new ToolStripStatusLabel("\u6b63\u5728\u52a0\u8f7d\u5de5\u4f5c\u53f0...");
            statusStrip = new StatusStrip();
            statusStrip.SizingGrip = false;
            statusStrip.Items.Add(statusLabel);
            UiTheme.ApplyModernMenuTheme(statusStrip);

            titleBarPanel = new DoubleBufferedPanel();
            titleBarPanel.Dock = DockStyle.Top;
            titleBarPanel.Height = TitleBarHeight;
            titleBarPanel.BackColor = UiTheme.WindowBackground;
            titleBarPanel.MouseDown += OnTitleBarMouseDown;
            titleBarPanel.MouseMove += OnTitleBarMouseMove;
            titleBarPanel.MouseUp += OnTitleBarMouseUp;
            titleBarPanel.MouseDoubleClick += OnTitleBarMouseDoubleClick;
            titleBarPanel.Resize += OnTitleBarResize;

            titleSidebarButton = new TitleBarIconButton(TitleBarButtonKind.Sidebar);
            titleSidebarButton.Location = new Point(S(10), S(6));
            titleSidebarButton.SidebarCollapsed = sidebarHidden;
            titleSidebarButton.Click += OnSidebarToggleClicked;

            titleBarLabel = new Label();
            titleBarLabel.AutoSize = false;
            titleBarLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleBarLabel.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
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

            titleMenu = new ContextMenuStrip();
            titleMenu.Opening += OnTitleMenuOpening;
            titleStartupMenuItem = new ToolStripMenuItem("开机自启");
            titleStartupMenuItem.CheckOnClick = true;
            titleStartupMenuItem.Click += OnTrayStartupMenuClicked;
            titleMenu.Items.Add("快捷键设置…", null, delegate { OnConfigureHotkeyClicked(this, EventArgs.Empty); });
            titleMenu.Items.Add(titleStartupMenuItem);
            titleMenu.Items.Add(new ToolStripSeparator());
            titleMenu.Items.Add("导入配置…", null, delegate { ImportConfig(); });
            titleMenu.Items.Add("导出配置…", null, delegate { ExportConfig(); });
            titleMenu.Items.Add("打开日志文件夹", null, delegate { OpenLogFolder(); });
            titleMenu.Items.Add(new ToolStripSeparator());
            titleMenu.Items.Add("退出", null, delegate { ExitApplication(); });
            UiTheme.ApplyModernMenuTheme(titleMenu);

            titleMenuButton = CreateToolbarButton("⋯", "菜单");
            titleMenuButton.Click += delegate
            {
                titleMenu.Show(titleMenuButton, new Point(0, titleMenuButton.Height));
            };

            titleBarPanel.Controls.Add(titleSidebarButton);
            titleBarPanel.Controls.Add(titleBarLabel);
            titleBarPanel.Controls.Add(titleMenuButton);
            titleBarPanel.Controls.Add(minimizeButton);
            titleBarPanel.Controls.Add(maximizeButton);
            titleBarPanel.Controls.Add(closeButton);
            LayoutTitleBarControls();

            rootPanel = new DoubleBufferedPanel();
            rootPanel.Dock = DockStyle.Fill;
            rootPanel.BackColor = BackColor;
            rootPanel.Margin = new Padding(0);
            rootPanel.Padding = new Padding(0);
            rootPanel.Resize += OnRootPanelResize;

            leftSidebar = new DoubleBufferedPanel();
            leftSidebar.Dock = DockStyle.None;
            expandedSidebarWidth = config.SidebarWidth > 0
                ? Math.Max(S(SidebarMinExpandedWidth), Math.Min(S(SidebarMaxWidth), config.SidebarWidth))
                : S(DefaultSidebarWidth);
            leftSidebar.Width = expandedSidebarWidth;
            leftSidebar.BackColor = UiTheme.SidebarBackground;
            leftSidebar.Padding = new Padding(0);
            leftSidebar.Visible = !sidebarHidden;

            sidebarSurface = new SidebarSurfaceControl();
            sidebarSurface.BackColor = UiTheme.SidebarBackground;
            sidebarSurface.SnapshotProvider = delegate(string commandId)
            {
                return commandManager.GetSnapshot(commandId);
            };
            sidebarSurface.StopAllCommandsClicked += OnStopAllCommandsClicked;
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
            sidebarSplitter.Collapsed = sidebarHidden;
            sidebarSplitter.MouseDown += OnSidebarSplitterMouseDown;
            sidebarSplitter.MouseMove += OnSidebarSplitterMouseMove;
            sidebarSplitter.MouseUp += OnSidebarSplitterMouseUp;
            sidebarSplitter.MouseDoubleClick += delegate
            {
                // Double-click restores the default sidebar width.
                SetSidebarWidth(S(DefaultSidebarWidth));
                PersistConfig();
            };

            leftSidebar.Controls.Add(sidebarSurface);

            workspacePanel = new DoubleBufferedPanel();
            workspacePanel.Dock = DockStyle.None;
            workspacePanel.Padding = new Padding(S(14), S(14), S(14), S(14));
            workspacePanel.BackColor = BackColor;

            rightBody = new DoubleBufferedPanel();
            rightBody.Dock = DockStyle.None;
            rightBody.Padding = new Padding(0);

            webPanel = new DoubleBufferedPanel();
            webPanel.Dock = DockStyle.None;
            webPanel.BackColor = UiTheme.Surface;
            webPanel.Padding = new Padding(S(10));

            webNavBar = new DoubleBufferedPanel();
            webNavBar.Dock = DockStyle.Top;
            webNavBar.Height = S(36);
            webNavBar.BackColor = UiTheme.Surface;
            webNavBar.Padding = new Padding(0, 0, 0, S(6));

            navLeftPanel = new Panel();
            navLeftPanel.Dock = DockStyle.Left;
            navLeftPanel.Width = S(144);
            navLeftPanel.BackColor = UiTheme.Surface;

            webBackButton = CreateToolbarButton("‹", "返回上一页 (Alt+Left)");
            webBackButton.Width = S(32);
            webBackButton.Height = S(30);
            webBackButton.Location = new Point(0, 0);
            webBackButton.Click += delegate { GoBackCurrentSite(); };

            webForwardButton = CreateToolbarButton("›", "前进 (Alt+Right)");
            webForwardButton.Width = S(32);
            webForwardButton.Height = S(30);
            webForwardButton.Location = new Point(S(36), 0);
            webForwardButton.Click += delegate { GoForwardCurrentSite(); };

            webReloadButton = CreateToolbarButton("↻", "刷新页面 (F5)");
            webReloadButton.Width = S(32);
            webReloadButton.Height = S(30);
            webReloadButton.Location = new Point(S(72), 0);
            webReloadButton.Click += delegate { ReloadCurrentSite(); };

            webHomeButton = CreateToolbarButton("⌂", "回到配置主页");
            webHomeButton.Width = S(32);
            webHomeButton.Height = S(30);
            webHomeButton.Location = new Point(S(108), 0);
            webHomeButton.Click += delegate { NavigateCurrentSiteHome(); };

            navLeftPanel.Controls.Add(webBackButton);
            navLeftPanel.Controls.Add(webForwardButton);
            navLeftPanel.Controls.Add(webReloadButton);
            navLeftPanel.Controls.Add(webHomeButton);

            navRightPanel = new Panel();
            navRightPanel.Dock = DockStyle.Right;
            navRightPanel.Width = S(148);
            navRightPanel.BackColor = UiTheme.Surface;

            webCopyUrlButton = CreateToolbarButton("复制", "复制当前网址");
            webCopyUrlButton.Width = S(52);
            webCopyUrlButton.Height = S(30);
            webCopyUrlButton.Location = new Point(S(6), 0);
            webCopyUrlButton.Click += delegate { CopyCurrentSiteUrl(); };

            webOpenBrowserButton = CreateToolbarButton("↗ 浏览器", "在系统默认浏览器中打开");
            webOpenBrowserButton.Width = S(84);
            webOpenBrowserButton.Height = S(30);
            webOpenBrowserButton.Location = new Point(S(62), 0);
            webOpenBrowserButton.Click += delegate { OpenCurrentSiteInDefaultBrowser(); };

            navRightPanel.Controls.Add(webCopyUrlButton);
            navRightPanel.Controls.Add(webOpenBrowserButton);

            RoundedPanel urlFrame = new RoundedPanel();
            urlFrame.Dock = DockStyle.Fill;
            urlFrame.Margin = new Padding(S(6), 0, S(6), 0);
            urlFrame.Padding = new Padding(S(12), S(7), S(12), S(6));
            urlFrame.BackColor = UiTheme.SecondaryBack;
            urlFrame.BorderColor = UiTheme.Border;
            urlFrame.BorderWidth = 1f;
            urlFrame.CornerRadius = 7;

            webUrlTextBox = new TextBox();
            webUrlTextBox.Dock = DockStyle.Fill;
            webUrlTextBox.BorderStyle = BorderStyle.None;
            webUrlTextBox.BackColor = UiTheme.SecondaryBack;
            webUrlTextBox.ForeColor = UiTheme.TextPrimary;
            webUrlTextBox.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            webUrlTextBox.KeyDown += OnWebUrlTextBoxKeyDown;

            webUrlTextBox.Enter += delegate
            {
                urlFrame.BorderColor = UiTheme.FocusRing;
                urlFrame.BorderWidth = 1.5f;
            };

            webUrlTextBox.Leave += delegate
            {
                urlFrame.BorderColor = UiTheme.Border;
                urlFrame.BorderWidth = 1f;
            };

            urlFrame.Controls.Add(webUrlTextBox);

            webNavBar.Controls.Add(urlFrame);
            webNavBar.Controls.Add(navRightPanel);
            webNavBar.Controls.Add(navLeftPanel);

            webViewHost = new DoubleBufferedPanel();
            webViewHost.Dock = DockStyle.Fill;
            webViewHost.BackColor = UiTheme.WindowBackground;
            webViewHost.Padding = new Padding(0);

            webStateOverlay = new DoubleBufferedPanel();
            webStateOverlay.BackColor = UiTheme.WindowBackground;
            webStateOverlay.Dock = DockStyle.Fill;
            webStateOverlay.Visible = true;

            TableLayoutPanel webStateLayout = new TableLayoutPanel();
            webStateLayout.Dock = DockStyle.Fill;
            webStateLayout.ColumnCount = 3;
            webStateLayout.RowCount = 5;
            webStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            webStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(420)));
            webStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(36)));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(48)));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(42)));
            webStateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            webStateTitleLabel = new Label();
            webStateTitleLabel.Dock = DockStyle.Fill;
            webStateTitleLabel.Font = UiTheme.CreateFont(13f, FontStyle.Bold);
            webStateTitleLabel.ForeColor = UiTheme.TextPrimary;
            webStateTitleLabel.TextAlign = ContentAlignment.BottomCenter;
            webStateTitleLabel.Text = "\u6b63\u5728\u51c6\u5907\u7f51\u9875\u5de5\u4f5c\u533a";

            webStateDetailLabel = new Label();
            webStateDetailLabel.Dock = DockStyle.Fill;
            webStateDetailLabel.Font = UiTheme.CreateFont(9.25f, FontStyle.Regular);
            webStateDetailLabel.ForeColor = UiTheme.TextSecondary;
            webStateDetailLabel.TextAlign = ContentAlignment.TopCenter;
            webStateDetailLabel.AutoEllipsis = true;
            webStateDetailLabel.Text = "\u7b49\u5f85 WebView2 \u521d\u59cb\u5316\u3002";

            webStateRetryButton = CreatePrimaryButton("重试", 0, 0, S(96));
            webStateRetryButton.Dock = DockStyle.Top;
            webStateRetryButton.Margin = new Padding(S(156), S(2), S(156), 0);
            webStateRetryButton.Click += OnReloadSiteClicked;

            webStateLayout.Controls.Add(webStateTitleLabel, 1, 1);
            webStateLayout.Controls.Add(webStateDetailLabel, 1, 2);
            webStateLayout.Controls.Add(webStateRetryButton, 1, 3);
            webStateOverlay.Controls.Add(webStateLayout);

            webPanel.Controls.Add(webViewHost);
            webPanel.Controls.Add(webNavBar);
            webViewHost.Controls.Add(webStateOverlay);

            logsPanel = new DoubleBufferedPanel();
            logsPanel.Dock = DockStyle.None;
            logsPanel.BackColor = UiTheme.Surface;
            logsPanel.Padding = new Padding(S(12));

            currentCommandLabel = new Label();
            currentCommandLabel.Text = "未选择命令";
            currentCommandLabel.Font = UiTheme.CreateFont(11.5f, FontStyle.Bold);
            currentCommandLabel.ForeColor = UiTheme.TextPrimary;
            currentCommandLabel.AutoSize = true;
            currentCommandLabel.Dock = DockStyle.Fill;
            currentCommandLabel.TextAlign = ContentAlignment.MiddleLeft;
            currentCommandLabel.AutoEllipsis = true;

            commandStatusBadge = UiTheme.CreateBadgeLabel();
            commandStatusBadge.Text = "已停止";
            commandStatusBadge.Size = new Size(S(104), S(30));
            commandStatusBadge.Dock = DockStyle.Right;
            commandStatusBadge.BackColor = UiTheme.BadgeNeutralBackground;
            commandStatusBadge.ForeColor = UiTheme.BadgeNeutralForeground;

            clearLogsButton = CreateSecondaryButton("清空日志", 0, 0, S(92));
            clearLogsButton.Click += OnClearLogsClicked;
            copyLogsButton = CreateSecondaryButton("复制日志", 0, 0, S(92));
            copyLogsButton.Click += OnCopyLogsClicked;

            logFilterTextBox = new TextBox();
            logFilterTextBox.BorderStyle = BorderStyle.FixedSingle;
            logFilterTextBox.BackColor = UiTheme.Surface;
            logFilterTextBox.ForeColor = UiTheme.TextPrimary;
            logFilterTextBox.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            logFilterTextBox.Size = new Size(S(112), S(24));
            logFilterTextBox.Location = new Point(0, S(9));
            logFilterTextBox.TextChanged += delegate { RefreshLogsView(); };
            SetCueBanner(logFilterTextBox, "筛选日志...");

            wrapLogsCheckBox = new CheckBox();
            wrapLogsCheckBox.Text = "自动换行";
            wrapLogsCheckBox.Checked = false;
            wrapLogsCheckBox.AutoSize = true;
            wrapLogsCheckBox.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            wrapLogsCheckBox.ForeColor = UiTheme.TextSecondary;
            wrapLogsCheckBox.Location = new Point(S(120), S(12));
            wrapLogsCheckBox.CheckedChanged += OnWrapLogsChanged;

            autoScrollLogsCheckBox = new CheckBox();
            autoScrollLogsCheckBox.Text = "自动滚动";
            autoScrollLogsCheckBox.Checked = true;
            autoScrollLogsCheckBox.AutoSize = true;
            autoScrollLogsCheckBox.Font = UiTheme.CreateFont(9f, FontStyle.Regular);
            autoScrollLogsCheckBox.ForeColor = UiTheme.TextSecondary;
            autoScrollLogsCheckBox.Location = new Point(S(202), S(12));
            autoScrollLogsCheckBox.CheckedChanged += OnAutoScrollLogsChanged;

            Panel logsToolbar = new Panel();
            logsToolbar.Dock = DockStyle.Top;
            logsToolbar.Height = S(44);
            logsToolbar.BackColor = UiTheme.Surface;

            Panel logsTitlePanel = new Panel();
            logsTitlePanel.Dock = DockStyle.Fill;
            logsTitlePanel.BackColor = UiTheme.Surface;
            logsTitlePanel.Controls.Add(commandStatusBadge);
            logsTitlePanel.Controls.Add(currentCommandLabel);

            Panel logsActionPanel = new Panel();
            logsActionPanel.Dock = DockStyle.Right;
            logsActionPanel.Width = S(498);
            logsActionPanel.BackColor = UiTheme.Surface;
            logsActionPanel.Controls.Add(logFilterTextBox);
            logsActionPanel.Controls.Add(wrapLogsCheckBox);
            logsActionPanel.Controls.Add(autoScrollLogsCheckBox);
            logsActionPanel.Controls.Add(clearLogsButton);
            logsActionPanel.Controls.Add(copyLogsButton);
            clearLogsButton.Location = new Point(S(300), S(7));
            copyLogsButton.Location = new Point(S(400), S(7));

            // Fill-docked panel must be added before the right-docked one.
            logsToolbar.Controls.Add(logsTitlePanel);
            logsToolbar.Controls.Add(logsActionPanel);

            logsTextBox = new RichTextBox();
            logsTextBox.Dock = DockStyle.Fill;
            logsTextBox.ReadOnly = true;
            logsTextBox.ScrollBars = RichTextBoxScrollBars.Both;
            logsTextBox.WordWrap = false;
            logsTextBox.BorderStyle = BorderStyle.None;
            logsTextBox.DetectUrls = false;
            logsTextBox.BackColor = UiTheme.TerminalBackground;
            logsTextBox.ForeColor = UiTheme.TerminalForeground;
            logsTextBox.Font = UiTheme.CreateMonospaceFont(10f, FontStyle.Regular);

            logsPanel.Controls.Add(logsTextBox);
            logsPanel.Controls.Add(logsToolbar);

            workspaceSplitter = new WorkspaceSplitterPanel();
            workspaceSplitter.Height = S(8);
            workspaceSplitter.Visible = false;
            workspaceSplitter.MouseDown += OnWorkspaceSplitterMouseDown;
            workspaceSplitter.MouseMove += OnWorkspaceSplitterMouseMove;
            workspaceSplitter.MouseUp += OnWorkspaceSplitterMouseUp;
            workspaceSplitter.MouseDoubleClick += delegate
            {
                if (workspaceMode == WorkspaceMode.Split)
                {
                    workspaceSplitRatio = AppConfigStore.DefaultWorkspaceSplitRatio;
                    LayoutRightBodyContent();
                    PersistConfig();
                }
            };

            rightBody.Controls.Add(webPanel);
            rightBody.Controls.Add(workspaceSplitter);
            rightBody.Controls.Add(logsPanel);
            rightBody.Resize += delegate { LayoutRightBodyContent(); };

            workspacePanel.Controls.Add(rightBody);

            rootPanel.Controls.Add(leftSidebar);
            rootPanel.Controls.Add(sidebarSplitter);
            rootPanel.Controls.Add(workspacePanel);
            LayoutShellPanels();

            Controls.Add(rootPanel);
            Controls.Add(statusStrip);
            Controls.Add(titleBarPanel);

            trayMenu = new ContextMenuStrip();
            trayMenu.Opening += delegate { AppLogger.Info("tray", "托盘菜单打开"); };
            trayMenu.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e) { AppLogger.Info("tray", "托盘菜单关闭 reason=" + e.CloseReason); };
            trayMenu.Items.Add("打开主界面", null, delegate { RestoreFromTray(); });
            trayMenu.Items.Add("刷新当前页面", null, delegate { ReloadCurrentSite(); });
            trayMenu.Items.Add("启动自启命令", null, delegate { commandManager.StartEnabledCommands(commands); });
            trayMenu.Items.Add("全部停止命令", null, delegate { ConfirmAndStopAll(); });
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

            trayMenu.Items.Add("\u6253\u5f00\u65e5\u5fd7\u6587\u4ef6\u5939", null, delegate { OpenLogFolder(); });
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

            SyncStartupMenuItems(WindowsStartupManager.IsEnabled());

            UiTheme.ApplyModernMenuTheme(trayMenu);
            InitializeContextMenus();
            sidebarSurface.CommandContextMenuRequested += delegate(object sender, SidebarItemContextMenuEventArgs<CommandEntry> e)
            {
                if (e != null && e.Item != null)
                {
                    SelectCommand(e.Item, false);
                    commandContextMenu.Show(e.ScreenLocation);
                }
            };
            sidebarSurface.SiteContextMenuRequested += delegate(object sender, SidebarItemContextMenuEventArgs<SiteEntry> e)
            {
                if (e != null && e.Item != null)
                {
                    // Right-click selects the entry without forcing a workspace-mode
                    // switch or a navigation; actions in the menu decide what happens.
                    SelectSite(e.Item, false);
                    siteContextMenu.Show(e.ScreenLocation);
                }
            };
            sidebarSurface.CommandInlineActionRequested += delegate(object sender, SidebarCommandInlineActionEventArgs e)
            {
                if (e != null && e.Command != null)
                {
                    SelectCommand(e.Command, false);
                    if (e.Action == CommandInlineAction.StartStop)
                    {
                        OnStartStopCommandClicked(this, EventArgs.Empty);
                    }
                    else if (e.Action == CommandInlineAction.Restart)
                    {
                        OnRestartCommandClicked(this, EventArgs.Empty);
                    }
                }
            };

            Shown += OnShown;
            Resize += OnResize;
            ResizeEnd += delegate { PersistConfig(); };
            FormClosing += OnFormClosing;

            RefreshCommandList();
            RefreshSiteList();
            SetWorkspaceMode(workspaceMode);
            RefreshCommandButtons();
            RefreshSiteButtons();
            UpdateStatusSummary();
            EnableDoubleBufferingRecursive(this);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.Style |= WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
                return createParams;
            }
        }

        private async void OnShown(object sender, EventArgs e)
        {
            try
            {
                AppLogger.Info("startup", "\u5f00\u59cb\u521d\u59cb\u5316 WebView2 \u8fd0\u884c\u73af\u5883");
                webViewEnvironment = await GetOrCreateEnvironmentAsync(string.Empty);

                AppLogger.Info("startup", "WebView2 \u5c31\u7eea\uff0c\u7ad9\u70b9 " + sites.Count + " \u4e2a\uff0c\u547d\u4ee4 " + commands.Count + " \u4e2a\uff0c\u81ea\u542f\u547d\u4ee4 " + CountEnabledOnStart());
                SetTransientStatus("\u5de5\u4f5c\u53f0\u5df2\u5c31\u7eea\u3002");
                SetWebState("\u7f51\u9875\u5de5\u4f5c\u533a\u5df2\u5c31\u7eea", "\u8bf7\u9009\u62e9\u4e00\u4e2a\u7ad9\u70b9\u6216\u7b49\u5f85\u9ed8\u8ba4\u7ad9\u70b9\u52a0\u8f7d\u3002", false);
                uiRefreshTimer.Start();
                RestartSiteHealthProbe();

                CommandEntry commandToSelect = FindCommandById(pendingSelectedCommandId);
                SiteEntry siteToSelect = FindSiteById(pendingSelectedSiteId);
                pendingSelectedCommandId = null;
                pendingSelectedSiteId = null;

                if (commandToSelect == null && commands.Count > 0)
                {
                    commandToSelect = commands[0];
                }

                if (siteToSelect == null && sites.Count > 0)
                {
                    siteToSelect = sites[0];
                }

                if (commandToSelect != null)
                {
                    SelectCommand(commandToSelect, false);
                }

                if (siteToSelect != null)
                {
                    // In Logs mode a plain selection is enough; Web/Split activate the view.
                    SelectSite(siteToSelect, workspaceMode != WorkspaceMode.Logs);
                }

                if (!startupCommandsRequested)
                {
                    startupCommandsRequested = true;
                    commandManager.StartEnabledCommands(commands);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("startup", "WebView2 \u521d\u59cb\u5316\u5931\u8d25", ex);
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
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = new IntPtr(1);
                return;
            }

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

            if (m.Msg == WM_GETMINMAXINFO)
            {
                HandleGetMinMaxInfo(ref m);
                return;
            }

            if (m.Msg == WM_DPICHANGED)
            {
                HandleDpiChanged(m);
                base.WndProc(ref m);
                return;
            }

            base.WndProc(ref m);
        }

        // A borderless window maximizes over the ENTIRE screen (taskbar included)
        // unless the system is told otherwise. Clip the maximized bounds to the
        // working area of the monitor the window is on.
        private void HandleGetMinMaxInfo(ref Message m)
        {
            Rectangle workingArea = Screen.FromHandle(Handle).WorkingArea;
            MinMaxInfo info = (MinMaxInfo)Marshal.PtrToStructure(m.LParam, typeof(MinMaxInfo));

            info.MaxSize = new NativePoint { X = workingArea.Width, Y = workingArea.Height };
            info.MaxPosition = new NativePoint { X = workingArea.Left, Y = workingArea.Top };
            info.MinTrackSize = new NativePoint { X = MinimumSize.Width, Y = MinimumSize.Height };

            Marshal.StructureToPtr(info, m.LParam, true);
            m.Result = IntPtr.Zero;
        }

        private void HandleDpiChanged(Message m)
        {
            float oldScale = formDpiScale;
            int dpi = unchecked((short)((long)m.WParam & 0xFFFF));

            if (dpi > 0)
            {
                UiTheme.SetDpiScale(dpi / 96f);
                formDpiScale = UiTheme.DpiScale;
            }

            // lParam carries the system-suggested window rect for the new DPI.
            if (m.LParam != IntPtr.Zero)
            {
                NativeRect suggested = (NativeRect)Marshal.PtrToStructure(m.LParam, typeof(NativeRect));

                if (WindowState == FormWindowState.Normal)
                {
                    Bounds = new Rectangle(
                        suggested.Left,
                        suggested.Top,
                        Math.Max(MinimumSize.Width, suggested.Right - suggested.Left),
                        Math.Max(MinimumSize.Height, suggested.Bottom - suggested.Top));
                }
            }

            RescaleFonts(this, oldScale, UiTheme.DpiScale);
            UiTheme.ApplyModernMenuTheme(trayMenu);
            UiTheme.ApplyModernMenuTheme(titleMenu);
            UiTheme.ApplyModernMenuTheme(statusStrip);
            ApplyDpiSizes();
        }

        // UiTheme fonts are pixel-unit and sized for the scale at creation time;
        // after a DPI change they must be rebuilt at the new scale.
        private static void RescaleFonts(Control control, float oldScale, float newScale)
        {
            if (oldScale <= 0.01f || Math.Abs(oldScale - newScale) < 0.001f)
            {
                return;
            }

            Font font = control.Font;

            if (font != null && font.Unit == GraphicsUnit.Pixel)
            {
                float designPixels = font.Size / oldScale;
                control.Font = new Font(font.FontFamily, designPixels * newScale, font.Style, GraphicsUnit.Pixel);
            }

            foreach (Control child in control.Controls)
            {
                RescaleFonts(child, oldScale, newScale);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Re-apply every DPI-dependent size after the scale factor changed. Fonts on        // native controls keep their pixel size (UiTheme fonts are pixel-based), so
        // they are recreated here from their current design size.
        private void ApplyDpiSizes()
        {
            titleBarPanel.Height = TitleBarHeight;
            webNavBar.Height = S(36);
            webNavBar.Padding = new Padding(0, 0, 0, S(6));
            navLeftPanel.Width = S(144);
            navRightPanel.Width = S(148);
            logsPanel.Padding = new Padding(S(12));
            workspacePanel.Padding = new Padding(S(14), S(14), S(14), S(14));
            workspaceSplitter.Height = S(8);

            LayoutTitleBarControls();
            LayoutShellPanels(true);
        }

        private void RestoreWindowPlacement(AppConfig config)
        {
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            Size defaultSize = new Size(
                Math.Min(S(1540), Math.Max(S(980), workingArea.Width - S(80))),
                Math.Min(S(930), Math.Max(S(640), workingArea.Height - S(80))));

            if (config.WindowWidth >= S(480) && config.WindowHeight >= S(360))
            {
                Rectangle saved = new Rectangle(config.WindowLeft, config.WindowTop, config.WindowWidth, config.WindowHeight);
                bool onScreen = false;

                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(saved))
                    {
                        onScreen = true;
                        break;
                    }
                }

                if (onScreen)
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = saved;
                }
                else
                {
                    Size = defaultSize;
                }
            }
            else
            {
                Size = defaultSize;
            }

            if (config.WindowMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }
        }

        private Rectangle GetPersistableBounds()
        {
            return WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.D1))
            {
                SetWorkspaceMode(WorkspaceMode.Web);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D2))
            {
                SetWorkspaceMode(WorkspaceMode.Split);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D3))
            {
                SetWorkspaceMode(WorkspaceMode.Logs);
                return true;
            }

            if (keyData == (Keys.Control | Keys.B))
            {
                OnSidebarToggleClicked(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.F5)
            {
                ReloadCurrentSite();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Left))
            {
                GoBackCurrentSite();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Right))
            {
                GoForwardCurrentSite();
                return true;
            }

            if (keyData == (Keys.Control | Keys.L))
            {
                if (webUrlTextBox != null && webUrlTextBox.Enabled)
                {
                    webUrlTextBox.Focus();
                    webUrlTextBox.SelectAll();
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void HandleWindowHitTest(ref Message m)
        {
            Point clientPoint = PointToClient(new Point(
                unchecked((short)((long)m.LParam & 0xFFFF)),
                unchecked((short)(((long)m.LParam >> 16) & 0xFFFF))));

            // While maximized the window must not offer resize edges; dragging the
            // screen edge would otherwise tear the window out of the maximized state.
            if (WindowState != FormWindowState.Maximized)
            {
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
                child == titleMenuButton ||
                child == minimizeButton ||
                child == maximizeButton ||
                child == closeButton;
        }

        private void OnUiRefreshTimerTick(object sender, EventArgs e)
        {
            // While the tray context menu is open the UI thread runs inside the menu's
            // modal loop; Explorer side is paused on our menu callback. Doing periodic
            // UI work here (in particular anything that talks to Explorer or the
            // registry) risks wedging the loop. The next tick after the menu closes
            // catches everything up.
            if (trayMenu.Visible)
            {
                return;
            }

            RefreshCommandCardsState();
            RefreshCommandButtons();
            if (workspaceMode == WorkspaceMode.Logs || workspaceMode == WorkspaceMode.Split)
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
            if (trayMenu.Visible)
            {
                return;
            }

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

            if ((workspaceMode != WorkspaceMode.Logs && workspaceMode != WorkspaceMode.Split) || currentCommand == null)
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
            if (e == null || string.IsNullOrEmpty(e.Id))
            {
                return;
            }

            int index = commands.FindIndex(delegate(CommandEntry command)
            {
                return string.Equals(command.Id, e.Id, StringComparison.OrdinalIgnoreCase);
            });
            int target = index + e.Delta;

            if (index < 0 || target < 0 || target >= commands.Count)
            {
                return;
            }

            CommandEntry temporary = commands[index];
            commands[index] = commands[target];
            commands[target] = temporary;

            PersistAndSyncCommands();
            RefreshCommandList();
            sidebarSurface.EnsureCommandVisible(target);
        }

        private void OnSiteReorderRequested(object sender, SidebarReorderEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.Id))
            {
                return;
            }

            int index = sites.FindIndex(delegate(SiteEntry site)
            {
                return string.Equals(site.Id, e.Id, StringComparison.OrdinalIgnoreCase);
            });
            int target = index + e.Delta;

            if (index < 0 || target < 0 || target >= sites.Count)
            {
                return;
            }

            SiteEntry temporary = sites[index];
            sites[index] = sites[target];
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

            if (switchToLogs && workspaceMode != WorkspaceMode.Split)
            {
                SetWorkspaceMode(WorkspaceMode.Logs);
            }
        }

        private void SelectSite(SiteEntry site)
        {
            SelectSite(site, true);
        }

        // activate=false selects the entry visually (right-click context menus) without
        // forcing a workspace-mode switch or navigating the embedded browser.
        private void SelectSite(SiteEntry site, bool activate)
        {
            currentSite = site;
            UpdateSiteSelectionVisuals();
            RefreshSiteButtons();
            RefreshWebNavigationVisuals();

            if (!activate)
            {
                return;
            }

            if (workspaceMode != WorkspaceMode.Split)
            {
                SetWorkspaceMode(WorkspaceMode.Web);
            }
            else
            {
                ShowSite(site);
            }
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

            string proxyKey = GetSiteProxyKey(site);
            state = GetOrCreateSiteView(site);

            if (state.IsInitialized && !string.Equals(state.ProxyKey, proxyKey, StringComparison.OrdinalIgnoreCase))
            {
                ResetSiteWebView(state, proxyKey);
            }

            foreach (Control control in webViewHost.Controls)
            {
                control.Visible = false;
            }

            if (!webViewHost.Visible && workspaceMode != WorkspaceMode.Logs)
            {
                webViewHost.Visible = true;
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

                RefreshWebNavigationVisuals();
                return;
            }

            state.InitializationStarted = true;

            try
            {
                SetTransientStatus("\u6b63\u5728\u521d\u59cb\u5316 " + site.Name + " \u7f51\u9875\u73af\u5883...");
                CoreWebView2Environment env = await GetOrCreateEnvironmentAsync(proxyKey);
                await state.WebView.EnsureCoreWebView2Async(env);
                state.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                state.WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                state.WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                state.IsInitialized = true;
                state.ProxyKey = proxyKey;
                state.LastNavigatedUrl = site.Url;
                state.WebView.CoreWebView2.Navigate(site.Url);
                SetWebState("\u6b63\u5728\u52a0\u8f7d " + site.Name, site.Url, false);
                RefreshWebNavigationVisuals();
            }
            catch (Exception ex)
            {
                AppLogger.Error("web", "打开站点失败: " + site.Name + " " + site.Url, ex);
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
            state.ProxyKey = GetSiteProxyKey(site);
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

        private void ResetSiteWebView(SiteViewState state, string newProxyKey)
        {
            if (state.WebView != null)
            {
                webViewHost.Controls.Remove(state.WebView);
                try
                {
                    state.WebView.Dispose();
                }
                catch
                {
                }
            }

            state.WebView = new WebView2();
            state.WebView.Dock = DockStyle.Fill;
            state.WebView.Visible = false;
            state.WebView.Margin = new Padding(0);
            state.WebView.Tag = state;
            state.WebView.NavigationStarting += OnNavigationStarting;
            state.WebView.NavigationCompleted += OnNavigationCompleted;
            webViewHost.Controls.Add(state.WebView);

            state.IsInitialized = false;
            state.InitializationStarted = false;
            state.ProxyKey = newProxyKey;
        }

        private void DisposeSiteViewsNotIn(List<SiteEntry> keep)
        {
            List<string> staleIds = new List<string>();

            foreach (KeyValuePair<string, SiteViewState> pair in siteViews)
            {
                bool found = false;

                for (int index = 0; index < keep.Count; index++)
                {
                    if (string.Equals(keep[index].Id, pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    staleIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < staleIds.Count; index++)
            {
                SiteViewState state = siteViews[staleIds[index]];
                webViewHost.Controls.Remove(state.WebView);
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

                siteViews.Remove(staleIds[index]);
            }
        }

        private string GetSiteProxyKey(SiteEntry site)
        {
            if (site == null || !site.ProxyEnabled || string.IsNullOrWhiteSpace(site.ProxyServer))
            {
                return string.Empty;
            }

            return site.ProxyServer.Trim();
        }

        private static string ComputeSimpleHash(string input)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input.ToLowerInvariant());
                byte[] hash = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder("p_");
                for (int i = 0; i < Math.Min(hash.Length, 8); i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync(string proxyKey)
        {
            lock (webViewEnvironments)
            {
                Task<CoreWebView2Environment> task;
                if (webViewEnvironments.TryGetValue(proxyKey, out task))
                {
                    return task;
                }

                task = CreateEnvironmentInternalAsync(proxyKey);
                webViewEnvironments[proxyKey] = task;
                return task;
            }
        }

        private async Task<CoreWebView2Environment> CreateEnvironmentInternalAsync(string proxyKey)
        {
            string userDataDir;
            CoreWebView2EnvironmentOptions options = null;

            if (string.IsNullOrEmpty(proxyKey))
            {
                userDataDir = AppPaths.WebViewUserDataDirectory;
            }
            else
            {
                string hash = ComputeSimpleHash(proxyKey);
                userDataDir = System.IO.Path.Combine(AppPaths.WebViewUserDataDirectory, "proxies", hash);
                options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = "--proxy-server=" + proxyKey;
            }

            AppLogger.Info("web", string.IsNullOrEmpty(proxyKey)
                ? "初始化直连 WebView2 环境: " + userDataDir
                : "初始化代理 WebView2 环境 [" + proxyKey + "]: " + userDataDir);

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir, options);
            return env;
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
                RefreshWebNavigationVisuals();
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

            // Popups (target=_blank etc.) used to be forced into the current view,
            // throwing away the page the user was on. Open them in the system
            // browser instead so the embedded page keeps its state.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri,
                    UseShellExecute = true
                });
                SetTransientStatus("已在外部浏览器打开新窗口链接。");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("web", "无法在外部浏览器打开链接: " + e.Uri + " " + ex.Message);
                SetTransientStatus("无法打开新窗口链接。", 4, true);
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
                AppLogger.Warn("web", "站点页面加载失败: " + state.Site.Name + " " + navigationUrl +
                    " 错误=" + e.WebErrorStatus);
            }

            SetTransientStatus(e.IsSuccess
                ? "\u5df2\u52a0\u8f7d " + state.Site.Name
                : "\u65e0\u6cd5\u8bbf\u95ee " + navigationUrl);
            SetWebState(
                e.IsSuccess ? string.Empty : "\u65e0\u6cd5\u8bbf\u95ee\u9875\u9762",
                e.IsSuccess ? string.Empty : navigationUrl,
                !e.IsSuccess);
            RefreshSiteButtons();
            RefreshWebNavigationVisuals();
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
                ProbeSingleSite(selectedSite);
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
                if (workspaceMode != WorkspaceMode.Split)
                {
                    SetWorkspaceMode(WorkspaceMode.Web);
                }
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
                if (workspaceMode != WorkspaceMode.Split)
                {
                    SetWorkspaceMode(WorkspaceMode.Web);
                }

                if (state.WebView.CoreWebView2.CanGoBack)
                {
                    PopCurrentSiteHistory(state);
                    state.WebView.CoreWebView2.GoBack();
                    SetTransientStatus("\u6b63\u5728\u8fd4\u56de\u4e0a\u4e00\u9875...");
                    RefreshSiteButtons();
                    RefreshWebNavigationVisuals();
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
                    RefreshWebNavigationVisuals();
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
                if (workspaceMode != WorkspaceMode.Split)
                {
                    SetWorkspaceMode(WorkspaceMode.Web);
                }
                state.LastNavigatedUrl = currentSite.Url;
                state.WebView.CoreWebView2.Navigate(currentSite.Url);
                SetTransientStatus("\u6b63\u5728\u56de\u5230 " + currentSite.Name + " \u4e3b\u9875...");
                SetWebState("\u6b63\u5728\u6253\u5f00 " + currentSite.Name, currentSite.Url, false);
                RefreshSiteButtons();
                RefreshWebNavigationVisuals();
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

        private void GoForwardCurrentSite()
        {
            SiteViewState state;

            if (currentSite == null)
            {
                return;
            }

            if (siteViews.TryGetValue(currentSite.Id, out state) &&
                state.IsInitialized &&
                state.WebView.CoreWebView2 != null &&
                state.WebView.CoreWebView2.CanGoForward)
            {
                state.WebView.CoreWebView2.GoForward();
                SetTransientStatus("\u6b63\u5728\u524d\u8fdb...");
                RefreshWebNavigationVisuals();
            }
        }

        private void CopyCurrentSiteUrl()
        {
            string url = webUrlTextBox != null && !string.IsNullOrWhiteSpace(webUrlTextBox.Text)
                ? webUrlTextBox.Text
                : currentSite != null ? currentSite.Url : string.Empty;

            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    Clipboard.SetText(url);
                    SetTransientStatus("\u7f51\u5740\u5df2\u590d\u5236\u5230\u526a\u8d34\u677f\u3002");
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("web", "\u590d\u5236\u7f51\u5740\u5931\u8d25: " + ex.Message);
                }
            }
        }

        private void OpenCurrentSiteInDefaultBrowser()
        {
            string url = webUrlTextBox != null && !string.IsNullOrWhiteSpace(webUrlTextBox.Text)
                ? webUrlTextBox.Text
                : currentSite != null ? currentSite.Url : string.Empty;

            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    SetTransientStatus("\u5df2\u5728\u9ed8\u8ba4\u6d4f\u89c8\u5668\u4e2d\u6253\u5f00\u3002");
                }
                catch (Exception ex)
                {
                    AppLogger.Error("web", "\u5728\u9ed8\u8ba4\u6d4f\u89c8\u5668\u4e2d\u6253\u5f00\u5931\u8d25", ex);
                }
            }
        }

        private void OnWebUrlTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                NavigateToCustomUrl(webUrlTextBox.Text);
            }
        }

        private void NavigateToCustomUrl(string inputUrl)
        {
            if (string.IsNullOrWhiteSpace(inputUrl) || currentSite == null)
            {
                return;
            }

            string target = inputUrl.Trim();
            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                target = "http://" + target;
            }

            Uri uri;
            if (Uri.TryCreate(target, UriKind.Absolute, out uri))
            {
                SiteViewState state;
                if (siteViews.TryGetValue(currentSite.Id, out state) &&
                    state.IsInitialized &&
                    state.WebView.CoreWebView2 != null)
                {
                    state.WebView.CoreWebView2.Navigate(uri.AbsoluteUri);
                    SetTransientStatus("\u6b63\u5728\u52a0\u8f7d: " + uri.AbsoluteUri);
                }
            }
        }

        private void RefreshWebNavigationVisuals()
        {
            SiteViewState state = null;
            bool hasSite = currentSite != null &&
                siteViews.TryGetValue(currentSite.Id, out state) &&
                state.IsInitialized &&
                state.WebView.CoreWebView2 != null;

            if (webBackButton != null)
            {
                webBackButton.Enabled = hasSite &&
                    (state.WebView.CoreWebView2.CanGoBack || (state.NavigationHistory != null && state.NavigationHistory.Count > 1));
            }

            if (webForwardButton != null)
            {
                webForwardButton.Enabled = hasSite && state.WebView.CoreWebView2.CanGoForward;
            }

            if (webReloadButton != null)
            {
                webReloadButton.Enabled = currentSite != null;
            }

            if (webHomeButton != null)
            {
                webHomeButton.Enabled = currentSite != null;
            }

            if (webCopyUrlButton != null)
            {
                webCopyUrlButton.Enabled = currentSite != null;
            }

            if (webOpenBrowserButton != null)
            {
                webOpenBrowserButton.Enabled = currentSite != null;
            }

            if (webUrlTextBox != null)
            {
                if (currentSite != null)
                {
                    string currentUrl = hasSite && !string.IsNullOrWhiteSpace(state.CurrentNavigationUrl)
                        ? state.CurrentNavigationUrl
                        : currentSite.Url;

                    if (!webUrlTextBox.Focused && !string.Equals(webUrlTextBox.Text, currentUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        webUrlTextBox.Text = currentUrl;
                    }
                    webUrlTextBox.Enabled = true;
                }
                else
                {
                    webUrlTextBox.Text = string.Empty;
                    webUrlTextBox.Enabled = false;
                }
            }
        }

        private ThemedButton CreateToolbarButton(string text, string tooltipText)
        {
            ThemedButton button = new ThemedButton();
            button.Text = text;
            button.Font = UiTheme.CreateFont(9.5f, FontStyle.Regular);
            button.CornerRadius = 6;
            button.NormalBackColor = Color.Transparent;
            button.HoverBackColor = UiTheme.SecondaryHover;
            button.PressedBackColor = UiTheme.SecondaryPressed;
            button.DisabledBackColor = Color.Transparent;
            button.NormalForeColor = UiTheme.TextSecondary;
            button.HoverForeColor = UiTheme.TextPrimary;
            button.DisabledForeColor = UiTheme.TextDisabled;
            button.BorderColor = Color.Transparent;
            button.HoverBorderColor = UiTheme.BorderSoft;
            button.Padding = new Padding(0);

            if (!string.IsNullOrEmpty(tooltipText))
            {
                ToolTip tooltip = new ToolTip();
                tooltip.SetToolTip(button, tooltipText);
            }

            return button;
        }

        private void InitializeContextMenus()
        {
            commandContextMenu = new ContextMenuStrip();
            UiTheme.ApplyModernMenuTheme(commandContextMenu);

            ToolStripMenuItem cmdStartStopItem = new ToolStripMenuItem("\u542f\u52a8 / \u505c\u6b62");
            cmdStartStopItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    OnStartStopCommandClicked(this, EventArgs.Empty);
                }
            };

            ToolStripMenuItem cmdRestartItem = new ToolStripMenuItem("\u91cd\u542f\u547d\u4ee4");
            cmdRestartItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    OnRestartCommandClicked(this, EventArgs.Empty);
                }
            };

            ToolStripMenuItem cmdLogsItem = new ToolStripMenuItem("\u67e5\u770b\u65e5\u5fd7");
            cmdLogsItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    SelectCommand(currentCommand, true);
                }
            };

            ToolStripMenuItem cmdCopyItem = new ToolStripMenuItem("\u590d\u5236\u547d\u4ee4\u5185\u5bb9");
            cmdCopyItem.Click += delegate
            {
                if (currentCommand != null && !string.IsNullOrWhiteSpace(currentCommand.Command))
                {
                    try
                    {
                        Clipboard.SetText(currentCommand.Command);
                        SetTransientStatus("\u547d\u4ee4\u5df2\u590d\u5236\u3002");
                    }
                    catch { }
                }
            };

            ToolStripMenuItem cmdEditItem = new ToolStripMenuItem("\u7f16\u8f91\u547d\u4ee4...");
            cmdEditItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    OnEditCommandClicked(this, EventArgs.Empty);
                }
            };

            ToolStripMenuItem cmdUpItem = new ToolStripMenuItem("\u4e0a\u79fb");
            cmdUpItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    OnCommandReorderRequested(this, new SidebarReorderEventArgs(currentCommand.Id, -1));
                }
            };

            ToolStripMenuItem cmdDownItem = new ToolStripMenuItem("\u4e0b\u79fb");
            cmdDownItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    OnCommandReorderRequested(this, new SidebarReorderEventArgs(currentCommand.Id, 1));
                }
            };

            ToolStripMenuItem cmdDeleteItem = new ToolStripMenuItem("\u5220\u9664\u547d\u4ee4");
            cmdDeleteItem.Click += delegate
            {
                if (currentCommand != null)
                {
                    OnDeleteCommandClicked(this, EventArgs.Empty);
                }
            };

            commandContextMenu.Items.Add(cmdStartStopItem);
            commandContextMenu.Items.Add(cmdRestartItem);
            commandContextMenu.Items.Add(cmdLogsItem);
            commandContextMenu.Items.Add(cmdCopyItem);
            commandContextMenu.Items.Add(new ToolStripSeparator());
            commandContextMenu.Items.Add(cmdEditItem);
            commandContextMenu.Items.Add(cmdUpItem);
            commandContextMenu.Items.Add(cmdDownItem);
            commandContextMenu.Items.Add(new ToolStripSeparator());
            commandContextMenu.Items.Add(cmdDeleteItem);

            siteContextMenu = new ContextMenuStrip();
            UiTheme.ApplyModernMenuTheme(siteContextMenu);

            ToolStripMenuItem siteOpenItem = new ToolStripMenuItem("\u5728\u5f53\u524d\u89c6\u53e3\u6253\u5f00");
            siteOpenItem.Click += delegate
            {
                if (currentSite != null)
                {
                    SelectSite(currentSite);
                }
            };

            ToolStripMenuItem siteExternalItem = new ToolStripMenuItem("\u5728\u9ed8\u8ba4\u6d4f\u89c8\u5668\u6253\u5f00");
            siteExternalItem.Click += delegate
            {
                OpenCurrentSiteInDefaultBrowser();
            };

            ToolStripMenuItem siteReloadItem = new ToolStripMenuItem("\u5237\u65b0\u9875\u9762");
            siteReloadItem.Click += delegate
            {
                ReloadCurrentSite();
            };

            ToolStripMenuItem siteCopyItem = new ToolStripMenuItem("\u590d\u5236\u7ad9\u70b9\u7f51\u5740");
            siteCopyItem.Click += delegate
            {
                CopyCurrentSiteUrl();
            };

            ToolStripMenuItem siteEditItem = new ToolStripMenuItem("\u7f16\u8f91\u7ad9\u70b9...");
            siteEditItem.Click += delegate
            {
                if (currentSite != null)
                {
                    OnEditSiteClicked(this, EventArgs.Empty);
                }
            };

            ToolStripMenuItem siteUpItem = new ToolStripMenuItem("\u4e0a\u79fb");
            siteUpItem.Click += delegate
            {
                if (currentSite != null)
                {
                    OnSiteReorderRequested(this, new SidebarReorderEventArgs(currentSite.Id, -1));
                }
            };

            ToolStripMenuItem siteDownItem = new ToolStripMenuItem("\u4e0b\u79fb");
            siteDownItem.Click += delegate
            {
                if (currentSite != null)
                {
                    OnSiteReorderRequested(this, new SidebarReorderEventArgs(currentSite.Id, 1));
                }
            };

            ToolStripMenuItem siteDeleteItem = new ToolStripMenuItem("\u5220\u9664\u7ad9\u70b9");
            siteDeleteItem.Click += delegate
            {
                if (currentSite != null)
                {
                    OnDeleteSiteClicked(this, EventArgs.Empty);
                }
            };

            siteContextMenu.Items.Add(siteOpenItem);
            siteContextMenu.Items.Add(siteExternalItem);
            siteContextMenu.Items.Add(siteReloadItem);
            siteContextMenu.Items.Add(siteCopyItem);
            siteContextMenu.Items.Add(new ToolStripSeparator());
            siteContextMenu.Items.Add(siteEditItem);
            siteContextMenu.Items.Add(siteUpItem);
            siteContextMenu.Items.Add(siteDownItem);
            siteContextMenu.Items.Add(new ToolStripSeparator());
            siteContextMenu.Items.Add(siteDeleteItem);
        }

        private void LayoutRightBodyContent()
        {
            int totalWidth = rightBody.Width;
            int totalHeight = rightBody.Height;

            if (totalWidth <= 0 || totalHeight <= 0)
            {
                return;
            }

            if (workspaceMode == WorkspaceMode.Web)
            {
                webPanel.Visible = true;
                workspaceSplitter.Visible = false;
                logsPanel.Visible = false;
                SetBoundsIfChanged(webPanel, 0, 0, totalWidth, totalHeight);
            }
            else if (workspaceMode == WorkspaceMode.Logs)
            {
                webPanel.Visible = false;
                workspaceSplitter.Visible = false;
                logsPanel.Visible = true;
                SetBoundsIfChanged(logsPanel, 0, 0, totalWidth, totalHeight);
            }
            else if (workspaceMode == WorkspaceMode.Split)
            {
                webPanel.Visible = true;
                workspaceSplitter.Visible = true;
                logsPanel.Visible = true;

                int splitterHeight = 8;
                int minWebHeight = 180;
                int minLogsHeight = 140;

                int available = totalHeight - splitterHeight;
                int desiredWeb = (int)(available * workspaceSplitRatio);
                int webHeight = Math.Max(minWebHeight, Math.Min(available - minLogsHeight, desiredWeb));
                int logsHeight = Math.Max(minLogsHeight, available - webHeight);

                SetBoundsIfChanged(webPanel, 0, 0, totalWidth, webHeight);
                SetBoundsIfChanged(workspaceSplitter, 0, webHeight, totalWidth, splitterHeight);
                SetBoundsIfChanged(logsPanel, 0, webHeight + splitterHeight, totalWidth, logsHeight);
            }
        }

        private void OnWorkspaceSplitterMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && workspaceMode == WorkspaceMode.Split)
            {
                draggingWorkspaceSplitter = true;
                workspaceSplitter.Capture = true;
                workspaceSplitter.Active = true;
            }
        }

        private void OnWorkspaceSplitterMouseMove(object sender, MouseEventArgs e)
        {
            if (draggingWorkspaceSplitter && workspaceMode == WorkspaceMode.Split)
            {
                Point pt = rightBody.PointToClient(Cursor.Position);
                int available = rightBody.Height - 8;
                if (available > 0)
                {
                    double ratio = (double)pt.Y / available;
                    workspaceSplitRatio = Math.Max(0.20, Math.Min(0.80, ratio));
                    LayoutRightBodyContent();
                }
            }
        }

        private void OnWorkspaceSplitterMouseUp(object sender, MouseEventArgs e)
        {
            if (draggingWorkspaceSplitter)
            {
                draggingWorkspaceSplitter = false;
                workspaceSplitter.Capture = false;
                workspaceSplitter.Active = false;
                PersistConfig();
            }
        }

        private void OnClearLogsClicked(object sender, EventArgs e)
        {
            if (currentCommand == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "确认清空该命令的全部日志吗？",
                    AppName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            commandManager.ClearLogs(currentCommand.Id);
            RefreshLogsView();
        }

        private void OnCopyLogsClicked(object sender, EventArgs e)
        {
            // With an active selection, copy only the selection; otherwise copy all.
            string text = logsTextBox.SelectionLength > 0
                ? logsTextBox.SelectedText
                : logsTextBox.Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    Clipboard.SetText(text);
                    SetTransientStatus("日志已复制到剪贴板。");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "无法复制日志。\r\n\r\n" + ex.Message,
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void OnWrapLogsChanged(object sender, EventArgs e)
        {
            logsTextBox.WordWrap = wrapLogsCheckBox.Checked;
            logsTextBox.ScrollBars = wrapLogsCheckBox.Checked
                ? RichTextBoxScrollBars.ForcedVertical
                : RichTextBoxScrollBars.Both;
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

        private string GetLogFilter()
        {
            return logFilterTextBox == null || string.IsNullOrWhiteSpace(logFilterTextBox.Text)
                ? string.Empty
                : logFilterTextBox.Text.Trim();
        }

        private void UpdateLogsText(string commandId, CommandLogSnapshot snapshot, bool shouldAutoScroll)
        {
            string[] lines = snapshot == null || snapshot.Lines == null
                ? new string[0]
                : snapshot.Lines;
            bool[] errorFlags = snapshot == null || snapshot.ErrorFlags == null
                ? new bool[0]
                : snapshot.ErrorFlags;
            string filter = GetLogFilter();

            if (snapshot != null &&
                string.Equals(renderedLogCommandId, commandId, StringComparison.OrdinalIgnoreCase) &&
                renderedLogFirstSequence == snapshot.FirstSequence &&
                renderedLogNextSequence == snapshot.NextSequence &&
                string.Equals(renderedLogFilter, filter, StringComparison.Ordinal))
            {
                return;
            }

            int firstVisibleLine = shouldAutoScroll ? 0 : GetFirstVisibleLine(logsTextBox);
            int selectionStart = shouldAutoScroll ? 0 : logsTextBox.SelectionStart;
            int selectionLength = shouldAutoScroll ? 0 : logsTextBox.SelectionLength;

            if (string.IsNullOrEmpty(filter) &&
                string.Equals(renderedLogFilter, filter, StringComparison.Ordinal) &&
                CanAppendLogLines(commandId, snapshot, lines))
            {
                AppendLogLines(snapshot, lines, errorFlags);
            }
            else
            {
                RebuildLogText(lines, errorFlags, filter);
            }

            renderedLogCommandId = commandId;
            renderedLogFirstSequence = snapshot == null ? 0 : snapshot.FirstSequence;
            renderedLogNextSequence = snapshot == null ? 0 : snapshot.NextSequence;
            renderedLogFilter = filter;

            if (!shouldAutoScroll)
            {
                selectionStart = Math.Min(selectionStart, logsTextBox.TextLength);
                selectionLength = Math.Min(selectionLength, logsTextBox.TextLength - selectionStart);
                logsTextBox.Select(selectionStart, selectionLength);
                ScrollTextBoxToFirstVisibleLine(logsTextBox, firstVisibleLine);
            }
        }

        // Full repaint path: writes the (optionally filtered) lines and colors stderr
        // lines, batching consecutive same-color runs to keep large buffers fast.
        private void RebuildLogText(string[] lines, bool[] errorFlags, string filter)
        {
            SuspendRedraw(logsTextBox);

            try
            {
                logsTextBox.Clear();

                if (lines.Length > 0)
                {
                    bool pendingError = false;
                    StringBuilder run = new StringBuilder();
                    bool anyWritten = false;

                    for (int index = 0; index < lines.Length; index++)
                    {
                        if (!string.IsNullOrEmpty(filter) &&
                            (lines[index] == null ||
                             lines[index].IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                        {
                            continue;
                        }

                        bool isError = index < errorFlags.Length && errorFlags[index];

                        if (anyWritten && isError != pendingError)
                        {
                            AppendColoredRun(run.ToString(), pendingError);
                            run.Length = 0;
                            anyWritten = false;
                        }

                        if (anyWritten)
                        {
                            run.Append(Environment.NewLine);
                        }

                        run.Append(lines[index]);
                        pendingError = isError;
                        anyWritten = true;
                    }

                    if (anyWritten)
                    {
                        AppendColoredRun(run.ToString(), pendingError);
                    }
                }

                logsTextBox.SelectionStart = 0;
                logsTextBox.SelectionLength = 0;
                logsTextBox.SelectionColor = UiTheme.TerminalForeground;
            }
            finally
            {
                ResumeRedraw(logsTextBox);
            }
        }

        private void AppendColoredRun(string text, bool isError)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            logsTextBox.SelectionStart = logsTextBox.TextLength;
            logsTextBox.SelectionLength = 0;
            logsTextBox.SelectionColor = isError ? UiTheme.TerminalErrorForeground : UiTheme.TerminalForeground;
            logsTextBox.AppendText(text);
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

        private void AppendLogLines(CommandLogSnapshot snapshot, string[] lines, bool[] errorFlags)
        {
            int startIndex = renderedLogNextSequence - snapshot.FirstSequence;

            for (int index = startIndex; index < lines.Length; index++)
            {
                if (logsTextBox.TextLength > 0)
                {
                    AppendColoredRun(Environment.NewLine, false);
                }

                bool isError = index < errorFlags.Length && errorFlags[index];
                AppendColoredRun(lines[index], isError);
            }

            logsTextBox.SelectionColor = UiTheme.TerminalForeground;
        }

        private void ResetRenderedLogState()
        {
            renderedLogCommandId = null;
            renderedLogFirstSequence = 0;
            renderedLogNextSequence = 0;
            renderedLogFilter = string.Empty;
        }

        private void SetWorkspaceMode(WorkspaceMode mode)
        {
            workspaceMode = mode;
            sidebarSurface.WorkspaceMode = mode;
            sidebarSurface.Invalidate();

            if (mode == WorkspaceMode.Logs)
            {
                SetWindowTitle(currentCommand == null ? AppName : AppName + " - " + currentCommand.Name);
            }
            else if (mode == WorkspaceMode.Split)
            {
                string sitePart = currentSite != null ? currentSite.Name : null;
                string cmdPart = currentCommand != null ? currentCommand.Name : null;
                if (sitePart != null && cmdPart != null)
                {
                    SetWindowTitle(AppName + " - " + sitePart + " [" + cmdPart + "]");
                }
                else if (sitePart != null)
                {
                    SetWindowTitle(AppName + " - " + sitePart);
                }
                else if (cmdPart != null)
                {
                    SetWindowTitle(AppName + " - " + cmdPart);
                }
                else
                {
                    SetWindowTitle(AppName);
                }
            }
            else
            {
                SetWindowTitle(currentSite == null ? AppName : AppName + " - " + currentSite.Name);
            }

            LayoutRightBodyContent();

            if (mode == WorkspaceMode.Web || mode == WorkspaceMode.Split)
            {
                if (currentSite != null)
                {
                    ShowSite(currentSite);
                }
            }

            if (mode == WorkspaceMode.Logs || mode == WorkspaceMode.Split)
            {
                RefreshLogsView();
            }

            RefreshWebNavigationVisuals();

            // During construction the restored bounds are not final yet; skip the
            // write so startup cannot clobber the persisted window placement.
            if (IsHandleCreated)
            {
                PersistConfig();
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
            PersistConfig();
        }

        private void OnSidebarToggleClicked(object sender, EventArgs e)
        {
            if (sidebarHidden)
            {
                SetSidebarWidth(expandedSidebarWidth <= 0 ? S(DefaultSidebarWidth) : expandedSidebarWidth);
                SetTransientStatus("左侧面板已展开。", 2);
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
            if (requestedWidth <= S(SidebarCollapseThreshold))
            {
                return 0;
            }

            return Math.Max(S(SidebarMinExpandedWidth), Math.Min(S(SidebarMaxWidth), requestedWidth));
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
            int splitterWidth = Math.Min(S(SidebarSplitterWidth), rootPanel.ClientSize.Width);
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

            LayoutRightBodyContent();
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
            string startupText = GetCachedStartupText();

            sidebarSurface.SummaryText =
                "\u547d\u4ee4 " + commands.Count + " \u4e2a\uff0c\u8fd0\u884c\u4e2d " +
                running + " \u4e2a\uff0c\u7b49\u5f85\u91cd\u8bd5 " +
                waitingRetry + " \u4e2a\uff0c\u7ad9\u70b9 " +
                sites.Count + " \u4e2a\uff0c" +
                startupText + "\u3002";
            sidebarSurface.Invalidate();

            // Updating NotifyIcon.Text issues a synchronous Shell_NotifyIcon call into
            // Explorer. If the tray context menu is open, Explorer is blocked in our menu
            // callback, so this cross-process call can deadlock the whole UI thread -- the
            // classic "tray menu frozen, clicks dead, desktop sluggish, 0% CPU" hang.
            // Skip tooltip updates while the menu is up, and skip no-op writes otherwise.
            string trayText = AppName + " - \u8fd0\u884c\u4e2d " + running + "/" + commands.Count;
            if (notifyIcon.Visible &&
                !trayMenu.Visible &&
                !string.Equals(lastTrayTooltipText, trayText, StringComparison.Ordinal))
            {
                lastTrayTooltipText = trayText;
                notifyIcon.Text = trayText;
            }

            if (DateTime.UtcNow >= statusSummaryHoldUntilUtc)
            {
                statusLabel.ForeColor = UiTheme.TextSecondary;
                statusLabel.Text = "运行中 " + running + "/" + commands.Count +
                    "，等待重试 " + waitingRetry +
                    "，站点 " + sites.Count + "。";
            }
        }

        // Reading HKCU\...\Run hits the registry synchronously. On machines with
        // registry-filtering software (AV / device management) that read can stall
        // indefinitely at 0% CPU, freezing whatever thread calls it. The UI thread used
        // to do this every second via UpdateStatusSummary -- including while the tray
        // menu's modal loop was running. Cache the value and refresh it only when the
        // user toggles the menu item or the window state changes.
        private string GetCachedStartupText()
        {
            if (cachedStartupEnabledText == null)
            {
                try
                {
                    cachedStartupEnabledText = WindowsStartupManager.IsEnabled()
                        ? "\u5df2\u542f\u7528\u81ea\u542f"
                        : "\u672a\u542f\u7528\u81ea\u542f";
                }
                catch
                {
                    cachedStartupEnabledText = "\u672a\u542f\u7528\u81ea\u542f";
                }
            }

            return cachedStartupEnabledText;
        }

        private void RefreshCachedStartupText()
        {
            cachedStartupEnabledText = null;
        }

        private void SetTransientStatus(string message)
        {
            SetTransientStatus(message, 3, false);
        }

        private void SetTransientStatus(string message, int holdSeconds)
        {
            SetTransientStatus(message, holdSeconds, false);
        }

        private void SetTransientStatus(string message, int holdSeconds, bool isError)
        {
            statusLabel.ForeColor = isError ? UiTheme.DangerForeground : UiTheme.TextSecondary;
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

        private AppConfig BuildCurrentConfig()
        {
            Rectangle bounds = GetPersistableBounds();

            return new AppConfig
            {
                Sites = sites.ToArray(),
                Commands = commands.ToArray(),
                GlobalHotkey = pendingHotkey,
                CommandSectionRatio = sidebarSurface.CommandSectionRatio,
                WindowLeft = bounds.Left,
                WindowTop = bounds.Top,
                WindowWidth = bounds.Width,
                WindowHeight = bounds.Height,
                WindowMaximized = WindowState == FormWindowState.Maximized,
                SidebarWidth = expandedSidebarWidth,
                SidebarHidden = sidebarHidden,
                WorkspaceMode = WorkspaceModeCatalog.ToString(workspaceMode),
                WorkspaceSplitRatio = workspaceSplitRatio,
                SelectedSiteId = currentSite == null ? null : currentSite.Id,
                SelectedCommandId = currentCommand == null ? null : currentCommand.Id
            };
        }

        private void PersistConfig()
        {
            AppConfigStore.Save(BuildCurrentConfig());
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
                    AppConfigStore.SaveTo(dialog.FileName, BuildCurrentConfig());
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
                    AppLogger.Warn("config", "导入失败：无法读取 " + dialog.FileName);
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

                    // The previous selection pointed at entries that no longer exist;
                    // reset it and drop cached WebViews of sites that were removed.
                    DisposeSiteViewsNotIn(sites);
                    currentSite = null;
                    currentCommand = null;

                    PersistConfig();
                    TryRegisterHotkey();
                    UpdateHotkeyMenuText();
                    RefreshCommandList();
                    RefreshSiteList();
                    RestartSiteHealthProbe();

                    if (commands.Count > 0)
                    {
                        SelectCommand(commands[0], false);
                    }
                    else
                    {
                        UpdateCommandSelectionVisuals();
                        RefreshCommandButtons();
                        RefreshLogsView();
                    }

                    if (sites.Count > 0)
                    {
                        SelectSite(sites[0]);
                    }
                    else
                    {
                        UpdateSiteSelectionVisuals();
                        RefreshSiteButtons();
                        RefreshWebNavigationVisuals();
                    }

                    SetTransientStatus("配置已导入。");
                    AppLogger.Info("config", "配置已导入: " + dialog.FileName + "（站点 " +
                        (loaded.Sites == null ? 0 : loaded.Sites.Length) + " 个，命令 " +
                        (loaded.Commands == null ? 0 : loaded.Commands.Length) + " 个）");
                }
                catch (Exception ex)
                {
                    AppLogger.Error("config", "导入配置失败: " + dialog.FileName, ex);
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
            int windowButtonWidth = S(50);
            int right = Math.Max(0, titleBarPanel.ClientSize.Width);
            int labelRight;

            closeButton.SetBounds(right - windowButtonWidth, 0, windowButtonWidth, TitleBarHeight);
            maximizeButton.SetBounds(closeButton.Left - windowButtonWidth, 0, windowButtonWidth, TitleBarHeight);
            minimizeButton.SetBounds(maximizeButton.Left - windowButtonWidth, 0, windowButtonWidth, TitleBarHeight);
            titleSidebarButton.SetBounds(S(10), S(6), S(36), S(32));
            titleMenuButton.SetBounds(minimizeButton.Left - S(36), S(7), S(30), S(30));

            labelRight = Math.Max(S(58), titleMenuButton.Left - S(8));
            titleBarLabel.SetBounds(S(58), 0, Math.Max(0, labelRight - S(58)), TitleBarHeight);
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

            ToolStripMenuItem item = sender as ToolStripMenuItem;
            bool desired = item != null && item.Checked;

            try
            {
                WindowsStartupManager.SetEnabled(desired);
                SyncStartupMenuItems(desired);
                RefreshCachedStartupText();
                UpdateStatusSummary();
                SetTransientStatus(desired
                    ? "已开启开机自启。"
                    : "已关闭开机自启。");
            }
            catch (Exception ex)
            {
                try
                {
                    SyncStartupMenuItems(WindowsStartupManager.IsEnabled());
                }
                catch
                {
                }

                MessageBox.Show(
                    "无法更新开机自启设置。\r\n\r\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SyncStartupMenuItems(bool enabled)
        {
            updatingStartupToggle = true;
            trayStartupMenuItem.Checked = enabled;

            if (titleStartupMenuItem != null)
            {
                titleStartupMenuItem.Checked = enabled;
            }

            updatingStartupToggle = false;
        }

        private void OnTitleMenuOpening(object sender, EventArgs e)
        {
            try
            {
                SyncStartupMenuItems(WindowsStartupManager.IsEnabled());
            }
            catch
            {
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
            PersistConfig();

            if (allowExit ||
                e.CloseReason == CloseReason.ApplicationExitCall ||
                e.CloseReason == CloseReason.WindowsShutDown ||
                e.CloseReason == CloseReason.TaskManagerClosing)
            {
                AppLogger.Info("exit", "FormClosing reason=" + e.CloseReason + "，停止计时器并释放命令管理器");
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
            AppLogger.Info("tray", "隐藏到托盘（原状态 " + preTrayWindowState + "）");

            // Hide webViewHost before hiding the form so WebView2's DirectComposition GPU surface
            // does not composite before the WinForms shell when restored later.
            if (webViewHost != null && webViewHost.Visible)
            {
                webViewHost.Visible = false;
            }

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
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = preTrayWindowState;
            }

            if (!Visible)
            {
                AppLogger.Info("tray", "从托盘恢复主界面");

                bool shouldShowWeb = (workspaceMode != WorkspaceMode.Logs) && (currentSite != null);

                // Stage 1: Ensure webViewHost is not visible so WebView2's DirectComposition surface
                // does not pop onto screen before the GDI shell (titlebar, sidebar, navbar) finishes painting.
                if (webViewHost != null && webViewHost.Visible)
                {
                    webViewHost.Visible = false;
                }

                // Stage 2: Show the form
                Show();

                // Stage 3: Synchronously flush GDI paints for the shell
                Refresh();
                if (titleBarPanel != null)
                {
                    titleBarPanel.Refresh();
                }
                if (leftSidebar != null)
                {
                    leftSidebar.Refresh();
                }
                if (sidebarSurface != null)
                {
                    sidebarSurface.Refresh();
                }
                if (webNavBar != null && webNavBar.Visible)
                {
                    webNavBar.Refresh();
                }

                // Stage 4: Reveal webViewHost seamlessly into the already-rendered shell
                if (shouldShowWeb && webViewHost != null && !webViewHost.Visible)
                {
                    webViewHost.Visible = true;
                }
            }

            Activate();
            BringToFront();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryDisableWindowTransitions();
            TryRegisterHotkey();
        }

        private void TryDisableWindowTransitions()
        {
            try
            {
                int disableTransitions = 1;
                DwmSetWindowAttribute(Handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransitions, sizeof(int));
            }
            catch
            {
            }
        }

        private static void EnableDoubleBufferingRecursive(Control control)
        {
            if (control == null)
            {
                return;
            }

            try
            {
                System.Reflection.PropertyInfo prop = typeof(Control).GetProperty(
                    "DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    prop.SetValue(control, true, null);
                }
            }
            catch
            {
            }

            foreach (Control child in control.Controls)
            {
                EnableDoubleBufferingRecursive(child);
            }
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
            AppLogger.Warn("hotkey", "\u5168\u5c40\u5feb\u6377\u952e\u6ce8\u518c\u5931\u8d25\uff08\u53ef\u80fd\u88ab\u5360\u7528\uff09: " + pendingHotkey.ToDisplayString());
            SetTransientStatus("全局快捷键注册失败：可能已被其他程序占用。", 6, true);

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
                string proxyServer = (site.ProxyEnabled && !string.IsNullOrWhiteSpace(site.ProxyServer))
                    ? site.ProxyServer.Trim()
                    : null;
                SiteHealth health = ProbeUrl(site.Url, proxyServer);
                SiteHealth previous;

                lock (siteHealthSync)
                {
                    siteHealth.TryGetValue(site.Id, out previous);
                    siteHealth[site.Id] = health;
                }

                if (previous != health)
                {
                    AppLogger.Info("health", "站点健康状态变化: " + site.Name + " " + site.Url +
                        " " + previous + " -> " + health);
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

        // Lightweight HTTP probe using WebRequest / SOCKS5 socket. Treat any HTTP response (even non-2xx)
        // as "up" -- the server is listening. Connection refused / timeout / DNS => "down".
        private static SiteHealth ProbeUrl(string url, string proxyServer)
        {
            if (!string.IsNullOrEmpty(proxyServer) &&
                proxyServer.StartsWith("socks", StringComparison.OrdinalIgnoreCase))
            {
                return ProbeSocks5(proxyServer, url, 3000) ? SiteHealth.Up : SiteHealth.Down;
            }

            try
            {
                System.Net.WebRequest request = System.Net.WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = 3000;

                if (!string.IsNullOrEmpty(proxyServer))
                {
                    request.Proxy = new System.Net.WebProxy(proxyServer);
                }
                else
                {
                    request.Proxy = null;
                }

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

        private static bool ProbeSocks5(string proxyServer, string targetUrl, int timeoutMs)
        {
            try
            {
                Uri proxyUri = new Uri(proxyServer);
                Uri targetUri = new Uri(targetUrl);
                string host = targetUri.DnsSafeHost;
                int port = targetUri.Port > 0 ? targetUri.Port : (string.Equals(targetUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);

                using (System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(proxyUri.DnsSafeHost, proxyUri.Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    {
                        return false;
                    }
                    client.EndConnect(ar);
                    client.ReceiveTimeout = timeoutMs;
                    client.SendTimeout = timeoutMs;

                    using (System.Net.Sockets.NetworkStream stream = client.GetStream())
                    {
                        stream.Write(new byte[] { 0x05, 0x01, 0x00 }, 0, 3);
                        byte[] authResp = new byte[2];
                        int read = stream.Read(authResp, 0, 2);
                        if (read < 2 || authResp[0] != 0x05 || authResp[1] != 0x00)
                        {
                            return false;
                        }

                        byte[] domainBytes = System.Text.Encoding.ASCII.GetBytes(host);
                        byte[] req = new byte[7 + domainBytes.Length];
                        req[0] = 0x05;
                        req[1] = 0x01;
                        req[2] = 0x00;
                        req[3] = 0x03;
                        req[4] = (byte)domainBytes.Length;
                        Array.Copy(domainBytes, 0, req, 5, domainBytes.Length);
                        req[5 + domainBytes.Length] = (byte)((port >> 8) & 0xFF);
                        req[6 + domainBytes.Length] = (byte)(port & 0xFF);

                        stream.Write(req, 0, req.Length);

                        byte[] resp = new byte[4];
                        read = stream.Read(resp, 0, 4);
                        if (read >= 2 && resp[0] == 0x05 && resp[1] == 0x00)
                        {
                            // CONNECT success only proves the proxy is alive; issue the
                            // actual HTTP request so "up" means the site responds.
                            return ProbeHttpOverStream(stream, targetUri);
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool ProbeHttpOverStream(System.Net.Sockets.NetworkStream stream, Uri targetUri)
        {
            try
            {
                string request = "HEAD " + (string.IsNullOrEmpty(targetUri.PathAndQuery) ? "/" : targetUri.PathAndQuery) +
                    " HTTP/1.1\r\nHost: " + targetUri.Host + "\r\nConnection: close\r\n\r\n";
                byte[] requestBytes = System.Text.Encoding.ASCII.GetBytes(request);
                stream.Write(requestBytes, 0, requestBytes.Length);

                byte[] buffer = new byte[12];
                int read = stream.Read(buffer, 0, buffer.Length);

                if (read >= 5)
                {
                    string head = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                    return head.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }

            return false;
        }

        private void ExitApplication()
        {
            AppLogger.Info("exit", "用户请求退出");
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
            AppLogger.Info("exit", "开始关闭，停止命令并退出");
            Close();
        }

        private int CountEnabledOnStart()
        {
            int count = 0;

            for (int index = 0; index < commands.Count; index++)
            {
                if (commands[index] != null && commands[index].EnabledOnStart)
                {
                    count++;
                }
            }

            return count;
        }

        private void OpenLogFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppLogger.LogDirectory);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppLogger.LogDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("logs", "无法打开日志文件夹", ex);
                MessageBox.Show(
                    "无法打开日志文件夹。\r\n\r\n" + AppLogger.LogDirectory + "\r\n\r\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
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
                Url = site.Url,
                ProxyEnabled = site.ProxyEnabled,
                ProxyServer = site.ProxyServer
            };
        }

        private SiteEntry FindSiteById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int index = 0; index < sites.Count; index++)
            {
                if (string.Equals(sites[index].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return sites[index];
                }
            }

            return null;
        }

        private CommandEntry FindCommandById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int index = 0; index < commands.Count; index++)
            {
                if (string.Equals(commands[index].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return commands[index];
                }
            }

            return null;
        }

        private void CopySite(SiteEntry source, SiteEntry target)
        {
            target.Name = source.Name;
            target.Url = source.Url;
            target.ProxyEnabled = source.ProxyEnabled;
            target.ProxyServer = source.ProxyServer;
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

        private bool IsNearBottom(RichTextBox textBox)
        {
            int firstVisibleLine = GetFirstVisibleLine(textBox);
            int lineHeight = textBox.Font.Height;
            int visibleLines = Math.Max(1, textBox.ClientSize.Height / Math.Max(1, lineHeight));
            int totalLines = Math.Max(0, textBox.GetLineFromCharIndex(textBox.TextLength) + 1);

            return firstVisibleLine + visibleLines >= totalLines - 1;
        }

        private int GetFirstVisibleLine(RichTextBox textBox)
        {
            if (textBox == null || !textBox.IsHandleCreated)
            {
                return 0;
            }

            return SendMessage(textBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
        }

        private void ScrollTextBoxToFirstVisibleLine(RichTextBox textBox, int firstVisibleLine)
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

            public string ProxyKey { get; set; }

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
