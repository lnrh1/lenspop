using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LensPop
{
    public class SettingsForm : Form
    {
        readonly AppConfig _cfg;
        bool _loading = true;

        ListBox _provList;
        Label _provEmpty;
        ComboBox _presetBox;
        TextBox _nameBox, _baseBox, _keyBox;
        Label _noteLabel;
        Button _fetchBtn, _addPoolBtn, _addAllBtn, _delProvBtn, _newProvBtn;
        ListBox _modelList;
        readonly List<string> _models = new List<string>();

        ListView _poolList;
        Button _poolDelBtn;
        ComboBox _roleTranslate, _roleOcr, _roleAsk, _levelBox;
        Label _levelHint;
        Button _testBtn;
        Label _testLabel;

        ComboBox _langBox, _engineBox;
        TextBox _searchTpl;
        NumericUpDown _temp, _maxTokens, _timeout;
        CheckBox _selPopup, _stream;

        ComboBox _promptPick;
        TextBox _promptBox;
        Button _promptReset;
        string _promptKey = "translate";
        readonly Dictionary<string, string> _promptDraft = new Dictionary<string, string>();

        System.Windows.Forms.Timer _commitTimer;
        bool _syncing;
        bool _committing;

        CheckBox _autostart;
        Button _shellBtn;
        Label _shellStatus;
        TextBox _hkOcr, _hkCapture, _hkTranslate;
        Label _status;

        public SettingsForm(AppConfig cfg)
        {
            _cfg = cfg;
            Text = "LensPop 设置";
            Width = 800;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.UI;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildUi();

            _commitTimer = new System.Windows.Forms.Timer();
            _commitTimer.Interval = 400;
            _commitTimer.Tick += delegate
            {
                _commitTimer.Stop();
                CommitAndSave();
            };

            LoadPrompts();
            LoadProviders();
            RefreshPool();
            FillSettings();
            _loading = false;
        }

        /* ---------------- 布局 ---------------- */

        static Label L(Control parent, string text, int x, int y, int w)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Width = w;
            l.Height = 20;
            l.ForeColor = Theme.Fg;
            l.Font = Theme.UI;
            parent.Controls.Add(l);
            return l;
        }

        static Label Hint(Control parent, string text, int x, int y, int w, int h)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Width = w;
            l.Height = h;
            l.ForeColor = Theme.Sub;
            l.Font = Theme.UISmall;
            parent.Controls.Add(l);
            return l;
        }

        static TextBox T(Control parent, int x, int y, int w, bool password)
        {
            var t = new TextBox();
            t.Location = new Point(x, y);
            t.Width = w;
            t.Font = Theme.UI;
            t.BorderStyle = BorderStyle.FixedSingle;
            if (password) t.PasswordChar = '●';
            parent.Controls.Add(t);
            return t;
        }

        static ComboBox C(Control parent, int x, int y, int w)
        {
            var c = new ComboBox();
            c.Location = new Point(x, y);
            c.Width = w;
            c.Font = Theme.UI;
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            parent.Controls.Add(c);
            return c;
        }

        static Button B(Control parent, string text, int x, int y, int w, int h)
        {
            var b = Theme.FlatButton(text, false);
            b.Location = new Point(x, y);
            b.Width = w;
            b.Height = h;
            parent.Controls.Add(b);
            return b;
        }

        void BuildUi()
        {
            var tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = Theme.UI;
            Controls.Add(tabs);

            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 46;
            bottom.BackColor = Theme.Panel;
            Controls.Add(bottom);

            _status = new Label();
            _status.Dock = DockStyle.Left;
            _status.Width = 520;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(14, 0, 0, 0);
            _status.ForeColor = Theme.Sub;
            _status.Font = Theme.UISmall;
            bottom.Controls.Add(_status);

            var close = Theme.FlatButton("关闭", false);
            close.Width = 74;
            close.Height = 30;
            close.Dock = DockStyle.Right;
            close.Margin = new Padding(6);
            close.Click += delegate { Close(); };
            bottom.Controls.Add(close);

            var save = Theme.FlatButton("保存", true);
            save.Width = 74;
            save.Height = 30;
            save.Dock = DockStyle.Right;
            save.Click += delegate
            {
                if (SaveAll()) Close();
            };
            bottom.Controls.Add(save);

            BuildProviderTab(tabs);
            BuildPoolTab(tabs);
            BuildBehaviorTab(tabs);
            BuildPromptTab(tabs);
            BuildSystemTab(tabs);
        }

        void BuildProviderTab(TabControl tabs)
        {
            var p = new TabPage("提供商");
            p.BackColor = Theme.Bg;
            tabs.TabPages.Add(p);

            _provList = new ListBox();
            _provList.Location = new Point(14, 14);
            _provList.Size = new Size(170, 400);
            _provList.Font = Theme.UI;
            _provList.IntegralHeight = false;
            _provList.SelectedIndexChanged += delegate { OnProviderSelected(); };
            p.Controls.Add(_provList);

            var empty = new Label();
            empty.Text = "还没有提供商\r\n点下面「新增」开始";
            empty.ForeColor = Theme.Sub;
            empty.Font = Theme.UISmall;
            empty.Location = new Point(24, 24);
            empty.Size = new Size(150, 44);
            empty.BackColor = Theme.Bg;
            p.Controls.Add(empty);
            empty.BringToFront();
            _provEmpty = empty;

            _newProvBtn = B(p, "新增", 14, 422, 78, 26);
            _newProvBtn.Click += delegate { NewProvider(); };
            _delProvBtn = B(p, "删除", 98, 422, 78, 26);
            _delProvBtn.Click += delegate { DeleteProvider(); };

            L(p, "预设", 200, 16, 60);
            _presetBox = C(p, 200, 36, 330);
            foreach (var preset in Providers.All) _presetBox.Items.Add(preset.Name);
            _presetBox.SelectedIndexChanged += delegate { OnPresetChanged(); };

            _noteLabel = Hint(p, "", 200, 64, 480, 34);

            L(p, "显示名称", 200, 100, 80);
            _nameBox = T(p, 200, 120, 330, false);

            L(p, "Base URL", 200, 152, 80);
            _baseBox = T(p, 200, 172, 460, false);

            L(p, "API Key", 200, 204, 80);
            _keyBox = T(p, 200, 224, 460, true);

            // 直接开填就自动建提供商，不用先去点「新增」
            _nameBox.TextChanged += delegate { OnProviderFieldEdited(); };
            _baseBox.TextChanged += delegate { OnProviderFieldEdited(); };
            _keyBox.TextChanged += delegate { OnProviderFieldEdited(); };

            _fetchBtn = B(p, "获取模型", 200, 262, 110, 28);
            _fetchBtn.Click += delegate { FetchModels(); };
            Hint(p, "填好 Base URL 与 Key 后点它，拉到模型列表再存进模型池", 320, 266, 400, 20);

            L(p, "模型列表（选中后存入模型池）", 200, 300, 300);
            _modelList = new ListBox();
            _modelList.Location = new Point(200, 322);
            _modelList.Size = new Size(460, 132);
            _modelList.Font = Theme.UI;
            _modelList.IntegralHeight = false;
            _modelList.DoubleClick += delegate { AddSelectedToPool(); };
            p.Controls.Add(_modelList);

            _addPoolBtn = B(p, "存入模型池", 200, 462, 110, 28);
            _addPoolBtn.Click += delegate { AddSelectedToPool(); };
            _addAllBtn = B(p, "全部存入", 318, 462, 100, 28);
            _addAllBtn.Click += delegate { AddAllToPool(); };
        }

        void BuildPoolTab(TabControl tabs)
        {
            var p = new TabPage("模型池 · 角色");
            p.BackColor = Theme.Bg;
            tabs.TabPages.Add(p);

            _poolList = new ListView();
            _poolList.Location = new Point(14, 14);
            _poolList.Size = new Size(740, 200);
            _poolList.View = View.Details;
            _poolList.FullRowSelect = true;
            _poolList.GridLines = false;
            _poolList.Font = Theme.UI;
            _poolList.Columns.Add("模型", 380);
            _poolList.Columns.Add("提供商", 240);
            _poolList.Columns.Add("读图", 60);
            p.Controls.Add(_poolList);

            _poolDelBtn = B(p, "从池里删除", 14, 222, 100, 26);
            _poolDelBtn.Click += delegate { DeletePoolItem(); };

            L(p, "翻译模型", 14, 266, 80);
            _roleTranslate = C(p, 100, 262, 420);
            _roleTranslate.SelectedIndexChanged += delegate { OnRoleChanged("translate", _roleTranslate); };

            L(p, "OCR 模型", 14, 302, 80);
            _roleOcr = C(p, 100, 298, 420);
            _roleOcr.SelectedIndexChanged += delegate { OnRoleChanged("ocr", _roleOcr); };
            Hint(p, "OCR / 图片翻译要选能读图的模型", 530, 302, 220, 20);

            L(p, "询问模型", 14, 338, 80);
            _roleAsk = C(p, 100, 334, 420);
            _roleAsk.SelectedIndexChanged += delegate { OnRoleChanged("ask", _roleAsk); };
            Hint(p, "留空就用翻译模型", 530, 338, 220, 20);

            L(p, "询问默认档位", 14, 374, 110);
            _levelBox = C(p, 120, 370, 130);
            _levelHint = Hint(p, "", 260, 376, 480, 32);

            _testBtn = B(p, "测试翻译模型", 100, 414, 120, 28);
            _testBtn.Click += delegate { TestModel(); };
            _testLabel = Hint(p, "", 230, 420, 520, 40);
        }

        void BuildBehaviorTab(TabControl tabs)
        {
            var p = new TabPage("行为");
            p.BackColor = Theme.Bg;
            tabs.TabPages.Add(p);

            L(p, "目标语言", 14, 20, 80);
            _langBox = C(p, 110, 16, 220);
            _langBox.DropDownStyle = ComboBoxStyle.DropDown;
            foreach (string s in SearchEngines.Langs) _langBox.Items.Add(s);

            L(p, "搜索引擎", 14, 56, 80);
            _engineBox = C(p, 110, 52, 220);
            foreach (string s in SearchEngines.Names) _engineBox.Items.Add(s);
            _engineBox.SelectedIndexChanged += delegate { UpdateSearchTplEnabled(); };

            L(p, "自定义模板", 14, 92, 80);
            _searchTpl = T(p, 110, 88, 460, false);
            Hint(p, "用 %s 代替关键词", 580, 92, 150, 20);

            L(p, "温度", 14, 132, 80);
            _temp = new NumericUpDown();
            _temp.Location = new Point(110, 128);
            _temp.Width = 90;
            _temp.DecimalPlaces = 1;
            _temp.Increment = 0.1M;
            _temp.Minimum = 0;
            _temp.Maximum = 2;
            _temp.Font = Theme.UI;
            p.Controls.Add(_temp);

            L(p, "最大输出 tokens", 14, 168, 120);
            _maxTokens = new NumericUpDown();
            _maxTokens.Location = new Point(140, 164);
            _maxTokens.Width = 100;
            _maxTokens.Minimum = 0;
            _maxTokens.Maximum = 64000;
            _maxTokens.Increment = 256;
            _maxTokens.Font = Theme.UI;
            p.Controls.Add(_maxTokens);
            Hint(p, "0 表示不限制", 250, 168, 150, 20);

            L(p, "请求超时（秒）", 14, 204, 110);
            _timeout = new NumericUpDown();
            _timeout.Location = new Point(130, 200);
            _timeout.Width = 90;
            _timeout.Minimum = 10;
            _timeout.Maximum = 600;
            _timeout.Increment = 10;
            _timeout.Font = Theme.UI;
            p.Controls.Add(_timeout);

            _selPopup = new CheckBox();
            _selPopup.Text = "选中文字后弹出小工具条（翻译 / 询问 / 搜索）";
            _selPopup.Location = new Point(14, 246);
            _selPopup.Width = 500;
            _selPopup.Font = Theme.UI;
            p.Controls.Add(_selPopup);

            _stream = new CheckBox();
            _stream.Text = "流式输出（边生成边显示）";
            _stream.Location = new Point(14, 274);
            _stream.Width = 400;
            _stream.Font = Theme.UI;
            p.Controls.Add(_stream);

            Hint(p, "划词原理：松开鼠标后程序会模拟一次 Ctrl+C 读取选中文字，因此会临时改写剪贴板。\r\n翻译与 OCR 一律不思考（直接出结果）；询问时可在小窗里选思考档位，默认关闭。\r\n搜索会用系统默认浏览器打开。", 14, 320, 740, 90);
        }

        void BuildPromptTab(TabControl tabs)
        {
            var p = new TabPage("提示词");
            p.BackColor = Theme.Bg;
            tabs.TabPages.Add(p);

            L(p, "要改哪一段", 14, 16, 90);
            _promptPick = C(p, 110, 12, 240);
            _promptPick.Items.Add("翻译");
            _promptPick.Items.Add("询问");
            _promptPick.Items.Add("图片识字");
            _promptPick.Items.Add("图片逐行定位");
            _promptPick.Items.Add("逐行翻译");
            _promptPick.SelectedIndexChanged += delegate { SwitchPrompt(); };

            _promptBox = new TextBox();
            _promptBox.Multiline = true;
            _promptBox.ScrollBars = ScrollBars.Vertical;
            _promptBox.Location = new Point(14, 44);
            _promptBox.Size = new Size(740, 380);
            _promptBox.Font = new Font("Microsoft YaHei UI", 9.5f);
            _promptBox.BorderStyle = BorderStyle.FixedSingle;
            _promptBox.TextChanged += delegate { StashPrompt(); };
            p.Controls.Add(_promptBox);

            _promptPick.SelectedIndex = 0;

            _promptReset = B(p, "恢复默认", 14, 432, 100, 26);
            _promptReset.Click += delegate
            {
                _promptDraft[_promptKey] = "";
                LoadPromptBox();
            };
            Hint(p, "留空就用内置提示词；{TARGET_LANG} 会被替换成目标语言。", 124, 436, 520, 20);
        }

        void BuildSystemTab(TabControl tabs)
        {
            var p = new TabPage("系统");
            p.BackColor = Theme.Bg;
            tabs.TabPages.Add(p);

            _autostart = new CheckBox();
            _autostart.Text = "开机自动启动（托盘常驻）";
            _autostart.Location = new Point(14, 20);
            _autostart.Width = 320;
            _autostart.Font = Theme.UI;
            p.Controls.Add(_autostart);

            L(p, "图片右键菜单", 14, 58, 100);
            _shellBtn = B(p, "注册", 130, 52, 90, 26);
            _shellBtn.Click += delegate { ToggleShell(); };
            _shellStatus = Hint(p, "", 232, 58, 480, 20);

            L(p, "识别剪贴板图片", 14, 96, 110);
            _hkOcr = T(p, 200, 92, 130, false);
            Hint(p, "例如 Ctrl+Alt+O", 340, 96, 160, 20);

            L(p, "截图识别", 14, 128, 110);
            _hkCapture = T(p, 200, 124, 130, false);
            Hint(p, "例如 Ctrl+Alt+S，框选屏幕区域后识别", 340, 128, 300, 20);

            L(p, "翻译选中文字", 14, 160, 110);
            _hkTranslate = T(p, 200, 156, 130, false);
            Hint(p, "选中文字后按它直接翻译（不需要鼠标钩子）", 340, 160, 320, 20);

            var diag = B(p, "运行自检", 200, 194, 100, 26);
            diag.Click += delegate { RunDiagnostics(); };
            var grab = B(p, "取词自测", 308, 194, 100, 26);
            grab.Click += delegate { TestGrab(); };
            Hint(p, "取词自测：3 秒内到别的窗口选中一段文字，程序会模拟 Ctrl+C 看能不能抓到", 416, 198, 360, 20);

            var popBtn = B(p, "测试弹层", 416, 236, 100, 26);
            popBtn.Click += delegate { TestPopup(); };

            var exportBtn = B(p, "导出配置", 14, 236, 100, 26);
            exportBtn.Click += delegate { ExportConfig(); };
            var importBtn = B(p, "导入配置", 122, 236, 100, 26);
            importBtn.Click += delegate { ImportConfig(); };
            Hint(p, "换版本、换机器时用这两个按钮搬配置（模型池、Key、提示词都在里面）", 232, 240, 480, 20);

            var openDir = B(p, "打开配置目录", 14, 274, 120, 26);
            openDir.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", AppConfig.Dir); } catch { }
            };
            var openLog = B(p, "关于 LensPop", 146, 274, 120, 26);
            openLog.Click += delegate
            {
                MessageBox.Show(this,
                    "LensPop · 划词透镜 " + Program.Version + "\r\n\r\n" +
                    "划词翻译 / 询问 / 搜索，右键图片识别文字，图片翻译贴回原图。\r\n" +
                    "MIT License · 单文件，无后台服务，不收集任何数据。\r\n\r\n" +
                    "配置目录：" + AppConfig.Dir + "\r\n" +
                    "（API Key 以明文存在 config.json 里，换机器时注意别误传）",
                    "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Hint(p, "配置会同时写一份 config.bak.json 作为自动备份；读不出来时自动回退。", 14, 316, 700, 24);
        }

        /* ---------------- 提供商 ---------------- */

        ProviderDef SelectedProvider()
        {
            int i = _provList.SelectedIndex;
            if (i < 0 || i >= _cfg.Providers.Count) return null;
            return _cfg.Providers[i];
        }

        void LoadProviders()
        {
            _provList.Items.Clear();
            foreach (var p in _cfg.Providers) _provList.Items.Add(string.IsNullOrEmpty(p.Name) ? "未命名" : p.Name);
            if (_provEmpty != null) _provEmpty.Visible = _cfg.Providers.Count == 0;
            if (_provList.Items.Count > 0) _provList.SelectedIndex = 0;
            else ShowProviderFields(null);
        }

        void ShowProviderFields(ProviderDef p)
        {
            _loading = true;
            if (p == null)
            {
                _presetBox.SelectedIndex = Providers.All.Count - 1;
                _nameBox.Text = "";
                _baseBox.Text = "";
                _keyBox.Text = "";
                _noteLabel.Text = "";
                _modelList.Items.Clear();
                _models.Clear();
            }
            else
            {
                int idx = -1;
                for (int i = 0; i < Providers.All.Count; i++) if (Providers.All[i].Id == p.PresetId) idx = i;
                if (idx < 0) idx = Providers.All.Count - 1;
                _presetBox.SelectedIndex = idx;
                _nameBox.Text = p.Name;
                _baseBox.Text = p.BaseUrl;
                _keyBox.Text = p.ApiKey;
                UpdateNote(Providers.All[idx]);
                _modelList.Items.Clear();
                _models.Clear();
            }
            _loading = false;
        }

        void UpdateNote(Preset preset)
        {
            string note = preset.Note;
            if (!string.IsNullOrEmpty(preset.KeyUrl)) note = note + "\r\n拿 Key：" + preset.KeyUrl;
            _noteLabel.Text = note;
        }

        void SetStatusText(string s)
        {
            if (_status != null) _status.Text = s;
        }

        void CommitAndSave()
        {
            if (_loading) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            CommitProviderFields();
            _cfg.Save();
            if (_provEmpty != null) _provEmpty.Visible = _cfg.Providers.Count == 0;
            if (sw.ElapsedMilliseconds > 50) Program.LogLine("保存配置耗时 " + sw.ElapsedMilliseconds + "ms");
        }

        void ScheduleCommit()
        {
            if (_commitTimer == null) return;
            _commitTimer.Stop();
            _commitTimer.Start();
        }

        void OnProviderFieldEdited()
        {
            if (_loading || _syncing) return;
            // 还没选提供商时先不落盘：等「获取模型」成功后再建（见 FetchModels）
            ScheduleCommit();
        }

        /// <summary>拿界面上的当前值新建一个提供商并存盘</summary>
        ProviderDef CreateProviderFromFields()
        {
            int pi = _presetBox.SelectedIndex;
            if (pi < 0 || pi >= Providers.All.Count) pi = Providers.All.Count - 1;
            var preset = Providers.All[pi];

            string url = _baseBox.Text.Trim();
            string name = _nameBox.Text.Trim();

            var prov = new ProviderDef();
            prov.Id = "p_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            prov.PresetId = preset.Id;
            prov.BaseUrl = url.Length > 0 ? url : preset.BaseUrl;
            prov.ApiKey = _keyBox.Text.Trim();
            prov.Name = name.Length > 0 ? name : (preset.Id == "custom" ? "自定义提供商" : preset.Name);

            _cfg.Providers.Add(prov);

            _syncing = true;
            _provList.Items.Add(prov.Name);
            _provList.SelectedIndex = _provList.Items.Count - 1;
            if (_baseBox.Text.Trim().Length == 0 && prov.BaseUrl.Length > 0) _baseBox.Text = prov.BaseUrl;
            if (_nameBox.Text.Trim().Length == 0) _nameBox.Text = prov.Name;
            _syncing = false;

            if (_provEmpty != null) _provEmpty.Visible = false;
            _cfg.Save();
            return prov;
        }

        void OnProviderSelected()
        {
            if (_loading || _syncing) return;
            CommitProviderFields();
            ShowProviderFields(SelectedProvider());
        }

        void OnPresetChanged()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (_loading) return;
            int i = _presetBox.SelectedIndex;
            if (i < 0 || i >= Providers.All.Count) return;
            var preset = Providers.All[i];
            UpdateNote(preset);
            if (!string.IsNullOrEmpty(preset.BaseUrl)) _baseBox.Text = preset.BaseUrl;
            string cur = _nameBox.Text.Trim();
            bool looksAuto = cur.Length == 0;
            if (preset.Id == "custom") looksAuto = false;
            if (looksAuto) _nameBox.Text = preset.Name;

            if (sw.ElapsedMilliseconds > 200) Program.LogLine("切换预设 " + preset.Id + " 耗时 " + sw.ElapsedMilliseconds + "ms");
        }

        void CommitProviderFields()
        {
            if (_committing) return;
            _committing = true;
            try
            {
                var p = SelectedProvider();
                if (p == null) return;
                int i = _presetBox.SelectedIndex;
                if (i >= 0 && i < Providers.All.Count) p.PresetId = Providers.All[i].Id;
                p.Name = _nameBox.Text.Trim();
                if (p.Name.Length == 0) p.Name = "未命名提供商";
                p.BaseUrl = _baseBox.Text.Trim();
                p.ApiKey = _keyBox.Text.Trim();

                int sel = _provList.SelectedIndex;
                if (sel >= 0 && sel < _provList.Items.Count && !Equals(_provList.Items[sel], p.Name))
                {
                    // 改 ListBox 的内容会反过来触发 SelectedIndexChanged，先压住
                    _syncing = true;
                    _provList.Items[sel] = p.Name;
                    _syncing = false;
                }
            }
            finally
            {
                _committing = false;
            }
        }

        void NewProvider()
        {
            CommitProviderFields();
            var p = new ProviderDef();
            p.Id = "p_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            p.PresetId = "deepseek";
            p.Name = "DeepSeek";
            p.BaseUrl = Providers.Find("deepseek").BaseUrl;
            _cfg.Providers.Add(p);
            _provList.Items.Add(p.Name);
            _provList.SelectedIndex = _provList.Items.Count - 1;
        }

        void DeleteProvider()
        {
            int i = _provList.SelectedIndex;
            if (i < 0) return;
            var p = _cfg.Providers[i];
            if (MessageBox.Show(this, "删除提供商「" + p.Name + "」？模型池里它的模型也会一起清掉。", "确认",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            _cfg.Pool.RemoveAll(delegate(PoolItem m) { return m.ProviderId == p.Id; });
            _cfg.Providers.RemoveAt(i);
            _provList.Items.RemoveAt(i);
            RefreshPool();
            if (_provList.Items.Count > 0) _provList.SelectedIndex = 0;
        }

        async void FetchModels()
        {
            var p = SelectedProvider();
            string baseUrl;
            string apiKey;
            if (p != null)
            {
                CommitProviderFields();
                baseUrl = p.BaseUrl;
                apiKey = p.ApiKey;
            }
            else
            {
                baseUrl = _baseBox.Text.Trim();
                apiKey = _keyBox.Text.Trim();
            }
            if (string.IsNullOrEmpty(baseUrl))
            {
                SetStatusText("先填 Base URL（选预设会自动填）");
                return;
            }
            _fetchBtn.Enabled = false;
            _fetchBtn.Text = "获取中…";
            SetStatusText("正在请求 /models …");
            try
            {
                var list = await Api.ListModelsAsync(baseUrl, apiKey, 30);

                // 拿到列表说明 Base URL / Key 都可用，这时候才真正建出提供商
                bool createdNew = false;
                string createdName = "";
                if (SelectedProvider() == null)
                {
                    var created = CreateProviderFromFields();
                    createdNew = true;
                    createdName = created.Name;
                }

                _models.Clear();
                _models.AddRange(list);
                _modelList.Items.Clear();
                foreach (string m in list)
                    _modelList.Items.Add(Providers.LooksVision(m) ? m + "   · 可读图" : m);

                SetStatusText(createdNew
                    ? "已新建提供商「" + createdName + "」，拿到 " + list.Count + " 个模型，选一个「存入模型池」"
                    : "拿到 " + list.Count + " 个模型，选一个点「存入模型池」");
            }
            catch (Exception ex)
            {
                _status.Text = Api.Humanize(ex).Replace("\r", " ").Replace("\n", " ");
            }
            finally
            {
                _fetchBtn.Enabled = true;
                _fetchBtn.Text = "获取模型";
                _cfg.Save();
            }
        }

        void AddToPool(string model)
        {
            var p = SelectedProvider();
            if (p == null || string.IsNullOrEmpty(model)) return;
            foreach (var m in _cfg.Pool)
            {
                if (m.ProviderId == p.Id && m.Model == model)
                {
                    _status.Text = "「" + model + "」已经在模型池里了";
                    return;
                }
            }
            var item = new PoolItem();
            item.Id = "m_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            item.ProviderId = p.Id;
            item.Model = model;
            item.AddedAt = DateTime.Now.Ticks;
            _cfg.Pool.Add(item);
            RefreshPool();
            _status.Text = "已存入模型池：" + model;
        }

        void AddSelectedToPool()
        {
            int i = _modelList.SelectedIndex;
            if (i < 0 || i >= _models.Count) return;
            AddToPool(_models[i]);
        }

        void AddAllToPool()
        {
            if (_models.Count == 0) return;
            foreach (string m in _models) AddToPool(m);
            _status.Text = "已把 " + _models.Count + " 个模型存入模型池";
        }

        /* ---------------- 模型池 / 角色 ---------------- */

        void RefreshPool()
        {
            _loading = true;
            var keepT = _cfg.Roles.ContainsKey("translate") ? _cfg.Roles["translate"] : "";
            var keepO = _cfg.Roles.ContainsKey("ocr") ? _cfg.Roles["ocr"] : "";
            var keepA = _cfg.Roles.ContainsKey("ask") ? _cfg.Roles["ask"] : "";

            _poolList.Items.Clear();
            foreach (var m in _cfg.Pool)
            {
                var prov = _cfg.FindProvider(m.ProviderId);
                string pname = prov == null ? "（提供商已删除）" : prov.Name;
                var it = new ListViewItem(m.Model);
                it.SubItems.Add(pname);
                it.SubItems.Add(Providers.LooksVision(m.Model) ? "可读图" : "");
                it.Tag = m.Id;
                _poolList.Items.Add(it);
            }

            FillRoleBox(_roleTranslate, keepT);
            FillRoleBox(_roleOcr, keepO);
            FillRoleBox(_roleAsk, keepA);
            RefreshLevels();
            _loading = false;
        }

        void FillRoleBox(ComboBox box, string selectedId)
        {
            box.Items.Clear();
            box.Items.Add("（未选择）");
            int idx = 0;
            for (int i = 0; i < _cfg.Pool.Count; i++)
            {
                var m = _cfg.Pool[i];
                var prov = _cfg.FindProvider(m.ProviderId);
                string pname = prov == null ? "?" : prov.Name;
                box.Items.Add(pname + " · " + m.Model);
                if (m.Id == selectedId) idx = i + 1;
            }
            box.SelectedIndex = idx;
        }

        void OnRoleChanged(string role, ComboBox box)
        {
            if (_loading) return;
            int i = box.SelectedIndex - 1;
            string id = (i >= 0 && i < _cfg.Pool.Count) ? _cfg.Pool[i].Id : "";
            _cfg.Roles[role] = id;
            if (role == "ask" || role == "translate") RefreshLevels();
        }

        void RefreshLevels()
        {
            if (_levelBox == null) return;
            var t = _cfg.ResolveRole("ask");
            if (t == null) t = _cfg.ResolveRole("translate");
            _levelBox.Items.Clear();
            if (t == null)
            {
                _levelBox.Items.Add("默认");
                _levelBox.SelectedIndex = 0;
                _levelHint.Text = "先给翻译或询问选个模型";
                return;
            }
            var levels = Thinking.LevelsFor(t.PresetId, t.Model);
            string sel = Thinking.Clamp(t.PresetId, t.Model, _cfg.Settings.AskLevel);
            int idx = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                _levelBox.Items.Add(Thinking.Label(levels[i]));
                if (levels[i] == sel) idx = i;
            }
            _levelBox.SelectedIndex = idx;
            var fam = Thinking.Get(t.PresetId, t.Model);
            _levelHint.Text = fam.Label + "：" + fam.Note;
        }

        async void TestModel()
        {
            _testBtn.Enabled = false;
            _testLabel.Text = "测试中…";
            try
            {
                var cfg = _cfg;
                var t = cfg.ResolveRole("translate");
                if (t == null)
                {
                    _testLabel.Text = "还没选翻译模型";
                    return;
                }
                var msgs = new List<ChatMsg>();
                msgs.Add(new ChatMsg("system", "你是连通性测试助手，只回复四个字：连接正常。"));
                msgs.Add(new ChatMsg("user", "ping"));
                string text = await Api.ChatAsync(t, msgs, Thinking.Disable(t.PresetId, t.Model), false, null,
                    Math.Min(cfg.Settings.TimeoutSec, 60), 0, 32);
                _testLabel.Text = t.Model + "：" + Api.Shorten(text, 60);
            }
            catch (Exception ex)
            {
                _testLabel.Text = Api.Humanize(ex).Replace("\r", " ").Replace("\n", " ");
            }
            finally
            {
                _testBtn.Enabled = true;
            }
        }

        void DeletePoolItem()
        {
            if (_poolList.SelectedItems.Count == 0) return;
            string id = Convert.ToString(_poolList.SelectedItems[0].Tag);
            _cfg.Pool.RemoveAll(delegate(PoolItem m) { return m.Id == id; });
            RefreshPool();
        }

        /* ---------------- 提示词 ---------------- */

        void LoadPrompts()
        {
            var p = _cfg.Settings.Prompts;
            _promptDraft["translate"] = p.Translate;
            _promptDraft["ask"] = p.Ask;
            _promptDraft["ocr"] = p.Ocr;
            _promptDraft["ocrLayout"] = p.OcrLayout;
            _promptDraft["translateLines"] = p.TranslateLines;
            LoadPromptBox();
        }

        string CurrentPrompt()
        {
            string v;
            if (_promptDraft.TryGetValue(_promptKey, out v)) return v;
            return "";
        }

        void LoadPromptBox()
        {
            if (_promptBox == null) return;
            _loading = true;
            _promptBox.Text = CurrentPrompt();
            _promptBox.Text = _promptBox.Text.Replace("\r\n", "\r\n");
            _loading = false;
        }

        void StashPrompt()
        {
            if (_loading) return;
            _promptDraft[_promptKey] = _promptBox.Text;
        }

        void SwitchPrompt()
        {
            StashPrompt();
            string[] keys = new string[] { "translate", "ask", "ocr", "ocrLayout", "translateLines" };
            int i = _promptPick.SelectedIndex;
            if (i < 0 || i >= keys.Length) return;
            _promptKey = keys[i];
            LoadPromptBox();
        }

        /* ---------------- 行为 / 系统 ---------------- */

        void FillSettings()
        {
            _loading = true;
            var s = _cfg.Settings;

            _langBox.Text = s.TargetLang;
            _engineBox.SelectedIndex = SearchEngines.IndexOf(s.SearchEngine);
            _searchTpl.Text = s.SearchUrlTpl;
            _temp.Value = (decimal)Math.Max(0, Math.Min(2, s.Temperature));
            _maxTokens.Value = Math.Max(0, Math.Min(64000, s.MaxTokens));
            _timeout.Value = Math.Max(10, Math.Min(600, s.TimeoutSec));
            _selPopup.Checked = s.SelectionPopup;
            _stream.Checked = s.Stream;
            _hkOcr.Text = s.HotkeyOcr;
            _hkCapture.Text = s.HotkeyCapture;
            _hkTranslate.Text = s.HotkeyTranslate;
            _autostart.Checked = IsAutostart();
            UpdateShellStatus();
            UpdateSearchTplEnabled();
            _loading = false;
        }

        void UpdateSearchTplEnabled()
        {
            int i = _engineBox.SelectedIndex;
            _searchTpl.Enabled = (i < 0 || SearchEngines.Ids[i] == "custom");
        }

        void UpdateShellStatus()
        {
            bool reg = ShellMenu.IsRegistered();
            bool send = ShellMenu.SendToRegistered();
            _shellStatus.Text = "注册表菜单：" + (reg ? "已注册" : "未注册") +
                                "　发送到：" + (send ? "已添加" : "无");
            _shellBtn.Text = (reg || send) ? "注销" : "注册";
        }

        void ToggleShell()
        {
            if (ShellMenu.IsRegistered() || ShellMenu.SendToRegistered())
            {
                ShellMenu.Unregister();
                ShellMenu.UnregisterSendTo();
                _cfg.Settings.ShellMenu = false;
                _cfg.Save();
                UpdateShellStatus();
                SetStatusText("已注销图片菜单");
                return;
            }

            bool okReg = ShellMenu.Register();
            if (okReg)
            {
                _cfg.Settings.ShellMenu = true;
                _cfg.Save();
                UpdateShellStatus();
                SetStatusText("已注册。Win11 上要在图片右键里点「显示更多选项」才能看到");
                return;
            }

            // 注册表被拦（很常见，安全软件会拒写 Classes），改用「发送到」菜单
            string why = ShellMenu.RegisterError == null ? "未知原因" : ShellMenu.RegisterError;
            bool okSend = ShellMenu.RegisterSendTo();
            _cfg.Settings.ShellMenu = okSend;
            _cfg.Save();
            UpdateShellStatus();
            if (okSend)
            {
                MessageBox.Show(this,
                    "注册表写入被拦住了（多半是安全软件）：\r\n" + why + "\r\n\r\n" +
                    "已自动改用「发送到」菜单：右键图片 → 发送到 → LensPop 识别文字 / 翻译图片。\r\n" +
                    "这条不需要写注册表，一般不会被拦。",
                    "已切换为发送到菜单", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string why2 = ShellMenu.RegisterError == null ? "未知原因" : ShellMenu.RegisterError;
                string regPath = null;
                try { regPath = ShellMenu.WriteRegFile(); } catch (Exception ex) { why2 += "\r\n写 .reg 也失败：" + ex.Message; }

                if (regPath != null)
                {
                    MessageBox.Show(this,
                        "本程序没有写注册表的权限（多半是被安全软件或受限账户拦了）：\r\n" + why + "\r\n" + why2 + "\r\n\r\n" +
                        "已经生成一个注册表文件，**双击导入**就行（导入是资源管理器执行的，不受这个限制）：\r\n" + regPath + "\r\n\r\n" +
                        "导入后：图片右键 →「显示更多选项」→ LensPop 识别文字 / 翻译图片。\r\n" +
                        "路径已经复制到剪贴板。",
                        "改用 .reg 导入", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    try { Clipboard.SetText(regPath); } catch { }
                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + regPath + "\""); } catch { }
                }
                else
                {
                    MessageBox.Show(this, "注册失败：\r\n" + why + "\r\n" + why2, "注册失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /* ---------------- 导入导出 ---------------- */

        void ExportConfig()
        {
            try
            {
                if (!File.Exists(AppConfig.FilePath))
                {
                    _cfg.Save();
                }
                var dlg = new SaveFileDialog();
                dlg.Filter = "LensPop 配置|*.json";
                dlg.FileName = "lenspop-config-" + DateTime.Now.ToString("yyyyMMdd") + ".json";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.Copy(AppConfig.FilePath, dlg.FileName, true);
                SetStatusText("已导出到 " + dlg.FileName);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "导出失败：" + e.Message);
            }
        }

        void ImportConfig()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "LensPop 配置|*.json";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var imported = AppConfig.LoadFrom(dlg.FileName);
            if (imported == null)
            {
                MessageBox.Show(this, "这个文件不是有效的 LensPop 配置。", "导入配置");
                return;
            }
            imported.Save();
            MessageBox.Show(this,
                "已导入：" + imported.Providers.Count + " 个提供商 / " + imported.Pool.Count + " 个模型池条目。\r\n" +
                "重新打开设置就能看到。",
                "导入配置");
            Close();
        }

        /* ---------------- 自检 ---------------- */

        void TestGrab()
        {
            using (var f = new GrabTestForm())
            {
                f.ShowDialog(this);
            }
            SetStatusText("取词测试完成");
            return;
        }

        void TestGrabOld_Unused()
        {
            SetStatusText("3 秒后模拟 Ctrl+C 取词：请先去别的窗口选中一段文字，保持它在前台…");
            var t = new System.Windows.Forms.Timer();
            t.Interval = 3000;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                string text = SelectionGrabber.Grab(800);
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show(this,
                        "没抓到文字。常见原因：\r\n\r\n" +
                        "1. 这 3 秒里焦点不在有选中文字的窗口上，或者没真的选中\r\n" +
                        "2. 目标窗口以管理员权限运行（普通权限拿不到）\r\n" +
                        "3. 目标程序不支持 Ctrl+C 复制（终端、某些游戏）\r\n" +
                        "4. 安全软件拦了模拟按键",
                        "取词自测", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    string show = text.Length > 200 ? text.Substring(0, 200) + "…" : text;
                    MessageBox.Show(this, "抓到 " + text.Length + " 个字符：\r\n\r\n" + show,
                        "取词自测", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                SetStatusText("自检完成");
            };
            t.Start();
        }

        void TestPopup()
        {
            var pop = new PopupForm();
            pop.ShowAt(Cursor.Position, "测试");
            var t = new System.Windows.Forms.Timer();
            t.Interval = 5000;
            t.Tick += delegate { t.Stop(); t.Dispose(); pop.Close(); };
            t.Start();
            SetStatusText("应该在鼠标旁出现一个小胶囊；没出现就是弹层被别的窗口盖住或被安全软件拦了");
        }

        void RunDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("LensPop 自检  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("版本        : " + Program.Version);
            sb.AppendLine("配置文件    : " + AppConfig.FilePath + (File.Exists(AppConfig.FilePath) ? "（存在）" : "（还不存在）"));
            sb.AppendLine("划词弹窗    : " + (_cfg.Settings.SelectionPopup ? "开" : "关"));
            sb.AppendLine("鼠标事件    : " + MouseHook.EventCount + " 个（按下 " + MouseHook.ClickCount + " / 拖拽 " + MouseHook.DragCount + "）");
            sb.AppendLine("钩子状态    : " + (MouseHook.EventCount > 0
                ? "正常（能收到鼠标事件）"
                : "没收到任何鼠标事件 —— 可能是钩子没装上、或被杀毒/安全软件拦截"));
            string clip = "";
            try { if (Clipboard.ContainsText()) clip = Clipboard.GetText(); } catch { }
            sb.AppendLine("剪贴板文字  : " + (clip == null ? 0 : clip.Trim().Length) + " 字符");
            sb.AppendLine("前台是本程序: " + (MouseHook.ForegroundIsSelf(System.Diagnostics.Process.GetCurrentProcess().Id) ? "是" : "否"));
            sb.AppendLine("注册表菜单  : " + (ShellMenu.IsRegistered() ? "已注册" : "未注册"));
            sb.AppendLine("发送到菜单  : " + (ShellMenu.SendToRegistered() ? "已添加" : "无"));
            sb.AppendLine("当前进程可写注册表: " + (CanWriteRegistry() ? "可以" : "不行（会给出 .reg 导入方案）"));
            sb.AppendLine("进程路径    : " + System.Windows.Forms.Application.ExecutablePath);
            sb.AppendLine("启动时间    : " + System.Diagnostics.Process.GetCurrentProcess().StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            uint mods;
            uint vk;
            sb.AppendLine("识别热键    : " + (_hkOcr.Text.Trim().Length == 0 ? "未设置" : (_hkOcr.Text + " " + (HotkeyHost.Parse(_hkOcr.Text, out mods, out vk) ? "格式有效" : "格式无效"))));
            sb.AppendLine("截图热键    : " + (_hkCapture.Text.Trim().Length == 0 ? "未设置" : (_hkCapture.Text + " " + (HotkeyHost.Parse(_hkCapture.Text, out mods, out vk) ? "格式有效" : "格式无效"))));
            sb.AppendLine("提供商      : " + _cfg.Providers.Count + " 个 / 模型池 " + _cfg.Pool.Count + " 个");
            sb.AppendLine("角色        : 翻译=" + RoleName("translate") + " OCR=" + RoleName("ocr") + " 询问=" + RoleName("ask"));

            string path = Path.Combine(AppConfig.Dir, "diag.txt");
            try { File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false)); } catch { }
            sb.AppendLine();
            sb.AppendLine("已保存到：" + path);
            MessageBox.Show(this, sb.ToString(), "自检结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatusText("自检完成");
        }

        static bool CanWriteRegistry()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\LensPopWriteProbe"))
                {
                    if (k == null) return false;
                }
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\LensPopWriteProbe", false);
                return true;
            }
            catch { return false; }
        }

        string RoleName(string role)
        {
            var item = _cfg.FindPool(_cfg.Roles.ContainsKey(role) ? _cfg.Roles[role] : null);
            return item == null ? "未设置" : item.Model;
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static bool IsAutostart()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (k == null) return false;
                    return k.GetValue("LensPop") != null;
                }
            }
            catch { return false; }
        }

        static void SetAutostart(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;
                    if (on) k.SetValue("LensPop", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("LensPop", false);
                }
            }
            catch { }
        }

        bool SaveAll()
        {
            CommitProviderFields();
            var s = _cfg.Settings;

            string lang = (_langBox.Text == null ? "" : _langBox.Text.Trim());
            s.TargetLang = lang.Length == 0 ? "中文（简体）" : lang;

            int ei = _engineBox.SelectedIndex;
            s.SearchEngine = (ei >= 0 && ei < SearchEngines.Ids.Length) ? SearchEngines.Ids[ei] : "bing";
            s.SearchUrlTpl = _searchTpl.Text.Trim();
            s.Temperature = (double)_temp.Value;
            s.MaxTokens = (int)_maxTokens.Value;
            s.TimeoutSec = (int)_timeout.Value;
            s.SelectionPopup = _selPopup.Checked;
            s.Stream = _stream.Checked;
            s.ShellMenu = ShellMenu.IsRegistered();

            // 档位
            var t = _cfg.ResolveRole("ask");
            if (t == null) t = _cfg.ResolveRole("translate");
            if (t != null)
            {
                var levels = Thinking.LevelsFor(t.PresetId, t.Model);
                int li = _levelBox.SelectedIndex;
                if (li >= 0 && li < levels.Count) s.AskLevel = levels[li];
            }

            // 热键
            uint mods;
            uint vk;
            string hk = _hkOcr.Text.Trim();
            if (hk.Length > 0 && !HotkeyHost.Parse(hk, out mods, out vk))
            {
                _status.Text = "识别热键格式不对：" + hk;
                return false;
            }
            s.HotkeyOcr = hk;
            string hc = _hkCapture.Text.Trim();
            if (hc.Length > 0 && !HotkeyHost.Parse(hc, out mods, out vk))
            {
                _status.Text = "截图热键格式不对：" + hc;
                return false;
            }
            s.HotkeyCapture = hc;

            string ht = _hkTranslate.Text.Trim();
            if (ht.Length > 0 && !HotkeyHost.Parse(ht, out mods, out vk))
            {
                _status.Text = "翻译热键格式不对：" + ht;
                return false;
            }
            s.HotkeyTranslate = ht;

            // 提示词
            StashPrompt();
            string v;
            _promptDraft.TryGetValue("translate", out v);
            s.Prompts.Translate = v == null ? "" : v.Trim();
            _promptDraft.TryGetValue("ask", out v);
            s.Prompts.Ask = v == null ? "" : v.Trim();
            _promptDraft.TryGetValue("ocr", out v);
            s.Prompts.Ocr = v == null ? "" : v.Trim();
            _promptDraft.TryGetValue("ocrLayout", out v);
            s.Prompts.OcrLayout = v == null ? "" : v.Trim();
            _promptDraft.TryGetValue("translateLines", out v);
            s.Prompts.TranslateLines = v == null ? "" : v.Trim();

            SetAutostart(_autostart.Checked);
            _cfg.Normalize();
            _cfg.Save();
            return true;
        }
    }
}
