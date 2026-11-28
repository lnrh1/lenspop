using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>框选屏幕区域，支持像截图工具那样吸附窗口</summary>
    public class CaptureForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hdc, int index);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20;
        const uint GA_ROOT = 2;
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        const int DESKTOPHORZRES = 118;
        const int DESKTOPVERTRES = 117;

        readonly Action<Bitmap, Point> _done;
        Bitmap _screen;
        Rectangle _bounds;
        Size _physical = Size.Empty;
        bool _dragging;
        Point _start;
        Rectangle _sel;
        Rectangle _hover = Rectangle.Empty;

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

            int ownDpi = 96;
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) ownDpi = (int)g.DpiX; }
            catch { }
            if (ownDpi <= 96)
            {
                Size phys = PhysicalScreen();
                if (phys.Width > _bounds.Width + 8 || phys.Height > _bounds.Height + 8) _physical = phys;
            }

            Bounds = _bounds;
            Grab();
            Shown += delegate { AdaptToPhysicalScreen(); };
        }

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
                if (r.Right - r.Left >= _physical.Width - 8) return;
                _bounds = new Rectangle(0, 0, _physical.Width, _physical.Height);
                Bounds = _bounds;
                Grab();
                Invalidate();
            }
            catch { }
        }

        /// <summary>鼠标下的顶层窗口矩形（换算到客户区）；探测时先把自己设成穿透，避免只探到遮罩</summary>
        Rectangle WindowRectAt(Point clientPt)
        {
            try
            {
                int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                SetWindowLong(Handle, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT);
                var sp = new POINT();
                sp.X = _bounds.Left + clientPt.X;
                sp.Y = _bounds.Top + clientPt.Y;
                IntPtr hwnd = WindowFromPoint(sp);
                SetWindowLong(Handle, GWL_EXSTYLE, ex);

                if (hwnd == IntPtr.Zero || hwnd == Handle) return Rectangle.Empty;
                IntPtr root = GetAncestor(hwnd, GA_ROOT);
                if (root == IntPtr.Zero || root == Handle) return Rectangle.Empty;

                RECT r;
                if (DwmGetWindowAttribute(root, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                {
                    if (!GetWindowRect(root, out r)) return Rectangle.Empty;
                }
                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;
                if (w < 24 || h < 24) return Rectangle.Empty;
                if (w >= _bounds.Width && h >= _bounds.Height) return Rectangle.Empty;

                var rect = new Rectangle(r.Left - _bounds.Left, r.Top - _bounds.Top, w, h);
                rect.Intersect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                if (rect.Width < 24 || rect.Height < 24) return Rectangle.Empty;
                return rect;
            }
            catch { return Rectangle.Empty; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImage(_screen, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.FillRectangle(dim, ClientRectangle);

            var showRect = (_sel.Width > 0 && _sel.Height > 0) ? _sel : (_dragging ? Rectangle.Empty : _hover);
            if (showRect.Width > 8 && showRect.Height > 8)
            {
                g.DrawImage(_screen, showRect, showRect, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(79, 70, 229), 2))
                    g.DrawRectangle(pen, showRect.Left, showRect.Top, showRect.Width - 1, showRect.Height - 1);
                string label = showRect.Width + " × " + showRect.Height;
                var f = Theme.UISmall;
                using (var b = new SolidBrush(Color.FromArgb(16, 19, 26)))
                {
                    SizeF sz = g.MeasureString(label, f);
                    var r = new RectangleF(showRect.Left, Math.Max(0, showRect.Top - sz.Height - 6), sz.Width + 12, sz.Height + 4);
                    g.FillRectangle(b, r);
                    g.DrawString(label, f, Brushes.White, r.X + 6, r.Y + 2);
                }
            }
            else
            {
                using (var f = new Font("Microsoft YaHei UI", 12f))
                using (var b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    string tip = "拖动框选区域 · 单击吸附窗口 · Esc 取消";
                    SizeF sz = g.MeasureString(tip, f);
                    g.DrawString(tip, f, b, (ClientSize.Width - sz.Width) / 2, 60);
                }
            }
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
            else
            {
                var r = WindowRectAt(e.Location);
                if (r != _hover)
                {
                    _hover = r;
                    Invalidate();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _dragging = true;
            _start = e.Location;
            _sel = new Rectangle(e.Location, Size.Empty);
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            var target = _sel;
            if (target.Width < 8 || target.Height < 8)
            {
                target = WindowRectAt(e.Location);
                if (target.Width < 8 || target.Height < 8)
                {
                    Close();
                    return;
                }
            }

            Bitmap crop = null;
            try
            {
                crop = new Bitmap(target.Width, target.Height);
                using (var g = Graphics.FromImage(crop))
                    g.DrawImage(_screen, new Rectangle(0, 0, target.Width, target.Height), target, GraphicsUnit.Pixel);
            }
            catch { crop = null; }

            Point at = new Point(_bounds.Left + target.Left, _bounds.Top + target.Top);
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
