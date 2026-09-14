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
        public SidebarReorderEventArgs(int index, int delta)
        {
            Index = index;
            Delta = delta;
        }

        public int Index { get; private set; }

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
        private const int OuterLeft = 16;
        private const int OuterTop = 16;
        private const int OuterRight = 16;
        private const int OuterBottom = 14;
        private const int BrandHeight = 164;
        private const int MinSectionHeight = 180;
        private const int SplitterHeight = 8;
        private const int SectionPaddingTop = 14;
        private const int SectionTitleHeight = 30;
        private const int ReorderColumnWidth = 28;
        private const int ActionHeight = 40;
        private const int SiteActionsHeight = 40;
        private const int CommandItemHeight = 62;
        private const int SiteItemHeight = 58;
        private const int ItemSpacing = 8;
        private const int ListHorizontalPadding = 8;
        private const int ListTopPadding = 6;

        private readonly List<CommandEntry> commands;
        private readonly List<SiteEntry> sites;
        private readonly Font appTitleFont;
        private readonly Font sectionTitleFont;
        private readonly Font buttonFont;
        private readonly Font itemTitleFont;
        private readonly Font itemMetaFont;
        private readonly Font summaryFont;
        private readonly Font badgeFont;
        private readonly Dictionary<string, Rectangle> hitRects;
        private Rectangle commandListRect;
        private Rectangle siteListRect;
        private Rectangle splitterRect;
        private string hoverKey;
        private int commandScrollY;
        private int siteScrollY;
        private double commandSectionRatio;
        private bool draggingSplitter;
        private Point lastMousePosition;
        private ToolTip surfaceToolTip;
        private string lastToolTipKey = string.Empty;

        public SidebarSurfaceControl()
        {
            commands = new List<CommandEntry>();
            sites = new List<SiteEntry>();
            hitRects = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            appTitleFont = UiTheme.CreateFont(13.5f, FontStyle.Bold);
            sectionTitleFont = UiTheme.CreateFont(11f, FontStyle.Bold);
            buttonFont = UiTheme.CreateFont(9f, FontStyle.Bold);
            itemTitleFont = UiTheme.CreateFont(9.25f, FontStyle.Bold);
            itemMetaFont = UiTheme.CreateFont(8.4f, FontStyle.Regular);
            summaryFont = UiTheme.CreateFont(8.75f, FontStyle.Regular);
            badgeFont = UiTheme.CreateFont(8.25f, FontStyle.Bold);

            BackColor = UiTheme.SidebarBackground;
            Cursor = Cursors.Default;
            DoubleBuffered = true;
            commandSectionRatio = AppConfigStore.DefaultCommandSectionRatio;
            surfaceToolTip = new ToolTip();
            surfaceToolTip.InitialDelay = 350;
            surfaceToolTip.ReshowDelay = 100;
            surfaceToolTip.AutoPopDelay = 3000;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
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
        public event EventHandler BackSiteClicked;
        public event EventHandler HomeSiteClicked;
        public event EventHandler ReloadSiteClicked;
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

        public string SummaryText { get; set; }

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

        public bool BackSiteEnabled { get; set; }

        public bool HomeSiteEnabled { get; set; }

        public bool ReloadSiteEnabled { get; set; }

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

            siteScrollY = Math.Min(siteScrollY, GetMaxSiteScroll());
            Invalidate();
        }

        public void EnsureCommandVisible(int index)
        {
            if (EnsureVisible(index, CommandItemHeight, commandListRect, commands.Count, GetMaxCommandScroll(), ref commandScrollY))
            {
                Invalidate();
            }
        }

        public void EnsureSiteVisible(int index)
        {
            if (EnsureVisible(index, SiteItemHeight, siteListRect, sites.Count, GetMaxSiteScroll(), ref siteScrollY))
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

            int stride = itemHeight + ItemSpacing;
            int itemTop = ListTopPadding + index * stride - scrollY;

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
                if (surfaceToolTip != null)
                {
                    surfaceToolTip.Dispose();
                    surfaceToolTip = null;
                }

                appTitleFont.Dispose();
                sectionTitleFont.Dispose();
                buttonFont.Dispose();
                itemTitleFont.Dispose();
                itemMetaFont.Dispose();
                summaryFont.Dispose();
                badgeFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle content = GetContentBounds();
            Rectangle brand = new Rectangle(content.X, content.Y, content.Width, BrandHeight);
            int availableHeight = Math.Max(0, content.Bottom - brand.Bottom);
            int commandHeight = ClampCommandSectionHeight(
                (int)(availableHeight * commandSectionRatio),
                availableHeight);

            Rectangle commandSection = new Rectangle(content.X, brand.Bottom, content.Width, commandHeight);
            splitterRect = new Rectangle(content.X, commandSection.Bottom, content.Width, SplitterHeight);
            Rectangle siteSection = new Rectangle(
                content.X,
                splitterRect.Bottom,
                content.Width,
                Math.Max(0, content.Bottom - splitterRect.Bottom));

            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            hitRects.Clear();
            DrawBrand(e.Graphics, brand);
            DrawCommandSection(e.Graphics, commandSection);
            DrawSplitter(e.Graphics, splitterRect);
            DrawSiteSection(e.Graphics, siteSection);
        }

        private static double ClampRatio(double ratio)
        {
            if (ratio < 0.20 || ratio > 0.80 || double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                return AppConfigStore.DefaultCommandSectionRatio;
            }

            return ratio;
        }

        // Keep both sections tall enough to render their title + action row + at least one item.
        private int ClampCommandSectionHeight(int desired, int availableHeight)
        {
            int min = MinSectionHeight;

            if (availableHeight < min * 2)
            {
                return Math.Max(min, availableHeight);
            }

            int max = availableHeight - min;

            return Math.Min(max, Math.Max(min, desired));
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
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            lastMousePosition = e.Location;

            if (draggingSplitter)
            {
                UpdateSplitterFromMouse(e.Location);
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

            return string.IsNullOrEmpty(hover) ? Cursors.Default : Cursors.Hand;
        }

        private void UpdateSplitterFromMouse(Point location)
        {
            Rectangle content = GetContentBounds();
            int brandBottom = content.Y + BrandHeight;
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

        protected override void OnMouseLeave(EventArgs e)
        {
            UpdateToolTip(string.Empty);

            if (!draggingSplitter && !string.IsNullOrEmpty(hoverKey))
            {
                hoverKey = string.Empty;
                Invalidate();
            }

            if (!draggingSplitter)
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
                int idx;
                if (int.TryParse(hitKey.Substring("cmd-runstop:".Length), out idx) && idx >= 0 && idx < commands.Count)
                {
                    CommandEntry cmd = commands[idx];
                    CommandRuntimeSnapshot snapshot = SnapshotProvider == null || cmd == null ? null : SnapshotProvider(cmd.Id);
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
            else if (hitKey.StartsWith("site:", StringComparison.OrdinalIgnoreCase))
            {
                int idx;
                if (int.TryParse(hitKey.Substring("site:".Length), out idx) && idx >= 0 && idx < sites.Count)
                {
                    SiteEntry site = sites[idx];
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

                    string statusDesc = health == SiteHealth.Up ? "服务正常" : health == SiteHealth.Down ? "服务不可达" : "状态检测中";
                    string proxyDesc = (site != null && site.ProxyEnabled && !string.IsNullOrWhiteSpace(site.ProxyServer))
                        ? " · 代理: " + site.ProxyServer.Trim()
                        : string.Empty;
                    surfaceToolTip.SetToolTip(this, (site != null ? site.Name : string.Empty) + " · " + statusDesc + proxyDesc + "\r\n" + (site != null ? site.Url : string.Empty));
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
                    if (key.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase) ||
                        key.StartsWith("cmd-", StringComparison.OrdinalIgnoreCase))
                    {
                        int colonIndex = key.IndexOf(':');
                        int index;
                        if (colonIndex >= 0 && int.TryParse(key.Substring(colonIndex + 1), out index) && index >= 0 && index < commands.Count)
                        {
                            SelectedCommandId = commands[index].Id;
                            Invalidate();
                            Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(commands[index]));
                            Raise(CommandContextMenuRequested, new SidebarItemContextMenuEventArgs<CommandEntry>(commands[index], PointToScreen(e.Location)));
                            return;
                        }
                    }
                    else if (key.StartsWith("site:", StringComparison.OrdinalIgnoreCase) ||
                             key.StartsWith("site-", StringComparison.OrdinalIgnoreCase))
                    {
                        int colonIndex = key.IndexOf(':');
                        int index;
                        if (colonIndex >= 0 && int.TryParse(key.Substring(colonIndex + 1), out index) && index >= 0 && index < sites.Count)
                        {
                            SelectedSiteId = sites[index].Id;
                            Invalidate();
                            Raise(SiteActivated, new SidebarListItemEventArgs<SiteEntry>(sites[index]));
                            Raise(SiteContextMenuRequested, new SidebarItemContextMenuEventArgs<SiteEntry>(sites[index], PointToScreen(e.Location)));
                            return;
                        }
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
                draggingSplitter = true;
                Capture = true;
                Cursor = Cursors.SizeNS;
                return;
            }

            DispatchHit(key);
            base.OnMouseDown(e);
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

        protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
        {
            // Make arrow keys reach OnKeyDown instead of being consumed for focus navigation.
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down ||
                e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown ||
                e.KeyCode == Keys.Enter || e.KeyCode == Keys.Delete)
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

            bool siteActive = siteListRect.Contains(lastMousePosition);

            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                    MoveSelection(siteActive, e.KeyCode);
                    e.Handled = true;
                    break;
                case Keys.Enter:
                    ActivateSelection(siteActive);
                    e.Handled = true;
                    break;
                case Keys.Delete:
                    DeleteSelection(siteActive);
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            int delta = (e.Delta / 120) * lines * 24;

            if (delta == 0)
            {
                delta = e.Delta > 0 ? 24 : -24;
            }

            if (commandListRect.Contains(e.Location))
            {
                commandScrollY = Math.Max(0, Math.Min(GetMaxCommandScroll(), commandScrollY - delta));
                Invalidate(commandListRect);
                return;
            }

            if (siteListRect.Contains(e.Location))
            {
                siteScrollY = Math.Max(0, Math.Min(GetMaxSiteScroll(), siteScrollY - delta));
                Invalidate(siteListRect);
                return;
            }

            base.OnMouseWheel(e);
        }

        private Rectangle GetContentBounds()
        {
            return new Rectangle(
                OuterLeft,
                OuterTop,
                Math.Max(0, ClientSize.Width - OuterLeft - OuterRight),
                Math.Max(0, ClientSize.Height - OuterTop - OuterBottom));
        }

        private void DrawBrand(Graphics graphics, Rectangle bounds)
        {
            Rectangle inner = new Rectangle(bounds.X + 18, bounds.Y + 16, Math.Max(0, bounds.Width - 36), Math.Max(0, bounds.Height - 30));
            int stopWidth = Math.Min(124, Math.Max(0, inner.Width / 2));
            int titleWidth = Math.Max(0, inner.Width - stopWidth - 8);
            Rectangle title = new Rectangle(inner.X, inner.Y, titleWidth, 42);
            Rectangle stop = new Rectangle(inner.Right - stopWidth, inner.Y + 2, stopWidth, 34);
            Rectangle summary = new Rectangle(inner.X, inner.Y + 52, inner.Width, 34);
            Rectangle actionRow = new Rectangle(inner.X, inner.Y + 92, inner.Width, 36);

            TextRenderer.DrawText(graphics, "Switch \u63a7\u5236\u53f0", appTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawDangerButton(graphics, stop, "\u5168\u90e8\u505c\u6b62", "stop-all");
            TextRenderer.DrawText(graphics, SummaryText ?? string.Empty, summaryFont, summary, UiTheme.TextSecondary, TextFlags(ContentAlignment.TopLeft) | TextFormatFlags.EndEllipsis);

            DrawSegmentControl(graphics, actionRow);
        }

        private void DrawCommandSection(Graphics graphics, Rectangle section)
        {
            Rectangle title = new Rectangle(section.X, section.Y + SectionPaddingTop, section.Width, SectionTitleHeight);
            Rectangle actions = new Rectangle(section.X, Math.Max(title.Bottom, section.Bottom - ActionHeight), section.Width, ActionHeight);

            commandListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            TextRenderer.DrawText(graphics, "\u547d\u4ee4", sectionTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft) | TextFormatFlags.NoPadding);
            DrawCommandList(graphics, commandListRect);
            DrawFourButtons(
                graphics,
                actions,
                new ButtonSpec("\u65b0\u589e", true, true, "cmd-add"),
                new ButtonSpec("\u7f16\u8f91", false, EditCommandEnabled, "cmd-edit"),
                new ButtonSpec("\u5220\u9664", false, DeleteCommandEnabled, "cmd-delete"),
                new ButtonSpec(string.IsNullOrEmpty(StartStopCommandText) ? "\u542f\u52a8" : StartStopCommandText, true, StartStopCommandEnabled, "cmd-startstop"));
        }

        private void DrawSiteSection(Graphics graphics, Rectangle section)
        {
            Rectangle title = new Rectangle(section.X, section.Y + SectionPaddingTop, section.Width, SectionTitleHeight);
            Rectangle actions = new Rectangle(section.X, Math.Max(title.Bottom, section.Bottom - SiteActionsHeight), section.Width, SiteActionsHeight);

            siteListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            TextRenderer.DrawText(graphics, "\u7ad9\u70b9", sectionTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft) | TextFormatFlags.NoPadding);
            DrawSiteList(graphics, siteListRect);
            DrawFourButtons(
                graphics,
                actions,
                new ButtonSpec("\u65b0\u589e", true, true, "site-add"),
                new ButtonSpec("\u7f16\u8f91", false, EditSiteEnabled, "site-edit"),
                new ButtonSpec("\u5220\u9664", false, DeleteSiteEnabled, "site-delete"),
                new ButtonSpec("\u6253\u5f00", false, OpenSiteEnabled, "site-open"));
        }

        private void DrawCommandList(Graphics graphics, Rectangle bounds)
        {
            if (commands.Count == 0)
            {
                DrawEmpty(graphics, bounds, "\u6682\u65e0\u547d\u4ee4\uff0c\u70b9\u51fb\u201c\u65b0\u589e\u201d\u521b\u5efa\u4e00\u4e2a\u672c\u5730\u670d\u52a1\u547d\u4ee4\u3002", "cmd-add");
                return;
            }

            int commandMaxScroll = Math.Max(0, GetListContentHeight(commands.Count, CommandItemHeight) - bounds.Height);
            commandScrollY = Math.Max(0, Math.Min(commandMaxScroll, commandScrollY));

            DrawClipped(graphics, bounds, delegate
            {
                int stride = CommandItemHeight + ItemSpacing;
                int first = Math.Max(0, (commandScrollY - ListTopPadding) / stride);
                int last = Math.Min(commands.Count - 1, ((commandScrollY + bounds.Height - ListTopPadding) / stride) + 1);

                for (int index = first; index <= last; index++)
                {
                    Rectangle itemBounds = new Rectangle(
                        bounds.X + ListHorizontalPadding,
                        bounds.Y + ListTopPadding + (index * stride) - commandScrollY,
                        Math.Max(1, bounds.Width - (ListHorizontalPadding * 2)),
                        CommandItemHeight);
                    DrawCommandItem(graphics, commands[index], itemBounds, index);
                }
            });
        }

        private void DrawSiteList(Graphics graphics, Rectangle bounds)
        {
            if (sites.Count == 0)
            {
                DrawEmpty(graphics, bounds, "\u6682\u65e0\u7ad9\u70b9\uff0c\u8bf7\u5148\u65b0\u589e\u8981\u67e5\u770b\u7684\u672c\u5730\u7f51\u9875\u3002", "site-add");
                return;
            }

            int siteMaxScroll = Math.Max(0, GetListContentHeight(sites.Count, SiteItemHeight) - bounds.Height);
            siteScrollY = Math.Max(0, Math.Min(siteMaxScroll, siteScrollY));

            DrawClipped(graphics, bounds, delegate
            {
                int stride = SiteItemHeight + ItemSpacing;
                int first = Math.Max(0, (siteScrollY - ListTopPadding) / stride);
                int last = Math.Min(sites.Count - 1, ((siteScrollY + bounds.Height - ListTopPadding) / stride) + 1);

                for (int index = first; index <= last; index++)
                {
                    Rectangle itemBounds = new Rectangle(
                        bounds.X + ListHorizontalPadding,
                        bounds.Y + ListTopPadding + (index * stride) - siteScrollY,
                        Math.Max(1, bounds.Width - (ListHorizontalPadding * 2)),
                        SiteItemHeight);
                    DrawSiteItem(graphics, sites[index], itemBounds, index);
                }
            });
        }

        private void DrawCommandItem(Graphics graphics, CommandEntry command, Rectangle bounds, int index)
        {
            CommandRuntimeSnapshot snapshot = SnapshotProvider == null || command == null ? null : SnapshotProvider(command.Id);
            CommandStatus status = snapshot == null ? CommandStatus.Stopped : snapshot.Status;
            Color accent = GetStatusAccent(status);
            bool selected = command != null && string.Equals(command.Id, SelectedCommandId, StringComparison.OrdinalIgnoreCase);
            bool itemHovered = IsItemHovered("cmd", index);
            Color fill = selected ? Color.FromArgb(240, 249, 255) : itemHovered ? Color.FromArgb(248, 250, 252) : UiTheme.Surface;
            Color border = selected ? UiTheme.Primary : itemHovered ? Color.FromArgb(186, 230, 253) : UiTheme.Border;
            int contentRight = bounds.Right - ReorderColumnWidth - 4;
            Rectangle badge = new Rectangle(contentRight - 76, bounds.Y + 9, 76, 23);
            int titleRight = badge.X - 6;

            DrawCard(graphics, bounds, fill, border);
            DrawRoundedFill(graphics, new Rectangle(bounds.X + 10, bounds.Y + 10, 5, Math.Max(10, bounds.Height - 20)), accent, accent, 2);

            if (itemHovered || selected)
            {
                bool isRunning = status == CommandStatus.Running;
                int btnWidth = 24;
                int btnHeight = 23;
                Rectangle runBtn = new Rectangle(badge.Left - btnWidth - 4, bounds.Y + 9, btnWidth, btnHeight);
                Rectangle restartBtn = new Rectangle(runBtn.Left - btnWidth - 3, bounds.Y + 9, btnWidth, btnHeight);

                hitRects["cmd-runstop:" + index] = runBtn;
                hitRects["cmd-restart:" + index] = restartBtn;

                bool runHover = string.Equals(hoverKey, "cmd-runstop:" + index, StringComparison.OrdinalIgnoreCase);
                bool restartHover = string.Equals(hoverKey, "cmd-restart:" + index, StringComparison.OrdinalIgnoreCase);

                DrawMiniIconButton(graphics, runBtn, isRunning ? MiniIconType.Stop : MiniIconType.Play, runHover);
                DrawMiniIconButton(graphics, restartBtn, MiniIconType.Restart, restartHover);

                titleRight = restartBtn.Left - 6;
            }

            Rectangle title = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, titleRight - bounds.X - 26), 23);
            Rectangle meta = new Rectangle(bounds.X + 26, bounds.Y + 38, Math.Max(1, titleRight - bounds.X - 26), 18);

            TextRenderer.DrawText(graphics, GetCommandTitle(command), itemTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawBadge(graphics, badge, snapshot == null ? "\u5df2\u505c\u6b62" : snapshot.GetDisplayStatus(), GetStatusBadgeBackground(status), accent);
            TextRenderer.DrawText(graphics, GetCommandMeta(command), itemMetaFont, meta, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["cmd:" + index] = bounds;
            DrawReorderHandles(graphics, bounds, "cmd", index, commands.Count, selected || itemHovered);
        }

        private enum MiniIconType
        {
            Play,
            Stop,
            Restart
        }

        private void DrawMiniIconButton(Graphics graphics, Rectangle bounds, MiniIconType iconType, bool hover)
        {
            Color fill;
            Color border;
            Color iconColor;

            if (iconType == MiniIconType.Stop)
            {
                fill = hover ? Color.FromArgb(254, 226, 226) : Color.FromArgb(254, 242, 242);
                border = hover ? UiTheme.DangerForeground : Color.FromArgb(252, 165, 165);
                iconColor = hover ? Color.FromArgb(153, 27, 27) : UiTheme.DangerForeground;
            }
            else if (iconType == MiniIconType.Play)
            {
                fill = hover ? Color.FromArgb(224, 242, 254) : UiTheme.SecondaryBack;
                border = hover ? UiTheme.Primary : UiTheme.BorderSoft;
                iconColor = hover ? Color.FromArgb(2, 132, 199) : UiTheme.Primary;
            }
            else
            {
                fill = hover ? Color.FromArgb(241, 245, 249) : UiTheme.SecondaryBack;
                border = hover ? UiTheme.Primary : UiTheme.BorderSoft;
                iconColor = hover ? UiTheme.Primary : UiTheme.TextSecondary;
            }

            DrawRoundedFill(graphics, bounds, fill, border, 5);

            SmoothingMode oldMode = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = bounds.X + (bounds.Width / 2);
            int cy = bounds.Y + (bounds.Height / 2);

            using (SolidBrush brush = new SolidBrush(iconColor))
            {
                if (iconType == MiniIconType.Play)
                {
                    Point[] playTriangle = new Point[]
                    {
                        new Point(cx - 3, cy - 5),
                        new Point(cx + 4, cy),
                        new Point(cx - 3, cy + 5)
                    };
                    graphics.FillPolygon(brush, playTriangle);
                }
                else if (iconType == MiniIconType.Stop)
                {
                    graphics.FillRectangle(brush, cx - 4, cy - 4, 8, 8);
                }
                else if (iconType == MiniIconType.Restart)
                {
                    using (Pen pen = new Pen(iconColor, 1.8f))
                    {
                        graphics.DrawArc(pen, cx - 5, cy - 5, 10, 10, 0, 270);
                    }

                    Point[] arrow = new Point[]
                    {
                        new Point(cx + 4, cy - 5),
                        new Point(cx, cy - 8),
                        new Point(cx, cy - 2)
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
            bool itemHovered = IsItemHovered("site", index);
            Color fill = selected ? Color.FromArgb(240, 249, 255) : itemHovered ? Color.FromArgb(248, 250, 252) : UiTheme.Surface;
            Color border = selected ? UiTheme.Primary : itemHovered ? Color.FromArgb(186, 230, 253) : UiTheme.Border;

            DrawCard(graphics, bounds, fill, border);
            DrawRoundedFill(graphics, new Rectangle(bounds.X + 10, bounds.Y + 10, 5, Math.Max(10, bounds.Height - 20)), accent, accent, 2);

            int siteContentRight = bounds.Right - ReorderColumnWidth - 4;
            Rectangle titleRect = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, siteContentRight - bounds.X - 30), 22);
            Rectangle urlRect = new Rectangle(bounds.X + 26, bounds.Y + 35, Math.Max(1, siteContentRight - bounds.X - 30), 18);

            bool hasProxy = site != null && site.ProxyEnabled && !string.IsNullOrWhiteSpace(site.ProxyServer);
            if (hasProxy)
            {
                int tagWidth = 32;
                int tagHeight = 16;
                Rectangle tagRect = new Rectangle(siteContentRight - tagWidth - 2, bounds.Y + 11, tagWidth, tagHeight);
                DrawRoundedFill(graphics, tagRect, Color.FromArgb(238, 242, 255), Color.FromArgb(199, 210, 254), 3);
                TextRenderer.DrawText(graphics, "代理", itemMetaFont, tagRect, Color.FromArgb(67, 56, 202), TextFlags(ContentAlignment.MiddleCenter));
                titleRect = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, tagRect.Left - bounds.X - 28), 22);
            }

            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Name ?? string.Empty, itemTitleFont, titleRect, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Url ?? string.Empty, itemMetaFont, urlRect, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["site:" + index] = bounds;
            DrawReorderHandles(graphics, bounds, "site", index, sites.Count, selected || itemHovered);
        }

        private static Color GetSiteAccent(SiteHealth health)
        {
            switch (health)
            {
                case SiteHealth.Up:
                    return Color.FromArgb(46, 160, 97);
                case SiteHealth.Down:
                    return Color.FromArgb(220, 68, 68);
                default:
                    return Color.FromArgb(160, 174, 192);
            }
        }

        private void DrawFourButtons(Graphics graphics, Rectangle bounds, ButtonSpec first, ButtonSpec second, ButtonSpec third, ButtonSpec fourth)
        {
            DrawButtons(graphics, bounds, first, second, third, fourth);
        }

        private void DrawFiveButtons(Graphics graphics, Rectangle bounds, ButtonSpec first, ButtonSpec second, ButtonSpec third, ButtonSpec fourth, ButtonSpec fifth)
        {
            DrawButtons(graphics, bounds, first, second, third, fourth, fifth);
        }

        private void DrawButtons(Graphics graphics, Rectangle bounds, params ButtonSpec[] specs)
        {
            int count = specs.Length;

            if (count == 0)
            {
                return;
            }

            int gap = bounds.Width < 260 ? 8 : 12;
            int buttonHeight = Math.Min(34, bounds.Height);
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

        private void DrawTwoButtons(Graphics graphics, Rectangle bounds, ButtonSpec first, ButtonSpec second)
        {
            int gap = bounds.Width < 260 ? 8 : 12;
            int buttonHeight = Math.Min(34, bounds.Height);
            int y = bounds.Y + Math.Max(0, (bounds.Height - buttonHeight) / 2);
            int available = Math.Max(0, bounds.Width - gap);
            int buttonWidth = available / 2;
            int x = bounds.X;

            DrawButton(graphics, new Rectangle(x, y, buttonWidth, buttonHeight), first.Text, first.Primary, first.Enabled, first.Key);
            x += buttonWidth + gap;
            DrawButton(graphics, new Rectangle(x, y, Math.Max(0, bounds.Right - x), buttonHeight), second.Text, second.Primary, second.Enabled, second.Key);
        }

        private void DrawSegmentControl(Graphics graphics, Rectangle bounds)
        {
            DrawRoundedFill(graphics, bounds, Color.FromArgb(238, 242, 246), Color.FromArgb(226, 232, 240), 8);

            int padding = 3;
            Rectangle inner = new Rectangle(bounds.X + padding, bounds.Y + padding, bounds.Width - (padding * 2), bounds.Height - (padding * 2));
            int segmentWidth = inner.Width / 3;

            Rectangle webRect = new Rectangle(inner.X, inner.Y, segmentWidth, inner.Height);
            Rectangle splitRect = new Rectangle(webRect.Right, inner.Y, segmentWidth, inner.Height);
            Rectangle logsRect = new Rectangle(splitRect.Right, inner.Y, Math.Max(0, inner.Right - splitRect.Right), inner.Height);

            DrawSegmentPill(graphics, webRect, "\u7f51\u9875", WorkspaceMode == WorkspaceMode.Web, "mode-web");
            DrawSegmentPill(graphics, splitRect, "\u5206\u5c4f", WorkspaceMode == WorkspaceMode.Split, "mode-split");
            DrawSegmentPill(graphics, logsRect, "\u65e5\u5fd7", WorkspaceMode == WorkspaceMode.Logs, "mode-logs");
        }

        private void DrawSegmentPill(Graphics graphics, Rectangle bounds, string text, bool active, string key)
        {
            bool hover = string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);

            if (active)
            {
                DrawRoundedFill(graphics, bounds, Color.White, Color.FromArgb(218, 225, 233), 6);
                TextRenderer.DrawText(graphics, text, buttonFont, bounds, UiTheme.Primary, TextFlags(ContentAlignment.MiddleCenter));
            }
            else
            {
                if (hover)
                {
                    DrawRoundedFill(graphics, bounds, Color.FromArgb(248, 250, 252), Color.Transparent, 6);
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
            Color fore = hover ? Color.FromArgb(153, 27, 27) : UiTheme.DangerForeground;

            DrawRoundedFill(graphics, bounds, fill, border, 7);
            TextRenderer.DrawText(graphics, text, buttonFont, bounds, fore, TextFlags(ContentAlignment.MiddleCenter));
            hitRects[key] = bounds;
        }

        private void DrawButton(Graphics graphics, Rectangle bounds, string text, bool primary, bool enabled, string key)
        {
            bool hover = enabled && string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);
            Color fill = !enabled ? UiTheme.SecondaryDisabled : primary ? (hover ? UiTheme.PrimaryHover : UiTheme.Primary) : (hover ? UiTheme.SecondaryHover : UiTheme.SecondaryBack);
            Color border = !enabled ? UiTheme.BorderSoft : primary ? UiTheme.Primary : (hover ? UiTheme.FocusRing : UiTheme.Border);
            Color fore = !enabled ? UiTheme.SecondaryDisabledText : primary ? Color.White : (hover ? UiTheme.TextPrimary : UiTheme.TextSecondary);

            DrawRoundedFill(graphics, bounds, fill, border, 7);
            TextRenderer.DrawText(graphics, text, buttonFont, bounds, fore, TextFlags(ContentAlignment.MiddleCenter));

            if (enabled)
            {
                hitRects[key] = bounds;
            }
        }

        private void DrawEmpty(Graphics graphics, Rectangle listBounds, string text, string key)
        {
            Rectangle bounds = new Rectangle(listBounds.X + 8, listBounds.Y + 8, Math.Max(160, listBounds.Width - 24), 72);
            DrawRoundedFill(graphics, bounds, Color.FromArgb(233, 241, 249), Color.FromArgb(233, 241, 249), 6);
            TextRenderer.DrawText(graphics, text, summaryFont, new Rectangle(bounds.X + 10, bounds.Y + 10, Math.Max(1, bounds.Width - 20), Math.Max(1, bounds.Height - 20)), UiTheme.TextMuted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            hitRects[key] = bounds;
        }

        private void DrawCard(Graphics graphics, Rectangle bounds, Color fill, Color border)
        {
            DrawRoundedFill(graphics, new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1)), fill, border, 8);
        }

        private void DrawBadge(Graphics graphics, Rectangle bounds, string text, Color fill, Color fore)
        {
            DrawRoundedFill(graphics, bounds, fill, fill, 6);
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

        private bool IsItemHovered(string prefix, int index)
        {
            if (string.IsNullOrEmpty(hoverKey))
            {
                return false;
            }

            string suffix = ":" + index;
            if (!hoverKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return hoverKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawReorderHandles(Graphics graphics, Rectangle bounds, string prefix, int index, int count, bool active)
        {
            if (!active || count <= 1)
            {
                return;
            }

            int columnX = bounds.Right - ReorderColumnWidth;
            int halfHeight = bounds.Height / 2;

            if (index > 0)
            {
                Rectangle up = new Rectangle(columnX, bounds.Y, ReorderColumnWidth, halfHeight);
                bool upHover = string.Equals(hoverKey, prefix + "-up:" + index, StringComparison.OrdinalIgnoreCase);
                DrawChevron(graphics, up, true, upHover);
                hitRects[prefix + "-up:" + index] = up;
            }

            if (index < count - 1)
            {
                Rectangle down = new Rectangle(columnX, bounds.Y + halfHeight, ReorderColumnWidth, bounds.Height - halfHeight);
                bool downHover = string.Equals(hoverKey, prefix + "-down:" + index, StringComparison.OrdinalIgnoreCase);
                DrawChevron(graphics, down, false, downHover);
                hitRects[prefix + "-down:" + index] = down;
            }
        }

        private void DrawChevron(Graphics graphics, Rectangle bounds, bool pointingUp, bool hover)
        {
            if (hover)
            {
                Rectangle bg = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
                DrawRoundedFill(graphics, bg, UiTheme.SecondaryPressed, UiTheme.BorderSoft, 4);
            }

            Color color = hover ? UiTheme.Primary : UiTheme.TextMuted;
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int size = 5;

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

            if (key == "back-site")
            {
                Raise(BackSiteClicked);
                return;
            }

            if (key == "home-site")
            {
                Raise(HomeSiteClicked);
                return;
            }

            if (key == "reload-site")
            {
                Raise(ReloadSiteClicked);
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
                int index;
                if (int.TryParse(key.Substring("cmd-runstop:".Length), out index) && index >= 0 && index < commands.Count)
                {
                    SelectedCommandId = commands[index].Id;
                    Invalidate();
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(commands[index]));
                    Raise(CommandInlineActionRequested, new SidebarCommandInlineActionEventArgs(commands[index], CommandInlineAction.StartStop));
                }
                return;
            }

            if (key.StartsWith("cmd-restart:", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(key.Substring("cmd-restart:".Length), out index) && index >= 0 && index < commands.Count)
                {
                    SelectedCommandId = commands[index].Id;
                    Invalidate();
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(commands[index]));
                    Raise(CommandInlineActionRequested, new SidebarCommandInlineActionEventArgs(commands[index], CommandInlineAction.Restart));
                }
                return;
            }

            if (key.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(key.Substring(4), out index) && index >= 0 && index < commands.Count)
                {
                    Raise(CommandActivated, new SidebarListItemEventArgs<CommandEntry>(commands[index]));
                }
                return;
            }

            if (key.StartsWith("site:", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(key.Substring(5), out index) && index >= 0 && index < sites.Count)
                {
                    Raise(SiteActivated, new SidebarListItemEventArgs<SiteEntry>(sites[index]));
                }
                return;
            }

            if (key.StartsWith("cmd-up:", StringComparison.OrdinalIgnoreCase))
            {
                RaiseReorder(CommandReorderRequested, key, -1);
                return;
            }

            if (key.StartsWith("cmd-down:", StringComparison.OrdinalIgnoreCase))
            {
                RaiseReorder(CommandReorderRequested, key, 1);
                return;
            }

            if (key.StartsWith("site-up:", StringComparison.OrdinalIgnoreCase))
            {
                RaiseReorder(SiteReorderRequested, key, -1);
                return;
            }

            if (key.StartsWith("site-down:", StringComparison.OrdinalIgnoreCase))
            {
                RaiseReorder(SiteReorderRequested, key, 1);
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

        private static void RaiseReorder(EventHandler<SidebarReorderEventArgs> handler, string key, int delta)
        {
            int colon = key.LastIndexOf(':');
            int index;

            if (colon >= 0 && int.TryParse(key.Substring(colon + 1), out index) && index >= 0)
            {
                Raise(handler, new SidebarReorderEventArgs(index, delta));
            }
        }

        private int GetMaxCommandScroll()
        {
            return Math.Max(0, GetListContentHeight(commands.Count, CommandItemHeight) - commandListRect.Height);
        }

        private int GetMaxSiteScroll()
        {
            return Math.Max(0, GetListContentHeight(sites.Count, SiteItemHeight) - siteListRect.Height);
        }

        private static int GetListContentHeight(int count, int itemHeight)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (ListTopPadding * 2) + (count * itemHeight) + ((count - 1) * ItemSpacing);
        }

        private string GetCommandTitle(CommandEntry command)
        {
            string text = command == null ? string.Empty : command.Name ?? string.Empty;

            if (command != null && command.EnabledOnStart)
            {
                text += "  [\u81ea\u542f]";
            }

            if (command != null && command.AutoRetry != null && command.AutoRetry.Enabled)
            {
                text += "  [\u91cd\u8bd5]";
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

        private static Color Blend(Color baseColor, Color overlay, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                255,
                (int)(baseColor.R + ((overlay.R - baseColor.R) * amount)),
                (int)(baseColor.G + ((overlay.G - baseColor.G) * amount)),
                (int)(baseColor.B + ((overlay.B - baseColor.B) * amount)));
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
