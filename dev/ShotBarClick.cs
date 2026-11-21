using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LensPopTests
{
    /// <summary>截图浮条点击自测：真实鼠标事件点三个分段</summary>
    static class ShotBarClick
    {
        [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        const uint DOWN = 0x0002, UP = 0x0004;

        public static int Run()
        {
            Application.EnableVisualStyles();
            int copied = 0, ocr = 0, tr = 0;

            var at = new System.Drawing.Point(700, 400);
            var b1 = new LensPop.ShotBarForm(); b1.CopyClicked += delegate { copied++; }; b1.ShowAt(at); Settle(); ClickAt(b1.Left + 36, b1.Top + 17); b1.Dispose();
            var b2 = new LensPop.ShotBarForm(); b2.OcrClicked += delegate { ocr++; }; b2.ShowAt(at); Settle(); ClickAt(b2.Left + 4 + 64 + 46, b2.Top + 17); b2.Dispose();
            var b3 = new LensPop.ShotBarForm(); b3.TranslateClicked += delegate { tr++; }; b3.ShowAt(at); Settle(); ClickAt(b3.Left + 4 + 64 + 92 + 32, b3.Top + 17); b3.Dispose();

            Console.WriteLine("复制=" + copied + " 提取=" + ocr + " 翻译=" + tr);
            return (copied == 1 && ocr == 1 && tr == 1) ? 0 : 1;
        }

        static void Settle()
        {
            for (int i = 0; i < 4; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
        }

        static void ClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Settle();
            mouse_event(DOWN, 0, 0, 0, UIntPtr.Zero);
            Settle();
            mouse_event(UP, 0, 0, 0, UIntPtr.Zero);
            Settle();
        }
    }
}
