using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace LensPop
{
    public enum ResultMode { Translate, Ask, Text }

    /// <summary>结果窗：翻译 / 询问 / 纯文本 共用，正文流式输出</summary>
    public class ResultForm : Form
    {
        readonly AppConfig _cfg;
        readonly Target _target;
        readonly ResultMode _mode;
        readonly string _source;

        readonly RichTextBox _rtb = new RichTextBox();
        readonly Label _status = new Label();
        readonly Label _model = new Label();
        readonly Label _title = new Label();
        readonly ComboBox _level = new ComboBox();
        readonly Panel _askBar = new Panel();
        readonly TextBox _input = new TextBox();
        readonly Button _send = new Button();
        readonly StringBuilder _acc = new StringBuilder();
        readonly object _lock = new object();
        readonly List<ChatMsg> _messages = new List<ChatMsg>();
        readonly List<string> _levels = new List<string>();
        readonly System.Windows.Forms.Timer _flushTimer = new System.Windows.Forms.Timer();

        bool _busy;
        public string TitleText = "";

        public ResultForm(AppConfig cfg, Target target, ResultMode mode, string source)
        {
            _cfg = cfg;
            _target = target;
            _mode = mode;
            _source = source == null ? "" : source;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            TopMost = true;
            BackColor = Theme.Bg;
            Font = Theme.UI;
            Size = new Size(480, mode == ResultMode.Ask ? 560 : 440);
            KeyPreview = true;
            DoubleBuffered = true;

            BuildUi();
            Load += delegate
            {
                Theme.Round(this, 12);
                BeginInvoke(new Action(RunInitial));
            };
            Resize += delegate { Theme.Round(this, 12); };
            FormClosed += delegate { _flushTimer.Stop(); };

            _flushTimer.Interval = 80;
            _flushTimer.Tick += delegate { Flush(); };
            _flushTimer.Start();
        }

        /* ---------------- 布局 ---------------- */

        void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.BackColor = Theme.Bg;
            root.Padding = new Padding(0);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, _mode == ResultMode.Ask ? 64 : 0));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(root);

            /* 头部 */
            var head = new Panel();
            head.Dock = DockStyle.Fill;
            head.BackColor = Theme.Bg;
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.Line))
                    e.Graphics.DrawLine(pen, 0, head.Height - 1, head.Width, head.Height - 1);
                using (var accent = new SolidBrush(Theme.Brand))
                    e.Graphics.FillRectangle(accent, 0, 0, 3, head.Height - 1);
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

            _title.Text = _mode == ResultMode.Ask ? "询问" : "翻译";
            _title.Font = Theme.Title;
            _title.ForeColor = Theme.Fg;
            _title.Dock = DockStyle.Fill;
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.Padding = new Padding(18, 0, 0, 0);
            _title.AutoEllipsis = true;
            headGrid.Controls.Add(_title, 0, 0);

            var acts = new FlowLayoutPanel();
            acts.Dock = DockStyle.Fill;
            acts.FlowDirection = FlowDirection.RightToLeft;
            acts.WrapContents = false;
            acts.AutoSize = false;
            acts.Padding = new Padding(0, 7, 8, 0);
            acts.Margin = new Padding(0);
            acts.BackColor = Theme.Bg;
            headGrid.Controls.Add(acts, 1, 0);

            var close = MakeMini("✕", 32);
            close.Click += delegate { Close(); };
            acts.Controls.Add(close);

            var copy = MakeMini("复制", 54);
            copy.Click += delegate { CopyAll(); };
            acts.Controls.Add(copy);

            var retry = MakeMini("重试", 54);
            retry.Click += delegate { Retry(); };
            acts.Controls.Add(retry);

            if (_mode == ResultMode.Ask)
            {
                _level.DropDownStyle = ComboBoxStyle.DropDownList;
                _level.Font = Theme.UISmall;
                _level.Width = 84;
                _level.Height = 24;
                _level.Margin = new Padding(0, 2, 6, 0);
                acts.Controls.Add(_level);
                FillLevels();
            }

            /* 正文 */
            var body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Padding = new Padding(16, 12, 16, 8);
            root.Controls.Add(body, 0, 1);

            _rtb.Dock = DockStyle.Fill;
            _rtb.BorderStyle = BorderStyle.None;
            _rtb.BackColor = Theme.Bg;
            _rtb.ForeColor = Theme.Fg;
            _rtb.Font = Theme.Content;
            _rtb.ReadOnly = true;
            _rtb.DetectUrls = true;
            _rtb.WordWrap = true;
            _rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
            _rtb.ShortcutsEnabled = true;
            body.Controls.Add(_rtb);

            /* 提问栏 */
            if (_mode == ResultMode.Ask)
            {
                _askBar.Dock = DockStyle.Fill;
                _askBar.BackColor = Theme.Bg;
                _askBar.Paint += delegate(object s, PaintEventArgs e)
                {
                    using (var pen = new Pen(Theme.Line))
                        e.Graphics.DrawLine(pen, 0, 0, _askBar.Width, 0);
                };
                root.Controls.Add(_askBar, 0, 2);

                var grid = new TableLayoutPanel();
                grid.Dock = DockStyle.Fill;
                grid.ColumnCount = 2;
                grid.RowCount = 1;
                grid.Padding = new Padding(12, 10, 12, 10);
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
                _askBar.Controls.Add(grid);

                _input.Multiline = true;
                _input.BorderStyle = BorderStyle.FixedSingle;
                _input.Font = Theme.UI;
                _input.ScrollBars = ScrollBars.None;
                _input.Dock = DockStyle.Fill;
                _input.KeyDown += InputKeyDown;
                grid.Controls.Add(_input, 0, 0);

                _send.Text = "发送";
                _send.Font = Theme.UI;
                _send.FlatStyle = FlatStyle.Flat;
                _send.FlatAppearance.BorderSize = 0;
                _send.BackColor = Theme.Brand;
                _send.ForeColor = Color.White;
                _send.Cursor = Cursors.Hand;
                _send.Dock = DockStyle.Fill;
                _send.Margin = new Padding(8, 8, 0, 8);
                _send.Click += delegate { SendQuestion(); };
                grid.Controls.Add(_send, 1, 0);
            }

            /* 页脚 */
            var foot = new Panel();
            foot.Dock = DockStyle.Fill;
            foot.BackColor = Theme.Bg;
            foot.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.Line))
                    e.Graphics.DrawLine(pen, 0, 0, foot.Width, 0);
            };
            root.Controls.Add(foot, 0, 3);

            _model.Dock = DockStyle.Left;
            _model.Width = 300;
            _model.TextAlign = ContentAlignment.MiddleLeft;
            _model.Padding = new Padding(12, 0, 0, 0);
            _model.Font = Theme.UISmall;
            _model.ForeColor = Theme.Sub;
            foot.Controls.Add(_model);

            _status.Dock = DockStyle.Right;
            _status.Width = 150;
            _status.TextAlign = ContentAlignment.MiddleRight;
            _status.Padding = new Padding(0, 0, 12, 0);
            _status.Font = Theme.UISmall;
            _status.ForeColor = Theme.Sub;
            foot.Controls.Add(_status);

            Theme.DragBy(this, head);
            Theme.DragBy(this, _title);
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

        void FillLevels()
        {
            _levels.Clear();
            _levels.AddRange(Thinking.LevelsFor(_target.PresetId, _target.Model));
            _level.Items.Clear();
            foreach (string l in _levels) _level.Items.Add(Thinking.Label(l));
            string want = Thinking.Clamp(_target.PresetId, _target.Model, _cfg.Settings.AskLevel);
            int idx = _levels.IndexOf(want);
            if (idx < 0) idx = 0;
            _level.SelectedIndex = idx;
            var fam = Thinking.Get(_target.PresetId, _target.Model);
            var tip = new ToolTip();
            tip.SetToolTip(_level, "思考档位 · " + fam.Label + "：" + fam.Note);
        }

        public string Level
        {
            get
            {
                int i = _level.SelectedIndex;
                if (i >= 0 && i < _levels.Count) return _levels[i];
                return "auto";
            }
        }

        void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendQuestion();
            }
        }

        void SendQuestion()
        {
            string q = _input.Text.Trim();
            if (q.Length == 0 || _busy) return;
            _input.Text = "";

            if (_messages.Count == 0)
            {
                _messages.Add(new ChatMsg("system", PromptFor(_cfg, "ask")));
                _messages.Add(new ChatMsg("user", "选中的内容：\n「" + _source + "」\n\n我的问题：" + q));
            }
            else
            {
                _messages.Add(new ChatMsg("user", q));
            }

            AppendText("\n\n▸ " + q + "\n");
            RunChat();
        }

        public static string PromptFor(AppConfig cfg, string key)
        {
            string custom = "";
            var p = cfg.Settings.Prompts;
            if (key == "translate") custom = p.Translate;
            else if (key == "ask") custom = p.Ask;
            else if (key == "ocr") custom = p.Ocr;
            else if (key == "ocrLayout") custom = p.OcrLayout;
            else if (key == "translateLines") custom = p.TranslateLines;
            if (custom == null) custom = "";
            custom = custom.Trim();

            string lang = cfg.Settings.TargetLang;
            string def;
            if (key == "translate")
                def = "你是专业的翻译引擎。把用户给出的文本翻译成" + lang + "。要求：只输出译文，不要解释、不要引号、不要重复原文；保留原文的换行、列表与代码格式；人名与专有名词用通行译法；如果原文本来就是" + lang + "，就用地道的" + lang + "把要点复述一遍。";
            else if (key == "ask")
                def = "你是 LensPop 的阅读助手，擅长把复杂内容讲清楚。请直接给结论和要点，简洁准确、分点作答，默认用中文。";
            else if (key == "ocr")
                def = "请逐行转写图片里所有可见文字，保持原有换行与阅读顺序。不要翻译、不要解释、不要添加任何前后缀。如果图片里没有文字，只输出：未识别到文字。";
            else if (key == "ocrLayout")
                def = "你是 OCR 引擎。请识别图片中的每一行文字，并给出每行的位置与颜色。只输出一个 JSON 对象，不要解释，也不要用代码块包裹。格式：{\"width\":图片宽度,\"height\":图片高度,\"lines\":[{\"box\":[x,y,w,h],\"color\":\"#rrggbb\",\"text\":\"原文\"}]}。box 是像素坐标（左上角 x、y 与宽、高），基于你看到的图片尺寸；color 是该行文字的近似颜色；逐行输出，不要合并不同行；忽略纯装饰元素。没有文字就输出 {\"width\":0,\"height\":0,\"lines\":[]}。";
            else
                def = "你是翻译引擎。用户会给你一个 JSON 字符串数组，每一项是一行文字。请把每一项翻译成" + lang + "，并输出长度相同、顺序一致的 JSON 字符串数组，不要输出任何其它内容。译文要简短自然，尽量不比原文长（这些译文要贴回原图）。已经是" + lang + "的项原样返回。";

            if (custom.Length > 0) return custom.Replace("{TARGET_LANG}", lang);
            return def;
        }

        void RunInitial()
        {
            if (IsDisposed) return;
            _model.Text = _target.ProviderName + " · " + _target.Model;

            if (_mode == ResultMode.Translate)
            {
                _title.Text = "翻译 → " + _cfg.Settings.TargetLang;
                _messages.Add(new ChatMsg("system", PromptFor(_cfg, "translate")));
                _messages.Add(new ChatMsg("user", _source));
                RunChat();
            }
            else if (_mode == ResultMode.Ask)
            {
                _title.Text = "询问";
                _input.Focus();
                SetStatus("输入问题，Enter 发送");
            }
            else
            {
                _title.Text = string.IsNullOrEmpty(TitleText) ? "识别文字" : TitleText;
                if (!string.IsNullOrEmpty(_source)) _rtb.Text = _source;
                SetStatus("完成");
            }
        }

        void Retry()
        {
            if (_busy) return;
            if (_mode == ResultMode.Translate)
            {
                _rtb.Clear();
                RunChat();
            }
        }

        async void RunChat()
        {
            if (_busy) return;
            _busy = true;
            lock (_lock) _acc.Length = 0;
            SetStatus("生成中…");
            try
            {
                Dictionary<string, object> patch;
                if (_mode == ResultMode.Ask) patch = Thinking.Build(_target.PresetId, _target.Model, Level);
                else patch = Thinking.Disable(_target.PresetId, _target.Model);

                await Api.ChatAsync(_target, _messages, patch, _cfg.Settings.Stream, OnDelta,
                    _cfg.Settings.TimeoutSec, _cfg.Settings.Temperature, _cfg.Settings.MaxTokens);
                Flush();
                SetStatus(TextLength() > 0 ? "完成" : "空回复");
            }
            catch (Exception ex)
            {
                Flush();
                AppendText("\n\n" + Api.Humanize(ex));
                SetStatus("出错了");
            }
            finally
            {
                _busy = false;
                if (_mode == ResultMode.Ask) _input.Focus();
            }
        }

        int TextLength()
        {
            lock (_lock) return _acc.Length + _rtb.TextLength;
        }

        void OnDelta(string text)
        {
            lock (_lock) _acc.Append(text);
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(new Action(Flush));
            }
            catch { }
        }

        void Flush()
        {
            string chunk;
            lock (_lock)
            {
                if (_acc.Length == 0) return;
                chunk = _acc.ToString();
                _acc.Length = 0;
            }
            AppendText(chunk);
        }

        public void AppendPlain(string text)
        {
            AppendText(text);
        }

        public void SetPlain(string text)
        {
            if (IsDisposed || !IsHandleCreated) return;
            _rtb.Text = text == null ? "" : text;
        }

        public void SetStatusText(string text)
        {
            SetStatus(text);
        }

        void AppendText(string text)
        {
            if (IsDisposed || !IsHandleCreated) return;
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionLength = 0;
            _rtb.AppendText(text);
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.ScrollToCaret();
        }

        void SetStatus(string s)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetStatus(s); })); return; }
            _status.Text = s;
        }

        void CopyAll()
        {
            string t = _rtb.Text;
            if (t.Trim().Length == 0) return;
            try
            {
                Clipboard.SetText(t);
                SetStatus("已复制");
            }
            catch { SetStatus("复制失败"); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        public void PlaceNear(Point screenPoint)
        {
            var scr = Screen.FromPoint(screenPoint);
            int left = screenPoint.X + 16;
            int top = screenPoint.Y + 16;
            if (left + Width > scr.WorkingArea.Right) left = Math.Max(scr.WorkingArea.Left + 8, screenPoint.X - Width - 16);
            if (top + Height > scr.WorkingArea.Bottom) top = Math.Max(scr.WorkingArea.Top + 8, scr.WorkingArea.Bottom - Height - 8);
            Location = new Point(left, top);
        }
    }
}
