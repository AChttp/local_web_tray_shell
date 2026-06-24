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

    internal sealed class SidebarSurfaceControl : Control
    {
        private const int OuterLeft = 16;
        private const int OuterTop = 16;
        private const int OuterRight = 16;
        private const int OuterBottom = 14;
        private const int BrandHeight = 164;
        private const int CommandSectionHeight = 332;
        private const int SectionPaddingTop = 14;
        private const int SectionTitleHeight = 30;
        private const int ReorderColumnWidth = 28;
        private const int ActionHeight = 40;
        private const int SiteActionsHeight = 84;
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
            int gap = actionRow.Width < 260 ? 8 : 10;
            int buttonWidth = Math.Max(0, (actionRow.Width - (gap * 2)) / 3);
            int x = actionRow.X;

            TextRenderer.DrawText(graphics, "Switch \u63a7\u5236\u53f0", appTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawButton(graphics, stop, "\u5168\u90e8\u505c\u6b62", false, true, "stop-all");
            TextRenderer.DrawText(graphics, SummaryText ?? string.Empty, summaryFont, summary, UiTheme.TextSecondary, TextFlags(ContentAlignment.TopLeft) | TextFormatFlags.EndEllipsis);

            DrawSegmentButton(graphics, new Rectangle(x, actionRow.Y, buttonWidth, actionRow.Height), "\u7f51\u9875", WorkspaceMode == WorkspaceMode.Web, "mode-web");
            x += buttonWidth + gap;
            DrawSegmentButton(graphics, new Rectangle(x, actionRow.Y, buttonWidth, actionRow.Height), "\u65e5\u5fd7", WorkspaceMode == WorkspaceMode.Logs, "mode-logs");
            x += buttonWidth + gap;
            DrawButton(graphics, new Rectangle(x, actionRow.Y, Math.Max(0, actionRow.Right - x), actionRow.Height), "\u5237\u65b0", false, ReloadSiteEnabled, "reload-site");
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
            Rectangle siteActions = new Rectangle(actions.X, actions.Y, actions.Width, ActionHeight);
            Rectangle navigationActions = new Rectangle(actions.X, siteActions.Bottom + 4, actions.Width, Math.Max(0, actions.Bottom - siteActions.Bottom - 4));

            siteListRect = new Rectangle(section.X, title.Bottom, section.Width, Math.Max(0, actions.Top - title.Bottom));
            TextRenderer.DrawText(graphics, "\u7ad9\u70b9", sectionTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft) | TextFormatFlags.NoPadding);
            DrawSiteList(graphics, siteListRect);
            DrawFourButtons(
                graphics,
                siteActions,
                new ButtonSpec("\u65b0\u589e", true, true, "site-add"),
                new ButtonSpec("\u7f16\u8f91", false, EditSiteEnabled, "site-edit"),
                new ButtonSpec("\u5220\u9664", false, DeleteSiteEnabled, "site-delete"),
                new ButtonSpec("\u6253\u5f00", false, OpenSiteEnabled, "site-open"));
            DrawTwoButtons(
                graphics,
                navigationActions,
                new ButtonSpec("\u8fd4\u56de", false, BackSiteEnabled, "back-site"),
                new ButtonSpec("\u4e3b\u9875", false, HomeSiteEnabled, "home-site"));
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
            bool itemHovered = IsItemHovered("cmd", index);
            Color fill = selected ? Color.FromArgb(221, 239, 247) : itemHovered ? Blend(UiTheme.Surface, accent, 0.06f) : UiTheme.Surface;
            Color border = selected ? Color.FromArgb(90, 166, 194) : itemHovered ? Blend(UiTheme.Border, accent, 0.18f) : UiTheme.Border;
            int contentRight = bounds.Right - ReorderColumnWidth - 4;
            Rectangle badge = new Rectangle(contentRight - 76, bounds.Y + 9, 76, 23);
            Rectangle title = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, badge.X - bounds.X - 32), 23);
            Rectangle meta = new Rectangle(bounds.X + 26, bounds.Y + 38, Math.Max(1, badge.X - bounds.X - 32), 18);

            DrawCard(graphics, bounds, fill, border);
            using (SolidBrush brush = new SolidBrush(accent))
            {
                graphics.FillRectangle(brush, bounds.X + 10, bounds.Y + 10, 6, Math.Max(10, bounds.Height - 20));
            }

            TextRenderer.DrawText(graphics, GetCommandTitle(command), itemTitleFont, title, UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            DrawBadge(graphics, badge, snapshot == null ? "\u5df2\u505c\u6b62" : snapshot.GetDisplayStatus(), GetStatusBadgeBackground(status), accent);
            TextRenderer.DrawText(graphics, GetCommandMeta(command), itemMetaFont, meta, UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["cmd:" + index] = bounds;
            DrawReorderHandles(graphics, bounds, "cmd", index, commands.Count, selected || itemHovered);
        }

        private void DrawSiteItem(Graphics graphics, SiteEntry site, Rectangle bounds, int index)
        {
            Color accent = Color.FromArgb(207, 137, 42);
            bool selected = site != null && string.Equals(site.Id, SelectedSiteId, StringComparison.OrdinalIgnoreCase);
            bool itemHovered = IsItemHovered("site", index);
            Color fill = selected ? Color.FromArgb(255, 239, 214) : itemHovered ? Blend(UiTheme.Surface, accent, 0.06f) : UiTheme.Surface;
            Color border = selected ? Color.FromArgb(224, 166, 75) : itemHovered ? Blend(UiTheme.Border, accent, 0.18f) : UiTheme.Border;

            DrawCard(graphics, bounds, fill, border);
            using (SolidBrush brush = new SolidBrush(accent))
            {
                graphics.FillRectangle(brush, bounds.X + 10, bounds.Y + 10, 6, Math.Max(10, bounds.Height - 20));
            }

            int siteContentRight = bounds.Right - ReorderColumnWidth - 4;
            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Name ?? string.Empty, itemTitleFont, new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, siteContentRight - bounds.X - 32), 20), UiTheme.TextPrimary, TextFlags(ContentAlignment.MiddleLeft));
            TextRenderer.DrawText(graphics, site == null ? string.Empty : site.Url ?? string.Empty, itemMetaFont, new Rectangle(bounds.X + 26, bounds.Y + 34, Math.Max(1, siteContentRight - bounds.X - 32), 18), UiTheme.TextMuted, TextFlags(ContentAlignment.MiddleLeft));
            hitRects["site:" + index] = bounds;
            DrawReorderHandles(graphics, bounds, "site", index, sites.Count, selected || itemHovered);
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

        private bool IsItemHovered(string prefix, int index)
        {
            if (string.IsNullOrEmpty(hoverKey))
            {
                return false;
            }

            return string.Equals(hoverKey, prefix + ":" + index, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hoverKey, prefix + "-up:" + index, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hoverKey, prefix + "-down:" + index, StringComparison.OrdinalIgnoreCase);
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
            Color color = hover ? UiTheme.TextPrimary : UiTheme.TextSecondary;
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int size = 4;

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
