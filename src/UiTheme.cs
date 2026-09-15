using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal static class UiTheme
    {
        public static readonly Color WindowBackground = Color.FromArgb(245, 247, 250);
        public static readonly Color SidebarBackground = Color.FromArgb(245, 247, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color CardBackground = Color.White;

        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderSoft = Color.FromArgb(241, 245, 249);
        public static readonly Color BorderHover = Color.FromArgb(203, 213, 225);

        public static readonly Color Primary = Color.FromArgb(2, 132, 199);
        public static readonly Color PrimaryHover = Color.FromArgb(2, 112, 169);
        public static readonly Color PrimarySoftBack = Color.FromArgb(240, 249, 255);
        public static readonly Color PrimarySoftBorder = Color.FromArgb(186, 230, 253);
        public static readonly Color FocusRing = Color.FromArgb(125, 211, 252);

        public static readonly Color SecondaryBack = Color.White;
        public static readonly Color SecondaryHover = Color.FromArgb(248, 250, 252);
        public static readonly Color SecondaryPressed = Color.FromArgb(241, 245, 249);
        public static readonly Color SecondaryDisabled = Color.FromArgb(226, 232, 240);
        public static readonly Color SecondaryDisabledText = Color.FromArgb(148, 163, 184);

        public static readonly Color DangerBackground = Color.FromArgb(254, 242, 242);
        public static readonly Color DangerForeground = Color.FromArgb(185, 28, 28);
        public static readonly Color DangerBorder = Color.FromArgb(254, 202, 202);
        public static readonly Color DangerBackgroundHover = Color.FromArgb(254, 226, 226);
        public static readonly Color DangerBorderHover = Color.FromArgb(252, 165, 165);

        public static readonly Color SuccessBackground = Color.FromArgb(220, 252, 231);
        public static readonly Color SuccessForeground = Color.FromArgb(21, 128, 61);
        public static readonly Color WarningBackground = Color.FromArgb(254, 243, 199);
        public static readonly Color WarningForeground = Color.FromArgb(180, 83, 9);
        public static readonly Color BadgeNeutralBackground = Color.FromArgb(241, 245, 249);
        public static readonly Color BadgeNeutralForeground = Color.FromArgb(71, 85, 105);

        // List item states (owner-drawn sidebar)
        public static readonly Color ItemSelectedBack = Color.FromArgb(240, 249, 255);
        public static readonly Color ItemHoverBack = Color.FromArgb(248, 250, 252);
        public static readonly Color ItemHoverBorder = Color.FromArgb(186, 230, 253);

        // Mini inline icon buttons (owner-drawn sidebar)
        public static readonly Color MiniStopBack = Color.FromArgb(254, 242, 242);
        public static readonly Color MiniStopBackHover = Color.FromArgb(254, 226, 226);
        public static readonly Color MiniStopBorder = Color.FromArgb(252, 165, 165);
        public static readonly Color MiniStopIconHover = Color.FromArgb(153, 27, 27);
        public static readonly Color MiniPlayBackHover = Color.FromArgb(224, 242, 254);
        public static readonly Color MiniPlayIconHover = Color.FromArgb(2, 132, 199);
        public static readonly Color MiniNeutralBackHover = Color.FromArgb(241, 245, 249);

        // Site entry accents / tags (owner-drawn sidebar)
        public static readonly Color SiteUpAccent = Color.FromArgb(46, 160, 97);
        public static readonly Color SiteDownAccent = Color.FromArgb(220, 68, 68);
        public static readonly Color SiteUnknownAccent = Color.FromArgb(160, 174, 192);
        public static readonly Color ProxyTagBack = Color.FromArgb(238, 242, 255);
        public static readonly Color ProxyTagBorder = Color.FromArgb(199, 210, 254);
        public static readonly Color ProxyTagText = Color.FromArgb(67, 56, 202);

        // Empty-state / misc surfaces (owner-drawn sidebar)
        public static readonly Color EmptyStateBack = Color.FromArgb(233, 241, 249);
        public static readonly Color SegmentTrackBack = Color.FromArgb(238, 242, 246);
        public static readonly Color SegmentTrackBorder = Color.FromArgb(226, 232, 240);
        public static readonly Color SegmentActiveBorder = Color.FromArgb(218, 225, 233);
        public static readonly Color ScrollbarThumb = Color.FromArgb(196, 205, 217);
        public static readonly Color ScrollbarThumbHover = Color.FromArgb(148, 163, 184);

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(51, 65, 85);
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        public static readonly Color TextDisabled = Color.FromArgb(148, 163, 184);

        public static readonly Color TerminalBackground = Color.FromArgb(15, 23, 42);
        public static readonly Color TerminalForeground = Color.FromArgb(226, 232, 240);
        public static readonly Color TerminalErrorForeground = Color.FromArgb(252, 129, 129);

        private static float dpiScale = 1.0f;

        // Event raised when the process DPI changes (window moved between monitors).
        // Subscribers must recreate any cached Font objects.
        public static event EventHandler DpiScaleChanged;

        public static float DpiScale
        {
            get { return dpiScale; }
        }

        public static void SetDpiScale(float scale)
        {
            if (scale <= 0.1f)
            {
                scale = 1.0f;
            }

            if (Math.Abs(scale - dpiScale) < 0.001f)
            {
                return;
            }

            dpiScale = scale;

            if (DpiScaleChanged != null)
            {
                DpiScaleChanged(null, EventArgs.Empty);
            }
        }

        public static int Scale(int value)
        {
            return (int)Math.Round(value * dpiScale);
        }

        public static float Scale(float value)
        {
            return value * dpiScale;
        }

        public static Padding ScalePadding(int left, int top, int right, int bottom)
        {
            return new Padding(Scale(left), Scale(top), Scale(right), Scale(bottom));
        }

        public static Font CreateFont(float size, FontStyle style)
        {
            return CreateFont(size, style, IsCjkCulture());
        }

        public static Font CreateMonospaceFont(float size, FontStyle style)
        {
            return CreateFontWithCandidates(
                new[]
                {
                    "Cascadia Mono",
                    "JetBrains Mono",
                    "Consolas",
                    "Courier New"
                },
                size,
                style,
                false);
        }

        public static Font CreateFont(float size, FontStyle style, bool allowCjk)
        {
            return CreateFontWithCandidates(
                new[]
                {
                    "Microsoft YaHei UI",
                    "PingFang SC",
                    "Segoe UI",
                    "Noto Sans CJK SC",
                    "SimSun"
                },
                size,
                style,
                allowCjk);
        }

        // Sizes are expressed in "points at 96 DPI" (the historical design unit).
        // Fonts are created in pixel units so GDI/GDI+ never re-scale them; the
        // layout code scales its rectangles with the same factor, keeping text and
        // geometry in proportion at any monitor DPI.
        private static Font CreateFontWithCandidates(string[] candidates, float size, FontStyle style, bool allowCjk)
        {
            float pixels = size * 96f / 72f * dpiScale;

            for (int index = 0; index < candidates.Length; index++)
            {
                string candidate = candidates[index];

                if (!allowCjk && IsCjkFamilyName(candidate))
                {
                    continue;
                }

                try
                {
                    FontFamily family = new FontFamily(candidate);
                    if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return new Font(family, pixels, style, GraphicsUnit.Pixel);
                    }
                }
                catch
                {
                }
            }

            return new Font(FontFamily.GenericSansSerif, pixels, style, GraphicsUnit.Pixel);
        }

        private static bool IsCjkFamilyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("yahei") ||
                lower.Contains("pingfang") ||
                lower.Contains("noto sans cjk") ||
                lower.Contains("simsun") ||
                lower.Contains("simhei") ||
                lower.Contains("source han") ||
                lower.Contains("wenquanyi");
        }

        private static bool IsCjkCulture()
        {
            string name = System.Globalization.CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
            return name.StartsWith("zh") ||
                name.StartsWith("ja") ||
                name.StartsWith("ko") ||
                name.Contains("chinese") ||
                name.Contains("japanese") ||
                name.Contains("korean");
        }

        public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
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
            button.NormalBackColor = Primary;
            button.HoverBackColor = PrimaryHover;
            button.PressedBackColor = PrimaryHover;
            button.DisabledBackColor = SecondaryDisabled;
            button.NormalForeColor = Color.White;
            button.HoverForeColor = Color.White;
            button.PressedForeColor = Color.White;
            button.DisabledForeColor = SecondaryDisabledText;
            button.BorderColor = Primary;
            button.BorderWidth = 1f;
        }

        public static void StyleSecondaryButton(ThemedButton button)
        {
            button.NormalBackColor = SecondaryBack;
            button.HoverBackColor = SecondaryHover;
            button.PressedBackColor = SecondaryPressed;
            button.DisabledBackColor = SecondaryDisabled;
            button.NormalForeColor = TextSecondary;
            button.HoverForeColor = TextPrimary;
            button.PressedForeColor = TextPrimary;
            button.DisabledForeColor = SecondaryDisabledText;
            button.BorderColor = Border;
            button.HoverBorderColor = FocusRing;
            button.BorderWidth = 1f;
        }

        public static void StyleDangerButton(ThemedButton button)
        {
            button.NormalBackColor = DangerBackground;
            button.HoverBackColor = DangerBackgroundHover;
            button.PressedBackColor = DangerBackgroundHover;
            button.DisabledBackColor = SecondaryDisabled;
            button.NormalForeColor = DangerForeground;
            button.HoverForeColor = DangerForeground;
            button.PressedForeColor = DangerForeground;
            button.DisabledForeColor = SecondaryDisabledText;
            button.BorderColor = DangerBorder;
            button.HoverBorderColor = DangerBorderHover;
            button.BorderWidth = 1f;
        }

        public static RoundedLabel CreateBadgeLabel()
        {
            RoundedLabel badge = new RoundedLabel();
            badge.AutoSize = false;
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.BackColor = BadgeNeutralBackground;
            badge.ForeColor = BadgeNeutralForeground;
            badge.Font = CreateFont(8.5f, FontStyle.Bold);
            return badge;
        }

        public static void ApplyModernMenuTheme(ToolStrip strip)
        {
            if (strip == null)
            {
                return;
            }

            strip.BackColor = Surface;
            strip.ForeColor = TextPrimary;
            strip.Font = CreateFont(9f, FontStyle.Regular);
            strip.Renderer = new ModernToolStripRenderer();
        }

        private sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
        {
            public ModernToolStripRenderer()
                : base(new ModernColorTable())
            {
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? TextPrimary : TextMuted;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (Pen pen = new Pen(BorderSoft))
                {
                    int y = e.Item.Height / 2;
                    e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 6, y);
                }
            }
        }

        private sealed class ModernColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected
            {
                get { return PrimarySoftBack; }
            }

            public override Color MenuItemBorder
            {
                get { return PrimarySoftBorder; }
            }

            public override Color MenuBorder
            {
                get { return Border; }
            }

            public override Color ToolStripDropDownBackground
            {
                get { return Surface; }
            }

            public override Color ImageMarginGradientBegin
            {
                get { return Surface; }
            }

            public override Color ImageMarginGradientMiddle
            {
                get { return Surface; }
            }

            public override Color ImageMarginGradientEnd
            {
                get { return Surface; }
            }

            public override Color SeparatorDark
            {
                get { return BorderSoft; }
            }

            public override Color SeparatorLight
            {
                get { return BorderSoft; }
            }
        }
    }
}
