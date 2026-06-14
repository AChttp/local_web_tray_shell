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

    internal abstract class SidebarListView<T> : ScrollableControl where T : class
    {
        private const int HorizontalPadding = 8;
        private const int VerticalPadding = 6;
        private const int ItemSpacing = 8;
        private readonly List<T> items;
        private int hoverIndex;
        private string selectedId;

        protected SidebarListView()
        {
            items = new List<T>();
            hoverIndex = -1;
            selectedId = string.Empty;
            AutoScroll = true;
            BackColor = UiTheme.SidebarBackground;
            Cursor = Cursors.Default;
            Margin = new Padding(0);
            Padding = new Padding(0);
            TabStop = false;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);
            UpdateStyles();
        }

        public event EventHandler<SidebarListItemEventArgs<T>> ItemActivated;

        public event EventHandler EmptyClicked;

        public string EmptyText { get; set; }

        public string SelectedId
        {
            get { return selectedId; }
            set
            {
                string nextId = value ?? string.Empty;

                if (string.Equals(selectedId, nextId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                InvalidateItemById(selectedId);
                selectedId = nextId;
                InvalidateItemById(selectedId);
            }
        }

        protected abstract int ItemHeight { get; }

        public void SetItems(IList<T> source)
        {
            items.Clear();

            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    if (source[index] != null)
                    {
                        items.Add(source[index]);
                    }
                }
            }

            if (hoverIndex >= items.Count)
            {
                hoverIndex = -1;
            }

            UpdateScrollRange();
            Invalidate();
        }

        public void RefreshItem(string id)
        {
            InvalidateItemById(id);
        }

        public void RefreshItems()
        {
            Invalidate();
        }

        protected abstract string GetItemId(T item);

        protected abstract void DrawItem(
            Graphics graphics,
            T item,
            Rectangle bounds,
            bool selected,
            bool hover);

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollRange();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int scrollY;
            int firstIndex;
            int lastIndex;
            int index;

            e.Graphics.Clear(BackColor);

            if (items.Count == 0)
            {
                DrawEmptyState(e.Graphics);
                return;
            }

            scrollY = GetScrollY();
            firstIndex = Math.Max(0, (scrollY - VerticalPadding) / GetRowStride());
            lastIndex = Math.Min(
                items.Count - 1,
                ((scrollY + ClientSize.Height - VerticalPadding) / GetRowStride()) + 1);

            for (index = firstIndex; index <= lastIndex; index++)
            {
                Rectangle itemBounds = GetItemBounds(index, scrollY);

                if (itemBounds.Bottom < 0 || itemBounds.Top > ClientSize.Height)
                {
                    continue;
                }

                T item = items[index];
                bool selected = string.Equals(GetItemId(item), selectedId, StringComparison.OrdinalIgnoreCase);
                DrawItem(e.Graphics, item, itemBounds, selected, index == hoverIndex);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);

            if (CanFocus)
            {
                Focus();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int nextHoverIndex = HitTestItem(e.Location);

            if (hoverIndex != nextHoverIndex)
            {
                InvalidateItem(hoverIndex);
                hoverIndex = nextHoverIndex;
                InvalidateItem(hoverIndex);
            }

            Cursor = nextHoverIndex >= 0 || IsPointInEmptyState(e.Location)
                ? Cursors.Hand
                : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hoverIndex >= 0)
            {
                InvalidateItem(hoverIndex);
                hoverIndex = -1;
            }

            Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            EventHandler<SidebarListItemEventArgs<T>> itemHandler;
            EventHandler emptyHandler;
            int index;

            if (e.Button != MouseButtons.Left)
            {
                base.OnMouseDown(e);
                return;
            }

            if (CanFocus)
            {
                Focus();
            }

            index = HitTestItem(e.Location);
            if (index >= 0)
            {
                itemHandler = ItemActivated;

                if (itemHandler != null)
                {
                    itemHandler(this, new SidebarListItemEventArgs<T>(items[index]));
                }

                base.OnMouseDown(e);
                return;
            }

            if (IsPointInEmptyState(e.Location))
            {
                emptyHandler = EmptyClicked;

                if (emptyHandler != null)
                {
                    emptyHandler(this, EventArgs.Empty);
                }
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int scrollY = GetScrollY();
            int notchCount = e.Delta / 120;
            int step = Math.Max(24, SystemInformation.MouseWheelScrollLines * 24);

            if (notchCount == 0)
            {
                notchCount = e.Delta > 0 ? 1 : -1;
            }

            SetScrollY(scrollY - (notchCount * step));
            UpdateHoverFromCursor();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            UpdateHoverFromCursor();
            Invalidate();
        }

        protected void DrawCardShell(Graphics graphics, Rectangle bounds, Color fill, Color border)
        {
            Rectangle cardBounds = new Rectangle(
                bounds.X,
                bounds.Y,
                Math.Max(1, bounds.Width - 1),
                Math.Max(1, bounds.Height - 1));

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(cardBounds, 8))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border))
            {
                graphics.FillPath(fillBrush, path);
                graphics.DrawPath(borderPen, path);
            }
        }

        protected void DrawBadge(Graphics graphics, Rectangle bounds, string text, Color fill, Color textColor)
        {
            Rectangle badgeBounds = new Rectangle(
                bounds.X,
                bounds.Y,
                Math.Max(1, bounds.Width - 1),
                Math.Max(1, bounds.Height - 1));

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(badgeBounds, 6))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            {
                graphics.FillPath(fillBrush, path);
            }

            TextRenderer.DrawText(
                graphics,
                text ?? string.Empty,
                Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        protected Color Blend(Color baseColor, Color overlay, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                255,
                (int)(baseColor.R + ((overlay.R - baseColor.R) * amount)),
                (int)(baseColor.G + ((overlay.G - baseColor.G) * amount)),
                (int)(baseColor.B + ((overlay.B - baseColor.B) * amount)));
        }

        protected Color GetFillColor(bool selected, bool hover, Color normal, Color selectedFill, Color accent)
        {
            if (selected)
            {
                return selectedFill;
            }

            return hover ? Blend(normal, accent, 0.06f) : normal;
        }

        protected Color GetBorderColor(bool selected, bool hover, Color normal, Color selectedBorder, Color accent)
        {
            if (selected)
            {
                return selectedBorder;
            }

            return hover ? Blend(normal, accent, 0.18f) : normal;
        }

        private void DrawEmptyState(Graphics graphics)
        {
            Rectangle bounds = GetEmptyBounds();
            string text = EmptyText ?? string.Empty;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, 6))
            using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(233, 241, 249)))
            {
                graphics.FillPath(fillBrush, path);
            }

            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                new Rectangle(bounds.X + 10, bounds.Y + 10, Math.Max(1, bounds.Width - 20), Math.Max(1, bounds.Height - 20)),
                UiTheme.TextMuted,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        private Rectangle GetEmptyBounds()
        {
            return new Rectangle(
                8,
                8,
                Math.Max(160, GetPaintWidth() - 24),
                72);
        }

        private bool IsPointInEmptyState(Point point)
        {
            return items.Count == 0 && GetEmptyBounds().Contains(point);
        }

        private int HitTestItem(Point point)
        {
            int logicalY;
            int offset;
            int index;

            if (items.Count == 0)
            {
                return -1;
            }

            logicalY = point.Y + GetScrollY();

            if (logicalY < VerticalPadding)
            {
                return -1;
            }

            offset = logicalY - VerticalPadding;
            index = offset / GetRowStride();

            if (index < 0 || index >= items.Count || offset % GetRowStride() >= ItemHeight)
            {
                return -1;
            }

            return index;
        }

        private Rectangle GetItemBounds(int index, int scrollY)
        {
            return new Rectangle(
                HorizontalPadding,
                VerticalPadding + (index * GetRowStride()) - scrollY,
                Math.Max(1, GetPaintWidth() - (HorizontalPadding * 2)),
                ItemHeight);
        }

        private int GetRowStride()
        {
            return ItemHeight + ItemSpacing;
        }

        private int GetContentHeight()
        {
            if (items.Count == 0)
            {
                return 0;
            }

            return (VerticalPadding * 2) + (items.Count * ItemHeight) + ((items.Count - 1) * ItemSpacing);
        }

        private int GetPaintWidth()
        {
            int scrollbarWidth = VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            return Math.Max(1, ClientSize.Width - scrollbarWidth);
        }

        private int GetScrollY()
        {
            return Math.Max(0, -AutoScrollPosition.Y);
        }

        private void SetScrollY(int value)
        {
            int maxScroll = Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height);
            int nextValue = Math.Max(0, Math.Min(maxScroll, value));

            AutoScrollPosition = new Point(0, nextValue);
            Invalidate();
        }

        private void UpdateScrollRange()
        {
            int scrollY = GetScrollY();
            AutoScrollMinSize = new Size(0, GetContentHeight());
            SetScrollY(scrollY);
        }

        private void UpdateHoverFromCursor()
        {
            if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                return;
            }

            OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, PointToClient(Cursor.Position).X, PointToClient(Cursor.Position).Y, 0));
        }

        private void InvalidateItemById(string id)
        {
            int index;

            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            index = FindIndexById(id);

            if (index >= 0)
            {
                InvalidateItem(index);
            }
        }

        private int FindIndexById(string id)
        {
            for (int index = 0; index < items.Count; index++)
            {
                if (string.Equals(GetItemId(items[index]), id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private void InvalidateItem(int index)
        {
            if (index < 0 || index >= items.Count)
            {
                return;
            }

            Rectangle bounds = GetItemBounds(index, GetScrollY());
            bounds.Inflate(2, 2);
            Invalidate(bounds);
        }
    }

    internal sealed class CommandSidebarListView : SidebarListView<CommandEntry>
    {
        private readonly Font titleFont;
        private readonly Font metaFont;

        public CommandSidebarListView()
        {
            titleFont = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Bold);
            metaFont = new Font("Microsoft YaHei UI", 8.4f, FontStyle.Regular);
            Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular);
        }

        public Func<string, CommandRuntimeSnapshot> SnapshotProvider { get; set; }

        protected override int ItemHeight
        {
            get { return 62; }
        }

        protected override string GetItemId(CommandEntry item)
        {
            return item == null ? string.Empty : item.Id ?? string.Empty;
        }

        protected override void DrawItem(Graphics graphics, CommandEntry item, Rectangle bounds, bool selected, bool hover)
        {
            CommandRuntimeSnapshot snapshot = GetSnapshot(item);
            CommandStatus status = snapshot == null ? CommandStatus.Stopped : snapshot.Status;
            Color accent = GetStatusAccent(status);
            Color fill = GetFillColor(selected, hover, UiTheme.Surface, Color.FromArgb(221, 239, 247), accent);
            Color border = GetBorderColor(selected, hover, UiTheme.Border, Color.FromArgb(90, 166, 194), accent);
            Rectangle accentBounds = new Rectangle(bounds.X + 10, bounds.Y + 10, 6, Math.Max(10, bounds.Height - 20));
            Rectangle titleBounds = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, bounds.Width - 26 - 14 - 82), 23);
            Rectangle badgeBounds = new Rectangle(bounds.Right - 14 - 76, bounds.Y + 9, 76, 23);
            Rectangle metaBounds = new Rectangle(bounds.X + 26, bounds.Y + 38, Math.Max(1, bounds.Width - 26 - 14), 18);

            DrawCardShell(graphics, bounds, fill, border);

            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                graphics.FillRectangle(accentBrush, accentBounds);
            }

            TextRenderer.DrawText(
                graphics,
                GetTitleText(item),
                titleFont,
                titleBounds,
                UiTheme.TextPrimary,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            DrawBadge(
                graphics,
                badgeBounds,
                snapshot == null ? "\u5df2\u505c\u6b62" : snapshot.GetDisplayStatus(),
                GetStatusBadgeBackground(status),
                accent);

            TextRenderer.DrawText(
                graphics,
                GetMetaText(item),
                metaFont,
                metaBounds,
                UiTheme.TextMuted,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                titleFont.Dispose();
                metaFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private CommandRuntimeSnapshot GetSnapshot(CommandEntry item)
        {
            Func<string, CommandRuntimeSnapshot> provider = SnapshotProvider;

            if (item == null || string.IsNullOrWhiteSpace(item.Id) || provider == null)
            {
                return null;
            }

            return provider(item.Id);
        }

        private string GetTitleText(CommandEntry command)
        {
            string titleText = command == null ? string.Empty : command.Name ?? string.Empty;

            if (command != null && command.EnabledOnStart)
            {
                titleText += "  [\u81ea\u542f]";
            }

            if (command != null && command.AutoRetry != null && command.AutoRetry.Enabled)
            {
                titleText += "  [\u91cd\u8bd5]";
            }

            return titleText;
        }

        private string GetMetaText(CommandEntry command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            return RunModeCatalog.GetDisplayName(command.RunMode) + "  |  " + SummarizeCommand(command.Command);
        }

        private string SummarizeCommand(string value)
        {
            value = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

            if (value.Length <= 240)
            {
                return value;
            }

            return value.Substring(0, 237) + "...";
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

            if (status == CommandStatus.Starting ||
                status == CommandStatus.Stopping ||
                status == CommandStatus.WaitingRetry)
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

            if (status == CommandStatus.Starting ||
                status == CommandStatus.Stopping ||
                status == CommandStatus.WaitingRetry)
            {
                return UiTheme.WarningBackground;
            }

            return UiTheme.BadgeNeutralBackground;
        }
    }

    internal sealed class SiteSidebarListView : SidebarListView<SiteEntry>
    {
        private readonly Font titleFont;
        private readonly Font urlFont;

        public SiteSidebarListView()
        {
            titleFont = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Bold);
            urlFont = new Font("Microsoft YaHei UI", 8.4f, FontStyle.Regular);
            Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular);
        }

        protected override int ItemHeight
        {
            get { return 58; }
        }

        protected override string GetItemId(SiteEntry item)
        {
            return item == null ? string.Empty : item.Id ?? string.Empty;
        }

        protected override void DrawItem(Graphics graphics, SiteEntry item, Rectangle bounds, bool selected, bool hover)
        {
            Color accent = Color.FromArgb(207, 137, 42);
            Color fill = GetFillColor(selected, hover, UiTheme.Surface, Color.FromArgb(255, 239, 214), accent);
            Color border = GetBorderColor(selected, hover, UiTheme.Border, Color.FromArgb(224, 166, 75), accent);
            Rectangle accentBounds = new Rectangle(bounds.X + 10, bounds.Y + 10, 6, Math.Max(10, bounds.Height - 20));
            Rectangle titleBounds = new Rectangle(bounds.X + 26, bounds.Y + 9, Math.Max(1, bounds.Width - 26 - 14), 20);
            Rectangle urlBounds = new Rectangle(bounds.X + 26, bounds.Y + 34, Math.Max(1, bounds.Width - 26 - 14), 18);

            DrawCardShell(graphics, bounds, fill, border);

            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                graphics.FillRectangle(accentBrush, accentBounds);
            }

            TextRenderer.DrawText(
                graphics,
                item == null ? string.Empty : item.Name ?? string.Empty,
                titleFont,
                titleBounds,
                UiTheme.TextPrimary,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                graphics,
                item == null ? string.Empty : item.Url ?? string.Empty,
                urlFont,
                urlBounds,
                UiTheme.TextMuted,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                titleFont.Dispose();
                urlFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
