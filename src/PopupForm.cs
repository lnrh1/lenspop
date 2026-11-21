using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>划词后的小胶囊：翻译 / 询问 / 搜索</summary>
    public class PopupForm : Form
    {
        public event EventHandler TranslateClicked;
        public event EventHandler AskClicked;
        public event EventHandler SearchClicked;
        public event EventHandler SettingsClicked;

        public string SelectionText = "";

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(16, 19, 26);
            Size = new Size(208, 36);
            DoubleBuffered = true;
            Opacity = 1.0;

            var flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.WrapContents = false;
            flow.Padding = new Padding(4, 4, 4, 4);
            flow.WrapContents = false;
            flow.BackColor = BackColor;
            Controls.Add(flow);

            flow.Controls.Add(MakeButton("译  翻译", TranslateClicked));
            flow.Controls.Add(MakeButton("问  询问", AskClicked));
            flow.Controls.Add(MakeButton("搜  搜索", SearchClicked));
            var gear = MakeButton("⚙", null);
            gear.Width = 30;
            gear.ForeColor = Color.FromArgb(170, 180, 198);
            gear.Click += delegate { if (SettingsClicked != null) SettingsClicked(this, EventArgs.Empty); };
            flow.Controls.Add(gear);

            int total = 8;
            foreach (Control c in flow.Controls) total += c.Width + c.Margin.Horizontal;
            Width = total;
            Height = 36;
        }

        Button MakeButton(string text, EventHandler handler)
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
            if (handler != null)
            {
                b.Click += delegate(object s, EventArgs e)
                {
                    Hide();
                    handler(this, EventArgs.Empty);
                };
            }
            return b;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Theme.Round(this, 10);
        }

        public void ShowAt(Point screenPoint, string text)
        {
            SelectionText = text;
            var scr = Screen.FromPoint(screenPoint);
            int left = screenPoint.X - Width / 2;
            int top = screenPoint.Y - Height - 12;
            if (top < scr.WorkingArea.Top) top = screenPoint.Y + 16;
            left = Math.Max(scr.WorkingArea.Left + 4, Math.Min(left, scr.WorkingArea.Right - Width - 4));
            top = Math.Max(scr.WorkingArea.Top + 4, Math.Min(top, scr.WorkingArea.Bottom - Height - 4));
            Location = new Point(left, top);
            Show();
            TopMost = true;
            Theme.NoActivate(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(60, 66, 80)))
            using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
                e.Graphics.DrawPath(pen, path);
        }
    }
}
