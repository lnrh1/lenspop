using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>复制后的小提示窗（右下角淡出）</summary>
    public class ToastForm : Form
    {
        readonly string _text;
        readonly Timer _timer = new Timer();

        public ToastForm(string text)
        {
            _text = text;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(23, 27, 35);
            DoubleBuffered = true;
            Size = new Size(150, 40);
            Opacity = 0.96;

            _timer.Interval = 1800;
            _timer.Tick += delegate { _timer.Stop(); Hide(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(52, 211, 153), 1.4f))
                e.Graphics.DrawEllipse(pen, 12, 13, 13, 13);
            using (var pen = new Pen(Color.FromArgb(52, 211, 153), 1.6f))
            {
                e.Graphics.DrawLine(pen, 15, 19, 18, 22);
                e.Graphics.DrawLine(pen, 18, 22, 23, 16);
            }
            using (var f = new Font("Microsoft YaHei UI", 9.5f))
            using (var b = new SolidBrush(Color.FromArgb(236, 240, 246)))
                e.Graphics.DrawString(_text, f, b, 32, 11);
        }

        public void ShowToast(Point anchor)
        {
            var scr = Screen.FromPoint(anchor);
            int left = scr.WorkingArea.Right - Width - 24;
            int top = scr.WorkingArea.Bottom - Height - 24;
            Location = new Point(left, top);
            Show();
            Theme.Round(this, 10);
            Theme.NoActivate(this);
            TopMost = true;
            BringToFront();
            _timer.Stop();
            _timer.Start();
        }
    }

    /// <summary>监听系统剪贴板变化：别人（或用户手动）复制后弹一个小提示</summary>
    public class ClipboardWatcher : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        const int WM_CLIPBOARDUPDATE = 0x031D;

        ToastForm _toast;
        bool _started;

        public void Start()
        {
            var cp = new CreateParams();
            cp.Caption = "LensPopClipboardWatcher";
            cp.Style = 0;
            cp.ExStyle = 0x00000080 | 0x08000000;   // TOOLWINDOW | NOACTIVATE
            CreateHandle(cp);
            _started = AddClipboardFormatListener(Handle);
            Program.LogLine("剪贴板监听=" + _started);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                try { OnChanged(); } catch { }
            }
            base.WndProc(ref m);
        }

        void OnChanged()
        {
            if (Program.ClipboardQuiet > 0) return;            // 正在动剪贴板
            if (System.Threading.Interlocked.CompareExchange(ref Program.IgnoreClipboardUpdates, 0, 0) > 0)
            {
                System.Threading.Interlocked.Decrement(ref Program.IgnoreClipboardUpdates);
                return;
            }
            if (Environment.TickCount < Program.ClipboardQuietUntil) return;   // 刚被我们自己改过（取词/截图）
            try
            {
                if (Program.OwnClipboardText != null && Clipboard.ContainsText() && Clipboard.GetText() == Program.OwnClipboardText) return;
            }
            catch { }
            // 自己窗口在前台时的复制也不用提示
            try
            {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return;
            }
            catch { }
            bool has = false;
            try { has = Clipboard.ContainsText() || Clipboard.ContainsImage() || Clipboard.ContainsFileDropList(); }
            catch { has = false; }
            if (!has) return;

            if (_toast == null || _toast.IsDisposed) _toast = new ToastForm("已复制到剪贴板");
            var pos = Cursor.Position;
            _toast.ShowToast(pos);
        }

        public void Dispose()
        {
            try { if (_started) RemoveClipboardFormatListener(Handle); } catch { }
            try { DestroyHandle(); } catch { }
            try { if (_toast != null) _toast.Dispose(); } catch { }
        }
    }
}
