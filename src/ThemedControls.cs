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
}
