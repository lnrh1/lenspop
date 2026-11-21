using System;
using System.Drawing;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>主窗口：托盘不一定会显示，用它保证「看得见、点得到」</summary>
    public class MainForm : Form
    {
        readonly TrayApp _app;
        readonly Label _status = new Label();
        readonly Label _foot = new Label();
        readonly Timer _tick = new Timer();

        public MainForm(TrayApp app)
        {
            _app = app;
            Text = "LensPop · 划词透镜";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.UI;
            ClientSize = new Size(440, 348);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            var title = new Label();
            title.Text = "LensPop";
            title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            title.ForeColor = Theme.Brand;
            title.SetBounds(18, 12, 200, 32);
            Controls.Add(title);

            var sub = new Label();
            sub.Text = "划词即译 · 右键图片识别与翻译";
            sub.ForeColor = Theme.Sub;
            sub.SetBounds(18, 44, 300, 20);
            Controls.Add(sub);

            _status.SetBounds(18, 76, 404, 150);
            _status.ForeColor = Theme.Fg;
            _status.Font = new Font("Microsoft YaHei UI", 9f);
            Controls.Add(_status);

            int y = 236;
            AddButton("设置…", 18, y, 96, delegate { _app.OpenSettings(); });
            AddButton("截图", 122, y, 120, delegate { _app.ShotCapture(); });
            AddButton("截图识别", 250, y, 84, delegate { _app.CaptureScreen(false); });
            AddButton("退出", 342, y, 80, delegate { _app.Quit(); });

            AddButton("注册图片菜单", 18, y + 42, 110, delegate { _app.RegisterShellFromUi(); });
            AddButton("测试取词", 136, y + 42, 96, delegate { _app.TestGrab(); });
            AddButton("打开配置目录", 240, y + 42, 120, delegate { _app.OpenConfigDir(); });

            _foot.SetBounds(18, y + 78, 404, 18);
            _foot.ForeColor = Theme.Sub;
            _foot.Font = Theme.UISmall;
            Controls.Add(_foot);

            _tick.Interval = 1000;
            _tick.Tick += delegate { Refresh2(); };
            _tick.Start();
            Refresh2();
        }

        void AddButton(string text, int x, int y, int w, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, 30);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.Line;
            b.BackColor = Theme.Bg;
            b.Cursor = Cursors.Hand;
            b.Click += onClick;
            Controls.Add(b);
        }

        void Refresh2()
        {
            if (IsDisposed) return;
            var cfg = _app.Config;
            string t = _app.TranslateTarget;
            string o = _app.OcrTarget;
            string a = _app.AskTarget;

            _status.Text =
                "划词弹窗　：" + (cfg.Settings.SelectionPopup ? "开" : "关") +
                "　　鼠标事件：" + MouseHook.EventCount + "（数字在涨＝钩子正常）\r\n" +
                "托盘图标　：" + (_app.TrayVisible ? "已创建" : "创建失败") + "\r\n" +
                "翻译模型　：" + (t.Length == 0 ? "未设置" : t) + "\r\n" +
                "OCR 模型　：" + (o.Length == 0 ? "未设置" : o) + "\r\n" +
                "询问模型　：" + (a.Length == 0 ? "未设置" : a) + "\r\n" +
                "图片菜单　：注册表" + (ShellMenu.IsRegistered() ? "已注册" : "未注册") +
                "，发送到" + (ShellMenu.SendToRegistered() ? "已添加" : "无");

            _foot.Text = "配置：" + AppConfig.FilePath;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _tick.Stop();
            base.OnFormClosing(e);
        }
    }
}
