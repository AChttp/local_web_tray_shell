using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal static class UiTheme
    {
        public static readonly Color WindowBackground = Color.FromArgb(232, 238, 246);
        public static readonly Color SidebarBackground = Color.FromArgb(242, 246, 251);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(242, 246, 251);
        public static readonly Color SurfaceSoft = Color.FromArgb(235, 241, 248);
        public static readonly Color Border = Color.FromArgb(199, 210, 224);
        public static readonly Color BorderSoft = Color.FromArgb(218, 227, 238);
        public static readonly Color TextPrimary = Color.FromArgb(18, 31, 47);
        public static readonly Color TextSecondary = Color.FromArgb(52, 69, 89);
        public static readonly Color TextMuted = Color.FromArgb(91, 108, 130);
        public static readonly Color TextDisabled = Color.FromArgb(145, 159, 177);
        public static readonly Color Primary = Color.FromArgb(0, 126, 167);
        public static readonly Color PrimaryHover = Color.FromArgb(0, 105, 143);
        public static readonly Color PrimaryPressed = Color.FromArgb(0, 82, 115);
        public static readonly Color PrimaryDisabled = Color.FromArgb(169, 203, 216);
        public static readonly Color PrimaryDisabledText = Color.FromArgb(234, 244, 248);
        public static readonly Color SecondaryBack = Color.FromArgb(250, 252, 255);
        public static readonly Color SecondaryHover = Color.FromArgb(235, 243, 250);
        public static readonly Color SecondaryPressed = Color.FromArgb(221, 232, 243);
        public static readonly Color SecondaryDisabled = Color.FromArgb(239, 244, 249);
        public static readonly Color SecondaryDisabledText = Color.FromArgb(145, 159, 177);
        public static readonly Color SegmentInactive = Color.FromArgb(225, 235, 244);
        public static readonly Color SegmentInactiveHover = Color.FromArgb(212, 226, 239);
        public static readonly Color SegmentActive = Color.FromArgb(0, 126, 167);
        public static readonly Color SegmentActiveHover = Color.FromArgb(0, 105, 143);
        public static readonly Color BadgeNeutralBackground = Color.FromArgb(226, 234, 244);
        public static readonly Color BadgeNeutralForeground = Color.FromArgb(70, 85, 105);
        public static readonly Color SuccessBackground = Color.FromArgb(218, 242, 229);
        public static readonly Color SuccessForeground = Color.FromArgb(29, 120, 73);
        public static readonly Color WarningBackground = Color.FromArgb(255, 240, 208);
        public static readonly Color WarningForeground = Color.FromArgb(159, 95, 10);
        public static readonly Color DangerBackground = Color.FromArgb(250, 226, 231);
        public static readonly Color DangerForeground = Color.FromArgb(169, 50, 67);

        public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        public static void StylePrimaryButton(ThemedButton button)
        {
            StyleButton(button, Primary, PrimaryHover, PrimaryPressed, PrimaryDisabled,
                PrimaryDisabledText, Color.White, Color.FromArgb(0, 98, 132), true);
        }

        public static void StyleSecondaryButton(ThemedButton button)
        {
            StyleButton(button, SecondaryBack, SecondaryHover, SecondaryPressed, SecondaryDisabled,
                SecondaryDisabledText, TextSecondary, Border, false);
        }

        public static void StyleSegmentButton(ThemedButton button)
        {
            StyleButton(button, SegmentInactive, SegmentInactiveHover, SecondaryPressed, SecondaryDisabled,
                SecondaryDisabledText, TextSecondary, Color.FromArgb(202, 216, 231), true);
        }

        public static void SetSegmentButtonState(ThemedButton button, bool active)
        {
            if (button == null)
            {
                return;
            }

            if (active)
            {
                button.NormalBackColor = SegmentActive;
                button.HoverBackColor = SegmentActiveHover;
                button.PressedBackColor = PrimaryPressed;
                button.NormalForeColor = Color.White;
                button.BorderColor = SegmentActive;
            }
            else
            {
                button.NormalBackColor = SegmentInactive;
                button.HoverBackColor = SegmentInactiveHover;
                button.PressedBackColor = SecondaryPressed;
                button.NormalForeColor = TextSecondary;
                button.BorderColor = Color.FromArgb(202, 216, 231);
            }

            button.Invalidate();
        }

        public static RoundedLabel CreateBadgeLabel()
        {
            RoundedLabel label = new RoundedLabel();
            label.AutoSize = false;
            label.CornerRadius = 6;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Bold);
            return label;
        }

        private static void StyleButton(
            ThemedButton button,
            Color normalBack,
            Color hoverBack,
            Color pressedBack,
            Color disabledBack,
            Color disabledFore,
            Color normalFore,
            Color borderColor,
            bool bold)
        {
            if (button == null)
            {
                return;
            }

            button.NormalBackColor = normalBack;
            button.HoverBackColor = hoverBack;
            button.PressedBackColor = pressedBack;
            button.DisabledBackColor = disabledBack;
            button.DisabledForeColor = disabledFore;
            button.NormalForeColor = normalFore;
            button.BorderColor = borderColor;
            button.DisabledBorderColor = BorderSoft;
            button.CornerRadius = 6;
            button.Font = new Font("Microsoft YaHei UI", 9f, bold ? FontStyle.Bold : FontStyle.Regular);
            button.MinimumSize = new Size(0, 34);
            button.Height = 34;
            button.Padding = new Padding(10, 0, 10, 0);
            button.Invalidate();
        }
    }
}
