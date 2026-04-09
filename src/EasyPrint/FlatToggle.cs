using System.Drawing.Drawing2D;

namespace EasyPrint
{
    /// <summary>
    /// 扁平化风格的开关控件，匹配暗色主题调色板。
    /// </summary>
    internal sealed class FlatToggle : Control
    {
        private bool _checked;

        // ── 调色板（与主题一致）────────────────────────────────────────────────
        private static readonly Color TrackOn   = Color.FromArgb(48,  110, 190);
        private static readonly Color TrackOff  = Color.FromArgb(45,  51,  70);
        private static readonly Color ThumbColor = Color.FromArgb(212, 218, 232);

        // ── 公共属性 ────────────────────────────────────────────────────────────

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CheckedChanged;

        // ── 构造 ────────────────────────────────────────────────────────────────

        public FlatToggle()
        {
            Size   = new Size(48, 26);
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.UserPaint            |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        // ── 交互 ────────────────────────────────────────────────────────────────

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        // ── 绘制 ────────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Track（胶囊形跑道）
            const int trackH = 16;
            int trackY = (Height - trackH) / 2;
            var trackRect = new Rectangle(0, trackY, Width - 1, trackH);

            using (var brush = new SolidBrush(_checked ? TrackOn : TrackOff))
                FillRoundRect(g, brush, trackRect, trackH / 2f);

            // Thumb（圆形滑块，略大于跑道高度，产生凸起效果）
            const int thumbD = 20;
            int thumbX = _checked ? Width - thumbD - 2 : 2;
            int thumbY = (Height - thumbD) / 2;

            using (var brush = new SolidBrush(ThumbColor))
                g.FillEllipse(brush, thumbX, thumbY, thumbD, thumbD);
        }

        private static void FillRoundRect(Graphics g, Brush brush, Rectangle rect, float radius)
        {
            using var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X,                rect.Y, d, rect.Height, 90, 180);
            path.AddArc(rect.Right - d,        rect.Y, d, rect.Height, 270, 180);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}
