using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

    internal sealed class SidebarSurfaceControl : Control
    {
        private const int OuterLeft = 16;
        private const int OuterTop = 16;
        private const int OuterRight = 16;
        private const int OuterBottom = 14;
        private const int BrandHeight = 164;
        private const int CommandSectionHeight = 332;
        private const int SectionPaddingTop = 14;
        private const int SectionTitleHeight = 24;
        private const int ActionHeight = 40;
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
        private string hoverKey;
        private int commandScrollY;
        private int siteScrollY;

        public SidebarSurfaceControl()
        {
            commands = new List<CommandEntry>();
            sites = new List<SiteEntry>();
            hitRects = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            appTitleFont = new Font("Microsoft YaHei UI", 13.5f, FontStyle.Bold);
            sectionTitleFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            buttonFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            itemTitleFont = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Bold);
            itemMetaFont = new Font("Microsoft YaHei UI", 8.4f, FontStyle.Regular);
            summaryFont = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular);
            badgeFont = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Bold);

            BackColor = UiTheme.SidebarBackground;
            Cursor = Cursors.Default;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
        }

        public event EventHandler StopAllCommandsClicked;
        public event EventHandler ReloadSiteClicked;
        public event EventHandler<SidebarWorkspaceModeEventArgs> WorkspaceModeRequested;
        public event EventHandler<SidebarListItemEventArgs<CommandEntry>> CommandActivated;
        public event EventHandler<SidebarListItemEventArgs<SiteEntry>> SiteActivated;
        public event EventHandler<SidebarCommandActionEventArgs> CommandActionRequested;
        public event EventHandler<SidebarSiteActionEventArgs> SiteActionRequested;

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

        public void RefreshCommand(string commandId)
        {
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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
            Rectangle commandSection = new Rectangle(content.X, brand.Bottom, content.Width, CommandSectionHeight);
            Rectangle siteSection = new Rectangle(
                content.X,
                commandSection.Bottom,
                content.Width,
                Math.Max(0, content.Bottom - commandSection.Bottom));

            e.Graphics.Clear(BackColor);
            hitRects.Clear();
            DrawBrand(e.Graphics, brand);
            DrawCommandSection(e.Graphics, commandSection);
            DrawSiteSection(e.Graphics, siteSection);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            string nextHover = GetHitKey(e.Location);

            if (!string.Equals(hoverKey, nextHover, StringComparison.OrdinalIgnoreCase))
            {
                hoverKey = nextHover;
                Invalidate();
            }

            Cursor = string.IsNullOrEmpty(nextHover) ? Cursors.Default : Cursors.Hand;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (!string.IsNullOrEmpty(hoverKey))
            {
                hoverKey = string.Empty;
                Invalidate();
            }

            Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            string key;

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
            DispatchHit(key);
            base.OnMouseDown(e);
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
            Rectangle actionRow = new Rectangle(inner.X, inner.Y + 90, inner.Width, 34);
            int firstWidth = Math.Max(0, (actionRow.Width * 30) / 100);
            int secondWidth = Math.Max(0, (actionRow.Width * 30) / 100);
            int thirdWidth = Math.Max(0, actionRow.Width - firstWidth - secondWidth);

            TextRenderer.DrawText(graphics, "Switch \u63a7\u5236\u53f0", appTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawButton(graphics, stop, "\u5168\u90e8\u505c\u6b62", false, true, "stop-all");
            TextRenderer.DrawText(graphics, SummaryText ?? string.Empty, summaryFont, summary, UiTheme.TextSecondary, TextFlags(ContentAlignment.TopLeft) | TextFormatFlags.EndEllipsis);

            DrawSegmentButton(graphics, new Rectangle(actionRow.X, actionRow.Y, Math.Max(0, firstWidth - 8), actionRow.Height), "\u7f51\u9875", WorkspaceMode == WorkspaceMode.Web, "mode-web");
            DrawSegmentButton(graphics, new Rectangle(actionRow.X + firstWidth, actionRow.Y, Math.Max(0, secondWidth - 8), actionRow.Height), "\u65e5\u5fd7", WorkspaceMode == WorkspaceMode.Logs, "mode-logs");
            DrawButton(graphics, new Rectangle(actionRow.X + firstWidth + secondWidth, actionRow.Y, thirdWidth, actionRow.Height), "\u5237\u65b0\u9875\u9762", false, ReloadSiteEnabled, "reload-site");
        }

        private void DrawCommandSection(Graphics graphics, Rectangle section)
        {
            Rectangle title = new Rectangle(section.X, section.Y + SectionPaddingTop, section.Width, SectionTitleHeight);
            Rectangle actions = new Rectangle(section.X, Math.Max(title.Bottom, section.Bottom - ActionHeight), section.Width, ActionHeight);

            commandListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            TextRenderer.DrawText(graphics, "\u547d\u4ee4", sectionTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
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
            Rectangle actions = new Rectangle(section.X, Math.Max(title.Bottom, section.Bottom - ActionHeight), section.Width, ActionHeight);

            siteListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            TextRenderer.DrawText(graphics, "\u7ad9\u70b9", sectionTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
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
            bool hover = string.Equals(hoverKey, "cmd:" + index, StringComparison.OrdinalIgnoreCase);
            Color fill = selected ? Color.FromArgb(221, 239, 247) : hover ? Blend(UiTheme.Surface, accent, 0.06f) : UiTheme.Surface;
            Color border = selected ? Color.FromArgb(90, 166, 194) : hover ? Blend(UiTheme.Border, accent, 0.18f) : UiTheme.Border;
            Rectangle badge = new Rectangle(bounds.Right - 90, bounds.Y + 9, 76, 23);
            Rectangle title = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, badge.X - bounds.X - 32), 23);
            Rectangle meta = new Rectangle(bounds.X + 26, bounds.Y + 38, Math.Max(1, bounds.Width - 40), 18);

            DrawCard(graphics, bounds, fill, border);
            using (SolidBrush brush = new SolidBrush(accent))
            {
                graphics.FillRectangle(brush, bounds.X + 10, bounds.Y + 10, 6, Math.Max(10, bounds.Height - 20));
            }

            TextRenderer.DrawText(graphics, GetCommandTitle(command), itemTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawBadge(graphics, badge, snapshot == null ? "\u5df2\u505c\u6b62" : snapshot.GetDisplayStatus(), GetStatusBadgeBackground(status), accent);
            TextRenderer.DrawText(graphics, GetCommandMeta(command), itemMetaFont, meta, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["cmd:" + index] = bounds;
        }

        private void DrawSiteItem(Graphics graphics, SiteEntry site, Rectangle bounds, int index)
        {
            Color accent = Color.FromArgb(207, 137, 42);
            bool selected = site != null && string.Equals(site.Id, SelectedSiteId, StringComparison.OrdinalIgnoreCase);
            bool hover = string.Equals(hoverKey, "site:" + index, StringComparison.OrdinalIgnoreCase);
            Color fill = selected ? Color.FromArgb(255, 239, 214) : hover ? Blend(UiTheme.Surface, accent, 0.06f) : UiTheme.Surface;
            Color border = selected ? Color.FromArgb(224, 166, 75) : hover ? Blend(UiTheme.Border, accent, 0.18f) : UiTheme.Border;

            DrawCard(graphics, bounds, fill, border);
            using (SolidBrush brush = new SolidBrush(accent))
            {
                graphics.FillRectangle(brush, bounds.X + 10, bounds.Y + 10, 6, Math.Max(10, bounds.Height - 20));
            }

            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Name ?? string.Empty, itemTitleFont, new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, bounds.Width - 40), 20), UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Url ?? string.Empty, itemMetaFont, new Rectangle(bounds.X + 26, bounds.Y + 34, Math.Max(1, bounds.Width - 40), 18), UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["site:" + index] = bounds;
        }

        private void DrawFourButtons(Graphics graphics, Rectangle bounds, ButtonSpec first, ButtonSpec second, ButtonSpec third, ButtonSpec fourth)
        {
            int gap = bounds.Width < 260 ? 8 : 12;
            int buttonHeight = Math.Min(34, bounds.Height);
            int y = bounds.Y + Math.Max(0, (bounds.Height - buttonHeight) / 2);
            int available = Math.Max(0, bounds.Width - (gap * 3));
            int buttonWidth = available / 4;
            int x = bounds.X;

            DrawButton(graphics, new Rectangle(x, y, buttonWidth, buttonHeight), first.Text, first.Primary, first.Enabled, first.Key);
            x += buttonWidth + gap;
            DrawButton(graphics, new Rectangle(x, y, buttonWidth, buttonHeight), second.Text, second.Primary, second.Enabled, second.Key);
            x += buttonWidth + gap;
            DrawButton(graphics, new Rectangle(x, y, buttonWidth, buttonHeight), third.Text, third.Primary, third.Enabled, third.Key);
            x += buttonWidth + gap;
            DrawButton(graphics, new Rectangle(x, y, Math.Max(0, bounds.Right - x), buttonHeight), fourth.Text, fourth.Primary, fourth.Enabled, fourth.Key);
        }

        private void DrawSegmentButton(Graphics graphics, Rectangle bounds, string text, bool active, string key)
        {
            bool hover = string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);
            Color fill = active ? (hover ? UiTheme.SegmentActiveHover : UiTheme.SegmentActive) : (hover ? UiTheme.SegmentInactiveHover : UiTheme.SegmentInactive);
            Color fore = active ? Color.White : UiTheme.TextSecondary;
            DrawRoundedFill(graphics, bounds, fill, active ? UiTheme.SegmentActive : Color.FromArgb(202, 216, 231), 6);
            TextRenderer.DrawText(graphics, text, buttonFont, bounds, fore, TextFlags(ContentAlignment.MiddleCenter));
            hitRects[key] = bounds;
        }

        private void DrawButton(Graphics graphics, Rectangle bounds, string text, bool primary, bool enabled, string key)
        {
            bool hover = enabled && string.Equals(hoverKey, key, StringComparison.OrdinalIgnoreCase);
            Color fill = !enabled ? UiTheme.SecondaryDisabled : primary ? (hover ? UiTheme.PrimaryHover : UiTheme.Primary) : (hover ? UiTheme.SecondaryHover : UiTheme.SecondaryBack);
            Color border = !enabled ? UiTheme.BorderSoft : primary ? Color.FromArgb(0, 98, 132) : UiTheme.Border;
            Color fore = !enabled ? UiTheme.SecondaryDisabledText : primary ? Color.White : UiTheme.TextSecondary;

            DrawRoundedFill(graphics, bounds, fill, border, 6);
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

        private void DrawClipped(Graphics graphics, Rectangle bounds, Action draw)
        {
            Region oldClip = graphics.Clip;

            try
            {
                graphics.SetClip(bounds);
                draw();
            }
            finally
            {
                graphics.Clip = oldClip;
            }
        }

        private string GetHitKey(Point point)
        {
            foreach (KeyValuePair<string, Rectangle> pair in hitRects)
            {
                if (pair.Value.Contains(point))
                {
                    return pair.Key;
                }
            }

            return string.Empty;
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

            if (key == "mode-logs")
            {
                Raise(WorkspaceModeRequested, new SidebarWorkspaceModeEventArgs(WorkspaceMode.Logs));
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
