using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal sealed class ThemedButton : Button
    {
        private bool hover;
        private bool pressed;

        public ThemedButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            UseVisualStyleBackColor = false;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            CornerRadius = 6;
            NormalBackColor = UiTheme.SecondaryBack;
            HoverBackColor = UiTheme.SecondaryHover;
            PressedBackColor = UiTheme.SecondaryPressed;
            DisabledBackColor = UiTheme.SecondaryDisabled;
            NormalForeColor = UiTheme.TextSecondary;
            DisabledForeColor = UiTheme.TextDisabled;
            BorderColor = UiTheme.Border;
            DisabledBorderColor = UiTheme.Border;
        }

        public int CornerRadius { get; set; }

        public Color NormalBackColor { get; set; }

        public Color HoverBackColor { get; set; }

        public Color PressedBackColor { get; set; }

        public Color DisabledBackColor { get; set; }

        public Color NormalForeColor { get; set; }

        public Color DisabledForeColor { get; set; }

        public Color BorderColor { get; set; }

        public Color DisabledBorderColor { get; set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }

            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            hover = false;
            pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color parentBack = Parent == null ? UiTheme.WindowBackground : Parent.BackColor;
            Color fill = GetFillColor();
            Color border = Enabled ? BorderColor : DisabledBorderColor;

            pevent.Graphics.Clear(parentBack);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, CornerRadius))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border))
            {
                pevent.Graphics.FillPath(fillBrush, path);
                pevent.Graphics.DrawPath(borderPen, path);
            }

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                ClientRectangle,
                Enabled ? NormalForeColor : DisabledForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        private Color GetFillColor()
        {
            if (!Enabled)
            {
                return DisabledBackColor;
            }

            if (pressed)
            {
                return PressedBackColor;
            }

            return hover ? HoverBackColor : NormalBackColor;
        }
    }

    internal sealed class RoundedLabel : Label
    {
        public RoundedLabel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            CornerRadius = 6;
        }

        public int CornerRadius { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color parentBack = Parent == null ? UiTheme.WindowBackground : Parent.BackColor;
            TextFormatFlags flags = GetTextFlags();

            e.Graphics.Clear(parentBack);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, CornerRadius))
            using (SolidBrush fillBrush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(fillBrush, path);
            }

            Rectangle textBounds = new Rectangle(
                Padding.Left,
                Padding.Top,
                Math.Max(1, Width - Padding.Horizontal),
                Math.Max(1, Height - Padding.Vertical));

            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ForeColor, flags);
        }

        private TextFormatFlags GetTextFlags()
        {
            TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

            if (!AutoEllipsis)
            {
                flags &= ~TextFormatFlags.EndEllipsis;
                flags |= TextFormatFlags.WordBreak;
            }

            if (TextAlign == ContentAlignment.MiddleCenter ||
                TextAlign == ContentAlignment.TopCenter ||
                TextAlign == ContentAlignment.BottomCenter)
            {
                flags |= TextFormatFlags.HorizontalCenter;
            }
            else if (TextAlign == ContentAlignment.MiddleRight ||
                     TextAlign == ContentAlignment.TopRight ||
                     TextAlign == ContentAlignment.BottomRight)
            {
                flags |= TextFormatFlags.Right;
            }
            else
            {
                flags |= TextFormatFlags.Left;
            }

            if (TextAlign == ContentAlignment.MiddleLeft ||
                TextAlign == ContentAlignment.MiddleCenter ||
                TextAlign == ContentAlignment.MiddleRight)
            {
                flags |= TextFormatFlags.VerticalCenter;
            }
            else if (TextAlign == ContentAlignment.BottomLeft ||
                     TextAlign == ContentAlignment.BottomCenter ||
                     TextAlign == ContentAlignment.BottomRight)
            {
                flags |= TextFormatFlags.Bottom;
            }
            else
            {
                flags |= TextFormatFlags.Top;
            }

            return flags;
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public RoundedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            CornerRadius = 8;
            BorderColor = UiTheme.BorderSoft;
        }

        public int CornerRadius { get; set; }

        public Color BorderColor { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));

            e.Graphics.Clear(Parent == null ? UiTheme.WindowBackground : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, CornerRadius))
            using (SolidBrush fillBrush = new SolidBrush(BackColor))
            using (Pen borderPen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            }
        }
    }

    internal sealed class SidebarSplitterPanel : Panel
    {
        private bool hover;
        private bool active;
        private bool collapsed;

        public SidebarSplitterPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = UiTheme.WindowBackground;
            Cursor = Cursors.VSplit;
        }

        public bool Active
        {
            get { return active; }
            set
            {
                if (active == value)
                {
                    return;
                }

                active = value;
                Invalidate();
            }
        }

        public bool Collapsed
        {
            get { return collapsed; }
            set
            {
                if (collapsed == value)
                {
                    return;
                }

                collapsed = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int lineWidth = active ? 3 : hover ? 2 : 1;
            int lineX = collapsed ? 1 : Math.Max(1, (Width - lineWidth) / 2);
            Color lineColor = active || hover ? UiTheme.Primary : UiTheme.BorderSoft;

            e.Graphics.Clear(BackColor);

            using (SolidBrush brush = new SolidBrush(lineColor))
            {
                e.Graphics.FillRectangle(brush, lineX, 0, lineWidth, Height);
            }
        }
    }

    internal enum TitleBarButtonKind
    {
        Sidebar,
        Minimize,
        Maximize,
        Close
    }

    internal sealed class TitleBarIconButton : Control
    {
        private readonly TitleBarButtonKind kind;
        private bool hover;
        private bool pressed;
        private bool sidebarCollapsed;
        private bool maximized;

        public TitleBarIconButton(TitleBarButtonKind kind)
        {
            this.kind = kind;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = UiTheme.WindowBackground;
            Cursor = Cursors.Hand;
        }

        public bool SidebarCollapsed
        {
            get { return sidebarCollapsed; }
            set
            {
                if (sidebarCollapsed == value)
                {
                    return;
                }

                sidebarCollapsed = value;
                Invalidate();
            }
        }

        public bool Maximized
        {
            get { return maximized; }
            set
            {
                if (maximized == value)
                {
                    return;
                }

                maximized = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = GetBackgroundColor();
            Color icon = kind == TitleBarButtonKind.Close && hover
                ? Color.White
                : hover || pressed ? UiTheme.TextPrimary : UiTheme.TextSecondary;

            e.Graphics.Clear(background);
            e.Graphics.SmoothingMode = SmoothingMode.None;

            using (Pen pen = new Pen(icon, 1.5f))
            using (SolidBrush brush = new SolidBrush(icon))
            {
                if (kind == TitleBarButtonKind.Sidebar)
                {
                    DrawSidebarIcon(e.Graphics, pen, brush);
                }
                else if (kind == TitleBarButtonKind.Minimize)
                {
                    DrawMinimizeIcon(e.Graphics, pen);
                }
                else if (kind == TitleBarButtonKind.Maximize)
                {
                    DrawMaximizeIcon(e.Graphics, pen);
                }
                else
                {
                    DrawCloseIcon(e.Graphics, pen);
                }
            }
        }

        private Color GetBackgroundColor()
        {
            if (kind == TitleBarButtonKind.Close && (hover || pressed))
            {
                return pressed ? Color.FromArgb(184, 35, 35) : Color.FromArgb(196, 43, 28);
            }

            if (pressed)
            {
                return UiTheme.SecondaryPressed;
            }

            return hover ? UiTheme.SecondaryHover : UiTheme.WindowBackground;
        }

        private void DrawSidebarIcon(Graphics graphics, Pen pen, SolidBrush brush)
        {
            Rectangle iconRect = new Rectangle((Width - 18) / 2, (Height - 18) / 2, 18, 18);

            graphics.DrawRectangle(pen, iconRect);
            graphics.DrawLine(pen, iconRect.Left + 5, iconRect.Top, iconRect.Left + 5, iconRect.Bottom);

            if (sidebarCollapsed)
            {
                graphics.FillRectangle(brush, iconRect.Left + 8, iconRect.Top + 4, 5, 2);
                graphics.FillRectangle(brush, iconRect.Left + 8, iconRect.Top + 8, 5, 2);
                graphics.FillRectangle(brush, iconRect.Left + 8, iconRect.Top + 12, 5, 2);
            }
            else
            {
                graphics.FillRectangle(brush, iconRect.Left + 2, iconRect.Top + 2, 2, iconRect.Height - 4);
            }
        }

        private void DrawMinimizeIcon(Graphics graphics, Pen pen)
        {
            int y = Height / 2 + 5;
            graphics.DrawLine(pen, Width / 2 - 6, y, Width / 2 + 6, y);
        }

        private void DrawMaximizeIcon(Graphics graphics, Pen pen)
        {
            int x = Width / 2 - 6;
            int y = Height / 2 - 6;

            if (maximized)
            {
                graphics.DrawRectangle(pen, x + 3, y, 10, 9);
                graphics.DrawRectangle(pen, x, y + 4, 10, 9);
                return;
            }

            graphics.DrawRectangle(pen, x, y, 12, 12);
        }

        private void DrawCloseIcon(Graphics graphics, Pen pen)
        {
            int left = Width / 2 - 6;
            int top = Height / 2 - 6;

            graphics.DrawLine(pen, left, top, left + 12, top + 12);
            graphics.DrawLine(pen, left + 12, top, left, top + 12);
        }
    }
}
