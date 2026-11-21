using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>全局鼠标钩子：用于划词检测；顺带在需要时模拟 Ctrl+C 取选中文本</summary>
    public class MouseHook : IDisposable
    {
        const int WH_MOUSE_LL = 14;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204;

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct HookPoint { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT
        {
            public HookPoint pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);

        HookProc _proc;
        IntPtr _hook = IntPtr.Zero;
        Point _down;
        bool _downValid;

        public event Action<Point> Clicked;      // 左键按下
        public event Action<Point> DragEnded;    // 拖拽后松开

        public void Install()
        {
            _proc = HookCallback;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        }

        public void Uninstall()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                int msg = (int)wParam;
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                {
                    _down = new Point(info.pt.X, info.pt.Y);
                    _downValid = true;
                    var clicked = Clicked;
                    if (clicked != null)
                    {
                        try { clicked(_down); } catch { }
                    }
                }
                else if (msg == WM_LBUTTONUP && _downValid)
                {
                    _downValid = false;
                    int dx = info.pt.X - _down.X;
                    int dy = info.pt.Y - _down.Y;
                    if (Math.Abs(dx) > 6 || Math.Abs(dy) > 6)
                    {
                        var up = new Point(info.pt.X, info.pt.Y);
                        var dragEnded = DragEnded;
                        if (dragEnded != null)
                        {
                            try { dragEnded(up); } catch { }
                        }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Uninstall();
        }
    }

    /// <summary>用 Ctrl+C 把当前选中文本抓出来（在独立 STA 线程里做，避免卡 UI）</summary>
    public static class SelectionGrabber
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const byte VK_CONTROL = 0x11;
        const byte VK_C = 0x43;
        const uint KEYUP = 0x0002;

        public static string Grab(int timeoutMs)
        {
            string result = null;
            var t = new Thread(delegate()
            {
                try { result = GrabOnSta(timeoutMs); }
                catch { result = null; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            t.Join(timeoutMs + 800);
            return result;
        }

        static string GrabOnSta(int timeoutMs)
        {
            string before = null;
            try { if (Clipboard.ContainsText()) before = Clipboard.GetText(); }
            catch { before = null; }

            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(25);
                try
                {
                    if (!Clipboard.ContainsText()) continue;
                    string now = Clipboard.GetText();
                    if (string.IsNullOrEmpty(now)) continue;
                    if (before != null && now == before) continue;
                    return now;
                }
                catch { }
            }
            return null;
        }
    }
}
