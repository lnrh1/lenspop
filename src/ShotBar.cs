using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>
    /// 截图后的小操作条：复制 / 提取文字 / 翻译。
    /// 整窗自绘、自己处理鼠标点击（不用子控件），避免不激活窗口 + 圆角 Region 造成点不到。
    /// </summary>
    public class ShotBarForm : Form
    {
        public event EventHandler CopyClicked;
        public event EventHandler OcrClicked;
        public event EventHandler TranslateClicked;

        static readonly string[] Labels = { "复制", "提取文字", "翻译" };
        static readonly int[] Widths = { 64, 92, 64 };

        int _hover = -1;
        int _lastFireTick;
        readonly Timer _autoHide = new Timer();

        public ShotBarForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(16, 19, 26);
            DoubleBuffered = true;
            Height = 34;
            int total = 8;
            foreach (int w in Widths) total += w;
            Width = total;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            _autoHide.Interval = 6000;
            _autoHide.Tick += delegate { _autoHide.Stop(); Hide(); };
        }

        int SegmentX(int index)
        {
            int x = 4;
            for (int i = 0; i < index; i++) x += Widths[i];
            return x;
        }

        int HitTest(Point p)
        {
            int x = 4;
            for (int i = 0; i < Widths.Length; i++)
            {
                if (p.X >= x && p.X < x + Widths[i] && p.Y >= 3 && p.Y < Height - 3) return i;
                x += Widths[i];
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var f = Theme.UI;

            for (int i = 0; i < Widths.Length; i++)
            {
                int x = SegmentX(i);
                var r = new Rectangle(x, 3, Widths[i], Height - 6);
                if (i == _hover)
                {
                    using (var b = new SolidBrush(Color.FromArgb(52, 58, 72)))
                    {
                        var path = RoundRect(r, 7);
                        g.FillPath(b, path);
                        path.Dispose();
                    }
                }
                using (var tb = new SolidBrush(Color.FromArgb(233, 237, 245)))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Labels[i], f, tb, r, sf);
                }
            }
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != _hover)
            {
                _hover = h;
                Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0) Fire(h);
            base.OnMouseDown(e);
        }

        public int SegmentAt(Point screenPoint)
        {
            return HitTest(PointToClient(screenPoint));
        }

        public void Fire(int index)
        {
            int now = Environment.TickCount;
            if (now - _lastFireTick < 300) return;
            _lastFireTick = now;
            Hide();
            _autoHide.Stop();
            EventHandler handler = index == 0 ? CopyClicked : (index == 1 ? OcrClicked : TranslateClicked);
            Program.LogLine("点击截图浮条: " + (index >= 0 && index < Labels.Length ? Labels[index] : "?"));
            if (handler != null) { try { handler(this, EventArgs.Empty); } catch { } }
        }

        public void ShowAt(Point screenPoint)
        {
            var scr = Screen.FromPoint(screenPoint);
            int left = screenPoint.X;
            int top = screenPoint.Y + 12;
            if (top + Height > scr.WorkingArea.Bottom) top = screenPoint.Y - Height - 12;
            left = Math.Max(scr.WorkingArea.Left + 4, Math.Min(left, scr.WorkingArea.Right - Width - 4));
            top = Math.Max(scr.WorkingArea.Top + 4, top);
            Location = new Point(left, top);
            Show();
            Activate();
            TopMost = true;
            BringToFront();
            Theme.Round(this, 8);
            _autoHide.Stop();
            _autoHide.Start();
            Program.LogLine("浮条显示 " + Bounds);
        }
    }
}
