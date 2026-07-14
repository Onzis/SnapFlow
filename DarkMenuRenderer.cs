using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Screenshoter
{
    // Тёмная цветовая схема для контекстного меню трея (в стиле окна настроек).
    public sealed class DarkColorTable : ProfessionalColorTable
    {
        public static readonly Color Background = Color.FromArgb(28, 28, 30);   // #1C1C1E
        public static readonly Color Hover = Color.FromArgb(58, 58, 60);        // #3A3A3C
        public static readonly Color Border = Color.FromArgb(58, 58, 60);
        public static readonly Color Separator = Color.FromArgb(72, 72, 74);

        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuBorder => Border;
        public override Color SeparatorDark => Separator;
        public override Color SeparatorLight => Separator;
    }

    // Полностью ручная отрисовка фона/подсветки — исключает белые системные цвета.
    public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { RoundedEdges = true; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(DarkColorTable.Background);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            var item = e.Item;
            var rect = new Rectangle(3, 1, item.Width - 6, item.Height - 2);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(DarkColorTable.Background))
                g.FillRectangle(bg, new Rectangle(0, 0, item.Width, item.Height));

            if ((item.Selected || item.Pressed) && item.Enabled)
            {
                using var path = Rounded(rect, 6);
                using var hb = new SolidBrush(DarkColorTable.Hover);
                g.FillPath(hb, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.White : Color.Gray;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics;
            int y = e.Item.Height / 2;
            using var pen = new Pen(DarkColorTable.Separator);
            g.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var pen = new Pen(DarkColorTable.Border);
            g.DrawRectangle(pen, r);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
