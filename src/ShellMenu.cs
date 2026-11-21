using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Forms;

namespace LensPop
{
    /// <summary>资源管理器右键菜单：图片 → LensPop 识别 / 翻译</summary>
    public static class ShellMenu
    {
        static readonly string[] Exts = new string[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff" };

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        const int SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;

        static string ExePath
        {
            get { return Application.ExecutablePath; }
        }

        static string Command(string arg)
        {
            return "\"" + ExePath + "\" " + arg + " \"%1\"";
        }

        static void WriteEntry(string keyPath, string title, string arg)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (k == null) return;
                k.SetValue("", title);
                k.SetValue("Icon", ExePath + ",0");
                k.SetValue("MultiSelectModel", "Single");
                using (var c = k.CreateSubKey("command"))
                {
                    if (c != null) c.SetValue("", Command(arg));
                }
            }
        }

        public static bool Register()
        {
            try
            {
                string[] roots = new string[]
                {
                    @"Software\Classes\SystemFileAssociations\image\shell\LensPopOCR",
                    @"Software\Classes\SystemFileAssociations\image\shell\LensPopTranslate"
                };
                WriteEntry(roots[0], "LensPop 识别文字", "--ocr");
                WriteEntry(roots[1], "LensPop 翻译图片（贴回原图）", "--translate");

                foreach (string ext in Exts)
                {
                    string a = @"Software\Classes\SystemFileAssociations\" + ext + @"\shell\LensPopOCR";
                    string b = @"Software\Classes\SystemFileAssociations\" + ext + @"\shell\LensPopTranslate";
                    WriteEntry(a, "LensPop 识别文字", "--ocr");
                    WriteEntry(b, "LensPop 翻译图片（贴回原图）", "--translate");
                }
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Unregister()
        {
            try
            {
                SafeDelete(@"Software\Classes\SystemFileAssociations\image\shell\LensPopOCR");
                SafeDelete(@"Software\Classes\SystemFileAssociations\image\shell\LensPopTranslate");
                foreach (string ext in Exts)
                {
                    SafeDelete(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\LensPopOCR");
                    SafeDelete(@"Software\Classes\SystemFileAssociations\" + ext + @"\shell\LensPopTranslate");
                }
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        static void SafeDelete(string path)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
            catch { }
        }

        public static bool IsRegistered()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\image\shell\LensPopOCR\command"))
                    return k != null && k.GetValue("") != null;
            }
            catch { return false; }
        }
    }
}
