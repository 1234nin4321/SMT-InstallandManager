using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SMTInstaller.UI
{
    /// <summary>A panel drawn as a rounded card. Its BackColor is the card fill.</summary>
    class RoundedPanel : Panel
    {
        public int Radius { get; set; } = Theme.S(10);
        public Color BorderColor { get; set; } = Theme.Border;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(r, Radius))
            using (var fill = new SolidBrush(BackColor))
            using (var pen = new Pen(BorderColor))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }
}
