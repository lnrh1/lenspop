using System;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>框选屏幕区域（截图 OCR / 图片翻译都用它）</summary>
    public class CaptureForm : Form
    {
        readonly Bitmap _screen;
        readonly Rectangle _bounds;
        readonly Action<Bitmap, Point> _done;

        bool _dragging;
        Point _start;
        Rectangle _sel;

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
            Bounds = _bounds;
            _screen = new Bitmap(Math.Max(1, _bounds.Width), Math.Max(1, _bounds.Height));
            using (var g = Graphics.FromImage(_screen))
                g.CopyFromScreen(_bounds.Left, _bounds.Top, 0, 0, _bounds.Size);
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
                var f = Theme.UISmall;          // 共享字体，绝对不能 Dispose
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
                int w = Math.Abs(e.X - _start.X);
                int h = Math.Abs(e.Y - _start.Y);
                _sel = new Rectangle(x, y, w, h);
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
            catch
            {
                crop = null;
            }
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
