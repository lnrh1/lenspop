using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LensPop
{
    static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string ocrPath = ArgValue(args, "--ocr");
            string transPath = ArgValue(args, "--translate");

            if (!string.IsNullOrEmpty(ocrPath)) { RunFile(ocrPath, false); return; }
            if (!string.IsNullOrEmpty(transPath)) { RunFile(transPath, true); return; }
            if (HasFlag(args, "--settings"))
            {
                new SettingsForm(AppConfig.Load()).ShowDialog();
                return;
            }

            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { Fatal(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Fatal(e.ExceptionObject as Exception); };

            bool created;
            using (var mutex = new Mutex(true, "LensPop.Tray.SingleInstance", out created))
            {
                if (!created) return;
                try
                {
                    Application.Run(new TrayApp());
                }
                catch (Exception e)
                {
                    Fatal(e);
                }
            }
        }

        static void Fatal(Exception e)
        {
            string msg = e == null ? "未知错误" : (e.GetType().Name + "：" + e.Message);
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppConfig.Dir, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n" + (e == null ? "" : e.StackTrace) + "\r\n\r\n");
            }
            catch { }
            try { MessageBox.Show("LensPop 出错了：\r\n\r\n" + msg, "LensPop", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
        }

        static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static bool HasFlag(string[] args, string name)
        {
            foreach (string a in args) if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void RunFile(string path, bool translate)
        {
            var cfg = AppConfig.Load();
            Bitmap bmp;
            try
            {
                bmp = ImageTools.LoadFromPath(path);
            }
            catch (Exception e)
            {
                MessageBox.Show("打不开这张图片：" + Api.Humanize(e), "LensPop");
                return;
            }

            var hider = new Form();
            hider.FormBorderStyle = FormBorderStyle.None;
            hider.ShowInTaskbar = false;
            hider.StartPosition = FormStartPosition.Manual;
            hider.Location = new Point(-32000, -32000);
            hider.Size = new Size(1, 1);
            hider.Show();

            Action<Form> wire = delegate(Form f)
            {
                f.FormClosed += delegate { Application.Exit(); };
            };

            hider.BeginInvoke(new Action(delegate
            {
                if (translate) ImageWorkflow.Translate(cfg, bmp, Cursor.Position, wire);
                else ImageWorkflow.Ocr(cfg, bmp, Cursor.Position, Info, wire);
            }));

            Application.Run(hider);
        }

        public static void Info(string text)
        {
            MessageBox.Show(text, "LensPop", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>托盘常驻：划词弹窗、热键、剪贴板图片</summary>
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon _tray = new NotifyIcon();
        readonly MouseHook _hook = new MouseHook();
        readonly HotkeyHost _hotkeys = new HotkeyHost();
        readonly Form _invoker = new Form();
        PopupForm _popup;
        AppConfig _cfg;
        ContextMenuStrip _menu;

        public TrayApp()
        {
            _cfg = AppConfig.Load();

            _invoker.FormBorderStyle = FormBorderStyle.None;
            _invoker.ShowInTaskbar = false;
            _invoker.StartPosition = FormStartPosition.Manual;
            _invoker.Location = new Point(-32000, -32000);
            _invoker.Size = new Size(1, 1);
            _invoker.Show();

            _tray.Icon = LoadIcon();
            _tray.Text = "LensPop · 划词透镜";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { OpenSettings(); };

            BuildMenu();

            _hook.Clicked += OnGlobalClick;
            _hook.DragEnded += OnDragEnded;
            _hook.Install();

            RegisterHotkeys();

            if (_cfg.Settings.ShellMenu && !ShellMenu.IsRegistered()) ShellMenu.Register();
        }

        static Icon LoadIcon()
        {
            try
            {
                var ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ic != null) return ic;
            }
            catch { }
            return SystemIcons.Application;
        }

        void BuildMenu()
        {
            if (_menu != null) _menu.Dispose();
            var m = new ContextMenuStrip();
            m.Font = Theme.UI;

            m.Items.Add("设置…", null, delegate { OpenSettings(); });

            var pop = new ToolStripMenuItem("划词弹窗");
            pop.Checked = _cfg.Settings.SelectionPopup;
            pop.Click += delegate
            {
                _cfg.Settings.SelectionPopup = !_cfg.Settings.SelectionPopup;
                pop.Checked = _cfg.Settings.SelectionPopup;
                _cfg.Save();
            };
            m.Items.Add(pop);
            m.Items.Add(new ToolStripSeparator());

            string hkOcr = string.IsNullOrEmpty(_cfg.Settings.HotkeyOcr) ? "" : "（" + _cfg.Settings.HotkeyOcr + "）";
            string hkCap = string.IsNullOrEmpty(_cfg.Settings.HotkeyCapture) ? "" : "（" + _cfg.Settings.HotkeyCapture + "）";

            m.Items.Add("识别剪贴板图片" + hkOcr, null, delegate { OcrClipboard(); });
            m.Items.Add("翻译剪贴板图片", null, delegate { TranslateClipboard(); });
            m.Items.Add("截图识别" + hkCap, null, delegate { Capture(false); });
            m.Items.Add("截图翻译", null, delegate { Capture(true); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("打开配置目录", null, delegate
            {
                try { Process.Start("explorer.exe", AppConfig.Dir); } catch { }
            });
            m.Items.Add("退出", null, delegate { Quit(); });

            _menu = m;
            _tray.ContextMenuStrip = m;
        }

        void Quit()
        {
            try { _hook.Uninstall(); } catch { }
            try { _hotkeys.Dispose(); } catch { }
            _tray.Visible = false;
            _tray.Dispose();
            Application.Exit();
        }

        void RegisterHotkeys()
        {
            _hotkeys.UnregisterAll();
            string hk = _cfg.Settings.HotkeyOcr;
            if (!string.IsNullOrEmpty(hk)) _hotkeys.Register(hk, delegate { OcrClipboard(); });
            string hc = _cfg.Settings.HotkeyCapture;
            if (!string.IsNullOrEmpty(hc)) _hotkeys.Register(hc, delegate { Capture(false); });
        }

        void OpenSettings()
        {
            HidePopup();
            using (var f = new SettingsForm(AppConfig.Load()))
            {
                f.ShowDialog();
            }
            _cfg = AppConfig.Load();
            BuildMenu();
            RegisterHotkeys();
        }

        /* ---------- 划词 ---------- */

        void OnGlobalClick(Point pt)
        {
            if (_popup == null || !_popup.Visible) return;
            if (_popup.Bounds.Contains(pt)) return;
            _popup.Hide();
        }

        void OnDragEnded(Point pt)
        {
            if (!_cfg.Settings.SelectionPopup) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string text = SelectionGrabber.Grab(500);
                if (string.IsNullOrEmpty(text)) return;
                text = text.Trim();
                if (text.Length < 2) return;
                if (text.Length > 6000) text = text.Substring(0, 6000);
                try
                {
                    _invoker.BeginInvoke(new Action(delegate { ShowPopup(pt, text); }));
                }
                catch { }
            });
        }

        void ShowPopup(Point pt, string text)
        {
            if (_popup == null)
            {
                _popup = new PopupForm();
                _popup.TranslateClicked += delegate { OpenTranslate(_popup.SelectionText); };
                _popup.AskClicked += delegate { OpenAsk(_popup.SelectionText); };
                _popup.SearchClicked += delegate { Search(_popup.SelectionText); };
                _popup.SettingsClicked += delegate { OpenSettings(); };
            }
            _popup.ShowAt(pt, text);
        }

        void HidePopup()
        {
            if (_popup != null && _popup.Visible) _popup.Hide();
        }

        void OpenTranslate(string text)
        {
            _cfg = AppConfig.Load();
            var t = _cfg.ResolveRole("translate");
            if (t == null)
            {
                Program.Info("还没选「翻译」模型。\r\n托盘图标右键 → 设置 → 模型池 → 选一个模型。");
                return;
            }
            var f = new ResultForm(_cfg, t, ResultMode.Translate, text);
            f.PlaceNear(Cursor.Position);
            f.Show();
        }

        void OpenAsk(string text)
        {
            _cfg = AppConfig.Load();
            var t = _cfg.ResolveRole("ask");
            if (t == null) t = _cfg.ResolveRole("translate");
            if (t == null)
            {
                Program.Info("还没选「询问」模型。\r\n托盘图标右键 → 设置 → 模型池。");
                return;
            }
            var f = new ResultForm(_cfg, t, ResultMode.Ask, text);
            f.PlaceNear(Cursor.Position);
            f.Show();
            f.Activate();
        }

        void Search(string text)
        {
            _cfg = AppConfig.Load();
            string url = SearchEngines.BuildUrl(_cfg.Settings, text);
            try
            {
                var psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Program.Info("打不开浏览器：" + e.Message);
            }
        }

        /* ---------- 图片 ---------- */

        Bitmap ClipboardImage()
        {
            try
            {
                if (!Clipboard.ContainsImage()) return null;
                var img = Clipboard.GetImage();
                if (img == null) return null;
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }

        void OcrClipboard()
        {
            var bmp = ClipboardImage();
            if (bmp == null)
            {
                Program.Info("剪贴板里没有图片。可以先截图（Win+Shift+S）或者复制一张图。");
                return;
            }
            _cfg = AppConfig.Load();
            ImageWorkflow.Ocr(_cfg, bmp, Cursor.Position, Program.Info, null);
        }

        void TranslateClipboard()
        {
            var bmp = ClipboardImage();
            if (bmp == null)
            {
                Program.Info("剪贴板里没有图片。");
                return;
            }
            _cfg = AppConfig.Load();
            ImageWorkflow.Translate(_cfg, bmp, Cursor.Position, null);
        }

        void Capture(bool translate)
        {
            HidePopup();
            _cfg = AppConfig.Load();
            var f = new CaptureForm(delegate(Bitmap bmp, Point at)
            {
                if (translate) ImageWorkflow.Translate(_cfg, bmp, at, null);
                else ImageWorkflow.Ocr(_cfg, bmp, at, Program.Info, null);
            });
            f.Show();
            f.Activate();
        }
    }

    /// <summary>图片流水线：识别 / 翻译后贴回原图</summary>
    static class ImageWorkflow
    {
        static List<object> ImageContent(string text, string dataUrl)
        {
            var list = new List<object>();
            var t = new Dictionary<string, object>();
            t["type"] = "text";
            t["text"] = text;
            list.Add(t);
            var img = new Dictionary<string, object>();
            img["type"] = "image_url";
            var inner = new Dictionary<string, object>();
            inner["url"] = dataUrl;
            img["image_url"] = inner;
            list.Add(img);
            return list;
        }

        public static async void Ocr(AppConfig cfg, Bitmap bmp, Point at, Action<string> info, Action<Form> onForm)
        {
            var t = cfg.ResolveRole("ocr");
            if (t == null)
            {
                if (info != null) info("还没配置 OCR 模型。\r\n托盘图标右键 → 设置 → 模型池 → 给「OCR」选一个能读图的模型。");
                try { bmp.Dispose(); } catch { }
                return;
            }

            Bitmap small = null;
            try
            {
                small = ImageTools.Downscale(bmp, ImageTools.MaxSide);
                string dataUrl = ImageTools.ToDataUrl(small);

                var form = new ResultForm(cfg, t, ResultMode.Text, "");
                form.TitleText = "识别文字";
                form.PlaceNear(at);
                form.SetStatusText("识别中…");
                if (onForm != null) onForm(form);
                form.Show();

                var msgs = new List<ChatMsg>();
                msgs.Add(new ChatMsg("system", ResultForm.PromptFor(cfg, "ocr")));
                msgs.Add(new ChatMsg("user", ImageContent("这是需要识别的图片。", dataUrl)));

                await Api.ChatAsync(t, msgs, Thinking.Disable(t.PresetId, t.Model), cfg.Settings.Stream,
                    delegate(string d) { form.AppendPlain(d); },
                    Math.Max(cfg.Settings.TimeoutSec, 120), 0, cfg.Settings.MaxTokens);
                form.SetStatusText("完成");
            }
            catch (Exception ex)
            {
                if (info != null) info(Api.Humanize(ex));
            }
            finally
            {
                try { if (small != null) small.Dispose(); } catch { }
            }
        }

        public static async void Translate(AppConfig cfg, Bitmap bmp, Point at, Action<Form> onForm)
        {
            var ocr = cfg.ResolveRole("ocr");
            if (ocr == null)
            {
                Program.Info("图片翻译需要一个能读图的 OCR 模型。\r\n托盘图标右键 → 设置 → 模型池 → 选「OCR」。");
                try { bmp.Dispose(); } catch { }
                return;
            }

            Bitmap small = null;
            var busy = new BusyForm("正在识别图片…", at);
            busy.Show();
            busy.Refresh();
            try
            {
                small = ImageTools.Downscale(bmp, ImageTools.MaxSide);
                string dataUrl = ImageTools.ToDataUrl(small);

                var msgs = new List<ChatMsg>();
                msgs.Add(new ChatMsg("system", ResultForm.PromptFor(cfg, "ocrLayout")));
                msgs.Add(new ChatMsg("user", ImageContent("识别这张图片里每一行文字的位置、颜色与内容。", dataUrl)));

                string raw = await Api.ChatAsync(ocr, msgs, Thinking.Disable(ocr.PresetId, ocr.Model), false, null,
                    Math.Max(cfg.Settings.TimeoutSec, 150), 0, cfg.Settings.MaxTokens);

                int lw;
                int lh;
                var lines = ImageTools.ParseLines(raw, out lw, out lh);
                if (lines.Count == 0)
                {
                    busy.Close();
                    var textForm = new ResultForm(cfg, ocr, ResultMode.Text, raw == null ? "" : raw.Trim());
                    textForm.TitleText = "识别文字（没定位到行框，只给了文本）";
                    textForm.PlaceNear(at);
                    if (onForm != null) onForm(textForm);
                    textForm.Show();
                    return;
                }

                busy.SetText("正在翻译…");
                var tr = cfg.ResolveRole("translate");
                if (tr == null) tr = ocr;

                var texts = new List<string>();
                foreach (var l in lines) texts.Add(l.Text);
                var translations = await TranslateLines(cfg, tr, texts);
                for (int i = 0; i < lines.Count && i < translations.Count; i++) lines[i].Translation = translations[i];

                busy.SetText("正在贴回原图…");
                var composed = ImageTools.Compose(small, lines, lw, lh);
                busy.Close();

                var form = new ImageResultForm(composed, lines, ocr.ProviderName + " · " + ocr.Model);
                if (onForm != null) onForm(form);
                form.Show();
                form.Activate();
            }
            catch (Exception ex)
            {
                busy.Close();
                Program.Info(Api.Humanize(ex));
            }
            finally
            {
                try { if (small != null) small.Dispose(); } catch { }
            }
        }

        static async Task<List<string>> TranslateLines(AppConfig cfg, Target tr, List<string> texts)
        {
            var result = new List<string>(texts);
            var ser = new JavaScriptSerializer();
            var msgs = new List<ChatMsg>();
            msgs.Add(new ChatMsg("system", ResultForm.PromptFor(cfg, "translateLines")));
            msgs.Add(new ChatMsg("user", ser.Serialize(texts)));

            string raw = await Api.ChatAsync(tr, msgs, Thinking.Disable(tr.PresetId, tr.Model), false, null,
                cfg.Settings.TimeoutSec, 0, cfg.Settings.MaxTokens);

            try
            {
                string s = ImageTools.StripFence(raw);
                int a = s.IndexOf('[');
                int b = s.LastIndexOf(']');
                if (a < 0 || b <= a) return result;
                var arr = ser.DeserializeObject(s.Substring(a, b - a + 1)) as object[];
                if (arr == null || arr.Length != texts.Count) return result;
                for (int i = 0; i < arr.Length; i++)
                {
                    string v = arr[i] as string;
                    if (string.IsNullOrEmpty(v))
                    {
                        var d = arr[i] as Dictionary<string, object>;
                        if (d != null && d.ContainsKey("translation")) v = Convert.ToString(d["translation"]);
                    }
                    if (!string.IsNullOrEmpty(v) && v.Trim().Length > 0) result[i] = v.Trim();
                }
            }
            catch { }
            return result;
        }
    }

    /// <summary>干活时的小提示窗</summary>
    public class BusyForm : Form
    {
        readonly Label _label;

        public BusyForm(string text, Point at)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(16, 19, 26);
            Size = new Size(190, 44);

            _label = new Label();
            _label.Text = text;
            _label.Dock = DockStyle.Fill;
            _label.TextAlign = ContentAlignment.MiddleCenter;
            _label.ForeColor = Color.White;
            _label.Font = Theme.UI;
            Controls.Add(_label);

            int left = at.X - Width / 2;
            int top = at.Y + 18;
            var scr = Screen.FromPoint(at);
            left = Math.Max(scr.WorkingArea.Left + 4, Math.Min(left, scr.WorkingArea.Right - Width - 4));
            top = Math.Max(scr.WorkingArea.Top + 4, Math.Min(top, scr.WorkingArea.Bottom - Height - 4));
            Location = new Point(left, top);
            Theme.Round(this, 10);
        }

        public void SetText(string t)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetText(t); })); return; }
            _label.Text = t;
            Refresh();
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
