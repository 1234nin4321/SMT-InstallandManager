using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SMTInstaller.UI
{
    enum ButtonKind { Primary, Secondary, Ghost }

    /// <summary>A rounded, flat button with hover/pressed states and an optional icon glyph.</summary>
    class FlatButton : Control, IButtonControl
    {
        bool hovered, pressed;
        string glyph;
        ButtonKind kind = ButtonKind.Secondary;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Font = Theme.Button;
        }

        public ButtonKind Kind
        {
            get => kind;
            set { kind = value; Invalidate(); }
        }

        public string Glyph
        {
            get => glyph;
            set { glyph = value; Invalidate(); }
        }

        public DialogResult DialogResult { get; set; }
        public void NotifyDefault(bool value) { }
        public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = pressed = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) PerformClick();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var parentColor = Parent?.BackColor ?? Theme.Background;
            g.Clear(parentColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill, text, border = Color.Empty;
            switch (kind)
            {
                case ButtonKind.Primary:
                    fill = !Enabled ? Theme.SurfaceHover : pressed ? Theme.AccentPressed : hovered ? Theme.AccentHover : Theme.Accent;
                    text = Enabled ? Theme.OnAccent : Theme.TextDisabled;
                    break;
                case ButtonKind.Ghost:
                    fill = !Enabled ? parentColor : pressed ? Theme.SurfacePressed : hovered ? Theme.SurfaceHover : parentColor;
                    text = Enabled ? (hovered ? Theme.Text : Theme.TextMuted) : Theme.TextDisabled;
                    break;
                default:
                    fill = !Enabled ? Theme.Surface : pressed ? Theme.SurfacePressed : hovered ? Theme.SurfaceHover : Theme.Mix(Color.White, parentColor, 0.05f);
                    text = Enabled ? Theme.Text : Theme.TextDisabled;
                    border = Theme.Border;
                    break;
            }

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var radius = Theme.S(8);
            using (var path = Theme.RoundedRect(r, radius))
            {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (!border.IsEmpty)
                    using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }

            if (Focused && ShowFocusCues)
            {
                var focus = RectangleF.Inflate(r, -Theme.S(2), -Theme.S(2));
                using (var path = Theme.RoundedRect(focus, radius - Theme.S(2)))
                using (var pen = new Pen(Color.FromArgb(160, kind == ButtonKind.Primary ? Theme.OnAccent : Theme.Accent), Theme.S(1.5f)))
                    g.DrawPath(pen, path);
            }

            // Icon + label, centered together
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            var textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags);
            var gap = Theme.S(8);
            Size glyphSize = Size.Empty;
            Font glyphFont = null;
            if (!string.IsNullOrEmpty(glyph))
            {
                glyphFont = Theme.Icons(Font.SizeInPoints * 0.95f);
                glyphSize = TextRenderer.MeasureText(g, glyph, glyphFont, Size.Empty, flags);
            }
            var total = textSize.Width + (glyphFont != null ? glyphSize.Width + (Text.Length > 0 ? gap : 0) : 0);
            var x = (Width - total) / 2;

            if (glyphFont != null)
            {
                TextRenderer.DrawText(g, glyph, glyphFont, new Rectangle(x, 0, glyphSize.Width, Height), text, flags);
                x += glyphSize.Width + gap;
                glyphFont.Dispose();
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textSize.Width + 1, Height), text, flags);
        }
    }
}
