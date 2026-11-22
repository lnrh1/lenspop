using System;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>询问前的小编辑框：预填划词内容，用户可补充问题后发送</summary>
    public class AskDialog : Form
    {
        public string Result = "";

        readonly TextBox _box = new TextBox();
        readonly Label _title = new Label();
        readonly Label _hint = new Label();

        public AskDialog(string preset)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Theme.Bg;
            Font = Theme.UI;
            Size = new Size(560, 360);
            KeyPreview = true;
            DoubleBuffered = true;
            Padding = new Padding(0, 78, 0, 56);

            _title.Visible = false;
            _title.Font = Theme.H1;
            _title.ForeColor = Theme.Fg;
            _title.AutoSize = false;
            _title.SetBounds(18, 12, 400, 30);
            Controls.Add(_title);

            _hint.Text = "下面是你选中的内容，可以随意修改，再补上你的问题";
            _hint.Font = Theme.UISmall;
            _hint.ForeColor = Theme.Sub;
            _hint.AutoSize = false;
            _hint.SetBounds(20, 52, 520, 18);
            Controls.Add(_hint);

            _box.Multiline = true;
            _box.ScrollBars = ScrollBars.Vertical;
            _box.BorderStyle = BorderStyle.FixedSingle;
            _box.Font = Theme.Content;
            _box.BackColor = Color.White;
            _box.ForeColor = Theme.Fg;
            _box.Text = preset == null ? "" : preset;
            _box.Dock = DockStyle.Fill;
            _box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.Enter) { Send(); }
            };
            Controls.Add(_box);

            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 56;
            bottom.BackColor = Theme.Panel;
            Controls.Add(bottom);

            var send = Theme.FlatButton("发送", true);
            send.Width = 88;
            send.Height = 32;
            send.Dock = DockStyle.Right;
            send.Margin = new Padding(6);
            send.Click += delegate { Send(); };
            bottom.Controls.Add(send);

            var cancel = Theme.FlatButton("取消", false);
            cancel.Width = 80;
            cancel.Height = 32;
            cancel.Dock = DockStyle.Right;
            cancel.Margin = new Padding(6);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            bottom.Controls.Add(cancel);

            var tip = new Label();
            tip.Text = "Ctrl+Enter 直接发送";
            tip.ForeColor = Theme.Sub;
            tip.Font = Theme.UISmall;
            tip.AutoSize = false;
            tip.Dock = DockStyle.Left;
            tip.Width = 220;
            tip.TextAlign = ContentAlignment.MiddleLeft;
            tip.Padding = new Padding(14, 0, 0, 0);
            bottom.Controls.Add(tip);

            Resize += delegate { Theme.Round(this, 12); };
            Load += delegate { Theme.Round(this, 12); Theme.ForceForeground(this); };

            Shown += delegate
            {
                _box.Focus();
                _box.SelectionStart = _box.TextLength;
            };
        }

        void Send()
        {
            string text = _box.Text == null ? "" : _box.Text.Trim();
            if (text.Length == 0)
            {
                _hint.Text = "内容不能为空";
                _hint.ForeColor = Theme.Danger;
                return;
            }
            Result = text;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Theme.PaintHeader(e.Graphics, new Rectangle(0, 0, ClientSize.Width, 46), "询问", "下面是划到的内容，可以修改或补充问题", "");
        }
    }
}
