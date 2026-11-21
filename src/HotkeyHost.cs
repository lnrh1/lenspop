using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>全局热键：注册到一个消息窗口上</summary>
    public class HotkeyHost : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint MOD_WIN = 0x0008;
        const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
        readonly List<int> _ids = new List<int>();
        int _next = 1;

        public HotkeyHost()
        {
            var cp = new CreateParams();
            cp.Caption = "LensPopHotkeys";
            cp.X = -100;
            cp.Y = -100;
            cp.Width = 1;
            cp.Height = 1;
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(string spec, Action action)
        {
            uint mods;
            uint vk;
            if (!Parse(spec, out mods, out vk)) return false;
            int id = _next++;
            if (!RegisterHotKey(Handle, id, mods | MOD_NOREPEAT, vk)) return false;
            _actions[id] = action;
            _ids.Add(id);
            return true;
        }

        public static bool Parse(string spec, out uint mods, out uint vk)
        {
            mods = 0;
            vk = 0;
            if (string.IsNullOrEmpty(spec)) return false;
            string[] parts = spec.Split('+');
            foreach (string raw in parts)
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                string u = p.ToUpperInvariant();
                if (u == "CTRL" || u == "CONTROL") { mods |= MOD_CONTROL; continue; }
                if (u == "ALT") { mods |= MOD_ALT; continue; }
                if (u == "SHIFT") { mods |= MOD_SHIFT; continue; }
                if (u == "WIN") { mods |= MOD_WIN; continue; }
                if (u.Length == 1 && u[0] >= 'A' && u[0] <= 'Z') { vk = u[0]; continue; }
                if (u.Length == 1 && u[0] >= '0' && u[0] <= '9') { vk = u[0]; continue; }
                if (u == "F1") { vk = 0x70; continue; }
                if (u == "F2") { vk = 0x71; continue; }
                if (u == "F3") { vk = 0x72; continue; }
                if (u == "F4") { vk = 0x73; continue; }
                if (u == "F5") { vk = 0x74; continue; }
                if (u == "F6") { vk = 0x75; continue; }
                if (u == "F7") { vk = 0x76; continue; }
                if (u == "F8") { vk = 0x77; continue; }
                if (u == "F9") { vk = 0x78; continue; }
                if (u == "F10") { vk = 0x79; continue; }
                if (u == "F11") { vk = 0x7A; continue; }
                if (u == "F12") { vk = 0x7B; continue; }
                if (u == "PRINT") { vk = 0x2C; continue; }
                if (u == "INSERT") { vk = 0x2D; continue; }
                return false;
            }
            return vk != 0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                Action a;
                if (_actions.TryGetValue(id, out a))
                {
                    try { a(); } catch { }
                }
                return;
            }
            base.WndProc(ref m);
        }

        public void UnregisterAll()
        {
            foreach (int id in _ids) UnregisterHotKey(Handle, id);
            _ids.Clear();
            _actions.Clear();
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }
    }
}
