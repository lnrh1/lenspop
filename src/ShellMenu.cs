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

        public static string RegisterError;

        public static bool Register()
        {
            RegisterError = null;
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
            catch (Exception e)
            {
                RegisterError = e.GetType().Name + "：" + e.Message;
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

        /* ---------------- 发送到菜单（不需要注册表） ---------------- */

        public static string SendToDir
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "SendTo");
            }
        }

        static string SendToLink(string title)
        {
            return System.IO.Path.Combine(SendToDir, "LensPop " + title + ".lnk");
        }

        static void CreateLink(string lnkPath, string args)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            object sc = shell.GetType().InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            Type t = sc.GetType();
            t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { ExePath });
            t.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { args });
            t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc,
                new object[] { System.IO.Path.GetDirectoryName(ExePath) });
            t.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { ExePath + ",0" });
            t.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "LensPop" });
            t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
        }

        static string SendToCmd(string title)
        {
            return System.IO.Path.Combine(SendToDir, "LensPop " + title + ".cmd");
        }

        static void WriteCmd(string path, string args)
        {
            string text = "@echo off\r\nstart \"\" \"" + ExePath + "\" " + args + " %*\r\n";
            System.IO.File.WriteAllText(path, text, System.Text.Encoding.Default);
        }

        /// <summary>往「发送到」里放两个入口：不碰注册表；COM 被拦就直接写 .cmd</summary>
        public static bool RegisterSendTo()
        {
            RegisterError = null;
            try
            {
                string dir = SendToDir;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                bool comOk = true;
                try
                {
                    CreateLink(SendToLink("识别文字"), "--ocr");
                    CreateLink(SendToLink("翻译图片"), "--translate");
                }
                catch (Exception e)
                {
                    comOk = false;
                    RegisterError = "COM 建快捷方式失败：" + e.Message;
                }
                if (comOk && SendToRegistered()) return true;

                // 兜底：直接放 .cmd，命令行会把选中的文件路径接在后面
                WriteCmd(SendToCmd("识别文字"), "--ocr");
                WriteCmd(SendToCmd("翻译图片"), "--translate");
                return SendToRegistered();
            }
            catch (Exception e)
            {
                RegisterError = e.GetType().Name + "：" + e.Message;
                return false;
            }
        }

        public static bool SendToRegistered()
        {
            try
            {
                bool a = System.IO.File.Exists(SendToLink("识别文字")) || System.IO.File.Exists(SendToCmd("识别文字"));
                bool b = System.IO.File.Exists(SendToLink("翻译图片")) || System.IO.File.Exists(SendToCmd("翻译图片"));
                return a && b;
            }
            catch { return false; }
        }

        public static void UnregisterSendTo()
        {
            try
            {
                System.IO.File.Delete(SendToLink("识别文字"));
                System.IO.File.Delete(SendToLink("翻译图片"));
                System.IO.File.Delete(SendToCmd("识别文字"));
                System.IO.File.Delete(SendToCmd("翻译图片"));
            }
            catch { }
        }

        /* ---------------- 生成 .reg 文件（绕开进程权限） ---------------- */

        static void RegVerb(System.Text.StringBuilder sb, string keyPath, string title, string arg)
        {
            string exe = ExePath.Replace("\\", "\\\\");
            sb.AppendLine("[HKEY_CURRENT_USER\\Software\\Classes\\" + keyPath + "]");
            sb.AppendLine("@=\"" + title + "\"");
            sb.AppendLine("\"Icon\"=\"" + exe + ",0\"");
            sb.AppendLine();
            sb.AppendLine("[HKEY_CURRENT_USER\\Software\\Classes\\" + keyPath + "\\command]");
            sb.AppendLine("@=\"\\\"" + exe + "\\\" " + arg + " \\\"%1\\\"\"");
            sb.AppendLine();
        }

        /// <summary>写一份 .reg 到配置目录：双击导入即可，导入动作由资源管理器执行，不受本进程权限限制</summary>
        public static string WriteRegFile()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            RegVerb(sb, @"SystemFileAssociations\image\shell\LensPopOCR", "LensPop 识别文字", "--ocr");
            RegVerb(sb, @"SystemFileAssociations\image\shell\LensPopTranslate", "LensPop 翻译图片", "--translate");
            foreach (string ext in Exts)
            {
                RegVerb(sb, @"SystemFileAssociations\" + ext + @"\shell\LensPopOCR", "LensPop 识别文字", "--ocr");
                RegVerb(sb, @"SystemFileAssociations\" + ext + @"\shell\LensPopTranslate", "LensPop 翻译图片", "--translate");
            }
            string path = System.IO.Path.Combine(AppConfig.Dir, "LensPop-右键菜单.reg");
            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UnicodeEncoding(false, true));
            return path;
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
