using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>取词测试：窗口里自带一个可划选的文本框，选好点按钮就地验证，不用切窗口</summary>
    public class GrabTestForm : Form
    {
        readonly TextBox _box = new TextBox();
        readonly Label _out = new Label();
        readonly Label _tip = new Label();
        readonly Button _run = new Button();

        const string Sample =
            "选中这段文字再点下面的「测试取词」。\r\n" +
            "Hello LensPop — 这是一行中英混排的示例。\r\n" +
            "如果取词正常，这里会显示刚才选中的内容。";

        public GrabTestForm()
        {
            Text = "取词测试";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = Theme.Bg;
            Font = Theme.UI;
            Size = new Size(520, 320);

            _tip.Text = "1. 在下面文本框里用鼠标选中一段文字    2. 点「测试取词」";
            _tip.SetBounds(14, 12, 480, 20);
            _tip.ForeColor = Theme.Fg;
            Controls.Add(_tip);

            _box.Multiline = true;
            _box.ReadOnly = false;
            _box.ScrollBars = ScrollBars.Vertical;
            _box.SetBounds(14, 36, 480, 130);
            _box.Font = new Font("Microsoft YaHei UI", 10f);
            _box.Text = Sample;
            _box.Select(0, 24);
            Controls.Add(_box);

            _run.Text = "测试取词";
            _run.SetBounds(14, 176, 100, 28);
            _run.FlatStyle = FlatStyle.Flat;
            _run.FlatAppearance.BorderColor = Theme.Line;
            _run.BackColor = Theme.Bg;
            _run.Cursor = Cursors.Hand;
            _run.Click += delegate { RunTest(); };
            Controls.Add(_run);

            var close = new Button();
            close.Text = "关闭";
            close.SetBounds(394, 244, 100, 28);
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderColor = Theme.Line;
            close.BackColor = Theme.Bg;
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            _out.SetBounds(14, 212, 480, 60);
            _out.ForeColor = Theme.Sub;
            _out.Font = Theme.UISmall;
            _out.Text = "结果会显示在这里。";
            Controls.Add(_out);
        }

        void RunTest()
        {
            if (_box.SelectionLength == 0)
            {
                _out.Text = "先在文本框里用鼠标选中一段文字。";
                return;
            }

            _out.Text = "正在模拟 Ctrl+C…";

            // 关键：把窗口和焦点都给回文本框，Ctrl+C 才会复制到它的选区
            Activate();
            _box.Focus();
            Application.DoEvents();

            // 目标就是本窗口的文本框，必须在本线程上泵消息，否则 Ctrl+C 要等很久才生效
            string got = SelectionGrabber.GrabOnUiThread(1500);
            ShowResult(got);
        }

        void ShowResult(string got)
        {
            if (got == null)
            {
                bool self = MouseHook.ForegroundIsSelf(System.Diagnostics.Process.GetCurrentProcess().Id);
                _out.Text = "没抓到。" + (self ? "" : "（注意：本窗口当前不在前台，Ctrl+C 发给了别的窗口）") + "\r\n" +
                            "· 如果是安全软件拦了模拟按键，把 LensPop 加白名单\r\n" +
                            "· 或者本机剪贴板被别的程序占用";
                return;
            }
            string show = got.Length > 80 ? got.Substring(0, 80) + "…" : got;
            _out.Text = "取词成功（" + got.Length + " 个字符）：" + show;
        }
    }
}
