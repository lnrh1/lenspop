using System;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>询问前的单行输入条：预填带引号的划词内容，回车发送</summary>
    public class AskDialog : Form
    {
        public string Result = "";

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

            Shown += delegate
            {
                _box.Focus();
                _box.SelectionStart = _box.TextLength;
            };
        }

        // 单行输入框里回车/ESC 交给窗体处理，避免被 TextBox 吞掉
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { Send(); return true; }
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
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
