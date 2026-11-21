using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>图片翻译结果窗：贴好译文的新图 + 原文/译文对照</summary>
    public class ImageResultForm : Form
    {
        readonly Bitmap _image;
        readonly List<TextLine> _lines;
        readonly string _modelText;

        readonly PictureBox _pic = new PictureBox();
        readonly RichTextBox _text = new RichTextBox();
        readonly Label _status = new Label();
        readonly Label _model = new Label();
        readonly Label _title = new Label();

        public ImageResultForm(Bitmap image, List<TextLine> lines, string modelText)
        {
            _image = image;
            _lines = lines;
            _modelText = modelText;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = Theme.Bg;
            Font = Theme.UI;
            Size = new Size(720, 620);
            KeyPreview = true;
            DoubleBuffered = true;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.BackColor = Theme.Bg;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(root);

            /* 头部 */
            var head = new Panel();
            head.Dock = DockStyle.Fill;
            head.BackColor = Theme.Bg;
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 0, head.Height - 1, head.Width, head.Height - 1);
            };
            root.Controls.Add(head, 0, 0);

            var headGrid = new TableLayoutPanel();
            headGrid.Dock = DockStyle.Fill;
            headGrid.ColumnCount = 2;
            headGrid.RowCount = 1;
            headGrid.Padding = new Padding(0);
            headGrid.Margin = new Padding(0);
            headGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            headGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            headGrid.AutoSize = false;
            head.Controls.Add(headGrid);

            _title.Text = "图片翻译";
            _title.Font = Theme.Title;
            _title.Dock = DockStyle.Fill;
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.Padding = new Padding(14, 0, 0, 0);
            headGrid.Controls.Add(_title, 0, 0);

            var acts = new FlowLayoutPanel();
            acts.Dock = DockStyle.Fill;
            acts.FlowDirection = FlowDirection.RightToLeft;
            acts.WrapContents = false;
            acts.Padding = new Padding(0, 7, 8, 0);
            acts.Margin = new Padding(0);
            acts.BackColor = Theme.Bg;
            headGrid.Controls.Add(acts, 1, 0);

            var close = MakeMini("✕", 32);
            close.Click += delegate { Close(); };
            acts.Controls.Add(close);

            var save = MakeMini("保存", 56);
            save.Click += delegate { SaveImage(); };
            acts.Controls.Add(save);

            var toggle = MakeMini("文字", 56);
            toggle.Click += delegate
            {
                bool showText = _pic.Visible;
                _pic.Visible = !showText;
                _text.Visible = showText;
                toggle.Text = _pic.Visible ? "文字对照" : "看图片";
            };
            acts.Controls.Add(toggle);

            /* 正文 */
            var body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Panel;
            body.Padding = new Padding(14);
            root.Controls.Add(body, 0, 1);

            _pic.Dock = DockStyle.Fill;
            _pic.SizeMode = PictureBoxSizeMode.Zoom;
            _pic.BackColor = Color.FromArgb(245, 246, 248);
            _pic.Image = _image;
            body.Controls.Add(_pic);

            _text.Dock = DockStyle.Fill;
            _text.BorderStyle = BorderStyle.None;
            _text.Font = Theme.Content;
            _text.ReadOnly = true;
            _text.BackColor = Theme.Bg;
            _text.Visible = false;
            _text.Text = BuildText();
            body.Controls.Add(_text);

            /* 页脚 */
            var foot = new Panel();
            foot.Dock = DockStyle.Fill;
            foot.BackColor = Theme.Bg;
            foot.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 0, 0, foot.Width, 0);
            };
            root.Controls.Add(foot, 0, 2);

            _model.Dock = DockStyle.Left;
            _model.Width = 400;
            _model.TextAlign = ContentAlignment.MiddleLeft;
            _model.Padding = new Padding(12, 0, 0, 0);
            _model.Font = Theme.UISmall;
            _model.ForeColor = Theme.Sub;
            _model.Text = _modelText;
            foot.Controls.Add(_model);

            _status.Dock = DockStyle.Right;
            _status.Width = 200;
            _status.TextAlign = ContentAlignment.MiddleRight;
            _status.Padding = new Padding(0, 0, 12, 0);
            _status.Font = Theme.UISmall;
            _status.ForeColor = Theme.Sub;
            _status.Text = "共 " + (_lines == null ? 0 : _lines.Count) + " 行译文";
            foot.Controls.Add(_status);

            Theme.DragBy(this, head);
            Load += delegate { Theme.Round(this, 12); };
            Resize += delegate { Theme.Round(this, 12); };
        }

        static Button MakeMini(string text, int width)
        {
            var b = Theme.FlatButton(text, false);
            b.Width = width;
            b.Height = 26;
            b.Margin = new Padding(2, 0, 0, 0);
            b.FlatAppearance.BorderSize = 0;
            b.ForeColor = Theme.Sub;
            return b;
        }

        string BuildText()
        {
            var sb = new System.Text.StringBuilder();
            if (_lines != null)
            {
                foreach (var l in _lines)
                {
                    string tr = string.IsNullOrEmpty(l.Translation) ? l.Text : l.Translation;
                    sb.AppendLine(tr);
                    if (!string.IsNullOrEmpty(l.Translation) && l.Text != null && l.Text != tr)
                        sb.AppendLine("    （原文：" + l.Text + "）");
                }
            }
            if (sb.Length == 0) sb.Append("（没有识别到文字）");
            return sb.ToString();
        }

        void SaveImage()
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "PNG 图片|*.png";
            dlg.FileName = "lenspop-translated-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _image.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
                _status.Text = "已保存";
            }
            catch (Exception e)
            {
                MessageBox.Show("保存失败：" + e.Message);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Theme.ForceForeground(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _image != null)
            {
                try { _image.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
