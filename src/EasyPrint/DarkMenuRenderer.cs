namespace EasyPrint
{
    // ── 调色板（与主题一致）─────────────────────────────────────────────────────
    // BG_DROP   (22,  26,  35)  菜单背景
    // BG_HOVER  (36,  42,  58)  悬停/选中行
    // BG_PRESS  (30,  62, 108)  按下行
    // BORDER    (45,  51,  70)  边框 / 分隔线
    // TXT_PRI   (212, 218, 232) 主要文字
    // TXT_DIM   (80,  90,  110) 禁用文字
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 暗色扁平风格的 ToolStrip 渲染器，专为托盘右键菜单设计。
    /// </summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color BgDrop   = Color.FromArgb(22,  26,  35);
        private static readonly Color BgHover  = Color.FromArgb(36,  42,  58);
        private static readonly Color Border   = Color.FromArgb(45,  51,  70);
        private static readonly Color TxtPri   = Color.FromArgb(212, 218, 232);
        private static readonly Color TxtDim   = Color.FromArgb(80,  90,  110);

        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        // ── 菜单整体背景 ──────────────────────────────────────────────────────────
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(BgDrop);
        }

        // ── 菜单外边框 ────────────────────────────────────────────────────────────
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen,
                new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
        }

        // ── 菜单项背景（hover / normal）──────────────────────────────────────────
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is ToolStripSeparator) return;

            var color = e.Item.Selected ? BgHover : BgDrop;
            var rect  = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, rect);
        }

        // ── 去掉左侧图标灰边 ──────────────────────────────────────────────────────
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        // ── 分隔线 ────────────────────────────────────────────────────────────────
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        // ── 文字颜色（遵守 item.ForeColor 显式设置，如退出按钮红色）──────────────
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
                e.TextColor = TxtDim;
            else if (e.Item.ForeColor != SystemColors.ControlText)
                e.TextColor = e.Item.ForeColor;
            else
                e.TextColor = TxtPri;

            base.OnRenderItemText(e);
        }
    }

    /// <summary>配合 DarkMenuRenderer 的颜色表，覆盖 Professional 默认色。</summary>
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Bg     = Color.FromArgb(22, 26, 35);
        private static readonly Color Border = Color.FromArgb(45, 51, 70);
        private static readonly Color Hover  = Color.FromArgb(36, 42, 58);
        private static readonly Color Press  = Color.FromArgb(30, 62, 108);

        public override Color MenuBorder                      => Border;
        public override Color MenuItemBorder                  => Color.Transparent;
        public override Color ToolStripDropDownBackground     => Bg;
        public override Color ImageMarginGradientBegin        => Bg;
        public override Color ImageMarginGradientMiddle       => Bg;
        public override Color ImageMarginGradientEnd          => Bg;
        public override Color MenuItemSelectedGradientBegin   => Hover;
        public override Color MenuItemSelectedGradientEnd     => Hover;
        public override Color MenuItemPressedGradientBegin    => Press;
        public override Color MenuItemPressedGradientEnd      => Press;
        public override Color SeparatorDark                   => Border;
        public override Color SeparatorLight                  => Border;
        public override Color MenuStripGradientBegin          => Bg;
        public override Color MenuStripGradientEnd            => Bg;
    }
}
