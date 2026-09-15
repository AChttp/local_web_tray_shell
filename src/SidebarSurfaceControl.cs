using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal sealed class SidebarListItemEventArgs<T> : EventArgs
    {
        public SidebarListItemEventArgs(T item)
        {
            Item = item;
        }

        public T Item { get; private set; }
    }

    internal enum SidebarCommandAction
    {
        Add,
        Edit,
        Delete,
        Restart,
        StartStop
    }

    internal enum SidebarSiteAction
    {
        Add,
        Edit,
        Delete,
        Open
    }

    internal sealed class SidebarCommandActionEventArgs : EventArgs
    {
        public SidebarCommandActionEventArgs(SidebarCommandAction action)
        {
            Action = action;
        }

        public SidebarCommandAction Action { get; private set; }
    }

    internal sealed class SidebarSiteActionEventArgs : EventArgs
    {
        public SidebarSiteActionEventArgs(SidebarSiteAction action)
        {
            Action = action;
        }

        public SidebarSiteAction Action { get; private set; }
    }

    internal sealed class SidebarWorkspaceModeEventArgs : EventArgs
    {
        public SidebarWorkspaceModeEventArgs(WorkspaceMode mode)
        {
            Mode = mode;
        }

        public WorkspaceMode Mode { get; private set; }
    }

    internal sealed class SidebarReorderEventArgs : EventArgs
    {
        public SidebarReorderEventArgs(string itemId, int delta)
        {
            Id = itemId;
            Delta = delta;
        }

        public string Id { get; private set; }

        public int Delta { get; private set; }
    }

    internal enum CommandInlineAction
    {
        StartStop,
        Restart
    }

    internal sealed class SidebarCommandInlineActionEventArgs : EventArgs
    {
        public SidebarCommandInlineActionEventArgs(CommandEntry command, CommandInlineAction action)
        {
            Command = command;
            Action = action;
        }

        public CommandEntry Command { get; private set; }

        public CommandInlineAction Action { get; private set; }
    }

    internal sealed class SidebarItemContextMenuEventArgs<T> : EventArgs
    {
        public SidebarItemContextMenuEventArgs(T item, Point screenLocation)
        {
            Item = item;
            ScreenLocation = screenLocation;
        }

        public T Item { get; private set; }

        public Point ScreenLocation { get; private set; }
    }

    internal sealed class SidebarSurfaceControl : Control
    {
        // Design metrics, expressed in pixels at 96 DPI. Everything that reaches the
        // screen goes through S(), so the whole surface scales proportionally on
        // high-DPI monitors instead of letting the text grow out of fixed boxes.
        // The layout is intentionally compact: density matters more than air here.
        private const int OuterLeft = 12;
        private const int OuterTop = 10;
        private const int OuterRight = 12;
        private const int OuterBottom = 10;
        private const int BrandHeight = 46;
        private const int MinSectionHeight = 120;
        private const int SplitterHeight = 8;
        private const int SectionPaddingTop = 6;
        private const int SectionTitleHeight = 22;
        private const int ReorderColumnWidth = 24;
        private const int ActionHeight = 32;
        private const int SiteActionsHeight = 32;
        private const int CommandItemHeight = 50;
        private const int SiteItemHeight = 44;
        private const int ItemSpacing = 4;
        private const int ListHorizontalPadding = 6;
        private const int ListTopPadding = 4;
        private const int ScrollbarWidth = 5;
        private const int BadgeWidth = 70;
        private const int SiteBadgeWidth = 56;
        private const int MiniButtonSize = 20;

        private const int FocusNone = 0;
        private const int FocusCommands = 1;
        private const int FocusSites = 2;

        private const int ScrollNone = 0;
        private const int ScrollCommands = 1;
        private const int ScrollSites = 2;

        private readonly List<CommandEntry> commands;
        private readonly List<SiteEntry> sites;
        private readonly Dictionary<string, Rectangle> hitRects;
        private Rectangle commandListRect;
        private Rectangle siteListRect;
        private string hoverKey;
        private int commandScrollY;
        private int siteScrollY;
        private double commandSectionRatio;
        private bool draggingSplitter;
        private int draggingScrollbar;
        private int scrollDragStartY;
        private int scrollDragStartScroll;
        private int wheelAccumulator;
        private int focusedList;
        private Point lastMousePosition;
        private ToolTip surfaceToolTip;
        private string lastToolTipKey = string.Empty;

        private Font sectionTitleFont;
        private Font buttonFont;
        private Font itemTitleFont;
        private Font itemMetaFont;
        private Font countFont;
        private Font badgeFont;

        public SidebarSurfaceControl()
        {
            commands = new List<CommandEntry>();
            sites = new List<SiteEntry>();
            hitRects = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            CreateFonts();

            BackColor = UiTheme.SidebarBackground;
            Cursor = Cursors.Default;
            DoubleBuffered = true;
            commandSectionRatio = AppConfigStore.DefaultCommandSectionRatio;
            surfaceToolTip = new ToolTip();
            surfaceToolTip.InitialDelay = 350;
            surfaceToolTip.ReshowDelay = 100;
            surfaceToolTip.AutoPopDelay = 8000;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);

            UiTheme.DpiScaleChanged += OnDpiScaleChanged;
        }

        private static int S(int value)
        {
            return UiTheme.Scale(value);
        }

        private void CreateFonts()
        {
            sectionTitleFont = UiTheme.CreateFont(9.5f, FontStyle.Bold);
            buttonFont = UiTheme.CreateFont(8.5f, FontStyle.Bold);
            itemTitleFont = UiTheme.CreateFont(9.25f, FontStyle.Bold);
            itemMetaFont = UiTheme.CreateFont(8.4f, FontStyle.Regular);
            countFont = UiTheme.CreateFont(8.25f, FontStyle.Regular);
            badgeFont = UiTheme.CreateFont(8f, FontStyle.Bold);
        }

        private void OnDpiScaleChanged(object sender, EventArgs e)
        {
            DisposeFonts();
            CreateFonts();
            Invalidate();
        }

        private void DisposeFonts()
        {
            if (sectionTitleFont != null) { sectionTitleFont.Dispose(); sectionTitleFont = null; }
            if (buttonFont != null) { buttonFont.Dispose(); buttonFont = null; }
            if (itemTitleFont != null) { itemTitleFont.Dispose(); itemTitleFont = null; }
            if (itemMetaFont != null) { itemMetaFont.Dispose(); itemMetaFont = null; }
            if (countFont != null) { countFont.Dispose(); countFont = null; }
            if (badgeFont != null) { badgeFont.Dispose(); badgeFont = null; }
        }

        public double CommandSectionRatio
        {
            get
            {
                return commandSectionRatio;
            }
            set
            {
                double clamped = ClampRatio(value);

                if (clamped != commandSectionRatio)
                {
                    commandSectionRatio = clamped;
                    Invalidate();
                }
            }
        }

        public Func<string, SiteHealth> SiteHealthProvider { get; set; }

        public bool RestartCommandEnabled { get; set; }

        public event EventHandler StopAllCommandsClicked;
        public event EventHandler<SidebarWorkspaceModeEventArgs> WorkspaceModeRequested;
        public event EventHandler<SidebarListItemEventArgs<CommandEntry>> CommandActivated;
        public event EventHandler<SidebarListItemEventArgs<SiteEntry>> SiteActivated;
        public event EventHandler<SidebarCommandActionEventArgs> CommandActionRequested;
        public event EventHandler<SidebarSiteActionEventArgs> SiteActionRequested;
        public event EventHandler<SidebarReorderEventArgs> CommandReorderRequested;
        public event EventHandler<SidebarReorderEventArgs> SiteReorderRequested;
        public event EventHandler<SidebarCommandInlineActionEventArgs> CommandInlineActionRequested;
        public event EventHandler<SidebarItemContextMenuEventArgs<CommandEntry>> CommandContextMenuRequested;
        public event EventHandler<SidebarItemContextMenuEventArgs<SiteEntry>> SiteContextMenuRequested;

        public Func<string, CommandRuntimeSnapshot> SnapshotProvider { get; set; }

        public string SelectedCommandId { get; set; }

        public string SelectedSiteId { get; set; }

        public WorkspaceMode WorkspaceMode { get; set; }

        public bool EditCommandEnabled { get; set; }

        public bool DeleteCommandEnabled { get; set; }

        public bool StartStopCommandEnabled { get; set; }

        public string StartStopCommandText { get; set; }

        public bool EditSiteEnabled { get; set; }

        public bool DeleteSiteEnabled { get; set; }

        public bool OpenSiteEnabled { get; set; }

        public void SetCommands(IList<CommandEntry> source)
        {
            commands.Clear();

            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    if (source[index] != null)
                    {
                        commands.Add(source[index]);
                    }
                }
            }

            // Hit rectangles are rebuilt on the next paint; drop them now so a click
            // landing between the data change and the repaint cannot hit stale items.
            hitRects.Clear();
            hoverKey = string.Empty;
            commandScrollY = Math.Min(commandScrollY, GetMaxCommandScroll());
            Invalidate();
        }

        public void SetSites(IList<SiteEntry> source)
        {
            sites.Clear();

            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    if (source[index] != null)
                    {
                        sites.Add(source[index]);
                    }
                }
            }

            hitRects.Clear();
            hoverKey = string.Empty;
            siteScrollY = Math.Min(siteScrollY, GetMaxSiteScroll());
            Invalidate();
        }

        public void EnsureCommandVisible(int index)
        {
            if (EnsureVisible(index, S(CommandItemHeight), commandListRect, commands.Count, GetMaxCommandScroll(), ref commandScrollY))
            {
                Invalidate();
            }
        }

        public void EnsureSiteVisible(int index)
        {
            if (EnsureVisible(index, S(SiteItemHeight), siteListRect, sites.Count, GetMaxSiteScroll(), ref siteScrollY))
            {
                Invalidate();
            }
        }

        private static bool EnsureVisible(int index, int itemHeight, Rectangle listRect, int count, int maxScroll, ref int scrollY)
        {
            if (index < 0 || index >= count || listRect.Height <= 0)
            {
                return false;
            }

            int stride = itemHeight + S(ItemSpacing);
            int itemTop = S(ListTopPadding) + index * stride - scrollY;

            if (itemTop < 0)
            {
                scrollY = Math.Max(0, scrollY + itemTop);
                return true;
            }

            if (itemTop + itemHeight > listRect.Height)
            {
                scrollY = Math.Min(maxScroll, scrollY + (itemTop + itemHeight - listRect.Height));
                return true;
            }

            return false;
        }

        public void RefreshCommand(string commandId)
        {
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UiTheme.DpiScaleChanged -= OnDpiScaleChanged;

                if (surfaceToolTip != null)
                {
                    surfaceToolTip.Dispose();
                    surfaceToolTip = null;
                }

                DisposeFonts();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle content = GetContentBounds();
            int brandBottom = content.Y + S(BrandHeight);
            int availableHeight = Math.Max(0, content.Bottom - brandBottom);
            int splitterHeight = S(SplitterHeight);
            int minSection = S(MinSectionHeight);
            int commandSectionHeight;
            int siteSectionHeight;

            if (availableHeight < minSection * 2 + splitterHeight)
            {
                commandSectionHeight = Math.Max(minSection, availableHeight / 2);
                siteSectionHeight = Math.Max(0, availableHeight - commandSectionHeight - splitterHeight);
            }
            else
            {
                commandSectionHeight = Math.Max(minSection, Math.Min(availableHeight - splitterHeight - minSection, (int)(availableHeight * commandSectionRatio)));
                siteSectionHeight = availableHeight - commandSectionHeight - splitterHeight;
            }

            hitRects.Clear();

            DrawBrand(graphics, new Rectangle(content.X, content.Y, content.Width, S(BrandHeight)));
            DrawCommandSection(graphics, new Rectangle(content.X, brandBottom, content.Width, commandSectionHeight));
            DrawSplitter(graphics, new Rectangle(content.X, brandBottom + commandSectionHeight, content.Width, splitterHeight));
            DrawSiteSection(graphics, new Rectangle(content.X, brandBottom + commandSectionHeight + splitterHeight, content.Width, siteSectionHeight));
        }

        private static double ClampRatio(double ratio)
        {
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                return AppConfigStore.DefaultCommandSectionRatio;
            }

            return Math.Max(0.22, Math.Min(0.78, ratio));
        }

        private void DrawSplitter(Graphics graphics, Rectangle bounds)
        {
            hitRects["section-split"] = bounds;
            bool hover = draggingSplitter || string.Equals(hoverKey, "section-split", StringComparison.OrdinalIgnoreCase);
            Color color = hover ? UiTheme.Border : UiTheme.BorderSoft;
            int y = bounds.Y + (bounds.Height / 2);

            using (Pen pen = new Pen(color))
            {
                graphics.DrawLine(pen, bounds.X, y, bounds.Right, y);
            }

            // Grip dots make the drag affordance discoverable.
            Color gripColor = hover ? UiTheme.TextMuted : UiTheme.Border;
            int cx = bounds.X + (bounds.Width / 2);
            int dotSize = Math.Max(2, S(3));
            int gap = Math.Max(4, S(8));

            using (SolidBrush brush = new SolidBrush(gripColor))
            {
                for (int i = -1; i <= 1; i++)
                {
                    graphics.FillEllipse(brush, cx + (i * gap) - (dotSize / 2), y - (dotSize / 2), dotSize, dotSize);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            lastMousePosition = e.Location;

            if (draggingSplitter)
            {
                UpdateSplitterFromMouse(e.Location);
                return;
            }

            if (draggingScrollbar != ScrollNone)
            {
                UpdateScrollbarDrag(e.Location);
                return;
            }

            string nextHover = GetHitKey(e.Location);

            if (!string.Equals(hoverKey, nextHover, StringComparison.OrdinalIgnoreCase))
            {
                hoverKey = nextHover;
                Invalidate();
            }

            UpdateToolTip(nextHover);
            Cursor = ResolveCursor(nextHover);
            base.OnMouseMove(e);
        }

        private Cursor ResolveCursor(string hover)
        {
            if (draggingSplitter || string.Equals(hover, "section-split", StringComparison.OrdinalIgnoreCase))
            {
                return Cursors.SizeNS;
            }

            if (draggingScrollbar != ScrollNone ||
                string.Equals(hover, "cmd-scrollbar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hover, "site-scrollbar", StringComparison.OrdinalIgnoreCase))
            {
                return Cursors.Default;
            }

            return string.IsNullOrEmpty(hover) ? Cursors.Default : Cursors.Hand;
        }

        private void UpdateSplitterFromMouse(Point location)
        {
            Rectangle content = GetContentBounds();
            int brandBottom = content.Y + S(BrandHeight);
            int availableHeight = Math.Max(0, content.Bottom - brandBottom);

            if (availableHeight <= 0)
            {
                return;
            }

            double ratio = (double)(location.Y - brandBottom) / availableHeight;
            double clamped = ClampRatio(ratio);

            if (clamped != commandSectionRatio)
            {
                commandSectionRatio = clamped;
                Invalidate();
            }
        }

        private void UpdateScrollbarDrag(Point location)
        {
            Rectangle track;
            int maxScroll;
            int contentHeight;

            if (draggingScrollbar == ScrollCommands)
            {
                track = GetCommandScrollTrack();
                maxScroll = GetMaxCommandScroll();
                contentHeight = GetListContentHeight(commands.Count, S(CommandItemHeight));
            }
            else
            {
                track = GetSiteScrollTrack();
                maxScroll = GetMaxSiteScroll();
                contentHeight = GetListContentHeight(sites.Count, S(SiteItemHeight));
            }

            if (track.Height <= 0 || maxScroll <= 0 || contentHeight <= 0)
            {
                return;
            }

            int thumbHeight = GetScrollbarThumbHeight(track, contentHeight);
            int travel = Math.Max(1, track.Height - thumbHeight);
            int delta = location.Y - scrollDragStartY;
            int next = scrollDragStartScroll + (int)((long)delta * maxScroll / travel);
            next = Math.Max(0, Math.Min(maxScroll, next));

            if (draggingScrollbar == ScrollCommands)
            {
                commandScrollY = next;
            }
            else
            {
                siteScrollY = next;
            }

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            UpdateToolTip(string.Empty);

            if (!draggingSplitter && draggingScrollbar == ScrollNone && !string.IsNullOrEmpty(hoverKey))
            {
                hoverKey = string.Empty;
                Invalidate();
            }

            if (!draggingSplitter && draggingScrollbar == ScrollNone)
            {
                Cursor = Cursors.Default;
            }

            base.OnMouseLeave(e);
        }

        private void UpdateToolTip(string hitKey)
        {
            if (string.Equals(lastToolTipKey, hitKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lastToolTipKey = hitKey ?? string.Empty;

            if (surfaceToolTip == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(hitKey))
            {
                surfaceToolTip.SetToolTip(this, null);
                return;
            }

            if (hitKey.StartsWith("cmd-runstop:", StringComparison.OrdinalIgnoreCase))
            {
                CommandEntry cmd = FindCommand(hitKey.Substring("cmd-runstop:".Length));
                if (cmd != null)
                {
                    CommandRuntimeSnapshot snapshot = SnapshotProvider == null ? null : SnapshotProvider(cmd.Id);
                    bool isRunning = snapshot != null && snapshot.Status == CommandStatus.Running;
                    surfaceToolTip.SetToolTip(this, isRunning ? "停止命令服务" : "启动命令服务");
                    return;
                }
            }
            else if (hitKey.StartsWith("cmd-restart:", StringComparison.OrdinalIgnoreCase))
            {
                surfaceToolTip.SetToolTip(this, "重启命令服务");
                return;
            }
            else if (hitKey.StartsWith("cmd-up:", StringComparison.OrdinalIgnoreCase) || hitKey.StartsWith("site-up:", StringComparison.OrdinalIgnoreCase))
            {
                surfaceToolTip.SetToolTip(this, "上移");
                return;
            }
            else if (hitKey.StartsWith("cmd-down:", StringComparison.OrdinalIgnoreCase) || hitKey.StartsWith("site-down:", StringComparison.OrdinalIgnoreCase))
            {
                surfaceToolTip.SetToolTip(this, "下移");
                return;
            }
            else if (hitKey.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            {
                CommandEntry cmd = FindCommand(hitKey.Substring(4));
                if (cmd != null)
                {
                    string text = (cmd.Name ?? string.Empty) + "  (" + RunModeCatalog.GetDisplayName(cmd.RunMode) + ")\r\n" + (cmd.Command ?? string.Empty);
                    surfaceToolTip.SetToolTip(this, text);
                    return;
                }
            }
            else if (hitKey.StartsWith("site:", StringComparison.OrdinalIgnoreCase))
            {
                SiteEntry site = FindSite(hitKey.Substring(5));
                if (site != null)
                {
                    SiteHealth health = SiteHealth.Unknown;
                    if (SiteHealthProvider != null)
                    {
                        try
                        {
                            health = SiteHealthProvider(site.Id);
                        }
                        catch
                        {
                        }
                    }

                    string statusDesc = health == SiteHealth.Up ? "服务正常" : health == SiteHealth.Down ? "服务不可达" : "状态检测中";
                    string proxyDesc = (site.ProxyEnabled && !string.IsNullOrWhiteSpace(site.ProxyServer))
                        ? " · 代理: " + site.ProxyServer.Trim()
                        : string.Empty;
                    surfaceToolTip.SetToolTip(this, (site.Name ?? string.Empty) + " · " + statusDesc + proxyDesc + "\r\n" + (site.Url ?? string.Empty));
                    return;
                }
            }

            surfaceToolTip.SetToolTip(this, null);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            string key;

            if (e.Button == MouseButtons.Right)
            {
                key = GetHitKey(e.Location);
                if (!string.IsNullOrEmpty(key))
                {
                    CommandEntry command = TryGetCommandHit(key);
                    if (command != null)
                    {
                        focusedList = FocusCommands;
                        SelectedCommandId = command.Id;
                        Invalidate();
                        Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(command));
                        Raise(CommandContextMenuRequested, new SidebarItemContextMenuEventArgs<CommandEntry>(command, PointToScreen(e.Location)));
                        return;
                    }

                    SiteEntry site = TryGetSiteHit(key);
                    if (site != null)
                    {
                        focusedList = FocusSites;
                        SelectedSiteId = site.Id;
                        Invalidate();
                        Raise(SiteContextMenuRequested, new SidebarItemContextMenuEventArgs<SiteEntry>(site, PointToScreen(e.Location)));
                        return;
                    }
                }
                base.OnMouseDown(e);
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                base.OnMouseDown(e);
                return;
            }

            if (CanFocus)
            {
                Focus();
            }

            key = GetHitKey(e.Location);

            if (string.Equals(key, "section-split", StringComparison.OrdinalIgnoreCase))
            {
                if (e.Clicks > 1)
                {
                    // Double-click resets the command/site split to the default ratio.
                    commandSectionRatio = AppConfigStore.DefaultCommandSectionRatio;
                    Invalidate();
                    RaiseRatioChanged();
                    return;
                }

                draggingSplitter = true;
                Capture = true;
                Cursor = Cursors.SizeNS;
                return;
            }

            if (string.Equals(key, "cmd-scrollbar", StringComparison.OrdinalIgnoreCase))
            {
                draggingScrollbar = ScrollCommands;
                scrollDragStartY = e.Y;
                scrollDragStartScroll = commandScrollY;
                Capture = true;
                return;
            }

            if (string.Equals(key, "site-scrollbar", StringComparison.OrdinalIgnoreCase))
            {
                draggingScrollbar = ScrollSites;
                scrollDragStartY = e.Y;
                scrollDragStartScroll = siteScrollY;
                Capture = true;
                return;
            }

            if (string.Equals(key, "cmd-scrolltrack", StringComparison.OrdinalIgnoreCase))
            {
                PageScroll(true, e.Y);
                return;
            }

            if (string.Equals(key, "site-scrolltrack", StringComparison.OrdinalIgnoreCase))
            {
                PageScroll(false, e.Y);
                return;
            }

            if (!string.IsNullOrEmpty(key))
            {
                if (key.StartsWith("cmd", StringComparison.OrdinalIgnoreCase))
                {
                    focusedList = FocusCommands;
                }
                else if (key.StartsWith("site", StringComparison.OrdinalIgnoreCase))
                {
                    focusedList = FocusSites;
                }
            }

            DispatchHit(key);
            base.OnMouseDown(e);
        }

        private void PageScroll(bool commandList, int clickY)
        {
            Rectangle track = commandList ? GetCommandScrollTrack() : GetSiteScrollTrack();
            int maxScroll = commandList ? GetMaxCommandScroll() : GetMaxSiteScroll();
            Rectangle listRect = commandList ? commandListRect : siteListRect;
            int scroll = commandList ? commandScrollY : siteScrollY;

            scroll += clickY < track.Y + (track.Height / 2) ? -listRect.Height : listRect.Height;
            scroll = Math.Max(0, Math.Min(maxScroll, scroll));

            if (commandList)
            {
                commandScrollY = scroll;
            }
            else
            {
                siteScrollY = scroll;
            }

            Invalidate();
        }

        private CommandEntry TryGetCommandHit(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (key.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            {
                return FindCommand(key.Substring(4));
            }

            if (key.StartsWith("cmd-runstop:", StringComparison.OrdinalIgnoreCase))
            {
                return FindCommand(key.Substring("cmd-runstop:".Length));
            }

            if (key.StartsWith("cmd-restart:", StringComparison.OrdinalIgnoreCase))
            {
                return FindCommand(key.Substring("cmd-restart:".Length));
            }

            if (key.StartsWith("cmd-up:", StringComparison.OrdinalIgnoreCase))
            {
                return FindCommand(key.Substring("cmd-up:".Length));
            }

            if (key.StartsWith("cmd-down:", StringComparison.OrdinalIgnoreCase))
            {
                return FindCommand(key.Substring("cmd-down:".Length));
            }

            return null;
        }

        private SiteEntry TryGetSiteHit(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (key.StartsWith("site:", StringComparison.OrdinalIgnoreCase))
            {
                return FindSite(key.Substring(5));
            }

            if (key.StartsWith("site-up:", StringComparison.OrdinalIgnoreCase))
            {
                return FindSite(key.Substring("site-up:".Length));
            }

            if (key.StartsWith("site-down:", StringComparison.OrdinalIgnoreCase))
            {
                return FindSite(key.Substring("site-down:".Length));
            }

            return null;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (draggingSplitter)
            {
                draggingSplitter = false;
                Capture = false;
                Invalidate();
                RaiseRatioChanged();
            }

            if (draggingScrollbar != ScrollNone)
            {
                draggingScrollbar = ScrollNone;
                Capture = false;
                Invalidate();
            }

            base.OnMouseUp(e);
        }

        private void RaiseRatioChanged()
        {
            EventHandler handler = CommandSectionRatioChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler CommandSectionRatioChanged;

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            if (focusedList == FocusNone)
            {
                focusedList = commands.Count > 0 ? FocusCommands : (sites.Count > 0 ? FocusSites : FocusNone);
            }

            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
        {
            // Make navigation keys reach OnKeyDown instead of being consumed for focus traversal.
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down ||
                e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown ||
                e.KeyCode == Keys.Enter || e.KeyCode == Keys.Delete ||
                e.KeyCode == Keys.Tab)
            {
                e.IsInputKey = true;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e == null || e.Handled)
            {
                base.OnKeyDown(e);
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Tab:
                    CycleFocusedList(e.Shift ? -1 : 1);
                    e.Handled = true;
                    break;
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                    if (focusedList == FocusNone)
                    {
                        focusedList = commands.Count > 0 ? FocusCommands : (sites.Count > 0 ? FocusSites : FocusNone);
                    }
                    MoveSelection(focusedList == FocusSites, e.KeyCode);
                    e.Handled = true;
                    break;
                case Keys.Enter:
                    ActivateSelection(focusedList == FocusSites);
                    e.Handled = true;
                    break;
                case Keys.Delete:
                    DeleteSelection(focusedList == FocusSites);
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        private void CycleFocusedList(int direction)
        {
            bool hasCommands = commands.Count > 0;
            bool hasSites = sites.Count > 0;

            if (!hasCommands && !hasSites)
            {
                focusedList = FocusNone;
                Invalidate();
                return;
            }

            if (!hasCommands)
            {
                focusedList = FocusSites;
            }
            else if (!hasSites)
            {
                focusedList = FocusCommands;
            }
            else
            {
                focusedList = focusedList == FocusCommands ? FocusSites : FocusCommands;
            }

            Invalidate();
        }

        private void MoveSelection(bool siteActive, Keys key)
        {
            int step = (key == Keys.PageUp || key == Keys.PageDown) ? 4 : 1;
            int direction = (key == Keys.Up || key == Keys.PageUp) ? -1 : 1;

            if (siteActive)
            {
                int index = IndexOfSelectedSite();

                if (index < 0)
                {
                    index = sites.Count > 0 ? 0 : -1;
                }
                else
                {
                    index = Math.Max(0, Math.Min(sites.Count - 1, index + direction * step));
                }

                if (index >= 0 && index < sites.Count)
                {
                    SelectedSiteId = sites[index].Id;
                    EnsureSiteVisible(index);
                    Invalidate();
                }
            }
            else
            {
                int index = IndexOfSelectedCommand();

                if (index < 0)
                {
                    index = commands.Count > 0 ? 0 : -1;
                }
                else
                {
                    index = Math.Max(0, Math.Min(commands.Count - 1, index + direction * step));
                }

                if (index >= 0 && index < commands.Count)
                {
                    SelectedCommandId = commands[index].Id;
                    EnsureCommandVisible(index);
                    Invalidate();
                }
            }
        }

        private void ActivateSelection(bool siteActive)
        {
            if (siteActive)
            {
                int index = IndexOfSelectedSite();

                if (index >= 0 && index < sites.Count)
                {
                    Raise(SiteActivated, new SidebarListItemEventArgs<SiteEntry>(sites[index]));
                }
            }
            else
            {
                int index = IndexOfSelectedCommand();

                if (index >= 0 && index < commands.Count)
                {
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(commands[index]));
                }
            }
        }

        private void DeleteSelection(bool siteActive)
        {
            if (siteActive)
            {
                Raise(SiteActionRequested, new SidebarSiteActionEventArgs(SidebarSiteAction.Delete));
            }
            else
            {
                Raise(CommandActionRequested, new SidebarCommandActionEventArgs(SidebarCommandAction.Delete));
            }
        }

        private int IndexOfSelectedCommand()
        {
            if (string.IsNullOrEmpty(SelectedCommandId))
            {
                return -1;
            }

            for (int index = 0; index < commands.Count; index++)
            {
                if (string.Equals(commands[index].Id, SelectedCommandId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private int IndexOfSelectedSite()
        {
            if (string.IsNullOrEmpty(SelectedSiteId))
            {
                return -1;
            }

            for (int index = 0; index < sites.Count; index++)
            {
                if (string.Equals(sites[index].Id, SelectedSiteId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private CommandEntry FindCommand(string id)
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

        private SiteEntry FindSite(string id)
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Accumulate sub-notch deltas so precision touchpads scroll smoothly
            // instead of jumping a fixed step per event.
            wheelAccumulator += e.Delta;
            int notches = wheelAccumulator / 120;
            wheelAccumulator -= notches * 120;

            if (notches != 0)
            {
                int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
                int delta = notches * lines * S(24);

                if (commandListRect.Contains(e.Location))
                {
                    commandScrollY = Math.Max(0, Math.Min(GetMaxCommandScroll(), commandScrollY - delta));
                    Invalidate(commandListRect);
                    RefreshHoverAfterScroll();
                    return;
                }

                if (siteListRect.Contains(e.Location))
                {
                    siteScrollY = Math.Max(0, Math.Min(GetMaxSiteScroll(), siteScrollY - delta));
                    Invalidate(siteListRect);
                    RefreshHoverAfterScroll();
                    return;
                }
            }

            base.OnMouseWheel(e);
        }

        private void RefreshHoverAfterScroll()
        {
            // The content under a stationary cursor changed; the previous hover target
            // almost certainly points at a different item now.
            string nextHover = GetHitKey(lastMousePosition);

            if (!string.Equals(hoverKey, nextHover, StringComparison.OrdinalIgnoreCase))
            {
                hoverKey = nextHover;
                UpdateToolTip(nextHover);
                Invalidate();
            }
        }

        private Rectangle GetContentBounds()
        {
            return new Rectangle(
                S(OuterLeft),
                S(OuterTop),
                Math.Max(0, ClientSize.Width - S(OuterLeft) - S(OuterRight)),
                Math.Max(0, ClientSize.Height - S(OuterTop) - S(OuterBottom)));
        }

        private void DrawBrand(Graphics graphics, Rectangle bounds)
        {
            // Single compact row: workspace mode switch on the left, stop-all on the
            // right. No title, no summary line -- the space belongs to the lists.
            int stopWidth = S(72);
            Rectangle row = new Rectangle(bounds.X, bounds.Y + S(7), bounds.Width, S(30));
            Rectangle seg = new Rectangle(row.X, row.Y, Math.Max(0, row.Width - stopWidth - S(8)), row.Height);
            Rectangle stop = new Rectangle(row.Right - stopWidth, row.Y, stopWidth, row.Height);

            DrawSegmentControl(graphics, seg);
            DrawDangerButton(graphics, stop, "全部停止", "stop-all");
        }

        private void DrawSectionTitle(Graphics graphics, Rectangle bounds, string text, int count)
        {
            TextRenderer.DrawText(graphics, text, sectionTitleFont, bounds, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft) | TextFormatFlags.NoPadding);

            Size measured = TextRenderer.MeasureText(graphics, text, sectionTitleFont, new Size(int.MaxValue, int.MaxValue), TextFlags(ContentAlignment.MiddleLeft) | TextFormatFlags.NoPadding);
            Rectangle countRect = new Rectangle(bounds.X + measured.Width + S(5), bounds.Y, Math.Max(0, bounds.Width - measured.Width - S(5)), bounds.Height);
            TextRenderer.DrawText(graphics, count.ToString(), countFont, countRect, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft) | TextFormatFlags.NoPadding);
        }

        private void DrawCommandSection(Graphics graphics, Rectangle section)
        {
            Rectangle title = new Rectangle(section.X, section.Y + S(SectionPaddingTop), section.Width, S(SectionTitleHeight));
            Rectangle actions = new Rectangle(section.X, Math.Max(title.Bottom, section.Bottom - S(ActionHeight)), section.Width, S(ActionHeight));

            commandListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            DrawSectionTitle(graphics, title, "命令", commands.Count);
            DrawCommandList(graphics, commandListRect);
            DrawButtons(
                graphics,
                actions,
                new ButtonSpec("新增", true, true, "cmd-add"),
                new ButtonSpec("编辑", false, EditCommandEnabled, "cmd-edit"),
                new ButtonSpec("删除", false, DeleteCommandEnabled, "cmd-delete"),
                new ButtonSpec(string.IsNullOrEmpty(StartStopCommandText) ? "启动" : StartStopCommandText, true, StartStopCommandEnabled, "cmd-startstop"));
        }

        private void DrawSiteSection(Graphics graphics, Rectangle section)
        {
            Rectangle title = new Rectangle(section.X, section.Y + S(SectionPaddingTop), section.Width, S(SectionTitleHeight));
            Rectangle actions = new Rectangle(section.X, Math.Max(title.Bottom, section.Bottom - S(SiteActionsHeight)), section.Width, S(SiteActionsHeight));

            siteListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            DrawSectionTitle(graphics, title, "站点", sites.Count);
            DrawSiteList(graphics, siteListRect);
            DrawButtons(
                graphics,
                actions,
                new ButtonSpec("新增", true, true, "site-add"),
                new ButtonSpec("编辑", false, EditSiteEnabled, "site-edit"),
                new ButtonSpec("删除", false, DeleteSiteEnabled, "site-delete"),
                new ButtonSpec("打开", false, OpenSiteEnabled, "site-open"));
        }

        private void DrawCommandList(Graphics graphics, Rectangle bounds)
        {
            if (commands.Count == 0)
            {
                DrawEmpty(graphics, bounds, "暂无命令", "点击新增一个本地服务命令", "＋ 新增命令", "cmd-add");
                return;
            }

            int commandMaxScroll = Math.Max(0, GetListContentHeight(commands.Count, S(CommandItemHeight)) - bounds.Height);
            commandScrollY = Math.Max(0, Math.Min(commandMaxScroll, commandScrollY));

            DrawClipped(graphics, bounds, delegate
            {
                int stride = S(CommandItemHeight) + S(ItemSpacing);
                int first = Math.Max(0, (commandScrollY - S(ListTopPadding)) / stride);
                int last = Math.Min(commands.Count - 1, ((commandScrollY + bounds.Height - S(ListTopPadding)) / stride) + 1);

                for (int index = first; index <= last; index++)
                {
                    Rectangle itemBounds = new Rectangle(
                        bounds.X + S(ListHorizontalPadding),
                        bounds.Y + S(ListTopPadding) + (index * stride) - commandScrollY,
                        Math.Max(1, bounds.Width - (S(ListHorizontalPadding) * 2) - GetScrollbarReserve(commands.Count, S(CommandItemHeight), bounds.Height)),
                        S(CommandItemHeight));
                    DrawCommandItem(graphics, commands[index], itemBounds, index);
                }
            });

            DrawScrollbar(graphics, bounds, commands.Count, S(CommandItemHeight), commandScrollY, "cmd-scrollbar", "cmd-scrolltrack");
        }

        private void DrawSiteList(Graphics graphics, Rectangle bounds)
        {
            if (sites.Count == 0)
            {
                DrawEmpty(graphics, bounds, "暂无站点", "点击新增要查看的本地网页", "＋ 新增站点", "site-add");
                return;
            }

            int siteMaxScroll = Math.Max(0, GetListContentHeight(sites.Count, S(SiteItemHeight)) - bounds.Height);
            siteScrollY = Math.Max(0, Math.Min(siteMaxScroll, siteScrollY));

            DrawClipped(graphics, bounds, delegate
            {
                int stride = S(SiteItemHeight) + S(ItemSpacing);
                int first = Math.Max(0, (siteScrollY - S(ListTopPadding)) / stride);
                int last = Math.Min(sites.Count - 1, ((siteScrollY + bounds.Height - S(ListTopPadding)) / stride) + 1);

                for (int index = first; index <= last; index++)
                {
                    Rectangle itemBounds = new Rectangle(
                        bounds.X + S(ListHorizontalPadding),
                        bounds.Y + S(ListTopPadding) + (index * stride) - siteScrollY,
                        Math.Max(1, bounds.Width - (S(ListHorizontalPadding) * 2) - GetScrollbarReserve(sites.Count, S(SiteItemHeight), bounds.Height)),
                        S(SiteItemHeight));
                    DrawSiteItem(graphics, sites[index], itemBounds, index);
                }
            });

            DrawScrollbar(graphics, bounds, sites.Count, S(SiteItemHeight), siteScrollY, "site-scrollbar", "site-scrolltrack");
        }

        private int GetScrollbarReserve(int count, int itemHeight, int viewportHeight)
        {
            int contentHeight = GetListContentHeight(count, itemHeight);
            return contentHeight > viewportHeight ? S(ScrollbarWidth) + S(6) : 0;
        }

        private Rectangle GetCommandScrollTrack()
        {
            return GetScrollTrack(commandListRect, commands.Count, S(CommandItemHeight));
        }

        private Rectangle GetSiteScrollTrack()
        {
            return GetScrollTrack(siteListRect, sites.Count, S(SiteItemHeight));
        }

        private Rectangle GetScrollTrack(Rectangle listBounds, int count, int itemHeight)
        {
            int contentHeight = GetListContentHeight(count, itemHeight);

            if (listBounds.Height <= 0 || contentHeight <= listBounds.Height)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                listBounds.Right - S(ScrollbarWidth) - S(2),
                listBounds.Y + S(ListTopPadding),
                S(ScrollbarWidth),
                Math.Max(0, listBounds.Height - S(ListTopPadding) * 2));
        }

        private static int GetScrollbarThumbHeight(Rectangle track, int contentHeight)
        {
            if (track.Height <= 0 || contentHeight <= 0)
            {
                return 0;
            }

            int thumb = (int)((long)track.Height * track.Height / contentHeight);
            return Math.Max(S(24), Math.Min(track.Height, thumb));
        }

        private void DrawScrollbar(Graphics graphics, Rectangle listBounds, int count, int itemHeight, int scrollY, string thumbKey, string trackKey)
        {
            Rectangle track = GetScrollTrack(listBounds, count, itemHeight);

            if (track.IsEmpty)
            {
                return;
            }

            int contentHeight = GetListContentHeight(count, itemHeight);
            int maxScroll = Math.Max(0, contentHeight - listBounds.Height);
            int thumbHeight = GetScrollbarThumbHeight(track, contentHeight);
            int travel = Math.Max(1, track.Height - thumbHeight);
            int thumbY = track.Y + (maxScroll <= 0 ? 0 : (int)((long)scrollY * travel / maxScroll));
            Rectangle thumb = new Rectangle(track.X, thumbY, track.Width, thumbHeight);

            bool thumbHot = draggingScrollbar != ScrollNone ||
                string.Equals(hoverKey, thumbKey, StringComparison.OrdinalIgnoreCase);

            using (SolidBrush brush = new SolidBrush(thumbHot ? UiTheme.ScrollbarThumbHover : UiTheme.ScrollbarThumb))
            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(thumb, track.Width / 2))
            {
                graphics.FillPath(brush, path);
            }

            hitRects[trackKey] = track;
            hitRects[thumbKey] = thumb;
        }

        private void DrawCommandItem(Graphics graphics, CommandEntry command, Rectangle bounds, int index)
        {
            CommandRuntimeSnapshot snapshot = SnapshotProvider == null || command == null ? null : SnapshotProvider(command.Id);
            CommandStatus status = snapshot == null ? CommandStatus.Stopped : snapshot.Status;
            Color accent = GetStatusAccent(status);
            bool selected = command != null && string.Equals(command.Id, SelectedCommandId, StringComparison.OrdinalIgnoreCase);
            bool itemHovered = IsItemHovered("cmd", command);
            Color fill = selected ? UiTheme.ItemSelectedBack : itemHovered ? UiTheme.ItemHoverBack : UiTheme.Surface;
            Color border = selected ? UiTheme.Primary : itemHovered ? UiTheme.ItemHoverBorder : UiTheme.Border;
            int contentRight = bounds.Right - S(ReorderColumnWidth) - S(4);
            Rectangle badge = new Rectangle(contentRight - S(BadgeWidth), bounds.Y + S(7), S(BadgeWidth), S(18));

            DrawCard(graphics, bounds, fill, border);

            if (selected && Focused && focusedList == FocusCommands)
            {
                DrawFocusRing(graphics, bounds);
            }

            DrawRoundedFill(graphics, new Rectangle(bounds.X + S(8), bounds.Y + S(8), S(4), Math.Max(S(8), bounds.Height - S(16))), accent, accent, 2);

            // Inline actions are always laid out (no text reflow on hover); they render
            // subdued until the row is hovered or selected.
            bool emphasized = itemHovered || selected;
            bool isRunning = status == CommandStatus.Running;
            int btnWidth = S(20);
            int btnHeight = S(MiniButtonSize);
            Rectangle runBtn = new Rectangle(badge.Left - btnWidth - S(3), bounds.Y + S(6), btnWidth, btnHeight);
            Rectangle restartBtn = new Rectangle(runBtn.Left - btnWidth - S(2), bounds.Y + S(6), btnWidth, btnHeight);
            int titleRight = restartBtn.Left - S(5);

            string id = command == null ? string.Empty : command.Id ?? string.Empty;
            hitRects["cmd-runstop:" + id] = runBtn;
            hitRects["cmd-restart:" + id] = restartBtn;

            bool runHover = string.Equals(hoverKey, "cmd-runstop:" + id, StringComparison.OrdinalIgnoreCase);
            bool restartHover = string.Equals(hoverKey, "cmd-restart:" + id, StringComparison.OrdinalIgnoreCase);

            DrawMiniIconButton(graphics, runBtn, isRunning ? MiniIconType.Stop : MiniIconType.Play, runHover, emphasized);
            DrawMiniIconButton(graphics, restartBtn, MiniIconType.Restart, restartHover, emphasized);

            Rectangle title = new Rectangle(bounds.X + S(20), bounds.Y + S(4), Math.Max(1, titleRight - bounds.X - S(20)), S(19));
            Rectangle meta = new Rectangle(bounds.X + S(20), bounds.Y + S(25), Math.Max(1, contentRight - bounds.X - S(24)), S(15));

            TextRenderer.DrawText(graphics, GetCommandTitle(command), itemTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawBadge(graphics, badge, snapshot == null ? "已停止" : snapshot.GetDisplayStatus(), GetStatusBadgeBackground(status), accent);
            TextRenderer.DrawText(graphics, GetCommandMeta(command), itemMetaFont, meta, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["cmd:" + id] = bounds;
            DrawReorderHandles(graphics, bounds, "cmd", id, index, commands.Count);
        }

        private enum MiniIconType
        {
            Play,
            Stop,
            Restart
        }

        private void DrawMiniIconButton(Graphics graphics, Rectangle bounds, MiniIconType iconType, bool hover, bool emphasized)
        {
            Color fill;
            Color border;
            Color iconColor;

            if (iconType == MiniIconType.Stop)
            {
                fill = hover ? UiTheme.MiniStopBackHover : UiTheme.MiniStopBack;
                border = hover ? UiTheme.DangerForeground : UiTheme.MiniStopBorder;
                iconColor = hover ? UiTheme.MiniStopIconHover : UiTheme.DangerForeground;
            }
            else if (iconType == MiniIconType.Play)
            {
                fill = hover ? UiTheme.MiniPlayBackHover : UiTheme.SecondaryBack;
                border = hover ? UiTheme.Primary : UiTheme.BorderSoft;
                iconColor = hover ? UiTheme.MiniPlayIconHover : UiTheme.Primary;
            }
            else
            {
                fill = hover ? UiTheme.MiniNeutralBackHover : UiTheme.SecondaryBack;
                border = hover ? UiTheme.Primary : UiTheme.BorderSoft;
                iconColor = hover ? UiTheme.Primary : UiTheme.TextSecondary;
            }

            // Subdued look while the row is not interacted with; keeps the buttons
            // discoverable without visually dominating every row.
            if (!emphasized && !hover)
            {
                fill = UiTheme.Surface;
                border = UiTheme.BorderSoft;
                iconColor = UiTheme.TextMuted;
            }

            DrawRoundedFill(graphics, bounds, fill, border, S(5));

            SmoothingMode oldMode = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = bounds.X + (bounds.Width / 2);
            int cy = bounds.Y + (bounds.Height / 2);
            int u = Math.Max(1, S(2)); // small geometry unit that scales with DPI

            using (SolidBrush brush = new SolidBrush(iconColor))
            {
                if (iconType == MiniIconType.Play)
                {
                    Point[] playTriangle = new Point[]
                    {
                        new Point(cx - u - (u / 2), cy - (2 * u) - (u / 2)),
                        new Point(cx + (2 * u), cy),
                        new Point(cx - u - (u / 2), cy + (2 * u) + (u / 2))
                    };
                    graphics.FillPolygon(brush, playTriangle);
                }
                else if (iconType == MiniIconType.Stop)
                {
                    int size = 4 * u;
                    graphics.FillRectangle(brush, cx - (size / 2), cy - (size / 2), size, size);
                }
                else if (iconType == MiniIconType.Restart)
                {
                    int radius = 5 * u / 2;
                    using (Pen pen = new Pen(iconColor, Math.Max(1.4f, UiTheme.Scale(1.8f))))
                    {
                        graphics.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, 0, 270);
                    }

                    Point[] arrow = new Point[]
                    {
                        new Point(cx + radius - (u / 2), cy - radius),
                        new Point(cx + radius - (2 * u), cy - radius - (3 * u / 2)),
                        new Point(cx + radius - (2 * u), cy - radius + (3 * u / 2))
                    };
                    graphics.FillPolygon(brush, arrow);
                }
            }

            graphics.SmoothingMode = oldMode;
        }

        private void DrawSiteItem(Graphics graphics, SiteEntry site, Rectangle bounds, int index)
        {
            SiteHealth health = SiteHealth.Unknown;
            if (site != null && SiteHealthProvider != null)
            {
                try
                {
                    health = SiteHealthProvider(site.Id);
                }
                catch
                {
                }
            }

            Color accent = GetSiteAccent(health);
            bool selected = site != null && string.Equals(site.Id, SelectedSiteId, StringComparison.OrdinalIgnoreCase);
            bool itemHovered = IsItemHovered("site", site);
            Color fill = selected ? UiTheme.ItemSelectedBack : itemHovered ? UiTheme.ItemHoverBack : UiTheme.Surface;
            Color border = selected ? UiTheme.Primary : itemHovered ? UiTheme.ItemHoverBorder : UiTheme.Border;

            DrawCard(graphics, bounds, fill, border);

            if (selected && Focused && focusedList == FocusSites)
            {
                DrawFocusRing(graphics, bounds);
            }

            DrawRoundedFill(graphics, new Rectangle(bounds.X + S(8), bounds.Y + S(7), S(4), Math.Max(S(8), bounds.Height - S(14))), accent, accent, 2);

            string id = site == null ? string.Empty : site.Id ?? string.Empty;
            int siteContentRight = bounds.Right - S(ReorderColumnWidth) - S(4);

            // Health is conveyed with a text badge as well as color, so the status
            // stays readable for color-blind users.
            Rectangle healthBadge = new Rectangle(siteContentRight - S(SiteBadgeWidth), bounds.Y + S(5), S(SiteBadgeWidth), S(17));
            Rectangle titleRect = new Rectangle(bounds.X + S(20), bounds.Y + S(3), Math.Max(1, healthBadge.Left - bounds.X - S(24)), S(18));
            Rectangle urlRect = new Rectangle(bounds.X + S(20), bounds.Y + S(22), Math.Max(1, siteContentRight - bounds.X - S(24)), S(14));

            bool hasProxy = site != null && site.ProxyEnabled && !string.IsNullOrWhiteSpace(site.ProxyServer);
            if (hasProxy)
            {
                int tagWidth = S(30);
                int tagHeight = S(13);
                Rectangle tagRect = new Rectangle(siteContentRight - tagWidth - S(2), bounds.Y + S(23), tagWidth, tagHeight);
                DrawRoundedFill(graphics, tagRect, UiTheme.ProxyTagBack, UiTheme.ProxyTagBorder, S(3));
                TextRenderer.DrawText(graphics, "代理", itemMetaFont, tagRect, UiTheme.ProxyTagText, TextFlags(ContentAlignment.MiddleCenter));
                urlRect = new Rectangle(bounds.X + S(20), bounds.Y + S(22), Math.Max(1, tagRect.Left - bounds.X - S(22)), S(14));
            }

            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Name ?? string.Empty, itemTitleFont, titleRect, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawBadge(graphics, healthBadge, GetSiteHealthText(health), GetSiteHealthBadgeBackground(health), GetSiteHealthBadgeForeground(health));
            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Url ?? string.Empty, itemMetaFont, urlRect, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["site:" + id] = bounds;
            DrawReorderHandles(graphics, bounds, "site", id, index, sites.Count);
        }

        private static string GetSiteHealthText(SiteHealth health)
        {
            switch (health)
            {
                case SiteHealth.Up:
                    return "正常";
                case SiteHealth.Down:
                    return "不可达";
                default:
                    return "检测中";
            }
        }

        private static Color GetSiteHealthBadgeBackground(SiteHealth health)
        {
            switch (health)
            {
                case SiteHealth.Up:
                    return UiTheme.SuccessBackground;
                case SiteHealth.Down:
                    return UiTheme.DangerBackground;
                default:
                    return UiTheme.BadgeNeutralBackground;
            }
        }

        private static Color GetSiteHealthBadgeForeground(SiteHealth health)
        {
            switch (health)
            {
                case SiteHealth.Up:
                    return UiTheme.SuccessForeground;
                case SiteHealth.Down:
                    return UiTheme.DangerForeground;
                default:
                    return UiTheme.BadgeNeutralForeground;
            }
        }

        private void DrawFocusRing(Graphics graphics, Rectangle bounds)
        {
            Rectangle ring = new Rectangle(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2);

            using (Pen pen = new Pen(UiTheme.FocusRing, 2f))
            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(ring, S(9)))
            {
                graphics.DrawPath(pen, path);
            }
        }

        private static Color GetSiteAccent(SiteHealth health)
        {
            switch (health)
            {
                case SiteHealth.Up:
                    return UiTheme.SiteUpAccent;
                case SiteHealth.Down:
                    return UiTheme.SiteDownAccent;
                default:
                    return UiTheme.SiteUnknownAccent;
            }
        }

        private void DrawButtons(Graphics graphics, Rectangle bounds, params ButtonSpec[] specs)
        {
            int count = specs.Length;

            if (count == 0)
            {
                return;
            }

            int gap = bounds.Width < S(260) ? S(6) : S(8);
            int buttonHeight = Math.Min(S(26), bounds.Height);
            int y = bounds.Y + Math.Max(0, (bounds.Height - buttonHeight) / 2);
            int available = Math.Max(0, bounds.Width - (gap * (count - 1)));
            int buttonWidth = available / count;
            int x = bounds.X;

            for (int index = 0; index < count; index++)
            {
                ButtonSpec spec = specs[index];
                int width = index == count - 1 ? Math.Max(0, bounds.Right - x) : buttonWidth;

                DrawButton(graphics, new Rectangle(x, y, width, buttonHeight), spec.Text, spec.Primary, spec.Enabled, spec.Key);
                x += buttonWidth + gap;
            }
        }

        private void DrawSegmentControl(Graphics graphics, Rectangle bounds)
        {
            DrawRoundedFill(graphics, bounds, UiTheme.SegmentTrackBack, UiTheme.SegmentTrackBorder, S(8));

            int padding = S(3);
            Rectangle inner = new Rectangle(bounds.X + padding, bounds.Y + padding, bounds.Width - (padding * 2), bounds.Height - (padding * 2));
            int segmentWidth = inner.Width / 3;

            Rectangle webRect = new Rectangle(inner.X, inner.Y, segmentWidth, inner.Height);
            Rectangle splitRect = new Rectangle(webRect.Right, inner.Y, segmentWidth, inner.Height);
            Rectangle logsRect = new Rectangle(splitRect.Right, inner.Y, Math.Max(0, inner.Right - splitRect.Right), inner.Height);

            DrawSegmentPill(graphics, webRect, "网页", WorkspaceMode == WorkspaceMode.Web, "mode-web");
            DrawSegmentPill(graphics, splitRect, "分屏", WorkspaceMode == WorkspaceMode.Split, "mode-split");
            DrawSegmentPill(graphics, logsRect, "日志", WorkspaceMode == WorkspaceMode.Logs, "mode-logs");
        }

        private void DrawSegmentPill(Graphics graphics, Rectangle bounds, string text, bool active, string key)
        {
            bool hover = string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);

            if (active)
            {
                DrawRoundedFill(graphics, bounds, Color.White, UiTheme.SegmentActiveBorder, S(6));
                TextRenderer.DrawText(graphics, text, buttonFont, bounds, UiTheme.Primary, TextFlags(ContentAlignment.MiddleCenter));
            }
            else
            {
                if (hover)
                {
                    DrawRoundedFill(graphics, bounds, UiTheme.SecondaryHover, Color.Transparent, S(6));
                }
                TextRenderer.DrawText(graphics, text, buttonFont, bounds, hover ? UiTheme.TextPrimary : UiTheme.TextSecondary, TextFlags(ContentAlignment.MiddleCenter));
            }

            hitRects[key] = bounds;
        }

        private void DrawDangerButton(Graphics graphics, Rectangle bounds, string text, string key)
        {
            bool hover = string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);
            Color fill = hover ? UiTheme.DangerBackgroundHover : UiTheme.DangerBackground;
            Color border = hover ? UiTheme.DangerBorderHover : UiTheme.DangerBorder;
            Color fore = hover ? UiTheme.MiniStopIconHover : UiTheme.DangerForeground;

            DrawRoundedFill(graphics, bounds, fill, border, S(7));
            TextRenderer.DrawText(graphics, text, buttonFont, bounds, fore, TextFlags(ContentAlignment.MiddleCenter));
            hitRects[key] = bounds;
        }

        private void DrawButton(Graphics graphics, Rectangle bounds, string text, bool primary, bool enabled, string key)
        {
            bool hover = enabled && string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);
            Color fill = !enabled ? UiTheme.SecondaryDisabled : primary ? (hover ? UiTheme.PrimaryHover : UiTheme.Primary) : (hover ? UiTheme.SecondaryHover : UiTheme.SecondaryBack);
            Color border = !enabled ? UiTheme.BorderSoft : primary ? UiTheme.Primary : (hover ? UiTheme.FocusRing : UiTheme.Border);
            Color fore = !enabled ? UiTheme.SecondaryDisabledText : primary ? Color.White : (hover ? UiTheme.TextPrimary : UiTheme.TextSecondary);

            DrawRoundedFill(graphics, bounds, fill, border, S(7));
            TextRenderer.DrawText(graphics, text, buttonFont, bounds, fore, TextFlags(ContentAlignment.MiddleCenter));

            if (enabled)
            {
                hitRects[key] = bounds;
            }
        }

        private void DrawEmpty(Graphics graphics, Rectangle listBounds, string title, string hint, string buttonText, string key)
        {
            Rectangle bounds = new Rectangle(
                listBounds.X + S(6),
                listBounds.Y + S(6),
                Math.Max(S(140), listBounds.Width - S(18)),
                S(64));

            // Dashed card reads as a placeholder; only the pill inside is clickable.
            using (Pen pen = new Pen(UiTheme.Border, 1f))
            {
                pen.DashStyle = DashStyle.Dash;
                using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, S(7)))
                {
                    graphics.DrawPath(pen, path);
                }
            }

            Rectangle titleRect = new Rectangle(bounds.X + S(12), bounds.Y + S(6), Math.Max(1, bounds.Width - S(24)), S(15));
            Rectangle hintRect = new Rectangle(bounds.X + S(12), bounds.Y + S(21), Math.Max(1, bounds.Width - S(24)), S(13));
            TextRenderer.DrawText(graphics, title, itemTitleFont, titleRect, UiTheme.TextSecondary, TextFlags(ContentAlignment.MiddleLeft));
            TextRenderer.DrawText(graphics, hint, itemMetaFont, hintRect, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));

            int pillWidth = Math.Min(S(104), Math.Max(S(72), bounds.Width - S(24)));
            Rectangle pill = new Rectangle(bounds.X + S(12), bounds.Bottom - S(28), pillWidth, S(21));
            bool hover = string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);

            DrawRoundedFill(graphics, pill, hover ? UiTheme.PrimaryHover : UiTheme.Primary, UiTheme.Primary, S(10));
            TextRenderer.DrawText(graphics, buttonText, buttonFont, pill, Color.White, TextFlags(ContentAlignment.MiddleCenter));
            hitRects[key] = pill;
        }

        private void DrawCard(Graphics graphics, Rectangle bounds, Color fill, Color border)
        {
            DrawRoundedFill(graphics, new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1)), fill, border, S(8));
        }

        private void DrawBadge(Graphics graphics, Rectangle bounds, string text, Color fill, Color fore)
        {
            DrawRoundedFill(graphics, bounds, fill, fill, S(6));
            TextRenderer.DrawText(graphics, text ?? string.Empty, badgeFont, bounds, fore, TextFlags(ContentAlignment.MiddleCenter));
        }

        private void DrawRoundedFill(Graphics graphics, Rectangle bounds, Color fill, Color border, int radius)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectClipRgn(IntPtr hdc, IntPtr hrgn);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        private void DrawClipped(Graphics graphics, Rectangle bounds, Action draw)
        {
            Region oldClip = graphics.Clip;
            graphics.SetClip(bounds);

            // TextRenderer.DrawText renders through GDI, which ignores the GDI+ clip set
            // above. Mirror the same clip onto the device context so partially-scrolled
            // items cannot bleed text past the list edge (e.g. into the section header).
            IntPtr clipRegion = graphics.Clip.GetHrgn(graphics);
            IntPtr hdc = graphics.GetHdc();
            SelectClipRgn(hdc, clipRegion);
            graphics.ReleaseHdc(hdc);

            try
            {
                draw();
            }
            finally
            {
                hdc = graphics.GetHdc();
                SelectClipRgn(hdc, IntPtr.Zero);
                graphics.ReleaseHdc(hdc);

                if (clipRegion != IntPtr.Zero)
                {
                    DeleteObject(clipRegion);
                }
                graphics.Clip = oldClip;
            }
        }

        private bool IsItemHovered(string prefix, CommandEntry command)
        {
            return command != null &&
                string.Equals(hoverKey, prefix + ":" + (command.Id ?? string.Empty), StringComparison.OrdinalIgnoreCase);
        }

        private bool IsItemHovered(string prefix, SiteEntry site)
        {
            return site != null &&
                string.Equals(hoverKey, prefix + ":" + (site.Id ?? string.Empty), StringComparison.OrdinalIgnoreCase);
        }

        private void DrawReorderHandles(Graphics graphics, Rectangle bounds, string prefix, string id, int index, int count)
        {
            if (count <= 1)
            {
                return;
            }

            int columnX = bounds.Right - S(ReorderColumnWidth);
            int halfHeight = bounds.Height / 2;

            if (index > 0)
            {
                Rectangle up = new Rectangle(columnX, bounds.Y, S(ReorderColumnWidth), halfHeight);
                bool upHover = string.Equals(hoverKey, prefix + "-up:" + id, StringComparison.OrdinalIgnoreCase);
                DrawChevron(graphics, up, true, upHover);
                hitRects[prefix + "-up:" + id] = up;
            }

            if (index < count - 1)
            {
                Rectangle down = new Rectangle(columnX, bounds.Y + halfHeight, S(ReorderColumnWidth), bounds.Height - halfHeight);
                bool downHover = string.Equals(hoverKey, prefix + "-down:" + id, StringComparison.OrdinalIgnoreCase);
                DrawChevron(graphics, down, false, downHover);
                hitRects[prefix + "-down:" + id] = down;
            }
        }

        private void DrawChevron(Graphics graphics, Rectangle bounds, bool pointingUp, bool hover)
        {
            if (hover)
            {
                Rectangle bg = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
                DrawRoundedFill(graphics, bg, UiTheme.SecondaryPressed, UiTheme.BorderSoft, S(4));
            }

            Color color = hover ? UiTheme.Primary : UiTheme.TextMuted;
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int size = Math.Max(3, S(5));

            Point[] triangle;

            if (pointingUp)
            {
                triangle = new Point[]
                {
                    new Point(cx, cy - size),
                    new Point(cx - size, cy + size),
                    new Point(cx + size, cy + size)
                };
            }
            else
            {
                triangle = new Point[]
                {
                    new Point(cx, cy + size),
                    new Point(cx - size, cy - size),
                    new Point(cx + size, cy - size)
                };
            }

            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPolygon(brush, triangle);
            }
        }

        private string GetHitKey(Point point)
        {
            string bestKey = string.Empty;
            long bestArea = long.MaxValue;

            foreach (KeyValuePair<string, Rectangle> pair in hitRects)
            {
                if (pair.Value.Contains(point))
                {
                    long area = (long)pair.Value.Width * pair.Value.Height;

                    if (area < bestArea)
                    {
                        bestArea = area;
                        bestKey = pair.Key;
                    }
                }
            }

            return bestKey;
        }

        private void DispatchHit(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (key == "stop-all")
            {
                Raise(StopAllCommandsClicked);
                return;
            }

            if (key == "mode-web")
            {
                Raise(WorkspaceModeRequested, new SidebarWorkspaceModeEventArgs(WorkspaceMode.Web));
                return;
            }

            if (key == "mode-split")
            {
                Raise(WorkspaceModeRequested, new SidebarWorkspaceModeEventArgs(WorkspaceMode.Split));
                return;
            }

            if (key == "mode-logs")
            {
                Raise(WorkspaceModeRequested, new SidebarWorkspaceModeEventArgs(WorkspaceMode.Logs));
                return;
            }

            if (key.StartsWith("cmd-runstop:", StringComparison.OrdinalIgnoreCase))
            {
                CommandEntry command = FindCommand(key.Substring("cmd-runstop:".Length));
                if (command != null)
                {
                    SelectedCommandId = command.Id;
                    Invalidate();
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(command));
                    Raise(CommandInlineActionRequested, new SidebarCommandInlineActionEventArgs(command, CommandInlineAction.StartStop));
                }
                return;
            }

            if (key.StartsWith("cmd-restart:", StringComparison.OrdinalIgnoreCase))
            {
                CommandEntry command = FindCommand(key.Substring("cmd-restart:".Length));
                if (command != null)
                {
                    SelectedCommandId = command.Id;
                    Invalidate();
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(command));
                    Raise(CommandInlineActionRequested, new SidebarCommandInlineActionEventArgs(command, CommandInlineAction.Restart));
                }
                return;
            }

            if (key.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            {
                CommandEntry command = FindCommand(key.Substring(4));
                if (command != null)
                {
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(command));
                }
                return;
            }

            if (key.StartsWith("site:", StringComparison.OrdinalIgnoreCase))
            {
                SiteEntry site = FindSite(key.Substring(5));
                if (site != null)
                {
                    Raise(SiteActivated, new SidebarListItemEventArgs<SiteEntry>(site));
                }
                return;
            }

            if (key.StartsWith("cmd-up:", StringComparison.OrdinalIgnoreCase))
            {
                Raise(CommandReorderRequested, new SidebarReorderEventArgs(key.Substring("cmd-up:".Length), -1));
                return;
            }

            if (key.StartsWith("cmd-down:", StringComparison.OrdinalIgnoreCase))
            {
                Raise(CommandReorderRequested, new SidebarReorderEventArgs(key.Substring("cmd-down:".Length), 1));
                return;
            }

            if (key.StartsWith("site-up:", StringComparison.OrdinalIgnoreCase))
            {
                Raise(SiteReorderRequested, new SidebarReorderEventArgs(key.Substring("site-up:".Length), -1));
                return;
            }

            if (key.StartsWith("site-down:", StringComparison.OrdinalIgnoreCase))
            {
                Raise(SiteReorderRequested, new SidebarReorderEventArgs(key.Substring("site-down:".Length), 1));
                return;
            }

            DispatchActionHit(key);
        }

        private void DispatchActionHit(string key)
        {
            if (key == "cmd-add")
            {
                Raise(CommandActionRequested, new SidebarCommandActionEventArgs(SidebarCommandAction.Add));
            }
            else if (key == "cmd-edit")
            {
                Raise(CommandActionRequested, new SidebarCommandActionEventArgs(SidebarCommandAction.Edit));
            }
            else if (key == "cmd-delete")
            {
                Raise(CommandActionRequested, new SidebarCommandActionEventArgs(SidebarCommandAction.Delete));
            }
            else if (key == "cmd-restart")
            {
                Raise(CommandActionRequested, new SidebarCommandActionEventArgs(SidebarCommandAction.Restart));
            }
            else if (key == "cmd-startstop")
            {
                Raise(CommandActionRequested, new SidebarCommandActionEventArgs(SidebarCommandAction.StartStop));
            }
            else if (key == "site-add")
            {
                Raise(SiteActionRequested, new SidebarSiteActionEventArgs(SidebarSiteAction.Add));
            }
            else if (key == "site-edit")
            {
                Raise(SiteActionRequested, new SidebarSiteActionEventArgs(SidebarSiteAction.Edit));
            }
            else if (key == "site-delete")
            {
                Raise(SiteActionRequested, new SidebarSiteActionEventArgs(SidebarSiteAction.Delete));
            }
            else if (key == "site-open")
            {
                Raise(SiteActionRequested, new SidebarSiteActionEventArgs(SidebarSiteAction.Open));
            }
        }

        private static void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private static void Raise<T>(EventHandler<T> handler, T args) where T : EventArgs
        {
            if (handler != null)
            {
                handler(null, args);
            }
        }

        private int GetMaxCommandScroll()
        {
            return Math.Max(0, GetListContentHeight(commands.Count, S(CommandItemHeight)) - commandListRect.Height);
        }

        private int GetMaxSiteScroll()
        {
            return Math.Max(0, GetListContentHeight(sites.Count, S(SiteItemHeight)) - siteListRect.Height);
        }

        private static int GetListContentHeight(int count, int itemHeight)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (S(ListTopPadding) * 2) + (count * itemHeight) + ((count - 1) * S(ItemSpacing));
        }

        private string GetCommandTitle(CommandEntry command)
        {
            string text = command == null ? string.Empty : command.Name ?? string.Empty;

            if (command != null && command.EnabledOnStart)
            {
                text += "  [自启]";
            }

            if (command != null && command.AutoRetry != null && command.AutoRetry.Enabled)
            {
                text += "  [重试]";
            }

            return text;
        }

        private string GetCommandMeta(CommandEntry command)
        {
            string value;

            if (command == null)
            {
                return string.Empty;
            }

            value = (command.Command ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

            if (value.Length > 240)
            {
                value = value.Substring(0, 237) + "...";
            }

            return RunModeCatalog.GetDisplayName(command.RunMode) + "  |  " + value;
        }

        private Color GetStatusAccent(CommandStatus status)
        {
            if (status == CommandStatus.Running)
            {
                return UiTheme.SuccessForeground;
            }

            if (status == CommandStatus.Error)
            {
                return UiTheme.DangerForeground;
            }

            if (status == CommandStatus.Starting || status == CommandStatus.Stopping || status == CommandStatus.WaitingRetry)
            {
                return UiTheme.WarningForeground;
            }

            return UiTheme.BadgeNeutralForeground;
        }

        private Color GetStatusBadgeBackground(CommandStatus status)
        {
            if (status == CommandStatus.Running)
            {
                return UiTheme.SuccessBackground;
            }

            if (status == CommandStatus.Error)
            {
                return UiTheme.DangerBackground;
            }

            if (status == CommandStatus.Starting || status == CommandStatus.Stopping || status == CommandStatus.WaitingRetry)
            {
                return UiTheme.WarningBackground;
            }

            return UiTheme.BadgeNeutralBackground;
        }

        private static TextFormatFlags TextFlags(ContentAlignment alignment)
        {
            TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

            if (alignment == ContentAlignment.MiddleCenter)
            {
                flags |= TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            }
            else if (alignment == ContentAlignment.MiddleLeft)
            {
                flags |= TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            }
            else
            {
                flags |= TextFormatFlags.Left | TextFormatFlags.Top;
            }

            return flags;
        }

        private struct ButtonSpec
        {
            public ButtonSpec(string text, bool primary, bool enabled, string key)
            {
                Text = text;
                Primary = primary;
                Enabled = enabled;
                Key = key;
            }

            public readonly string Text;
            public readonly bool Primary;
            public readonly bool Enabled;
            public readonly string Key;
        }
    }
}
