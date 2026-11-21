using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>划词后的小胶囊：先显示「取词中…」，拿到文字再展开按钮</summary>
    public class PopupForm : Form
    {
        public event EventHandler TranslateClicked;
        public event EventHandler AskClicked;
        public event EventHandler SearchClicked;
        public event EventHandler SettingsClicked;

        public string SelectionText = "";

        protected override bool ShowWithoutActivation { get { return true; } }

        readonly FlowLayoutPanel _flow = new FlowLayoutPanel();
        readonly Label _status = new Label();
        readonly List<Button> _buttons = new List<Button>();
        readonly Timer _autoHide = new Timer();
        int _readyWidth;

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(16, 19, 26);
            DoubleBuffered = true;
            Size = new Size(150, 36);

            _flow.Dock = DockStyle.Fill;
            _flow.WrapContents = false;
            _flow.Padding = new Padding(4);
            _flow.BackColor = BackColor;
            Controls.Add(_flow);

            _buttons.Add(MakeButton("译  翻译", delegate { Raise(TranslateClicked); }));
            _buttons.Add(MakeButton("问  询问", delegate { Raise(AskClicked); }));
            _buttons.Add(MakeButton("搜  搜索", delegate { Raise(SearchClicked); }));
            var gear = MakeButton("⚙", delegate { Raise(SettingsClicked); });
            gear.Width = 30;
            gear.ForeColor = Color.FromArgb(170, 180, 198);
            _buttons.Add(gear);

            foreach (var b in _buttons) _flow.Controls.Add(b);

            _status.AutoSize = false;
            _status.Height = 28;
            _status.Width = 230;
            _status.TextAlign = ContentAlignment.MiddleCenter;
            _status.ForeColor = Color.FromArgb(233, 237, 245);
            _status.Font = Theme.UI;
            _status.BackColor = BackColor;
            _status.Text = "取词中…";
            _flow.Controls.Add(_status);

            int total = 8;
            foreach (var b in _buttons) total += b.Width + b.Margin.Horizontal;
            _readyWidth = total;

            _autoHide.Interval = 2000;
            _autoHide.Tick += delegate { _autoHide.Stop(); Hide(); };

            Load += delegate { Theme.Round(this, 10); Theme.NoActivate(this); };
        Resize += delegate { Theme.Round(this, 10); };
        }

        Button MakeButton(string text, Action onClick)
        {
            var b = new Button();
            b.Text = text;
            b.AutoSize = false;
            b.Height = 28;
            b.Width = text.Length > 2 ? 80 : 30;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = BackColor;
            b.ForeColor = Color.FromArgb(233, 237, 245);
            b.Font = Theme.UI;
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(1, 0, 1, 0);
            b.TabStop = false;
            b.MouseEnter += delegate { b.BackColor = Color.FromArgb(45, 50, 62); };
            b.MouseLeave += delegate { b.BackColor = BackColor; };
            b.Click += delegate
            {
                Hide();
                try { onClick(); } catch { }
            };
            return b;
        }

        void Raise(EventHandler h)
        {
            if (h != null) h(this, EventArgs.Empty);
        }

        static void ShowButtons(List<Button> list, bool on)
        {
            foreach (var b in list) b.Visible = on;
        }

        void PlaceAt(Point screenPoint, int width)
        {
            Width = width;
            var scr = Screen.FromPoint(screenPoint);
            int left = screenPoint.X - width / 2;
            int top = screenPoint.Y - Height - 12;
            if (top < scr.WorkingArea.Top) top = screenPoint.Y + 18;
            left = Math.Max(scr.WorkingArea.Left + 4, Math.Min(left, scr.WorkingArea.Right - width - 4));
            top = Math.Max(scr.WorkingArea.Top + 4, Math.Min(top, scr.WorkingArea.Bottom - Height - 4));
            Location = new Point(left, top);
            if (!Visible) Show();
            Theme.Round(this, 10);
            // 不抢焦点：否则接下来的 Ctrl+C 会发到我们自己身上，取不到选中文字
            Theme.NoActivate(this);
            TopMost = true;
            BringToFront();
        }

        /// <summary>松手立刻显示：此时还没有文字</summary>
        public void ShowLoading(Point screenPoint)
        {
            _autoHide.Stop();
            _status.Text = "取词中…";
            _status.Visible = true;
            ShowButtons(_buttons, false);
            SelectionText = "";
            PlaceAt(screenPoint, 150);
        }

        /// <summary>取到文字，展开按钮</summary>
        public void Ready(Point screenPoint, string text)
        {
            _autoHide.Stop();
            SelectionText = text;
            _status.Visible = false;
            ShowButtons(_buttons, true);
            PlaceAt(screenPoint, _readyWidth);
        }

        /// <summary>没取到：给个明确提示，2 秒后自动消失</summary>
        public void Fail(Point screenPoint, string message)
        {
            _status.Text = message;
            _status.Visible = true;
            ShowButtons(_buttons, false);
            PlaceAt(screenPoint, 280);
            _autoHide.Start();
        }
    }
}
