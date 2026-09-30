using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SMTInstaller.UI
{
    /// <summary>A row of text tabs with an accent underline under the selected one. Tabs can carry a small pill, e.g. "2 selected".</summary>
    class TabStrip : Control
    {
        readonly List<string> titles = new List<string>();
        readonly List<string> badges = new List<string>();
        readonly List<Rectangle> tabBounds = new List<Rectangle>();
        int selected, hovered = -1;

        public event EventHandler SelectedIndexChanged;

        public TabStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.Background;
            Font = Theme.BodyBold;
            Height = Theme.S(40);
        }

        public int Count => titles.Count;

        public int SelectedIndex
        {
            get => selected;
            set
            {
                if (value < 0 || value >= titles.Count || value == selected) return;
                selected = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AddTab(string title)
        {
            titles.Add(title);
            badges.Add(null);
            LayoutTabs();
        }

        /// <summary>Shows a pill after the tab's title; null or empty hides it.</summary>
        public void SetBadge(int index, string badge)
        {
            if (badges[index] == badge) return;
            badges[index] = badge;
            LayoutTabs();
        }

        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        Size BadgeSize(string badge)
        {
            var text = TextRenderer.MeasureText(badge, Theme.SmallBold, Size.Empty, Flags);
            return new Size(text.Width + Theme.S(16), Theme.S(20));
        }

        void LayoutTabs()
        {
            tabBounds.Clear();
            var x = 0;
            for (var i = 0; i < titles.Count; i++)
            {
                var width = Theme.S(4) + TextRenderer.MeasureText(titles[i], Font, Size.Empty, Flags).Width + Theme.S(4);
                if (!string.IsNullOrEmpty(badges[i])) width += Theme.S(8) + BadgeSize(badges[i]).Width;
                tabBounds.Add(new Rectangle(x, 0, width, Height));
                x += width + Theme.S(24);
            }
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); LayoutTabs(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutTabs(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHovered(tabBounds.FindIndex(r => r.Contains(e.Location)));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHovered(-1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && hovered >= 0) SelectedIndex = hovered;
        }

        void SetHovered(int index)
        {
            if (index == hovered) return;
            hovered = index;
            Cursor = index >= 0 && index != selected ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) SelectedIndex = selected - 1;
            else if (e.KeyCode == Keys.Right) SelectedIndex = selected + 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            for (var i = 0; i < titles.Count; i++)
            {
                var r = tabBounds[i];
                var color = i == selected ? Theme.Text : i == hovered ? Theme.Mix(Theme.Text, Theme.TextMuted, 0.5f) : Theme.TextMuted;
                var x = r.X + Theme.S(4);
                var textWidth = TextRenderer.MeasureText(g, titles[i], Font, Size.Empty, Flags).Width;
                TextRenderer.DrawText(g, titles[i], Font, new Rectangle(x, 0, textWidth + 1, Height - Theme.S(3)), color, Flags);
                x += textWidth + Theme.S(8);

                if (!string.IsNullOrEmpty(badges[i]))
                {
                    var size = BadgeSize(badges[i]);
                    var pill = new Rectangle(x, (Height - Theme.S(3) - size.Height) / 2, size.Width, size.Height);
                    Theme.FillRounded(g, Theme.Mix(Theme.Accent, BackColor, 0.14f), pill, pill.Height / 2f);
                    TextRenderer.DrawText(g, badges[i], Theme.SmallBold, pill, Theme.Accent, Flags | TextFormatFlags.HorizontalCenter);
                }

                if (i == selected)
                    Theme.FillRounded(g, Theme.Accent, new RectangleF(r.X, Height - Theme.S(3), r.Width, Theme.S(3)), Theme.S(1.5f));

                if (i == selected && Focused && ShowFocusCues)
                    using (var path = Theme.RoundedRect(new RectangleF(r.X + 0.5f, Theme.S(4) + 0.5f, r.Width - 1, Height - Theme.S(10)), Theme.S(6)))
                    using (var pen = new Pen(Color.FromArgb(160, Theme.Accent), Theme.S(1.5f)))
                        g.DrawPath(pen, path);
            }
        }
    }
}
