using System;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>截图后的小操作条：复制 / 提取文字 / 翻译</summary>
    public class ShotBarForm : Form
    {
        public event EventHandler CopyClicked;
        public event EventHandler OcrClicked;
        public event EventHandler TranslateClicked;

        readonly Label _hint = new Label();

        public ShotBarForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(16, 19, 26);
            DoubleBuffered = true;
            Size = new Size(300, 36);

            var flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.WrapContents = false;
            flow.Padding = new Padding(4);
            flow.BackColor = BackColor;
            Controls.Add(flow);

            flow.Controls.Add(MakeButton("复制", delegate { Raise(CopyClicked); }));
            flow.Controls.Add(MakeButton("提取文字", delegate { Raise(OcrClicked); }));
            flow.Controls.Add(MakeButton("翻译", delegate { Raise(TranslateClicked); }));

            _hint.AutoSize = false;
            _hint.SetBounds(4, 4, 120, 28);
            _hint.TextAlign = ContentAlignment.MiddleCenter;
            _hint.ForeColor = Color.FromArgb(160, 255, 190);
            _hint.Font = Theme.UISmall;
            _hint.Text = "已复制";
            _hint.Visible = false;
            _hint.BackColor = BackColor;
            flow.Controls.Add(_hint);

            int total = 8;
            foreach (Control c in flow.Controls) total += c.Width + c.Margin.Horizontal;
            Width = total;

            Load += delegate { Theme.Round(this, 10); Theme.NoActivate(this); };
            Resize += delegate { Theme.Round(this, 10); };
        }

        Button MakeButton(string text, Action onClick)
        {
            var b = new Button();
            b.Text = text;
            b.Height = 28;
            b.Width = 72;
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
            b.Click += delegate { Hide(); try { onClick(); } catch { } };
            return b;
        }

        void Raise(EventHandler h)
        {
            if (h != null) h(this, EventArgs.Empty);
        }

        public void ShowAt(Point screenPoint, string toast)
        {
            var scr = Screen.FromPoint(screenPoint);
            int left = screenPoint.X;
            int top = screenPoint.Y + 10;
            if (top + Height > scr.WorkingArea.Bottom) top = screenPoint.Y - Height - 10;
            left = Math.Max(scr.WorkingArea.Left + 4, Math.Min(left, scr.WorkingArea.Right - Width - 4));
            top = Math.Max(scr.WorkingArea.Top + 4, top);
            Location = new Point(left, top);
            Show();
            TopMost = true;
            BringToFront();
            Theme.Round(this, 10);
            Theme.NoActivate(this);
        }
    }
}
