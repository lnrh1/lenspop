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
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        /// <summary>前台窗口是不是自己的进程（自己的界面里不该触发划词）</summary>
        public static bool ForegroundIsSelf(int myPid)
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                if (h == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                return (int)pid == myPid;
            }
            catch { return false; }
        }

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
        public static Action<int, int, int> RawEvent;   // 调试用：消息、x、y

        public bool Installed { get { return _hook != IntPtr.Zero; } }
        public int LastError { get { return Marshal.GetLastWin32Error(); } }

        public bool Install()
        {
            _proc = HookCallback;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
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
                var raw = RawEvent;
                if (raw != null) { try { raw(msg, info.pt.X, info.pt.Y); } catch { } }
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

    /// <summary>钩子线程用的原生消息泵（WinForms 的 Application.Run 在非主线程上不可靠）</summary>
    public static class MessagePump
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct WinMsg
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern int GetMessage(out WinMsg msg, IntPtr hWnd, uint min, uint max);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref WinMsg msg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref WinMsg msg);

        public static void Run()
        {
            WinMsg msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
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
