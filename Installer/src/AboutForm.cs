using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SMTInstaller.UI;
using static SMTInstaller.UI.Theme;

namespace SMTInstaller
{
    /// <summary>Who made the installer, what it builds on, and where to find it.</summary>
    class AboutForm : Form
    {
        static readonly string Author = SelfUpdater.Repo.Split('/')[0];
        static readonly string RepoUrl = $"https://github.com/{SelfUpdater.Repo}";

        static readonly (string Name, string Description, string Url)[] Credits =
        {
            ("BepInEx", "The mod loader that runs plugins inside the game", "https://github.com/BepInEx/BepInEx"),
            ("Configuration Manager", "The in-game mod settings menu (F1)", "https://github.com/BepInEx/BepInEx.ConfigurationManager"),
            ("Thunderstore", "Hosts the mods you find in the Mod Browser", "https://thunderstore.io/c/supermarket-together/"),
        };

        readonly Bitmap logo;

        public AboutForm()
        {
            SuspendLayout();
            Text = "About";
            BackColor = Background;
            ForeColor = Theme.Text;
            Font = Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;   // sizes are scaled by Theme.S instead
            Icon = AppIcon(32);
            using (var icon = AppIcon(256)) logo = icon?.ToBitmap();

            var width = S(480);
            var pad = S(28);
            var inner = width - pad * 2;
            var y = pad;

            // Logo, name, version and author
            var logoSize = S(64);
            Controls.Add(new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(pad, y, logoSize, logoSize) });
            var textX = pad + logoSize + S(16);
            var title = AddLabel("Supermarket Together Mod Installer", BodyBold, Theme.Text, textX, y + S(2), width - pad - textX);
            var version = AddLabel($"Version {SelfUpdater.CurrentVersion}", Small, TextMuted, textX, title.Bottom + S(2), width - pad - textX);
            var byLine = AddLabel("Made by", Small, TextMuted, textX, version.Bottom + S(6), 0);
            AddLink(Author, $"https://github.com/{Author}", Small, byLine.Right, byLine.Top);
            y += logoSize + S(20);

            var blurb = AddLabel("Sets up BepInEx and the Mod Browser for Supermarket Together, so you can browse, install " +
                "and remove Thunderstore mods from inside the game.", Body, TextMuted, pad, y, inner);
            y = blurb.Bottom + S(12);

            var github = AddLink("GitHub", RepoUrl, Body, pad, y);
            AddLink("Report a problem", RepoUrl + "/issues", Body, github.Right + S(16), y);
            y = github.Bottom + S(22);

            // Credits
            Controls.Add(new Label { Text = "BUILT WITH", Font = Section, ForeColor = TextMuted, AutoSize = true, Location = new Point(pad + S(2), y) });
            y += S(24);
            var card = new RoundedPanel();
            var rowY = S(12);
            foreach (var credit in Credits)
            {
                var name = AddLink(credit.Name, credit.Url, BodyBold, S(16), rowY, card);
                var description = AddLabel(credit.Description, Small, TextMuted, S(16), name.Bottom + S(1), inner - S(32), card);
                rowY = description.Bottom + S(10);
            }
            card.SetBounds(pad, y, inner, rowY + S(2));
            Controls.Add(card);
            y = card.Bottom + S(16);

            var disclaimer = AddLabel("A free, unofficial fan project. Not affiliated with the developers of Supermarket Together.",
                Small, TextMuted, pad, y, inner);
            y = disclaimer.Bottom + S(20);

            var close = new FlatButton { Text = "Close", Kind = ButtonKind.Secondary, DialogResult = DialogResult.Cancel };
            close.SetBounds(width - pad - S(110), y, S(110), S(38));
            close.Click += (s, e) => Close();
            Controls.Add(close);
            AcceptButton = CancelButton = close;

            ClientSize = new Size(width, close.Bottom + pad);
            ResumeLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar(Handle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) logo?.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>Adds a label; a width above zero wraps the text to that width.</summary>
        Label AddLabel(string text, Font font, Color color, int x, int y, int width, Control parent = null)
        {
            var label = new Label { Text = text, Font = font, ForeColor = color, AutoSize = false };
            var size = width > 0 ? label.GetPreferredSize(new Size(width, 0)) : label.GetPreferredSize(Size.Empty);
            label.SetBounds(x, y, width > 0 ? width : size.Width, size.Height);
            (parent ?? this).Controls.Add(label);
            return label;
        }

        LinkLabel AddLink(string text, string url, Font font, int x, int y, Control parent = null)
        {
            var link = new LinkLabel
            {
                Text = text,
                Font = font,
                AutoSize = true,
                Location = new Point(x, y),
                LinkColor = Accent,
                ActiveLinkColor = AccentPressed,
                VisitedLinkColor = Accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
            };
            link.LinkClicked += (s, e) => OpenUrl(url);
            (parent ?? this).Controls.Add(link);
            return link;
        }

        void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Couldn't open your browser:\n{ex.Message}\n\n{url}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
