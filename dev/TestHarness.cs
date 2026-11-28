using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LensPop;

namespace LensPopTests
{
    static class Harness
    {
        static StringBuilder Log = new StringBuilder();

        static void Line(string s) { Log.AppendLine(s); Console.WriteLine(s); }

        [STAThread]
        static int Main(string[] args)
        {
            foreach (string a in args) if (a == "--ui") { UiShots.Run(); return 0; }
            foreach (string a in args) if (a == "--hook-debug") return HookDebug.Run();
            foreach (string a in args) if (a == "--ui-preset") return PresetTest.Run();
            foreach (string a in args) if (a == "--config") return ConfigTest.Run();
            foreach (string a in args) if (a == "--grab") return GrabTest.Run();
            foreach (string a in args) if (a == "--grab-raw") return GrabRaw.Run();
            foreach (string a in args) if (a == "--shell") return ShellTest.Run();
            foreach (string a in args) if (a == "--sendto") return SendToTest.Run();
            foreach (string a in args) if (a == "--ui-provider") return ProviderSwitchTest.Run();
            foreach (string a in args) if (a == "--uia") return UiaTest.Run();
            foreach (string a in args) if (a == "--capture") return CaptureShot.Run();
            foreach (string a in args) if (a == "--screen") return ScreenInfo.Run();
            foreach (string a in args) if (a == "--screenshot") return ScreenShot.Run();
            foreach (string a in args) if (a == "--shotbar") return ShotBarClick.Run();
            foreach (string a in args) if (a == "--pill-html") return HtmlPill.Run();
            foreach (string a in args) if (a == "--ask") return AskShot.Run();
            foreach (string a in args) if (a == "--ask-enter") return AskEnter.Run();
            foreach (string a in args) if (a == "--md") return MdShot.Run();
            foreach (string a in args) if (a == "--focus") return FocusTest.Run();
            foreach (string a in args) if (a == "--hooktest") return HookTest.Run();
            foreach (string a in args) if (a == "--snap") return SnapShot.Run();
            try
            {
                TestImageEncode();
                TestUi();
                TestThinking();
                TestParseLines();
                TestSearch();
                TestCompose();
                MockServer.Run();
                TestApi().Wait();
            }
            catch (Exception e)
            {
                Line("!!! 失败：" + e);
                return 1;
            }
            File.WriteAllText("dist\\test-report.txt", Log.ToString(), new UTF8Encoding(false));
            return 0;
        }

        static void TestUi()
        {
            Line("== 界面构造 ==");
            var cfg = AppConfig.Load();
            cfg.Normalize();
            try
            {
                using (var f0 = new Form()) Line("  裸 Form ok");
            }
            catch (Exception e) { Line("  裸 Form 失败：" + e.Message); }
            try
            {
                using (var f = new PopupForm()) Line("  PopupForm ok " + f.Width + "x" + f.Height);
            }
            catch (Exception e) { Line("  PopupForm 失败：" + e.GetType().Name + " " + e.Message + " @ " + e.StackTrace); }
            try
            {
                using (var f = new SettingsForm(cfg)) Line("  SettingsForm ok");
            }
            catch (Exception e) { Line("  SettingsForm 失败：" + e.Message + " @ " + e.StackTrace); }
            try
            {
                using (var f = new BusyForm("测试", new Point(100, 100))) Line("  BusyForm ok");
            }
            catch (Exception e) { Line("  BusyForm 失败：" + e.Message); }
        }


        static void TestImageEncode()
        {
            Line("== 图片编码 ==");
            var bmp = new System.Drawing.Bitmap(200, 120);   // 默认 32bppArgb，正是截图那条路径
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.White);
                g.DrawString("LensPop", new System.Drawing.Font("Segoe UI", 18f), System.Drawing.Brushes.Black, 10, 40);
            }
            try
            {
                string url = LensPop.ImageTools.ToDataUrl(bmp);
                Line("  ToDataUrl 成功，长度=" + url.Length + " 前缀=" + url.Substring(0, 22));
            }
            catch (Exception e)
            {
                Line("  ToDataUrl 失败：" + e.GetType().Name + " " + e.Message);
            }
            bmp.Dispose();
        }

        static void TestThinking()
        {
            Line("== 思考档位 ==");
            var cases = new string[][]
            {
                new string[] { "deepseek", "deepseek-flash" },
                new string[] { "kimi", "kimi-k3-1-pro" },
                new string[] { "zhipu", "glm-5.3" },
                new string[] { "zhipu", "glm-4.6" },
                new string[] { "dashscope", "qwen3-max" },
                new string[] { "openai", "gpt-5" },
                new string[] { "openrouter", "openai/gpt-4o" }
            };
            foreach (var c in cases)
            {
                var fam = Thinking.Get(c[0], c[1]);
                var lv = Thinking.LevelsFor(c[0], c[1]);
                Line("  " + c[1] + " -> " + fam.Label + " [" + string.Join("/", lv.ToArray()) + "] 关思考参数=" + Json(Thinking.Disable(c[0], c[1])));
            }
            Line("  deepseek 询问档位 high = " + Json(Thinking.Build("deepseek", "deepseek-flash", "high")));
            Line("  deepseek 询问档位 off  = " + Json(Thinking.Build("deepseek", "deepseek-flash", "off")));
        }

        static void TestParseLines()
        {
            Line("== 布局 JSON 解析 ==");
            string raw = "好的，结果如下：\n" + new string((char)96, 3) + "json\n" +
                "{\"width\":900,\"height\":500,\"lines\":[" +
                "{\"box\":[40,60,320,34],\"color\":\"#1f3a8a\",\"text\":\"Hello world\",\"translation\":\"你好，世界\"}," +
                "{\"box\":[40,140,420,30],\"color\":\"#223344\",\"text\":\"This is a longer English sentence that needs shrinking\",\"translation\":\"这是一句更长的英文句子，翻译之后需要自动缩小字号才能塞进原来的位置\"}" +
                "]}" + new string((char)96, 3);
            int lw, lh;
            var lines = ImageTools.ParseLines(raw, out lw, out lh);
            Line("  图片尺寸=" + lw + "x" + lh + "，行数=" + lines.Count);
            foreach (var l in lines) Line("   - box=" + l.X + "," + l.Y + "," + l.W + "," + l.H + " color=" + l.Color + " 原文=" + l.Text + " 译文=" + l.Translation);
        }

        static void TestSearch()
        {
            Line("== 搜索 ==");
            var s = new Settings();
            s.SearchEngine = "bing";
            Line("  bing: " + SearchEngines.BuildUrl(s, "划词 翻译"));
            s.SearchEngine = "custom";
            s.SearchUrlTpl = "https://example.com/find?kw=%s";
            Line("  custom: " + SearchEngines.BuildUrl(s, "a b&c"));
        }

        static void TestCompose()
        {
            Line("== 图片排版（就地替换） ==");
            var src = new Bitmap(900, 500);
            using (var g = Graphics.FromImage(src))
            {
                g.Clear(Color.FromArgb(246, 247, 249));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var f1 = new Font("Segoe UI", 22f, FontStyle.Bold))
                using (var f2 = new Font("Segoe UI", 15f))
                using (var b1 = new SolidBrush(Color.FromArgb(31, 58, 138)))
                using (var b2 = new SolidBrush(Color.FromArgb(34, 51, 68)))
                {
                    g.DrawString("Hello world", f1, b1, 40, 60);
                    g.DrawString("This is a longer English sentence that needs shrinking", f2, b2, 40, 140);
                }
                using (var pen = new Pen(Color.FromArgb(220, 222, 228)))
                    g.DrawLine(pen, 40, 200, 860, 200);
            }

            string raw = "{\"width\":900,\"height\":500,\"lines\":[" +
                "{\"box\":[40,58,320,38],\"color\":\"#1f3a8a\",\"text\":\"Hello world\",\"translation\":\"你好，世界\"}," +
                "{\"box\":[40,138,420,32],\"color\":\"#223344\",\"text\":\"This is a longer English sentence that needs shrinking\",\"translation\":\"这是一句更长的英文句子，翻译之后需要自动缩小字号才能塞进原来的位置\"}" +
                "]}";
            int lw, lh;
            var lines = ImageTools.ParseLines(raw, out lw, out lh);
            var outBmp = ImageTools.Compose(src, lines, lw, lh);
            outBmp.Save("dist\\test-compose.png", System.Drawing.Imaging.ImageFormat.Png);
            Line("  已输出 dist\\test-compose.png（900x500）");
            src.Dispose();
            outBmp.Dispose();
        }

        static async Task Probe()
        {
            Line("== 原始 POST 探测 ==");
            string url = "http://127.0.0.1:" + MockServer.Port + "/v1/chat/completions";
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            req.AllowReadStreamBuffering = false;
            req.ServicePoint.Expect100Continue = false;
            byte[] data = Encoding.UTF8.GetBytes("{\"model\":\"m\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}],\"stream\":true}");
            req.ContentLength = data.Length;
            Line("  -> 取请求流");
            using (var rs = await req.GetRequestStreamAsync())
            {
                Line("  -> 拿到请求流，写入");
                await rs.WriteAsync(data, 0, data.Length);
            }
            Line("  -> 写完了，等响应");
            var resp = (HttpWebResponse)await req.GetResponseAsync();
            Line("  -> 响应 " + (int)resp.StatusCode + " " + resp.ContentType);
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string l;
                while ((l = await sr.ReadLineAsync()) != null) Line("     SSE| " + l);
            }
            Line("  -> 流结束");
        }

        static async Task TestApi()
        {
            Line("== API 客户端（本地假服务） ==");
            await Probe();
            string baseUrl = "http://127.0.0.1:" + MockServer.Port + "/v1";
            try
            {
                var models = await Api.ListModelsAsync(baseUrl, "test-key", 15);
                Line("  /models 返回 " + models.Count + " 个：" + string.Join(", ", models.ToArray()));
            }
            catch (Exception e)
            {
                Line("  /models 失败：" + e.Message);
            }

            var t = new Target();
            t.BaseUrl = baseUrl;
            t.ApiKey = "test-key";
            t.Model = "mock-model";
            t.PresetId = "deepseek";
            t.ProviderName = "Mock";

            Api.Trace = delegate(string m) { Line("    [api] " + m); };

            var msgs = new List<ChatMsg>();
            msgs.Add(new ChatMsg("system", "你是翻译引擎"));
            msgs.Add(new ChatMsg("user", "Hello"));

            var sb = new StringBuilder();
            Line("  开始流式调用…");
            string text = await Api.ChatAsync(t, msgs, Thinking.Disable("deepseek", "mock-model"), true,
                delegate(string d) { sb.Append(d); }, 30, 0.3, 0);
            Line("  流式返回结束");
            Line("  流式结果：" + text);
            Line("  onDelta 累计：" + sb.ToString());

            string once = await Api.ChatAsync(t, msgs, new Dictionary<string, object>(), false, null, 30, 0.3, 0);
            Line("  非流式结果：" + once);
        }

        static string Json(object o)
        {
            var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
            return ser.Serialize(o);
        }
    }

    /// <summary>把几个窗口依次显示并截图，用于人工核对界面（需要交互桌面会话）</summary>
    static class UiShots
    {
        static readonly List<Action> Steps = new List<Action>();
        static int _index;
        static string _outDir;
        static Form _current;

        public static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _outDir = Path.Combine(Environment.CurrentDirectory, "dev", "shots");
            Directory.CreateDirectory(_outDir);

            var cfg = AppConfig.Load();
            cfg.Normalize();
            var t = new Target();
            t.ProviderId = "demo";
            t.PresetId = "deepseek";
            t.ProviderName = "DeepSeek";
            t.Model = "deepseek-flash";
            t.BaseUrl = "https://api.deepseek.com/v1";

            Steps.Add(delegate
            {
                var f = new SettingsForm(cfg);
                Shot(f, "01-settings.png", 1200);
            });
            Steps.Add(delegate
            {
                var f = new ResultForm(cfg, t, ResultMode.Ask, "The quick brown fox jumps over the lazy dog. 这行字是怎么翻译的？");
                f.Shown += delegate
                {
                    f.SetPlain("▸ 这行英文在说什么？\n\n这是一句常见的英文练习句：那只敏捷的棕色狐狸跳过了懒狗。\n\n它本身没有特殊含义，主要用来展示字母表里的全部 26 个字母。");
                    f.SetStatusText("输入问题，Enter 发送");
                };
                Shot(f, "02-ask.png", 1300);
            });
            Steps.Add(delegate
            {
                var f = new ResultForm(cfg, t, ResultMode.Text, "In-place translation：只擦掉原来那一行字，用原文字色写回译文。\n\n模型给的框比原文窄时，会先按原文字号量出真实宽度，把多出来的原文一起擦掉。");
                f.TitleText = "翻译 → 中文（简体）";
                Shot(f, "03-translate.png", 1300);
            });
            Steps.Add(delegate
            {
                var img = new Bitmap(Path.Combine(_outDir, "..", "..", "docs", "example-image-translate.png"));
                var lines = new List<TextLine>();
                var f = new ImageResultForm(img, lines, "DeepSeek · deepseek-flash");
                Shot(f, "04-image.png", 1300);
            });
            Steps.Add(delegate
            {
                var f = new PopupForm();
                f.ShowLoading(new Point(520, 460));
                f.Ready(new Point(520, 460), "演示文字");
                _current = f;
                var tm = new System.Windows.Forms.Timer();
                tm.Interval = 1000;
                tm.Tick += delegate
                {
                    tm.Stop();
                    var r = f.Bounds;
                    r.Inflate(70, 70);
                    TakeShot(r, "05-popup.png");
                    f.Close();
                    Next();
                };
                tm.Start();
            });
            Steps.Add(delegate
            {
                Application.Exit();
            });
            Next();
            Application.Run();
        }

        static void Shot(Form f, string file, int delayMs)
        {
            _current = f;
            if (f.StartPosition != FormStartPosition.Manual)
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(120, 90);
            }
            f.TopMost = true;
            f.Show();
            f.Activate();
            f.BringToFront();
            var tm = new System.Windows.Forms.Timer();
            tm.Interval = delayMs;
            tm.Tick += delegate
            {
                tm.Stop();
                TakeShot(f.Bounds, file);
                f.Close();
                Next();
            };
            tm.Start();
        }

        static void Next()
        {
            int i = _index++;
            if (i >= Steps.Count) return;
            Steps[i]();
        }

        static void TakeShot(Rectangle bounds, string file)
        {
            try
            {
                var bmp = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(bounds.Width, bounds.Height));
                bmp.Save(Path.Combine(_outDir, file), System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
                Console.WriteLine("shot: " + file);
            }
            catch (Exception e)
            {
                Console.WriteLine("shot failed " + file + ": " + e.Message);
            }
        }
    }


    /// <summary>最小化验证：文本框选中 → 哨兵 → Ctrl+C → 读剪贴板（--grab-raw）</summary>
    static class GrabRaw
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        public static int Run()
        {
            Application.EnableVisualStyles();
            var f = new Form();
            f.Size = new System.Drawing.Size(400, 300);
            var box = new System.Windows.Forms.TextBox();
            box.Multiline = true;
            box.Dock = System.Windows.Forms.DockStyle.Fill;
            box.Text = "选中这段文字再点测试。Hello LensPop raw test.";
            f.Controls.Add(box);
            f.Show();
            f.Activate();
            box.Focus();
            box.Select(0, 12);
            Application.DoEvents();
            Console.WriteLine("前台是自己=" + LensPop.MouseHook.ForegroundIsSelf(System.Diagnostics.Process.GetCurrentProcess().Id) + " 选区=" + box.SelectionLength);

            string marker = "\u0001lenspop-raw";
            try { System.Windows.Forms.Clipboard.SetText(marker); Console.WriteLine("哨兵写入 ok"); }
            catch (Exception e) { Console.WriteLine("哨兵写入失败: " + e.Message); }

            keybd_event(0x11, 0, 0, UIntPtr.Zero);
            keybd_event(0x43, 0, 0, UIntPtr.Zero);
            keybd_event(0x43, 0, 0x0002, UIntPtr.Zero);
            keybd_event(0x11, 0, 0x0002, UIntPtr.Zero);

            for (int i = 0; i < 20; i++)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(50);
                string now = "";
                try { if (System.Windows.Forms.Clipboard.ContainsText()) now = System.Windows.Forms.Clipboard.GetText(); }
                catch (Exception e) { Console.WriteLine("  第" + i + "次读取异常: " + e.Message); continue; }
                Console.WriteLine("  第" + i + "次: [" + (now.Length > 20 ? now.Substring(0, 20) : now) + "]");
                if (now.Length > 0 && now != marker) { Console.WriteLine("OK 取到选中文字"); f.Close(); return 0; }
            }
            Console.WriteLine("FAIL 一直是哨兵或空");
            f.Close();
            return 1;
        }
    }

    /// <summary>取词测试窗自测：选中文字 → 跑一次 Grab（--grab）</summary>
    static class GrabTest
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var f = new LensPop.GrabTestForm();
            f.Show();
            Application.DoEvents();

            var t = typeof(LensPop.GrabTestForm);
            var box = (System.Windows.Forms.TextBox)t.GetField("_box", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(f);
            var outLbl = (System.Windows.Forms.Label)t.GetField("_out", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(f);
            box.Focus();
            box.Select(0, 20);
            Application.DoEvents();
            Console.WriteLine("选中的长度=" + box.SelectionLength);

            t.GetMethod("RunTest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(f, null);

            var until = DateTime.Now.AddSeconds(4);
            while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            f.Activate();
            Console.WriteLine("前台是自己=" + LensPop.MouseHook.ForegroundIsSelf(System.Diagnostics.Process.GetCurrentProcess().Id));
            string clip = "";
            try { if (System.Windows.Forms.Clipboard.ContainsText()) clip = System.Windows.Forms.Clipboard.GetText(); } catch { }
            Console.WriteLine("剪贴板前30字: " + (clip.Length > 30 ? clip.Substring(0, 30) : clip));
            Console.WriteLine("结果: " + outLbl.Text);
            f.Close();
            return outLbl.Text.StartsWith("取词成功") ? 0 : 1;
        }
    }

    /// <summary>回归：切换提供商不能把名字/BaseURL 写丢（--ui-provider）</summary>
    static class ProviderSwitchTest
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var seed = new LensPop.AppConfig();
            var a = new LensPop.ProviderDef();
            a.Id = "p_a"; a.PresetId = "deepseek"; a.Name = "提供商A"; a.BaseUrl = "https://a.example/v1"; a.ApiKey = "sk-a";
            var b = new LensPop.ProviderDef();
            b.Id = "p_b"; b.PresetId = "kimi"; b.Name = "提供商B"; b.BaseUrl = "https://b.example/v1"; b.ApiKey = "sk-b";
            seed.Providers.Add(a);
            seed.Providers.Add(b);
            seed.Save();

            var cfg = LensPop.AppConfig.Load();
            var form = new LensPop.SettingsForm(cfg);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(100, 80);
            form.Show();
            Application.DoEvents();

            var f = typeof(LensPop.SettingsForm).GetField("_provList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var list = (System.Windows.Forms.ListBox)f.GetValue(form);
            Console.WriteLine("provider 数=" + list.Items.Count + " 当前选中=" + list.SelectedIndex);

            list.SelectedIndex = 1;
            Application.DoEvents();
            System.Threading.Thread.Sleep(700);
            Application.DoEvents();

            list.SelectedIndex = 0;
            Application.DoEvents();
            System.Threading.Thread.Sleep(900);
            Application.DoEvents();

            var after = LensPop.AppConfig.Load();
            foreach (var p in after.Providers)
                Console.WriteLine("  " + p.Id + " name=" + p.Name + " url=" + p.BaseUrl + " key=" + p.ApiKey);

            bool ok = after.Providers.Count == 2
                && after.Providers[0].Name == "提供商A" && after.Providers[0].BaseUrl == "https://a.example/v1" && after.Providers[0].ApiKey == "sk-a"
                && after.Providers[1].Name == "提供商B" && after.Providers[1].BaseUrl == "https://b.example/v1" && after.Providers[1].ApiKey == "sk-b";
            Console.WriteLine(ok ? "PASS" : "FAIL");
            form.Close();
            return ok ? 0 : 1;
        }
    }

    /// <summary>发送到菜单注册实测（--sendto）</summary>
    static class SendToTest
    {
        public static int Run()
        {
            bool ok = LensPop.ShellMenu.RegisterSendTo();
            Console.WriteLine("注册结果=" + ok + " 错误=" + (LensPop.ShellMenu.RegisterError == null ? "无" : LensPop.ShellMenu.RegisterError));
            Console.WriteLine("已存在=" + LensPop.ShellMenu.SendToRegistered());
            Console.WriteLine("目录=" + LensPop.ShellMenu.SendToDir);
            return ok ? 0 : 1;
        }
    }

    /// <summary>右键菜单注册实测（--shell）</summary>
    static class ShellTest
    {
        public static int Run()
        {
            Console.WriteLine("注册前: " + LensPop.ShellMenu.IsRegistered());
            bool ok = LensPop.ShellMenu.Register();
            Console.WriteLine("注册结果=" + ok + " 错误=" + (LensPop.ShellMenu.RegisterError == null ? "无" : LensPop.ShellMenu.RegisterError));
            Console.WriteLine("注册后: " + LensPop.ShellMenu.IsRegistered());
            return ok ? 0 : 1;
        }
    }

    /// <summary>配置备份/恢复：删掉主配置后应能从 config.bak.json 恢复模型池（--config）</summary>
    static class ConfigTest
    {
        public static int Run()
        {
            Console.WriteLine("配置路径: " + LensPop.AppConfig.FilePath);
            var cfg = new LensPop.AppConfig();
            var p = new LensPop.ProviderDef();
            p.Id = "p_test";
            p.PresetId = "deepseek";
            p.Name = "测试提供商";
            p.BaseUrl = "https://api.deepseek.com/v1";
            p.ApiKey = "sk-test";
            cfg.Providers.Add(p);

            var m1 = new LensPop.PoolItem { Id = "m1", ProviderId = "p_test", Model = "deepseek-flash" };
            var m2 = new LensPop.PoolItem { Id = "m2", ProviderId = "p_test", Model = "deepseek-v4-pro" };
            cfg.Pool.Add(m1);
            cfg.Pool.Add(m2);
            cfg.Roles["translate"] = "m1";
            cfg.Roles["ask"] = "m2";
            cfg.Save();
            cfg.Save();   // 第二次保存才会把上一份写成 .bak

            bool bak = System.IO.File.Exists(LensPop.AppConfig.BackupPath);
            Console.WriteLine("备份文件存在=" + bak);

            // 模拟误删主配置
            System.IO.File.Delete(LensPop.AppConfig.FilePath);
            var back = LensPop.AppConfig.Load();
            Console.WriteLine("恢复后 提供商=" + back.Providers.Count + " 模型池=" + back.Pool.Count + " 翻译角色=" + back.Roles["translate"]);
            int ok = (back.Providers.Count == 1 && back.Pool.Count == 2 && back.Roles["translate"] == "m1") ? 0 : 1;
            Console.WriteLine(ok == 0 ? "PASS" : "FAIL");
            return ok;
        }
    }

    /// <summary>把设置页的每个预设都切一遍，看会不会卡住或变慢（--ui-preset）</summary>
    static class PresetTest
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var seed = new LensPop.AppConfig();
            var prov = new LensPop.ProviderDef();
            prov.Id = "p_seed";
            prov.PresetId = "deepseek";
            prov.Name = "DeepSeek";
            prov.BaseUrl = "https://api.deepseek.com/v1";
            prov.ApiKey = "sk-test";
            seed.Providers.Add(prov);
            seed.Pool.Add(new LensPop.PoolItem { Id = "m1", ProviderId = "p_seed", Model = "deepseek-flash" });
            seed.Roles["translate"] = "m1";
            seed.Save();

            var cfg = AppConfig.Load();
            var form = new LensPop.SettingsForm(cfg);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(100, 80);
            form.Show();
            Application.DoEvents();

            var field = typeof(LensPop.SettingsForm).GetField("_presetBox",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var combo = field == null ? null : (System.Windows.Forms.ComboBox)field.GetValue(form);
            if (combo == null) { Console.WriteLine("拿不到预设下拉框"); return 1; }

            Console.WriteLine("presets=" + LensPop.Providers.All.Count);
            for (int round = 0; round < 3; round++)
            for (int i = 0; i < LensPop.Providers.All.Count; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                combo.SelectedIndex = i;
                Application.DoEvents();
                var until = DateTime.Now.AddMilliseconds(700);
                while (DateTime.Now < until)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                Console.WriteLine("  " + i + " " + LensPop.Providers.All[i].Id + " " + sw.ElapsedMilliseconds + "ms");
            }

            Console.WriteLine("done");
            form.Close();
            return 0;
        }
    }

    /// <summary>钩子调试：用 SendInput 模拟拖拽并打印钩子收到的消息（--hook-debug）</summary>
    static class HookDebug
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint n, INPUT[] inputs, int size);

        static void Send(uint flags)
        {
            var arr = new INPUT[1];
            arr[0].type = 0;
            arr[0].mi.dwFlags = flags;
            uint r = SendInput(1, arr, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT)));
            if (r == 0) Console.WriteLine("SendInput 失败 err=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        public static int Run()
        {
            var hook = new LensPop.MouseHook();
            LensPop.MouseHook.RawEvent = delegate(int msg, int x, int y)
            {
                Console.WriteLine("evt 0x" + msg.ToString("x4") + " @" + x + "," + y);
            };
            hook.Clicked += delegate(System.Drawing.Point p) { Console.WriteLine("  -> Clicked"); };
            hook.DragEnded += delegate(System.Drawing.Point p) { Console.WriteLine("  -> DragEnded"); };

            var th = new System.Threading.Thread(delegate()
            {
                bool ok = hook.Install();
                Console.WriteLine("installed=" + ok + " err=" + hook.LastError);
                LensPop.MessagePump.Run();
                Console.WriteLine("pump exited");
            });
            th.SetApartmentState(System.Threading.ApartmentState.STA);
            th.IsBackground = true;
            th.Start();

            var t2 = new System.Threading.Thread(delegate()
            {
                System.Threading.Thread.Sleep(1500);
                SetCursorPos(320, 320);
                System.Threading.Thread.Sleep(200);
                Send(0x0002);
                System.Threading.Thread.Sleep(200);
                SetCursorPos(560, 350);
                System.Threading.Thread.Sleep(200);
                Send(0x0004);
                System.Threading.Thread.Sleep(1200);
                Console.WriteLine("events=" + LensPop.MouseHook.EventCount + " clicks=" + LensPop.MouseHook.ClickCount + " drags=" + LensPop.MouseHook.DragCount);
                Environment.Exit(0);
            });
            t2.IsBackground = true;
            t2.Start();
            System.Threading.Thread.Sleep(6000);
            return 0;
        }
    }


    /// <summary>UIA 取词自测：文本框里选中一段，用 UI Automation 读回来（--uia）</summary>
    static class UiaTest
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            var f = new Form();
            f.Size = new System.Drawing.Size(460, 260);
            f.StartPosition = FormStartPosition.CenterScreen;
            var box = new System.Windows.Forms.TextBox();
            box.Multiline = true;
            box.Dock = System.Windows.Forms.DockStyle.Fill;
            box.Text = "UI Automation fallback test: 选区能不能直接读到。";
            f.Controls.Add(box);
            f.Show();
            f.Activate();
            box.Focus();
            box.Select(0, 18);
            Application.DoEvents();
            System.Threading.Thread.Sleep(300);
            Application.DoEvents();

            string got = LensPop.UiaGrab.TryGetSelection(800);
            Console.WriteLine("UIA 结果: " + (string.IsNullOrEmpty(got) ? "(空)" : got));
            f.Close();
            return string.IsNullOrEmpty(got) ? 1 : 0;
        }
    }


    /// <summary>截图遮罩自测：显示 CaptureForm 并截屏，检查是否还有红叉（--capture）</summary>
    static class CaptureShot
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            var f = new LensPop.CaptureForm(delegate(System.Drawing.Bitmap b, System.Drawing.Point p) { if (b != null) b.Dispose(); });
            f.Show();
            var until = DateTime.Now.AddMilliseconds(1200);
            while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }

            var scr = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var shot = new System.Drawing.Bitmap(scr.Width, scr.Height);
            using (var g = System.Drawing.Graphics.FromImage(shot))
                g.CopyFromScreen(scr.X, scr.Y, 0, 0, scr.Size);
            string dir = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots");
            System.IO.Directory.CreateDirectory(dir);
            shot.Save(System.IO.Path.Combine(dir, "capture.png"), System.Drawing.Imaging.ImageFormat.Png);
            shot.Dispose();
            f.Close();
            Console.WriteLine("capture shot saved");
            return 0;
        }
    }


    /// <summary>屏幕/DPI 信息（--screen）</summary>
    static class ScreenInfo
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int GetSystemMetrics(int n);

        public static int Run()
        {
            Application.EnableVisualStyles();
            var primary = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var virt = System.Windows.Forms.SystemInformation.VirtualScreen;
            Console.WriteLine("PrimaryScreen.Bounds = " + primary);
            Console.WriteLine("SystemInformation.VirtualScreen = " + virt);
            Console.WriteLine("GetSystemMetrics SM_CXSCREEN/CY = " + GetSystemMetrics(0) + "x" + GetSystemMetrics(1));
            Console.WriteLine("ScreenCount = " + System.Windows.Forms.Screen.AllScreens.Length);
            foreach (var sc in System.Windows.Forms.Screen.AllScreens)
                Console.WriteLine("  screen " + sc.DeviceName + " bounds=" + sc.Bounds + " working=" + sc.WorkingArea);

            var f = new Form();
            f.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            f.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            f.Bounds = virt;
            f.Show();
            Application.DoEvents();
            Console.WriteLine("Form.Bounds = " + f.Bounds);
            using (var g = System.Drawing.Graphics.FromHwnd(f.Handle))
                Console.WriteLine("Form DpiX/Y = " + g.DpiX + "/" + g.DpiY);
            f.Close();
            return 0;
        }
    }

    /// <summary>DPI 感知视角截图（--screenshot）</summary>
    static class ScreenShot
    {
        public static int Run()
        {
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var shot = new System.Drawing.Bitmap(b.Width, b.Height);
            using (var g = System.Drawing.Graphics.FromImage(shot)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size);
            string dir = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots");
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "full.png");
            shot.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            shot.Dispose();
            Console.WriteLine("saved " + file + " " + b.Width + "x" + b.Height);
            return 0;
        }
    }

    // HTML 模式的胶囊（截图用）
    static class HtmlPill
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            var f = new LensPop.PopupForm();
            f.Ready(new System.Drawing.Point(700, 500), "<!DOCTYPE html><html><body><h1>Hello</h1></body></html>");
            Console.WriteLine("pill width=" + f.Width);
            for (int i = 0; i < 30; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var shot = new System.Drawing.Bitmap(b.Width, b.Height);
            using (var g = System.Drawing.Graphics.FromImage(shot)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size);
            string dir = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots");
            System.IO.Directory.CreateDirectory(dir);
            shot.Save(System.IO.Path.Combine(dir, "pill-html.png"), System.Drawing.Imaging.ImageFormat.Png);
            shot.Dispose();
            f.Close();
            Console.WriteLine("saved pill-html.png");
            return 0;
        }
    }

    static class AskShot
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            var d = new LensPop.AskDialog("Anthropic 五小时会话额度是按消息条数算的吗？还是按 token？");
            d.Show();
            for (int i = 0; i < 40; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var shot = new System.Drawing.Bitmap(b.Width, b.Height);
            using (var g = System.Drawing.Graphics.FromImage(shot)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size);
            string dir = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots");
            System.IO.Directory.CreateDirectory(dir);
            shot.Save(System.IO.Path.Combine(dir, "ask.png"), System.Drawing.Imaging.ImageFormat.Png);
            shot.Dispose();
            d.Close();
            Console.WriteLine("saved ask.png");
            return 0;
        }
    }

    static class AskEnter
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, System.UIntPtr e);
        const byte VK_RETURN = 0x0D;
        const uint KEYUP = 2;
        public static int Run()
        {
            Application.EnableVisualStyles();
            var dlg = new LensPop.AskDialog("测试内容 hello");
            var t = new System.Threading.Thread(delegate()
            {
                System.Threading.Thread.Sleep(800);
                keybd_event(VK_RETURN, 0, 0, System.UIntPtr.Zero);
                keybd_event(VK_RETURN, 0, KEYUP, System.UIntPtr.Zero);
            });
            t.IsBackground = true;
            t.Start();
            var r = dlg.ShowDialog();
            Console.WriteLine("dialog=" + r + " text=[" + dlg.Result + "]");
            return r == System.Windows.Forms.DialogResult.OK ? 0 : 1;
        }
    }

    static class MdShot
    {
        const string NL = "\n";
        public static int Run()
        {
            Application.EnableVisualStyles();
            var cfg = new LensPop.AppConfig();
            var tgt = new LensPop.Target();
            tgt.ProviderName = "Demo"; tgt.Model = "demo-model";
            var f = new LensPop.ResultForm(cfg, tgt, LensPop.ResultMode.Text, "");
            string md = "# 标题示例" + NL + "这是 **粗体** 和 `行内代码` 的演示。" + NL + "- 第一点" + NL + "- 第二点" + NL + NL + "```" + NL + "int a = 1;" + NL + "Console.WriteLine(a);" + NL + "```" + NL;
            var t = typeof(LensPop.ResultForm);
            var rawField = t.GetField("_raw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var sb = (System.Text.StringBuilder)rawField.GetValue(f);
            sb.Append(md);
            var m = t.GetMethod("RenderMarkdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.Show();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            m.Invoke(f, null);
            for (int i = 0; i < 20; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var shot = new System.Drawing.Bitmap(b.Width, b.Height);
            using (var g = System.Drawing.Graphics.FromImage(shot)) g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size);
            string dir = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots");
            System.IO.Directory.CreateDirectory(dir);
            shot.Save(System.IO.Path.Combine(dir, "md.png"), System.Drawing.Imaging.ImageFormat.Png);
            shot.Dispose();
            f.Close();
            Console.WriteLine("saved md.png");
            return 0;
        }
    }

    static class FocusTest
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, System.UIntPtr e);
        const uint KEYUP = 2;
        static void TypeText(string s2)
        {
            foreach (char ch in s2)
            {
                keybd_event((byte)char.ToUpper(ch), 0, 0, System.UIntPtr.Zero);
                keybd_event((byte)char.ToUpper(ch), 0, KEYUP, System.UIntPtr.Zero);
            }
        }
        public static int Run()
        {
            Application.EnableVisualStyles();
            var other = new Form();
            other.Text = "mock";
            other.SetBounds(200, 200, 600, 400);
            other.Show();
            other.Activate();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            var dlg = new LensPop.AskDialog("preset");
            dlg.Show();
            for (int i = 0; i < 20; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            IntPtr fg = GetForegroundWindow();
            Console.WriteLine("fg_is_dialog=" + (fg == dlg.Handle));
            TypeText("xyz");
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
            string txt = dlg.CurrentText;
            Console.WriteLine("text=[" + txt + "]");
            try
            {
                string outFile = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots", "focus-result.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outFile));
                System.IO.File.WriteAllText(outFile, "fg=" + (fg == dlg.Handle) + " text=[" + txt + "]");
            }
            catch { }

            bool ok = dlg.CurrentText.IndexOf("xyz") >= 0;
            dlg.Close();
            other.Close();
            Console.WriteLine(ok ? "PASS" : "FAIL");
            return ok ? 0 : 1;
        }
    }

    static class HookTest
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, System.UIntPtr e);
        const uint KEYUP = 2;
        static void TypeText(string s2) { foreach (char ch in s2) { keybd_event((byte)char.ToUpper(ch), 0, 0, System.UIntPtr.Zero); keybd_event((byte)char.ToUpper(ch), 0, KEYUP, System.UIntPtr.Zero); } }
        public static int Run()
        {
            Application.EnableVisualStyles();
            var dlg = new LensPop.AskDialog("p");
            dlg.Show();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            var other = new Form();
            other.SetBounds(300, 300, 400, 300);
            other.Show();
            other.Activate();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            TypeText("xyz");
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            string txt = dlg.CurrentText;
            bool ok = txt.IndexOf("xyz") >= 0;
            Console.WriteLine("box=[" + txt + "] " + (ok ? "PASS" : "FAIL"));
            try { System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots")); System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots", "hook.txt"), txt); } catch { }
            dlg.Close(); other.Close();
            return ok ? 0 : 1;
        }
    }

    static class SnapShot
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        public static int Run()
        {
            Application.EnableVisualStyles();
            var mock = new Form();
            mock.Text = "mock-window";
            mock.SetBounds(300, 200, 520, 340);
            mock.Show();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            System.Drawing.Bitmap shot = null;
            var cap = new LensPop.CaptureForm(delegate(System.Drawing.Bitmap b, System.Drawing.Point p) { shot = b; });
            cap.Show();
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            SetCursorPos(560, 370);
            for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
            try
            {
                var b2 = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using (var shotBmp = new System.Drawing.Bitmap(b2.Width, b2.Height))
                {
                    using (var g2 = System.Drawing.Graphics.FromImage(shotBmp)) g2.CopyFromScreen(b2.X, b2.Y, 0, 0, b2.Size);
                    string dir2 = System.IO.Path.Combine(Environment.CurrentDirectory, "dev", "shots");
                    System.IO.Directory.CreateDirectory(dir2);
                    shotBmp.Save(System.IO.Path.Combine(dir2, "snap.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch { }
            cap.Close();
            mock.Close();
            Console.WriteLine("snap test done");
            return 0;
        }
    }
    /// <summary>手写 HTTP 假服务：验证 /models 与 SSE 聊天</summary>
    static class MockServer
    {
        public static int Port = 0;
        static TcpListener _listener;

        public static void Run()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var th = new Thread(Loop);
            th.IsBackground = true;
            th.Start();
        }

        static void Loop()
        {
            while (true)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    Console.WriteLine("[mock] accepted");
                    var th = new Thread(delegate() { Handle(client); });
                    th.IsBackground = true;
                    th.Start();
                }
                catch (Exception e) { Console.WriteLine("[mock] accept loop ended: " + e.Message); return; }
            }
        }

        static void Handle(TcpClient client)
        {
            try
            {
                using (client)
                using (var ns = client.GetStream())
                {
                    var head = new List<byte>();
                    var one = new byte[1];
                    while (true)
                    {
                        int n = ns.Read(one, 0, 1);
                        if (n <= 0) return;
                        head.Add(one[0]);
                        int c = head.Count;
                        if (c >= 4 && head[c - 4] == 13 && head[c - 3] == 10 && head[c - 2] == 13 && head[c - 1] == 10) break;
                    }
                    string headerText = Encoding.UTF8.GetString(head.ToArray());
                    string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) return;
                    string first = lines[0];
                    Console.WriteLine("[mock] REQ " + first);

                    int contentLength = 0;
                    for (int i = 1; i < lines.Length; i++)
                    {
                        int c = lines[i].IndexOf(':');
                        if (c > 0 && lines[i].Substring(0, c).Trim().ToLowerInvariant() == "content-length")
                            int.TryParse(lines[i].Substring(c + 1).Trim(), out contentLength);
                    }

                    var bodyBytes = new List<byte>();
                    int need = contentLength;
                    var buf = new byte[4096];
                    while (need > 0)
                    {
                        int n = ns.Read(buf, 0, Math.Min(buf.Length, need));
                        if (n <= 0) break;
                        for (int i = 0; i < n; i++) bodyBytes.Add(buf[i]);
                        need -= n;
                    }
                    string reqBody = Encoding.UTF8.GetString(bodyBytes.ToArray());

                    bool stream = reqBody.IndexOf("\"stream\":true") >= 0;
                    string path = first.Split(' ')[1];

                    if (path.EndsWith("/models"))
                    {
                        string json = "{\"object\":\"list\",\"data\":[" +
                            "{\"id\":\"mock-flash\",\"object\":\"model\"}," +
                            "{\"id\":\"mock-vl-4v\",\"object\":\"model\"}," +
                            "{\"id\":\"mock-large\",\"object\":\"model\"}]}";
                        Write(ns, "application/json", Encoding.UTF8.GetBytes(json));
                    }
                    else if (stream)
                    {
                        var headBytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream; charset=utf-8\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
                        ns.Write(headBytes, 0, headBytes.Length);
                        string[] parts = new string[] { "你好", "，", "世界", "。" };
                        foreach (string p in parts)
                        {
                            string chunk = "data: {\"choices\":[{\"delta\":{\"content\":\"" + p + "\"}}]}\n\n";
                            var bytes = Encoding.UTF8.GetBytes(chunk);
                            ns.Write(bytes, 0, bytes.Length);
                            ns.Flush();
                            Thread.Sleep(10);
                        }
                        var done = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
                        ns.Write(done, 0, done.Length);
                        ns.Flush();
                    }
                    else
                    {
                        string json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"非流式你好\"}}]}";
                        Write(ns, "application/json", Encoding.UTF8.GetBytes(json));
                    }
                }
            }
            catch (Exception e) { Console.WriteLine("[mock] handle error: " + e.Message); }
        }

        static void Write(NetworkStream ns, string ctype, byte[] body)
        {
            string head = "HTTP/1.1 200 OK\r\nContent-Type: " + ctype + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(head);
            ns.Write(hb, 0, hb.Length);
            ns.Write(body, 0, body.Length);
            ns.Flush();
        }
    }
}