using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SMTInstaller.UI
{
    enum FolderStatus { Searching, Found, Selected, NotFound, Suspicious }

    /// <summary>Card showing the selected game folder and a Browse button.</summary>
    class FolderCard : RoundedPanel
    {
        readonly FlatButton browseButton = new FlatButton { Text = "Browse", Glyph = Theme.GlyphFolder, Kind = ButtonKind.Secondary };
        string folderPath;
        FolderStatus status = FolderStatus.Searching;

        public event EventHandler BrowseClicked;

        public FolderCard()
        {
            Height = Theme.S(80);
            browseButton.Size = new Size(Theme.S(116), Theme.S(38));
            browseButton.Anchor = AnchorStyles.Right;
            browseButton.Click += (s, e) => BrowseClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(browseButton);
        }

        public string FolderPath
        {
            get => folderPath;
            set { folderPath = value; Invalidate(); }
        }

        public FolderStatus Status
        {
            get => status;
            set { status = value; Invalidate(); }
        }

        public bool BrowseEnabled
        {
            get => browseButton.Enabled;
            set => browseButton.Enabled = value;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            browseButton.Location = new Point(Width - Theme.S(20) - browseButton.Width, (Height - browseButton.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color color;
            string statusText;
            switch (status)
            {
                case FolderStatus.Found: color = Theme.Success; statusText = "Found in your Steam library"; break;
                case FolderStatus.Selected: color = Theme.Success; statusText = "Game folder selected"; break;
                case FolderStatus.NotFound: color = Theme.Warning; statusText = "Not found automatically — click Browse to choose the game folder"; break;
                case FolderStatus.Suspicious: color = Theme.Warning; statusText = "This doesn't look like a Unity game folder"; break;
                default: color = Theme.TextMuted; statusText = "Searching Steam libraries…"; break;
            }

            var pad = Theme.S(20);
            var badge = new Rectangle(pad, (Height - Theme.S(44)) / 2, Theme.S(44), Theme.S(44));
            Theme.DrawIconBadge(g, Theme.GlyphFolder, color == Theme.Success ? Theme.Accent : color, BackColor, badge);

            var textX = badge.Right + Theme.S(16);
            var textWidth = browseButton.Left - Theme.S(16) - textX;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            var pathText = string.IsNullOrEmpty(folderPath) ? "No folder selected" : folderPath;
            TextRenderer.DrawText(g, pathText, Theme.BodyBold,
                new Rectangle(textX, Theme.S(18), textWidth, Theme.S(24)),
                string.IsNullOrEmpty(folderPath) ? Theme.TextMuted : Theme.Text,
                flags | TextFormatFlags.PathEllipsis);

            var dot = Theme.S(8);
            var statusY = Theme.S(46);
            using (var brush = new SolidBrush(color))
                g.FillEllipse(brush, textX, statusY + Theme.S(4), dot, dot);
            TextRenderer.DrawText(g, statusText, Theme.Small,
                new Rectangle(textX + dot + Theme.S(8), statusY, textWidth - dot - Theme.S(8), Theme.S(18)),
                Theme.TextMuted, flags | TextFormatFlags.EndEllipsis);
        }
    }
}
