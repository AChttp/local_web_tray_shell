using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal static class UiTheme
    {
        // Canvas & Surfaces (Modern Slate Palette)
        public static readonly Color WindowBackground = Color.FromArgb(244, 247, 251);
        public static readonly Color SidebarBackground = Color.FromArgb(255, 255, 255);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color CardBackground = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(248, 250, 252);
        public static readonly Color SurfaceSoft = Color.FromArgb(241, 245, 249);

        // Borders & Dividers
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderSoft = Color.FromArgb(241, 245, 249);
        public static readonly Color BorderHover = Color.FromArgb(186, 230, 253);
        public static readonly Color FocusRing = Color.FromArgb(2, 132, 199);

        // Typography Colors
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(51, 65, 85);
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        public static readonly Color TextDisabled = Color.FromArgb(148, 163, 184);

        // Primary Brand Accent (Sky / Electric Blue)
        public static readonly Color Primary = Color.FromArgb(2, 132, 199);
        public static readonly Color PrimaryHover = Color.FromArgb(3, 105, 161);
        public static readonly Color PrimaryPressed = Color.FromArgb(7, 89, 133);
        public static readonly Color PrimaryDisabled = Color.FromArgb(186, 230, 253);
        public static readonly Color PrimaryDisabledText = Color.FromArgb(255, 255, 255);

        // Secondary Buttons
        public static readonly Color SecondaryBack = Color.FromArgb(255, 255, 255);
        public static readonly Color SecondaryHover = Color.FromArgb(248, 250, 252);
        public static readonly Color SecondaryPressed = Color.FromArgb(241, 245, 249);
        public static readonly Color SecondaryDisabled = Color.FromArgb(248, 250, 252);
        public static readonly Color SecondaryDisabledText = Color.FromArgb(148, 163, 184);

        // Segmented Control (Apple / Fluent Pill)
        public static readonly Color SegmentInactive = Color.Transparent;
        public static readonly Color SegmentInactiveHover = Color.FromArgb(226, 232, 240);
        public static readonly Color SegmentActive = Color.FromArgb(255, 255, 255);
        public static readonly Color SegmentActiveHover = Color.FromArgb(255, 255, 255);
        public static readonly Color SegmentGroove = Color.FromArgb(241, 245, 249);

        // Badges & Feedback Colors
        public static readonly Color BadgeNeutralBackground = Color.FromArgb(241, 245, 249);
        public static readonly Color BadgeNeutralForeground = Color.FromArgb(71, 85, 105);

        public static readonly Color SuccessBackground = Color.FromArgb(236, 253, 245);
        public static readonly Color SuccessForeground = Color.FromArgb(5, 150, 105);
        public static readonly Color SuccessBorder = Color.FromArgb(167, 243, 208);

        public static readonly Color WarningBackground = Color.FromArgb(255, 251, 235);
        public static readonly Color WarningForeground = Color.FromArgb(217, 119, 6);
        public static readonly Color WarningBorder = Color.FromArgb(253, 230, 138);

        public static readonly Color DangerBackground = Color.FromArgb(254, 242, 242);
        public static readonly Color DangerBackgroundHover = Color.FromArgb(254, 226, 226);
        public static readonly Color DangerForeground = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerBorder = Color.FromArgb(254, 202, 202);
        public static readonly Color DangerBorderHover = Color.FromArgb(248, 113, 113);

        public static readonly Color ProxyBadgeBackground = Color.FromArgb(238, 242, 255);
        public static readonly Color ProxyBadgeForeground = Color.FromArgb(79, 70, 229);
        public static readonly Color ProxyBadgeBorder = Color.FromArgb(199, 210, 254);

        // Console & Layout Panels
        public static readonly Color TerminalBackground = Color.FromArgb(15, 23, 42);
        public static readonly Color TerminalForeground = Color.FromArgb(226, 232, 240);
        public static readonly Color ToolbarBackground = Color.FromArgb(255, 255, 255);
        public static readonly Color ToolbarBorder = Color.FromArgb(226, 232, 240);
        public static readonly Color SplitterColor = Color.FromArgb(226, 232, 240);
        public static readonly Color SplitterHoverColor = Color.FromArgb(2, 132, 199);

        // Typography Helpers with Intelligent System Fallback
        private static string cachedFontFamily;
        private static string cachedMonospaceFamily;

        public static string FontName
        {
            get
            {
                if (cachedFontFamily == null)
                {
                    cachedFontFamily = ResolveFontFamily("Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI");
                }
                return cachedFontFamily;
            }
        }

        public static string MonospaceFontName
        {
            get
            {
                if (cachedMonospaceFamily == null)
                {
                    cachedMonospaceFamily = ResolveFontFamily("Cascadia Code", "Consolas", "Courier New");
                }
                return cachedMonospaceFamily;
            }
        }

        private static string ResolveFontFamily(params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                try
                {
                    using (Font test = new Font(candidate, 9f))
                    {
                        if (string.Equals(test.FontFamily.Name, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            return candidate;
                        }
                    }
                }
                catch
                {
                }
            }
            return "Microsoft YaHei UI";
        }

        public static Font CreateFont(float emSize, FontStyle style)
        {
            return new Font(FontName, emSize, style);
        }

        public static Font CreateFont(float emSize)
        {
            return new Font(FontName, emSize, FontStyle.Regular);
        }

        public static Font CreateMonospaceFont(float emSize, FontStyle style)
        {
            return new Font(MonospaceFontName, emSize, style);
        }

        public static Font CreateMonospaceFont(float emSize)
        {
            return new Font(MonospaceFontName, emSize, FontStyle.Regular);
        }

        public static void ApplyModernMenuTheme(ContextMenuStrip menu)
        {
            if (menu == null)
            {
                return;
            }

            menu.Renderer = new ModernMenuRenderer();
            menu.BackColor = Surface;
            menu.ForeColor = TextPrimary;
            menu.Font = CreateFont(9f, FontStyle.Regular);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(4, 5, 4, 5);
            menu.DropShadowEnabled = true;

            for (int i = 0; i < menu.Items.Count; i++)
            {
                ToolStripItem item = menu.Items[i];
                if (item is ToolStripSeparator)
                {
                    item.Height = 7;
                }
                else
                {
                    item.AutoSize = false;
                    item.Height = 32;
                    item.Padding = new Padding(8, 0, 8, 0);
                }
            }
        }

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
                PrimaryDisabledText, Color.White, Primary, true);
        }

        public static void StyleSecondaryButton(ThemedButton button)
        {
            StyleButton(button, SecondaryBack, SecondaryHover, SecondaryPressed, SecondaryDisabled,
                SecondaryDisabledText, TextSecondary, Border, false);
        }

        public static void StyleDangerButton(ThemedButton button)
        {
            StyleButton(button, DangerBackground, Color.FromArgb(254, 226, 226), Color.FromArgb(252, 165, 165), SecondaryDisabled,
                SecondaryDisabledText, DangerForeground, DangerBorder, true);
        }

        public static void StyleSegmentButton(ThemedButton button)
        {
            StyleButton(button, SegmentInactive, SegmentInactiveHover, SecondaryPressed, SecondaryDisabled,
                SecondaryDisabledText, TextSecondary, Color.Transparent, false);
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
                button.PressedBackColor = SecondaryPressed;
                button.NormalForeColor = TextPrimary;
                button.BorderColor = Border;
            }
            else
            {
                button.NormalBackColor = SegmentInactive;
                button.HoverBackColor = SegmentInactiveHover;
                button.PressedBackColor = SecondaryPressed;
                button.NormalForeColor = TextMuted;
                button.BorderColor = Color.Transparent;
            }

            button.Invalidate();
        }

        public static RoundedLabel CreateBadgeLabel()
        {
            RoundedLabel label = new RoundedLabel();
            label.AutoSize = false;
            label.CornerRadius = 6;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = CreateFont(8.5f, FontStyle.Bold);
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
            button.CornerRadius = 7;
            button.Font = CreateFont(9f, bold ? FontStyle.Bold : FontStyle.Regular);
            button.MinimumSize = new Size(0, 34);
            button.Height = 34;
            button.Padding = new Padding(12, 0, 12, 0);
            button.Invalidate();
        }
    }

    internal sealed class ModernMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(bounds, 6))
            using (Pen borderPen = new Pen(UiTheme.Border, 1f))
            {
                e.Graphics.DrawPath(borderPen, path);
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width, e.ToolStrip.Height);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 6))
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled)
            {
                return;
            }

            Rectangle itemRect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);

            if (e.Item.Selected || e.Item.Pressed)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool isDanger = e.Item.Text != null && (e.Item.Text.Contains("删除") || e.Item.Text.Contains("Delete"));
                Color hoverFill = isDanger ? UiTheme.DangerBackground : UiTheme.SurfaceSoft;

                using (GraphicsPath path = UiTheme.CreateRoundedRectanglePath(itemRect, 5))
                using (SolidBrush brush = new SolidBrush(hoverFill))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            bool isDanger = e.Item.Text != null && (e.Item.Text.Contains("删除") || e.Item.Text.Contains("Delete"));
            Color textColor = !e.Item.Enabled
                ? UiTheme.TextDisabled
                : isDanger
                    ? UiTheme.DangerForeground
                    : (e.Item.Selected ? UiTheme.TextPrimary : UiTheme.TextSecondary);

            Rectangle textRect = new Rectangle(
                e.Item.Padding.Left + 12,
                0,
                e.Item.Width - e.Item.Padding.Horizontal - 20,
                e.Item.Height);

            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen pen = new Pen(UiTheme.Border, 1f))
            {
                e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
            }
        }
    }
}
