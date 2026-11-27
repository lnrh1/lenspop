using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LensPop
{
    /// <summary>临时键盘钩子：输入条抢不到焦点时，也把用户敲的字接过来</summary>
    public class KeyCapture : IDisposable
    {
        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll")]
        static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);

        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104;

        HookProc _proc;
        IntPtr _hook = IntPtr.Zero;

        public Func<bool> ShouldCapture;
        public Action<char> OnChar;
        public Action OnBackspace;
        public Action OnEnter;
        public Action OnEscape;

        public bool Installed { get { return _hook != IntPtr.Zero; } }

        public bool Install()
        {
            _proc = Callback;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    bool capture = ShouldCapture != null && ShouldCapture();
                    if (capture)
                    {
                        int vk = Marshal.ReadInt32(lParam);
                        if (vk == 13) { if (OnEnter != null) OnEnter(); return (IntPtr)1; }
                        if (vk == 27) { if (OnEscape != null) OnEscape(); return (IntPtr)1; }
                        if (vk == 8) { if (OnBackspace != null) OnBackspace(); return (IntPtr)1; }

                        var sb = new StringBuilder(8);
                        var state = new byte[256];
                        if (GetKeyboardState(state))
                        {
                            int n = ToUnicode((uint)vk, 0, state, sb, sb.Capacity, 0);
                            if (n > 0)
                            {
                                char ch = sb[0];
                                if (ch >= 32 && OnChar != null) { OnChar(ch); return (IntPtr)1; }
                            }
                        }
                    }
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    /// <summary>监听一次左键按下：点在窗口外就关掉输入条，避免继续抢键
    public class MouseWatch : IDisposable
    {
        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);

        const int WH_MOUSE_LL = 14;
        const int WM_LBUTTONDOWN = 0x0201;

        HookProc _proc;
        IntPtr _hook = IntPtr.Zero;
        public Action<System.Drawing.Point> OnLeftDown;

        public bool Install()
        {
            _proc = Callback;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && OnLeftDown != null)
                {
                    int x = Marshal.ReadInt32(lParam);
                    int y = Marshal.ReadInt32(lParam, 4);
                    OnLeftDown(new System.Drawing.Point(x, y));
                }
            }
            catch { }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}
