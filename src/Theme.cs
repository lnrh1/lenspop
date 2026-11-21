using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPop
{
    public static class Theme
    {
        public static readonly Color Brand = Color.FromArgb(79, 70, 229);
        public static readonly Color BrandSoft = Color.FromArgb(238, 242, 255);
        public static readonly Color Fg = Color.FromArgb(17, 24, 39);
        public static readonly Color Sub = Color.FromArgb(107, 114, 128);
        public static readonly Color Line = Color.FromArgb(226, 232, 240);
        public static readonly Color Bg = Color.White;
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Panel = Color.FromArgb(249, 250, 251);

        public static readonly Font UI = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font UIBold = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        public static readonly Font UISmall = new Font("Microsoft YaHei UI", 8f);
        public static readonly Font Content = new Font("Microsoft YaHei UI", 10f);
        public static readonly Font Title = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);

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
            try
            {
                f.Region = new Region(RoundedRect(new Rectangle(0, 0, f.Width, f.Height), radius));
            }
            catch { }
        }

        public static Button FlatButton(string text, bool primary)
        {
            var b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.Font = UI;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Line;
            b.BackColor = primary ? Brand : Bg;
            b.ForeColor = primary ? Color.White : Fg;
            b.Height = 28;
            b.AutoSize = false;
            return b;
        }

        public static void StyleInput(Control c)
        {
            c.Font = UI;
            c.ForeColor = Fg;
            c.BackColor = Bg;
        }

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;

        /// <summary>点它不抢焦点：不打断原来的选中状态</summary>
        public static void NoActivate(Form f)
        {
            int ex = GetWindowLong(f.Handle, GWL_EXSTYLE);
            SetWindowLong(f.Handle, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        /// <summary>无边框窗口按住某块区域拖动</summary>
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
                if (!dragging) return;
                Point p = f.PointToScreen(e.Location);
                f.Location = new Point(p.X - start.X, p.Y - start.Y);
            };
            handle.MouseUp += delegate { dragging = false; };
        }
    }
}
