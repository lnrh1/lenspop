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
                if (!created)
                {
                    // 已经有实例在跑：先让它把设置窗弹出来
                    bool signaled = false;
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting("LensPop.OpenSettings"))
                        {
                            ev.Set();
                            signaled = true;
                        }
                    }
                    catch { }

                    if (signaled)
                    {
                        Program.LogLine("检测到已有实例，已通知它打开设置");
                        return;
                    }

                    // 通知不到（多半是某个看不见的旧实例占着）：别静默退出，让用户决定
                    var answer = MessageBox.Show(
                        "LensPop 已经在运行，但没响应。\r\n\r\n" +
                        "如果你看不到托盘图标，它可能被 Windows 折叠进隐藏区（点任务栏的 ^ 展开看看），" +
                        "也可能那个实例已经卡住了。\r\n\r\n" +
                        "要结束已有实例并重新启动吗？",
                        "LensPop", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer != DialogResult.Yes) return;

                    KillOtherInstances();
                    System.Threading.Thread.Sleep(500);
                }

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

        static void KillOtherInstances()
        {
            int me = System.Diagnostics.Process.GetCurrentProcess().Id;
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("LensPop"))
            {
                try
                {
                    if (p.Id == me) continue;
                    LogLine("结束旧实例 pid=" + p.Id);
                    p.Kill();
                }
                catch { }
            }
        }

        public static bool IsAdmin()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static void LogLine(string s)
        {
            try
            {
                string p = System.IO.Path.Combine(AppConfig.Dir, "debug.log");
                var fi = new System.IO.FileInfo(p);
                if (fi.Exists && fi.Length > 128 * 1024) System.IO.File.Delete(p);
                System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n");
            }
            catch { }
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
        MouseHook _hook;
        Thread _hookThread;
        readonly HotkeyHost _hotkeys = new HotkeyHost();
        readonly Form _invoker = new Form();
        PopupForm _popup;
        AppConfig _cfg;
        ContextMenuStrip _menu;
        EventWaitHandle _openEvent;

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

            // 鼠标钩子必须跑在独立线程里：装在 UI 线程上时，界面一忙鼠标就会发飘
            _hookThread = new Thread(delegate()
            {
                try
                {
                    _hook = new MouseHook();
                    _hook.Clicked += delegate(Point pt)
                    {
                        // 弹层没显示时不需要跨线程，减少一切无谓开销
                        if (_popup == null || !_popup.Visible) return;
                        Post(delegate { OnGlobalClick(pt); });
                    };
                    _hook.DragEnded += delegate(Point pt) { Post(delegate { OnDragEnded(pt); }); };

                    if (!_cfg.Settings.SelectionPopup)
                    {
                        Program.LogLine("划词弹窗已关闭，不安装鼠标钩子");
                        MessagePump.Run();
                        return;
                    }

                    bool ok = _hook.Install();
                    Program.LogLine("钩子安装=" + ok + (ok ? "" : (" 错误码=" + _hook.LastError)));
                    if (!ok)
                    {
                        Post(delegate
                        {
                            try
                            {
                                _tray.BalloonTipTitle = "划词功能没起来";
                                _tray.BalloonTipText = "全局鼠标钩子安装失败（可能被安全软件拦了）。打开设置 → 系统 → 运行自检 看详情。";
                                _tray.ShowBalloonTip(6000);
                            }
                            catch { }
                        });
                    }
                    MessagePump.Run();
                }
                catch (Exception e)
                {
                    Program.LogLine("钩子线程异常：" + e.Message);
                }
            });
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.IsBackground = true;
            _hookThread.Start();

            RegisterHotkeys();

            if (_cfg.Settings.ShellMenu && !ShellMenu.IsRegistered()) ShellMenu.Register();

            // 第二个实例双击启动时，把设置窗调出来
            try
            {
                _openEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "LensPop.OpenSettings");
                var th = new Thread(delegate()
                {
                    while (true)
                    {
                        try
                        {
                            _openEvent.WaitOne();
                            _invoker.BeginInvoke(new Action(OpenSettings));
                        }
                        catch { return; }
                    }
                });
                th.IsBackground = true;
                th.Start();
            }
            catch { }

            // 第一次运行（还没有配置文件）直接把设置窗摆出来，别让人以为没反应
            Program.LogLine("托盘就绪 pid=" + System.Diagnostics.Process.GetCurrentProcess().Id +
                " 图标可见=" + _tray.Visible + " 管理员=" + Program.IsAdmin());

            bool firstRun = !System.IO.File.Exists(AppConfig.FilePath);
            Program.LogLine("启动 firstRun=" + firstRun + " cfg=" + AppConfig.FilePath);
            try
            {
                _invoker.BeginInvoke(new Action(delegate
                {
                    Program.LogLine("进入首屏逻辑");
                    try
                    {
                        _tray.BalloonTipTitle = firstRun ? "LensPop 已启动" : "LensPop 已在托盘运行";
                        _tray.BalloonTipText = firstRun ? "右键托盘图标 → 设置，先填一个 API Key。" : "划词即译；右键托盘图标打开设置。";
                        _tray.ShowBalloonTip(3000);
                    }
                    catch { }
                    if (firstRun) OpenSettings();
                }));
            }
            catch (Exception e)
            {
                Program.LogLine("首屏调度失败：" + e.Message);
            }
        }

        void Post(Action a)
        {
            try
            {
                if (_invoker != null && _invoker.IsHandleCreated) _invoker.BeginInvoke(a);
            }
            catch { }
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

            string hkT = string.IsNullOrEmpty(_cfg.Settings.HotkeyTranslate) ? "" : "（" + _cfg.Settings.HotkeyTranslate + "）";
            m.Items.Add("翻译选中文字" + hkT, null, delegate { TranslateSelection(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("识别剪贴板图片" + hkOcr, null, delegate { OcrClipboard(); });
            m.Items.Add("翻译剪贴板图片", null, delegate { TranslateClipboard(); });
            m.Items.Add("截图识别" + hkCap, null, delegate { Capture(false); });
            m.Items.Add("截图翻译", null, delegate { Capture(true); });
            m.Items.Add(new ToolStripSeparator());
            bool shellOn = ShellMenu.IsRegistered();
            m.Items.Add(shellOn ? "注销图片右键菜单" : "注册图片右键菜单", null, delegate
            {
                if (ShellMenu.IsRegistered()) ShellMenu.Unregister();
                else ShellMenu.Register();
                _cfg.Settings.ShellMenu = ShellMenu.IsRegistered();
                _cfg.Save();
                BuildMenu();
                try
                {
                    _tray.BalloonTipTitle = "图片右键菜单";
                    _tray.BalloonTipText = _cfg.Settings.ShellMenu
                        ? "已注册。Win11 需要在图片上右键 →「显示更多选项」里找 LensPop。"
                        : "已注销。";
                    _tray.ShowBalloonTip(4000);
                }
                catch { }
            });
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
            try { if (_hook != null) _hook.Uninstall(); } catch { }
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
            string ht = _cfg.Settings.HotkeyTranslate;
            if (!string.IsNullOrEmpty(ht)) _hotkeys.Register(ht, delegate { TranslateSelection(); });
        }

        void OpenSettings()
        {
            Program.LogLine("OpenSettings 进入");
            HidePopup();
            try
            {
                var cfg = AppConfig.Load();
                Program.LogLine("配置载入 ok providers=" + cfg.Providers.Count);
                using (var f = new SettingsForm(cfg))
                {
                    Program.LogLine("设置窗已创建，准备 ShowDialog");
                    f.ShowDialog();
                    Program.LogLine("设置窗已关闭");
                }
            }
            catch (Exception e)
            {
                Program.LogLine("OpenSettings 异常：" + e.GetType().Name + " / " + e.Message + " @ " + e.StackTrace);
                throw;
            }
            _cfg = AppConfig.Load();
            BuildMenu();
            RegisterHotkeys();
            Program.LogLine("OpenSettings 结束");
        }

        /* ---------- 划词 ---------- */

        void OnGlobalClick(Point pt)
        {
            if (_popup == null || !_popup.Visible) return;
            if (_popup.Bounds.Contains(pt)) return;
            _popup.Hide();
        }

        bool _grabbing;

        void OnDragEnded(Point pt)
        {
            Program.LogLine("拖拽结束 @" + pt.X + "," + pt.Y + " 划词开关=" + _cfg.Settings.SelectionPopup);
            if (!_cfg.Settings.SelectionPopup) return;
            if (_grabbing) return;
            _grabbing = true;
            try
            {
                DoDragEnded(pt);
            }
            finally
            {
                _grabbing = false;
            }
        }

        void DoDragEnded(Point pt)
        {
            // 在自己的窗口里拖选文字（设置、结果窗）不要弹划词，也别去抢剪贴板
            if (MouseHook.ForegroundIsSelf(System.Diagnostics.Process.GetCurrentProcess().Id)) return;
            // 在 UI 线程上取词：后台线程发 Ctrl+C 会明显延迟，实测会一直等到超时
            string text = SelectionGrabber.GrabOnUiThread(700);
            Program.LogLine("取词结果长度=" + (string.IsNullOrEmpty(text) ? 0 : text.Trim().Length));
            if (string.IsNullOrEmpty(text)) return;
            text = text.Trim();
            if (text.Length < 2) return;
            if (text.Length > 6000) text = text.Substring(0, 6000);
            ShowPopup(pt, text);
        }

        void ShowPopup(Point pt, string text)
        {
            Program.LogLine("弹出划词条");
            if (_popup == null)
            {
                _popup = new PopupForm();
                _popup.TranslateClicked += delegate { OpenTranslate(_popup.SelectionText); };
                _popup.AskClicked += delegate { OpenAsk(_popup.SelectionText); };
                _popup.SearchClicked += delegate { Search(_popup.SelectionText); };
                _popup.SettingsClicked += delegate { OpenSettings(); };
            }
            _popup.ShowAt(pt, text);
            Program.LogLine("弹层位置 " + _popup.Bounds.X + "," + _popup.Bounds.Y + " 尺寸 " +
                _popup.Width + "x" + _popup.Height + " 可见=" + _popup.Visible);
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

        /// <summary>不依赖鼠标钩子的取词路径：按热键 → 模拟 Ctrl+C → 直接翻译</summary>
        void TranslateSelection()
        {
            Program.LogLine("热键取词");
            string text = SelectionGrabber.Grab(700);
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                Program.Info("没抓到选中文字。\r\n\r\n· 先在别的窗口里选中一段文字再按热键\r\n· 管理员权限的窗口需要 LensPop 也用管理员身份运行\r\n· 终端窗口不支持 Ctrl+C 复制");
                return;
            }
            OpenTranslate(text.Trim());
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
