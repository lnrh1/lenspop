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
        int _lastUpTick;
        Point _lastUpPoint;

        public event Action<Point> Clicked;      // 左键按下
        public event Action<Point> DragEnded;    // 拖拽后松开
        public event Action<Point> DoubleClicked; // 双击选词
        public static Action<int, int, int> RawEvent;   // 调试用：消息、x、y
        public static int EventCount;                   // 收到多少鼠标事件
        public static int ClickCount;                   // 左键按下次数
        public static int DragCount;                    // 拖拽结束次数

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
                System.Threading.Interlocked.Increment(ref EventCount);
                var raw = RawEvent;
                if (raw != null) { try { raw(msg, info.pt.X, info.pt.Y); } catch { } }
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                {
                    System.Threading.Interlocked.Increment(ref ClickCount);
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
                    int nowTick = Environment.TickCount;
                    if (Math.Abs(dx) > 10 || Math.Abs(dy) > 10)
                    {
                        System.Threading.Interlocked.Increment(ref DragCount);
                        var up = new Point(info.pt.X, info.pt.Y);
                        var dragEnded = DragEnded;
                        if (dragEnded != null)
                        {
                            try { dragEnded(up); } catch { }
                        }
                    }
                    else if (nowTick - _lastUpTick < 450 &&
                             Math.Abs(info.pt.X - _lastUpPoint.X) < 5 &&
                             Math.Abs(info.pt.Y - _lastUpPoint.Y) < 5)
                    {
                        var up2 = new Point(info.pt.X, info.pt.Y);
                        var dbl = DoubleClicked;
                        if (dbl != null)
                        {
                            try { dbl(up2); } catch { }
                        }
                    }
                    _lastUpTick = nowTick;
                    _lastUpPoint = new Point(info.pt.X, info.pt.Y);
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

    /// <summary>用 Ctrl+C 把当前选中文本抓出来</summary>
    public static class SelectionGrabber
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct INPUT
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public uint type;
            [System.Runtime.InteropServices.FieldOffset(8)] public KEYBDINPUT ki;
            [System.Runtime.InteropServices.FieldOffset(8)] public MOUSEINPUT mi;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint n, INPUT[] inputs, int size);

        const uint INPUT_KEYBOARD = 1;
        const ushort VK_CONTROL = 0x11;
        const ushort VK_C = 0x43;
        const uint KEYEVENTF_KEYUP = 0x0002;

        static void SendCtrlC()
        {
            var arr = new INPUT[4];
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));

            arr[0].type = INPUT_KEYBOARD;
            arr[0].ki.wVk = VK_CONTROL;
            arr[1].type = INPUT_KEYBOARD;
            arr[1].ki.wVk = VK_C;
            arr[2].type = INPUT_KEYBOARD;
            arr[2].ki.wVk = VK_C;
            arr[2].ki.dwFlags = KEYEVENTF_KEYUP;
            arr[3].type = INPUT_KEYBOARD;
            arr[3].ki.wVk = VK_CONTROL;
            arr[3].ki.dwFlags = KEYEVENTF_KEYUP;

            uint r = SendInput(4, arr, size);
            if (r == 0)
                Program.LogLine("SendInput 失败 err=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// 划词主路径：不再预写“哨兵”值（浏览器/Electron 在剪贴板刚被动过时会放弃复制），
        /// 改成「读旧值 → 发 Ctrl+C → 等变化；第二次仍未变化就直接采用剪贴板内容」。
        /// </summary>
        /// <summary>把剪贴板常见格式复制一份，取完词后原样还回去</summary>
        static DataObject SnapshotClipboard()
        {
            try
            {
                var d = new DataObject();
                bool any = false;
                if (Clipboard.ContainsText()) { d.SetData(DataFormats.UnicodeText, Clipboard.GetText()); any = true; }
                if (Clipboard.ContainsText(TextDataFormat.Rtf)) { d.SetData(DataFormats.Rtf, Clipboard.GetText(TextDataFormat.Rtf)); any = true; }
                if (Clipboard.ContainsText(TextDataFormat.Html)) { d.SetData(DataFormats.Html, Clipboard.GetText(TextDataFormat.Html)); any = true; }
                if (Clipboard.ContainsFileDropList()) { d.SetFileDropList(Clipboard.GetFileDropList()); any = true; }
                if (Clipboard.ContainsImage()) { var img = Clipboard.GetImage(); if (img != null) { d.SetImage(img); any = true; } }
                return any ? d : null;
            }
            catch { return null; }
        }

        static void RestoreClipboard(DataObject backup)
        {
            if (backup == null) return;
            for (int i = 0; i < 4; i++)
            {
                try { Clipboard.SetDataObject(backup, true); return; }
                catch { System.Threading.Thread.Sleep(40); }
            }
            Program.LogLine("还原剪贴板失败");
        }
        public static string GrabOnUiThread(int timeoutMs, bool allowUia)
        {
            string before = null;
            try { if (Clipboard.ContainsText()) before = Clipboard.GetText(); }
            catch { before = null; }
            DataObject backup = SnapshotClipboard();
            System.Threading.Interlocked.Increment(ref Program.ClipboardQuiet);
            try { return GrabCore(timeoutMs, allowUia, before); }
            finally { RestoreClipboard(backup); try { Program.OwnClipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { } Program.ClipboardQuietUntil = Environment.TickCount + 100; Program.IgnoreClipboardUpdates = 2; System.Threading.Interlocked.Decrement(ref Program.ClipboardQuiet); }
        }

        static string GrabCore(int timeoutMs, bool allowUia, string before)
        {

            for (int attempt = 0; attempt < 2; attempt++)
            {
                int settle = attempt == 0 ? 45 : 120;
                var sw0 = System.Diagnostics.Stopwatch.StartNew();
                while (sw0.ElapsedMilliseconds < settle)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(5);
                }

                SendCtrlC();

                int waitMs = attempt == 0 ? 400 : 550;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < waitMs)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(15);
                    try
                    {
                        if (!Clipboard.ContainsText()) continue;
                        string now = Clipboard.GetText();
                        if (string.IsNullOrEmpty(now)) continue;
                        if (before != null && now == before) continue;   // 剪贴板没变化 = 没有复制动作（拖窗口/滚动条），继续等或换 UIA
                        Program.LogLine("grab(ui): 成功 " + now.Length + " 字（第" + (attempt + 1) + "次）");
                        return now;
                    }
                    catch { }
                }
                Program.LogLine("grab(ui): 第" + (attempt + 1) + "次没取到");
            }

            if (!allowUia)
            {
                Program.LogLine("grab(ui): 两次都没拿到新文字，按无取词处理");
                return null;
            }
            Program.LogLine("grab(ui): 剪贴板两次都失败，改用 UI Automation");
            string uia = UiaGrab.TryGetSelection(600);
            if (!string.IsNullOrEmpty(uia))
            {
                Program.LogLine("grab(uia): 成功 " + uia.Length + " 字");
                return uia;
            }
            Program.LogLine("grab(ui): 全部失败");
            return null;
        }

        public static string Grab(int timeoutMs)
        {
            string result = null;
            var t = new System.Threading.Thread(delegate()
            {
                try { result = GrabOnUiThread(timeoutMs, true); }
                catch { result = null; }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            t.Join(timeoutMs + 1500);
            return result;
        }
    }
}
