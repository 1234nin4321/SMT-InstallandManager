using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SMTInstaller.UI
{
    /// <summary>Colors, fonts and drawing helpers shared by all custom controls.</summary>
    static class Theme
    {
        public static readonly Color Background = Hex("#14161B");
        public static readonly Color Surface = Hex("#1C1F26");
        public static readonly Color SurfaceHover = Hex("#252932");
        public static readonly Color SurfacePressed = Hex("#2C313B");
        public static readonly Color Border = Hex("#2A2E37");
        public static readonly Color Text = Hex("#ECEFF4");
        public static readonly Color TextMuted = Hex("#8B93A1");
        public static readonly Color TextDisabled = Hex("#555C68");

        public static readonly Color Accent = Hex("#2ECC71");
        public static readonly Color AccentHover = Hex("#48D884");
        public static readonly Color AccentPressed = Hex("#27AE60");
        public static readonly Color AccentDark = Hex("#17694A");
        public static readonly Color OnAccent = Hex("#0B1F14");

        public static readonly Color Success = Hex("#2ECC71");
        public static readonly Color Warning = Hex("#F5B041");
        public static readonly Color Error = Hex("#EF5350");
        public static readonly Color Info = Hex("#5DADE2");

        public static readonly Font Title = new Font("Segoe UI Semibold", 17f);
        public static readonly Font Subtitle = new Font("Segoe UI", 10f);
        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.75f);
        public static readonly Font SmallBold = new Font("Segoe UI Semibold", 8.25f);
        public static readonly Font Section = new Font("Segoe UI Semibold", 8f);
        public static readonly Font Button = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Mono = new Font("Consolas", 9f);

        /// <summary>Segoe MDL2 Assets icon font (Windows 10 and 11).</summary>
        public static Font Icons(float size) => new Font("Segoe MDL2 Assets", size);

        // Segoe MDL2 Assets code points
        public const string GlyphFolder = "";
        public const string GlyphCheck = "";
        public const string GlyphDownload = "";
        public const string GlyphError = "";
        public const string GlyphWarning = "";
        public const string GlyphSync = "";
        public const string GlyphRefresh = "";
        public const string GlyphCode = "";
        public const string GlyphSettings = "";
        public const string GlyphShop = "\uE719";
        public const string GlyphInfo = "\uE946";
        public const string GlyphPackage = "\uE7B8";
        public const string GlyphLink = "\uE8A7";   // OpenInNewWindow
        public const string GlyphChevronDown = "";
        public const string GlyphChevronUp = "";

        /// <summary>DPI scale factor; the app is system-DPI aware, so this is fixed at startup.</summary>
        public static float Scale { get; private set; } = 1f;

        public static void InitScale()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                Scale = g.DpiX / 96f;
        }

        /// <summary>Scales a 96-DPI pixel value to the current DPI.</summary>
        public static int S(float value) => (int)Math.Round(value * Scale);

        public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

        /// <summary>Blends <paramref name="top"/> over <paramref name="bottom"/> at the given opacity.</summary>
        public static Color Mix(Color top, Color bottom, float amount) => Color.FromArgb(
            (int)(top.R * amount + bottom.R * (1 - amount)),
            (int)(top.G * amount + bottom.G * (1 - amount)),
            (int)(top.B * amount + bottom.B * (1 - amount)));

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
        {
            using (var brush = new SolidBrush(color))
            using (var path = RoundedRect(r, radius))
                g.FillPath(brush, path);
        }

        /// <summary>Draws an icon glyph centered in a tinted circle.</summary>
        public static void DrawIconBadge(Graphics g, string glyph, Color color, Color surface, Rectangle circle)
        {
            using (var brush = new SolidBrush(Mix(color, surface, 0.16f)))
                g.FillEllipse(brush, circle);
            using (var font = Icons(circle.Height * 0.3f / Scale))
                System.Windows.Forms.TextRenderer.DrawText(g, glyph, font, circle, color,
                    System.Windows.Forms.TextFormatFlags.HorizontalCenter |
                    System.Windows.Forms.TextFormatFlags.VerticalCenter |
                    System.Windows.Forms.TextFormatFlags.NoPadding);
        }

        /// <summary>Dark title bar (Windows 10 20H1+ uses 20, older builds 19) and matching caption color on Windows 11.</summary>
        public static void DarkTitleBar(IntPtr hwnd)
        {
            try
            {
                var on = 1;
                if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
                var caption = ColorTranslator.ToWin32(Background);
                DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
            }
            catch (DllNotFoundException) { /* pre-Vista, keep the default title bar */ }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>The embedded app icon at the requested pixel size.</summary>
        public static Icon AppIcon(int size)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SMTInstaller.app.ico"))
                return stream == null ? null : new Icon(stream, size, size);
        }
    }
}
