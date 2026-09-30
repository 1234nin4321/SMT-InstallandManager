using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SMTInstaller.UI
{
    enum ModState { Checking, Ready, Outdated, Downloading, Installing, Installed, Failed }

    /// <summary>Card for one mod: icon, name, version, status and a progress bar. Optional mods get a checkbox.</summary>
    class ModCard : RoundedPanel
    {
        readonly Timer animation = new Timer { Interval = 16 };
        float phase;   // 0..1 position of the indeterminate progress shimmer

        ModState state = ModState.Checking;
        double progress;
        string version, detail;
        bool optional, isChecked, locked;

        public event EventHandler CheckedChanged;

        public string Title { get; set; }
        public string Description { get; set; }
        public string Glyph { get; set; }

        public ModCard()
        {
            Height = Theme.S(84);
            animation.Tick += (s, e) =>
            {
                phase = (phase + 0.012f) % 1f;
                Invalidate(ProgressBounds);
            };
            UpdateAnimation();
        }

        public string Version
        {
            get => version;
            set { version = value; Invalidate(); }
        }

        public ModState State
        {
            get => state;
            set { state = value; UpdateAnimation(); Invalidate(); }
        }

        /// <summary>0..1, shown while downloading.</summary>
        public double Progress
        {
            get => progress;
            set { progress = Math.Max(0, Math.Min(1, value)); Invalidate(); }
        }

        /// <summary>Optional extra text after the status, e.g. "7 files".</summary>
        public string Detail
        {
            get => detail;
            set { detail = value; Invalidate(); }
        }

        /// <summary>Shows a checkbox; clicking the card toggles it.</summary>
        public bool Optional
        {
            get => optional;
            set { optional = value; UpdateCursor(); Invalidate(); }
        }

        public bool Checked
        {
            get => isChecked;
            set
            {
                if (value == isChecked) return;
                isChecked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Stops the checkbox from changing, e.g. while installing.</summary>
        public bool Locked
        {
            get => locked;
            set { locked = value; UpdateCursor(); Invalidate(); }
        }

        /// <summary>An optional mod the user left unticked.</summary>
        bool Skipped => optional && !isChecked;

        void UpdateCursor() => Cursor = optional && !locked ? Cursors.Hand : Cursors.Default;

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (optional && !locked) Checked = !isChecked;
        }

        Rectangle CheckboxBounds => new Rectangle(Width - Theme.S(20) - Theme.S(20), (Height - Theme.S(20)) / 2, Theme.S(20), Theme.S(20));

        bool Indeterminate => state == ModState.Checking || state == ModState.Installing;

        void UpdateAnimation() => animation.Enabled = Indeterminate;

        Rectangle ProgressBounds
        {
            get
            {
                var x = TextLeft;
                return new Rectangle(x, Height - Theme.S(16), Width - x - Theme.S(20), Theme.S(4));
            }
        }

        int TextLeft => Theme.S(20) + Theme.S(44) + Theme.S(16);

        protected override void Dispose(bool disposing)
        {
            if (disposing) animation.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color statusColor;
            string statusGlyph, statusText;
            switch (state)
            {
                case ModState.Ready: statusColor = Theme.TextMuted; statusGlyph = Theme.GlyphDownload; statusText = "Ready to install"; break;
                case ModState.Outdated: statusColor = Theme.Warning; statusGlyph = Theme.GlyphRefresh; statusText = "Update available"; break;
                case ModState.Downloading: statusColor = Theme.Info; statusGlyph = Theme.GlyphDownload; statusText = $"Downloading {progress:P0}"; break;
                case ModState.Installing: statusColor = Theme.Info; statusGlyph = Theme.GlyphSync; statusText = "Installing…"; break;
                case ModState.Installed: statusColor = Theme.Success; statusGlyph = Theme.GlyphCheck; statusText = "Installed"; break;
                case ModState.Failed: statusColor = Theme.Error; statusGlyph = Theme.GlyphError; statusText = "Failed"; break;
                default: statusColor = Theme.TextMuted; statusGlyph = Theme.GlyphSync; statusText = "Checking GitHub…"; break;
            }
            if (Skipped && state == ModState.Ready) { statusText = "Not selected"; statusGlyph = Theme.GlyphDownload; }
            if (!string.IsNullOrEmpty(detail)) statusText += " · " + detail;

            var pad = Theme.S(20);
            var badge = new Rectangle(pad, (Height - Theme.S(44)) / 2, Theme.S(44), Theme.S(44));
            Theme.DrawIconBadge(g, Glyph, state == ModState.Failed ? Theme.Error : Skipped ? Theme.TextMuted : Theme.Accent, BackColor, badge);

            // Status and description stop short of the checkbox
            var contentRight = Width - pad;
            if (optional)
            {
                DrawCheckbox(g, CheckboxBounds);
                contentRight = CheckboxBounds.Left - Theme.S(14);
            }

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var x = TextLeft;

            // Status (right-aligned, measured first so the title can use the remaining space)
            var statusSize = TextRenderer.MeasureText(g, statusText, Theme.Small, Size.Empty, flags);
            using (var glyphFont = Theme.Icons(8.5f))
            {
                var glyphSize = TextRenderer.MeasureText(g, statusGlyph, glyphFont, Size.Empty, flags);
                var statusRight = contentRight;
                var statusY = Theme.S(18);
                TextRenderer.DrawText(g, statusText, Theme.Small,
                    new Rectangle(statusRight - statusSize.Width, statusY, statusSize.Width + 1, Theme.S(22)), statusColor,
                    flags | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, statusGlyph, glyphFont,
                    new Rectangle(statusRight - statusSize.Width - Theme.S(6) - glyphSize.Width, statusY, glyphSize.Width, Theme.S(22)),
                    statusColor, flags | TextFormatFlags.VerticalCenter);
            }

            // Title + version pill
            var titleSize = TextRenderer.MeasureText(g, Title, Theme.BodyBold, Size.Empty, flags);
            var titleRect = new Rectangle(x, Theme.S(18), titleSize.Width + 1, Theme.S(22));
            TextRenderer.DrawText(g, Title, Theme.BodyBold, titleRect, Skipped ? Theme.TextMuted : Theme.Text, flags | TextFormatFlags.VerticalCenter);

            if (!string.IsNullOrEmpty(version))
            {
                var vSize = TextRenderer.MeasureText(g, version, Theme.SmallBold, Size.Empty, flags);
                var pill = new Rectangle(titleRect.Right + Theme.S(10), titleRect.Y + Theme.S(1), vSize.Width + Theme.S(16), Theme.S(20));
                Theme.FillRounded(g, Theme.Mix(Theme.Accent, BackColor, 0.14f), pill, pill.Height / 2f);
                TextRenderer.DrawText(g, version, Theme.SmallBold, pill, Theme.Accent,
                    flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            TextRenderer.DrawText(g, Description, Theme.Small,
                new Rectangle(x, Theme.S(42), contentRight - x, Theme.S(18)), Theme.TextMuted, flags | TextFormatFlags.EndEllipsis);

            DrawProgress(g);
        }

        void DrawCheckbox(Graphics g, Rectangle r)
        {
            var box = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
            var dim = locked && !isChecked;
            if (isChecked)
            {
                Theme.FillRounded(g, locked ? Theme.Mix(Theme.Accent, BackColor, 0.5f) : Theme.Accent, box, Theme.S(5));
                using (var glyphFont = Theme.Icons(9f))
                    TextRenderer.DrawText(g, Theme.GlyphCheck, glyphFont, r, Theme.OnAccent,
                        TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                Theme.FillRounded(g, Theme.Mix(Color.White, BackColor, 0.04f), box, Theme.S(5));
                using (var path = Theme.RoundedRect(box, Theme.S(5)))
                using (var pen = new Pen(dim ? Theme.Border : Theme.TextMuted, Theme.S(1.5f)))
                    g.DrawPath(pen, path);
            }
        }

        void DrawProgress(Graphics g)
        {
            var r = ProgressBounds;
            var radius = r.Height / 2f;
            Theme.FillRounded(g, Theme.Mix(Color.White, BackColor, 0.06f), r, radius);

            switch (state)
            {
                case ModState.Downloading:
                    if (progress > 0)
                        Theme.FillRounded(g, Theme.Info, new RectangleF(r.X, r.Y, Math.Max(r.Height, (float)(r.Width * progress)), r.Height), radius);
                    break;
                case ModState.Installed:
                    Theme.FillRounded(g, Theme.Success, r, radius);
                    break;
                case ModState.Failed:
                    Theme.FillRounded(g, Theme.Error, r, radius);
                    break;
                case ModState.Checking:
                case ModState.Installing:
                    // A short bar sweeping left to right
                    var width = r.Width * 0.28f;
                    var left = r.X - width + (r.Width + width) * phase;
                    var bar = RectangleF.Intersect(new RectangleF(left, r.Y, width, r.Height), r);
                    if (bar.Width > 0)
                    {
                        var color = state == ModState.Installing ? Theme.Info : Theme.Mix(Color.White, BackColor, 0.25f);
                        Theme.FillRounded(g, color, bar, radius);
                    }
                    break;
            }
        }
    }
}
