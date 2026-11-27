using System;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>询问前的单行输入条：预填带引号的划词内容，回车发送</summary>
    public class AskDialog : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        public string Result = "";
        public string CurrentText { get { return _box.Text; } }
        public bool Sent;
        public TextBox InputBox { get { return _box; } }
        public bool Finished;
        KeyCapture _hook;
        MouseWatch _mouse;
        DateTime _shownAt = DateTime.MinValue;
        bool _hookedOnce;

        readonly TextBox _box = new TextBox();

        public AskDialog(string preset)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.White;
            Font = Theme.UI;
            Size = new Size(680, 54);
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(14, 12, 96, 12);

            _box.BorderStyle = BorderStyle.None;
            _box.Font = new Font("Microsoft YaHei UI", 10.5f);
            _box.BackColor = Color.White;
            _box.ForeColor = Theme.Fg;
            _box.Dock = DockStyle.Fill;
            _box.Text = preset == null ? "“”" : ("“" + preset.Trim() + "”");
            _box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(); }
                else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };
            Controls.Add(_box);

            var send = Theme.FlatButton("发送", true);
            send.Width = 64;
            send.Height = 30;
            send.SetBounds(Width - 78, 12, 64, 30);
            send.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            send.Click += delegate { Send(); };
            Controls.Add(send);

            // 左侧品牌色小竖条
            var stripe = new Panel();
            stripe.BackColor = Theme.Brand;
            stripe.SetBounds(0, 0, 3, Height);
            stripe.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(stripe);

            Load += delegate
            {
                Theme.Round(this, 10);
                Theme.ForceForeground(this);
            };
            Resize += delegate { Theme.Round(this, 10); };

            // 抢不到前台时的兜底：用低级键盘钩子把按键接过来写进输入框
            _hook = new KeyCapture();
            _hook.ShouldCapture = delegate
            {
                if (IsDisposed || !Visible) return false;
                // 窗口还开着就一直兜底；点到窗口外面会自动关闭，所以不会误吃别的程序输入
                if ((DateTime.Now - _shownAt).TotalSeconds > 60) return false;
                return GetForegroundWindow() != Handle;   // 前台是我们自己就交给系统正常输入
            };
            _hook.OnChar = delegate(char ch)
            {
                if (!_hookedOnce)
                {
                    _hookedOnce = true;
                    Program.LogLine("输入条：键盘钩子接管输入（窗口未获前台）");
                }
                _box.Text = _box.Text.Insert(_box.SelectionStart, ch.ToString());
                _box.SelectionStart = _box.TextLength;
            };
            _hook.OnBackspace = delegate
            {
                if (_box.TextLength > 0) _box.Text = _box.Text.Substring(0, _box.TextLength - 1);
                _box.SelectionStart = _box.TextLength;
            };
            _hook.OnEnter = delegate { Send(); };
            _hook.OnEscape = delegate { Finished = true; DialogResult = DialogResult.Cancel; Close(); };
            _hook.Install();
            // 点到窗口外面就关掉输入条，避免继续接管键盘
            _mouse = new MouseWatch();
            _mouse.OnLeftDown = delegate(Point p)
            {
                try
                {
                    if (!Bounds.Contains(p)) { Finished = true; Close(); }
                }
                catch { }
            };
            _mouse.Install();

            Shown += delegate
            {
                _shownAt = DateTime.Now;
                _box.SelectionStart = _box.TextLength;
                Theme.ForceForeground(this);
                Theme.FocusControl(this, _box);

                // 窗口刚映射时前台可能还没切过来，延时再抢两次焦点
                foreach (int delay in new int[] { 100, 300 })
                {
                    var t = new System.Windows.Forms.Timer();
                    t.Interval = delay;
                    t.Tick += delegate
                    {
                        t.Stop();
                        t.Dispose();
                        Theme.FocusControl(this, _box);
                        _box.SelectionStart = _box.TextLength;
                    };
                    t.Start();
                }
            };
        }

        // 单行输入框里回车/ESC 交给窗体处理，避免被 TextBox 吞掉
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { Send(); return true; }
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        // 兜底：即使焦点没落到输入框，窗体收到按键也直接写进输入框
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (_box.Focused || e.KeyChar < 32) return;
            _box.Text = _box.Text.Insert(_box.SelectionStart, e.KeyChar.ToString());
            _box.SelectionStart = _box.TextLength;
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_box.Focused) return;
            if (e.KeyCode == Keys.Back)
            {
                if (_box.TextLength > 0) _box.Text = _box.Text.Substring(0, _box.TextLength - 1);
                _box.SelectionStart = _box.TextLength;
                e.Handled = true;
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { if (_hook != null) { _hook.Dispose(); _hook = null; } } catch { }
            try { if (_mouse != null) { _mouse.Dispose(); _mouse = null; } } catch { }
            Finished = true;
            base.OnFormClosed(e);
        }

        void Send()
        {
            string text = _box.Text == null ? "" : _box.Text.Trim();
            // 去掉外层引号再交给模型
            if (text.Length >= 2)
            {
                bool lq = text[0] == '“' || text[0] == '"';
                bool rq = text[text.Length - 1] == '”' || text[text.Length - 1] == '"';
                if (lq && rq) text = text.Substring(1, text.Length - 2).Trim();
            }
            if (text.Length == 0) { Close(); return; }
            Result = text;
            Sent = true;
            Finished = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Line))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
