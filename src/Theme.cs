using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPop
{
    public static class Theme
    {
        // ---- 配色（slate + indigo）----
        public static readonly Color Brand = Color.FromArgb(79, 70, 229);
        public static readonly Color BrandHover = Color.FromArgb(99, 91, 255);
        public static readonly Color BrandPress = Color.FromArgb(67, 56, 202);
        public static readonly Color BrandSoft = Color.FromArgb(238, 240, 255);
        public static readonly Color Fg = Color.FromArgb(15, 23, 42);
        public static readonly Color Sub = Color.FromArgb(100, 116, 139);
        public static readonly Color Line = Color.FromArgb(226, 232, 240);
        public static readonly Color Bg = Color.White;
        public static readonly Color Panel = Color.FromArgb(248, 250, 252);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Ok = Color.FromArgb(5, 150, 105);

        // ---- 字体 ----
        public static readonly Font UI = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font UIBold = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        public static readonly Font UISmall = new Font("Microsoft YaHei UI", 8f);
        public static readonly Font Content = new Font("Microsoft YaHei UI", 10f);
        public static readonly Font Title = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        public static readonly Font H1 = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Round(Form f, int radius)
        {
            if (f.Width <= 0 || f.Height <= 0) return;
            try { f.Region = new Region(RoundedRect(new Rectangle(0, 0, f.Width, f.Height), radius)); }
            catch { }
        }

        public static void RoundControl(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            try { c.Region = new Region(RoundedRect(new Rectangle(0, 0, c.Width, c.Height), radius)); }
            catch { }
        }

        public static Button FlatButton(string text, bool primary)
        {
            var b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.Font = primary ? UIBold : UI;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = primary ? BrandHover : BrandSoft;
            b.FlatAppearance.MouseDownBackColor = primary ? BrandPress : Color.FromArgb(224, 231, 255);
            b.BackColor = primary ? Brand : Bg;
            b.ForeColor = primary ? Color.White : Fg;
            b.Height = 28;
            b.AutoSize = false;
            b.Resize += delegate { RoundControl(b, 6); };
            return b;
        }

        public static void StyleInput(Control c)
        {
            c.Font = UI;
            c.ForeColor = Fg;
            c.BackColor = Bg;
        }

        /// <summary>现代化 Tab：胶囊样式，选中项用品牌色底</summary>
        public static void StyleTabs(TabControl tabs)
        {
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(108, 34);
            tabs.Padding = new Point(14, 6);
            tabs.Font = UIBold;
            tabs.Appearance = TabAppearance.Normal;
            tabs.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool selected = e.Index == tabs.SelectedIndex;
                var r = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 4, e.Bounds.Width - 4, e.Bounds.Height - 8);
                using (var b = new SolidBrush(selected ? Brand : Color.Transparent))
                {
                    var path = RoundedRect(r, 8);
                    g.FillPath(b, path);
                    path.Dispose();
                }
                string text = tabs.TabPages[e.Index].Text;
                using (var tb = new SolidBrush(selected ? Color.White : Sub))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(text, tabs.Font, tb, r, sf);
                }
            };
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        }

        /// <summary>画窗口顶部品牌区（标题 + 副标题 + 版本）</summary>
        /// <summary>隐藏系统标签条（改由窗口自绘）</summary>
        public static void StripTabs(TabControl tabs)
        {
            tabs.Appearance = TabAppearance.FlatButtons;
            tabs.ItemSize = new Size(0, 1);
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.Padding = new Point(0, 0);
            tabs.DrawMode = TabDrawMode.Normal;
        }

        static Rectangle TabRect(Rectangle area, int index)
        {
            return new Rectangle(area.X + 18 + index * 116, area.Y + 4, 108, 32);
        }

        public static void PaintTabStrip(Graphics g, Rectangle area, TabControl tabs)
        {
            if (tabs == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Bg)) g.FillRectangle(bg, area);
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                var r = TabRect(area, i);
                bool sel = i == tabs.SelectedIndex;
                using (var b = new SolidBrush(sel ? Brand : Panel))
                {
                    var path = RoundedRect(r, 8);
                    g.FillPath(b, path);
                    path.Dispose();
                }
                using (var tb = new SolidBrush(sel ? Color.White : Sub))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(tabs.TabPages[i].Text, UIBold, tb, r, sf);
                }
            }
            using (var pen = new Pen(Line)) g.DrawLine(pen, area.X, area.Bottom - 1, area.Right, area.Bottom - 1);
        }

        public static int TabStripHit(Rectangle area, TabControl tabs, int x)
        {
            if (tabs == null) return -1;
            for (int i = 0; i < tabs.TabPages.Count; i++)
                if (TabRect(area, i).Contains(area.X + x, area.Y + 20)) return i;
            return -1;
        }
        public static void PaintHeader(Graphics g, Rectangle area, string title, string subtitle, string right)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Bg)) g.FillRectangle(bg, area);

            var logo = new Rectangle(area.X + 18, area.Y + 14, 28, 28);
            var path = RoundedRect(logo, 9);
            using (var b = new LinearGradientBrush(logo, Brand, Color.FromArgb(14, 165, 233), 45f))
                g.FillPath(b, path);
            path.Dispose();
            using (var f = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold))
            using (var tb = new SolidBrush(Color.White))
            using (var sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("L", f, tb, logo, sf);
            }

            using (var tb = new SolidBrush(Fg)) g.DrawString(title, H1, tb, logo.Right + 12, area.Y + 12);
            if (!string.IsNullOrEmpty(subtitle))
                using (var tb = new SolidBrush(Sub)) g.DrawString(subtitle, UISmall, tb, logo.Right + 14, area.Y + 36);
            if (!string.IsNullOrEmpty(right))
                using (var tb = new SolidBrush(Sub))
                {
                    var sz = g.MeasureString(right, UISmall);
                    g.DrawString(right, UISmall, tb, area.Right - sz.Width - 18, area.Y + 22);
                }
            using (var pen = new Pen(Line)) g.DrawLine(pen, area.X, area.Bottom - 1, area.Right, area.Bottom - 1);
        }

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;

        public static void NoActivate(Form f)
        {
            int ex = GetWindowLong(f.Handle, GWL_EXSTYLE);
            SetWindowLong(f.Handle, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        public static void DragBy(Form f, Control handle)
        {
            Point start = Point.Empty;
            bool dragging = false;
            handle.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { dragging = true; start = e.Location; }
            };
            handle.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (dragging) f.Location = new Point(f.Left + e.X - start.X, f.Top + e.Y - start.Y);
            };
            handle.MouseUp += delegate { dragging = false; };
        }
    }
}
