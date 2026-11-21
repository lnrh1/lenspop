using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>框选屏幕区域（截图 / OCR / 图片翻译都用它）</summary>
    public class CaptureForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        readonly Action<Bitmap, Point> _done;
        Bitmap _screen;
        Rectangle _bounds;
        Size _physical = Size.Empty;
        bool _dragging;
        Point _start;
        Rectangle _sel;

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hdc, int index);

        const int DESKTOPHORZRES = 118;
        const int DESKTOPVERTRES = 117;

        static Size PhysicalScreen()
        {
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                int w = GetDeviceCaps(dc, DESKTOPHORZRES);
                int h = GetDeviceCaps(dc, DESKTOPVERTRES);
                ReleaseDC(IntPtr.Zero, dc);
                if (w > 0 && h > 0) return new Size(w, h);
            }
            catch { }
            return Size.Empty;
        }

        static int AppliedDpi()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics"))
                {
                    object v = k == null ? null : k.GetValue("AppliedDPI");
                    if (v != null) return Convert.ToInt32(v);
                }
            }
            catch { }
            return 96;
        }

        public CaptureForm(Action<Bitmap, Point> done)
        {
            _done = done;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            BackColor = Color.Black;

            _bounds = SystemInformation.VirtualScreen;

            // 本进程 DPI 不感知时，屏幕坐标是「虚拟」的；系统若不帮我们放大窗口，遮罩会只盖左上角
            int ownDpi = 96;
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) ownDpi = (int)g.DpiX; }
            catch { }
            if (ownDpi <= 96)
            {
                Size phys = PhysicalScreen();
                if (phys.Width > _bounds.Width + 8 || phys.Height > _bounds.Height + 8)
                    _physical = phys;
            }
            Program.LogLine("遮罩初始化 虚拟=" + _bounds + " 物理=" + (_physical.IsEmpty ? "同虚拟" : _physical.ToString()) + " ownDpi=" + ownDpi);

            Bounds = _bounds;
            Grab();

            Shown += delegate { AdaptToPhysicalScreen(); };
        }

        void Grab()
        {
            if (_screen != null) { try { _screen.Dispose(); } catch { } }
            _screen = new Bitmap(Math.Max(1, _bounds.Width), Math.Max(1, _bounds.Height));
            using (var g = Graphics.FromImage(_screen))
                g.CopyFromScreen(_bounds.Left, _bounds.Top, 0, 0, _bounds.Size);
        }

        void AdaptToPhysicalScreen()
        {
            if (_physical.Width <= 0) return;
            try
            {
                RECT r;
                if (!GetWindowRect(Handle, out r)) return;
                int w = r.Right - r.Left;
                Program.LogLine("遮罩窗口实际=" + w + "x" + (r.Bottom - r.Top) + " 目标=" + _physical.Width + "x" + _physical.Height);
                if (w >= _physical.Width - 8) return;   // 系统已经按 DPI 放大过，不用管

                _bounds = new Rectangle(0, 0, _physical.Width, _physical.Height);
                Bounds = _bounds;
                RECT r2; if (GetWindowRect(Handle, out r2)) Program.LogLine("遮罩调整后=" + (r2.Right - r2.Left) + "x" + (r2.Bottom - r2.Top));
                Grab();
                Invalidate();
            }
            catch { }
        }

        const int WM_GETMINMAXINFO = 0x0024;

        // 进程 DPI 不感知时，Windows 会把窗口最大尺寸限制在虚拟屏幕大小；这里放开到物理分辨率
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETMINMAXINFO && _physical.Width > 0)
            {
                try
                {
                    // MINMAXINFO: ptReserved(0), ptMaxSize(8), ptMaxPosition(16), ptMinTrackSize(24), ptMaxTrackSize(32)
                    int[] buf = new int[10];
                    System.Runtime.InteropServices.Marshal.Copy(m.LParam, buf, 0, 10);
                    buf[8] = _physical.Width;
                    buf[9] = _physical.Height;
                    System.Runtime.InteropServices.Marshal.Copy(buf, 0, m.LParam, 10);
                }
                catch { }
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImage(_screen, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.FillRectangle(dim, ClientRectangle);

            if (_sel.Width > 0 && _sel.Height > 0)
            {
                g.DrawImage(_screen, _sel, _sel, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(79, 70, 229), 2))
                    g.DrawRectangle(pen, _sel.Left, _sel.Top, _sel.Width, _sel.Height);
                string label = _sel.Width + " × " + _sel.Height;
                var f = Theme.UISmall;
                using (var b = new SolidBrush(Color.FromArgb(16, 19, 26)))
                {
                    SizeF sz = g.MeasureString(label, f);
                    var r = new RectangleF(_sel.Left, Math.Max(0, _sel.Top - sz.Height - 6), sz.Width + 12, sz.Height + 4);
                    g.FillRectangle(b, r);
                    g.DrawString(label, f, Brushes.White, r.X + 6, r.Y + 2);
                }
            }
            else
            {
                using (var f = new Font("Microsoft YaHei UI", 12f))
                using (var b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    string tip = "拖动框选区域 · Esc 取消";
                    SizeF sz = g.MeasureString(tip, f);
                    g.DrawString(tip, f, b, (ClientSize.Width - sz.Width) / 2, 60);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _dragging = true;
            _start = e.Location;
            _sel = new Rectangle(e.Location, Size.Empty);
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                int x = Math.Min(_start.X, e.X);
                int y = Math.Min(_start.Y, e.Y);
                _sel = new Rectangle(x, y, Math.Abs(e.X - _start.X), Math.Abs(e.Y - _start.Y));
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            if (_sel.Width < 8 || _sel.Height < 8)
            {
                Close();
                return;
            }
            Bitmap crop = null;
            try
            {
                crop = new Bitmap(_sel.Width, _sel.Height);
                using (var g = Graphics.FromImage(crop))
                    g.DrawImage(_screen, new Rectangle(0, 0, _sel.Width, _sel.Height), _sel, GraphicsUnit.Pixel);
            }
            catch { crop = null; }

            Point at = new Point(_bounds.Left + _sel.Left, _bounds.Top + _sel.Top);
            Close();
            if (crop != null && _done != null) _done(crop, at);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _screen != null)
            {
                try { _screen.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
