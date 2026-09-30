using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SMTInstaller.UI
{
    /// <summary>The gradient banner at the top of the window.</summary>
    class HeaderPanel : Control
    {
        readonly Bitmap logo;
        Rectangle aboutRect;
        bool aboutHovered;

        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Badge { get; set; } = "";

        /// <summary>Raised when the "About" pill in the top right is clicked.</summary>
        public event EventHandler AboutClicked;

        public HeaderPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
            using (var icon = Theme.AppIcon(256))
                logo = icon?.ToBitmap();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) logo?.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetAboutHovered(aboutRect.Contains(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetAboutHovered(false);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && aboutRect.Contains(e.Location)) AboutClicked?.Invoke(this, EventArgs.Empty);
        }

        void SetAboutHovered(bool value)
        {
            if (value == aboutHovered) return;
            aboutHovered = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            Invalidate(aboutRect);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var bounds = ClientRectangle;

            using (var brush = new LinearGradientBrush(bounds, Theme.AccentDark, Theme.Background, 25f))
                g.FillRectangle(brush, bounds);

            // Soft decorative rings on the right
            using (var ring = new Pen(Color.FromArgb(18, Color.White), Theme.S(28)))
            {
                g.DrawEllipse(ring, Width - Theme.S(170), -Theme.S(90), Theme.S(260), Theme.S(260));
                g.DrawEllipse(ring, Width - Theme.S(60), Theme.S(40), Theme.S(140), Theme.S(140));
            }

            // Fade into the body at the bottom edge
            var fade = new Rectangle(0, Height - Theme.S(24), Width, Theme.S(24));
            // Brush rect is padded by a pixel each side so the gradient doesn't wrap at the edges
            var brushRect = Rectangle.Inflate(fade, 0, 1);
            using (var brush = new LinearGradientBrush(brushRect, Color.FromArgb(0, Theme.Background), Theme.Background, 90f))
                g.FillRectangle(brush, fade);

            var pad = Theme.S(28);
            var logoSize = Theme.S(60);
            var logoRect = new Rectangle(pad, (Height - logoSize) / 2, logoSize, logoSize);
            if (logo != null)
            {
                // Drop shadow
                using (var shadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
                using (var path = Theme.RoundedRect(new RectangleF(logoRect.X + 1, logoRect.Y + Theme.S(3), logoSize, logoSize), logoSize * 0.22f))
                    g.FillPath(shadow, path);
                g.DrawImage(logo, logoRect);
            }

            var textX = logoRect.Right + Theme.S(18);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var titleHeight = TextRenderer.MeasureText(g, Title, Theme.Title, Size.Empty, flags).Height;
            var subHeight = TextRenderer.MeasureText(g, Subtitle, Theme.Subtitle, Size.Empty, flags).Height;
            var top = (Height - titleHeight - subHeight - Theme.S(2)) / 2;

            TextRenderer.DrawText(g, Title, Theme.Title, new Rectangle(textX, top, Width - textX - pad, titleHeight), Color.White, flags);
            TextRenderer.DrawText(g, Subtitle, Theme.Subtitle,
                new Rectangle(textX, top + titleHeight + Theme.S(2), Width - textX - pad, subHeight), Theme.Hex("#A9DCC0"), flags);

            var pillRight = Width - pad;
            if (!string.IsNullOrEmpty(Badge))
            {
                var size = TextRenderer.MeasureText(g, Badge, Theme.SmallBold, Size.Empty, flags);
                var pill = new Rectangle(pillRight - size.Width - Theme.S(20), Theme.S(18), size.Width + Theme.S(20), Theme.S(22));
                Theme.FillRounded(g, Color.FromArgb(40, Color.Black), pill, pill.Height / 2f);
                TextRenderer.DrawText(g, Badge, Theme.SmallBold, pill, Theme.Hex("#CFEFDC"),
                    flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                pillRight = pill.Left - Theme.S(8);
            }

            // "About" pill, same shape as the badge but clickable
            using (var glyphFont = Theme.Icons(8f))
            {
                const string label = "About";
                var glyphSize = TextRenderer.MeasureText(g, Theme.GlyphInfo, glyphFont, Size.Empty, flags);
                var labelSize = TextRenderer.MeasureText(g, label, Theme.SmallBold, Size.Empty, flags);
                var gap = Theme.S(6);
                var width = glyphSize.Width + gap + labelSize.Width + Theme.S(20);
                aboutRect = new Rectangle(pillRight - width, Theme.S(18), width, Theme.S(22));
                Theme.FillRounded(g, Color.FromArgb(aboutHovered ? 80 : 40, Color.Black), aboutRect, aboutRect.Height / 2f);

                var color = aboutHovered ? Color.White : Theme.Hex("#CFEFDC");
                var x = aboutRect.X + Theme.S(10);
                TextRenderer.DrawText(g, Theme.GlyphInfo, glyphFont, new Rectangle(x, aboutRect.Y, glyphSize.Width, aboutRect.Height), color,
                    flags | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, label, Theme.SmallBold, new Rectangle(x + glyphSize.Width + gap, aboutRect.Y, labelSize.Width + 1, aboutRect.Height), color,
                    flags | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
