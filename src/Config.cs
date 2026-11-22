using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace LensPop
{
    public class ProviderDef
    {
        public string Id = "";
        public string PresetId = "custom";
        public string Name = "";
        public string BaseUrl = "";
        public string ApiKey = "";
        public string ExtraBody = "";
    }

    public class PoolItem
    {
        public string Id = "";
        public string ProviderId = "";
        public string Model = "";
        public long AddedAt = 0;
    }

    public class Prompts
    {
        public string Translate = "";
        public string Ask = "";
        public string Ocr = "";
        public string OcrLayout = "";
        public string TranslateLines = "";
    }

    public class Settings
    {
        public string TargetLang = "中文（简体）";
        public bool AutoLang = true;
        public string SearchEngine = "bing";
        public string SearchUrlTpl = "https://www.bing.com/search?q=%s";
        public bool SelectionPopup = true;
        public bool Stream = true;
        public double Temperature = 0.3;
        public int MaxTokens = 0;
        public int TimeoutSec = 120;
        public string AskLevel = "off";
        public string HotkeyShot = "Ctrl+Alt+A";
        public string HotkeyCapture = "Ctrl+Alt+S";
        public string HotkeyCaptureTr = "Ctrl+Alt+F";
        public bool Autostart = false;
        public bool RunAsAdmin = true;
        public bool ShellMenu = false;
        public Prompts Prompts = new Prompts();
    }

    /// <summary>角色解析后的调用目标</summary>
    public class Target
    {
        public string ProviderId = "";
        public string PresetId = "";
        public string ProviderName = "";
        public string BaseUrl = "";
        public string ApiKey = "";
        public string ExtraBody = "";
        public string Model = "";
    }

    public class AppConfig
    {
        public int Version = 1;
        public List<ProviderDef> Providers = new List<ProviderDef>();
        public List<PoolItem> Pool = new List<PoolItem>();
        public Dictionary<string, string> Roles = new Dictionary<string, string>();
        public Settings Settings = new Settings();

        [ScriptIgnore]
        static string _dir;

        /// <summary>配置目录：优先 %APPDATA%，不可写就退到 %LOCALAPPDATA%，再退到临时目录</summary>
        public static string Dir
        {
            get
            {
                if (_dir != null) return _dir;

                // LocalAppData 优先：Roaming 可能因历史权限/OneDrive 读写被拒
                string[] bases = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    System.IO.Path.GetTempPath()
                };

                foreach (string b in bases)
                {
                    if (string.IsNullOrEmpty(b)) continue;
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        try
                        {
                            string d = Path.Combine(b, "LensPop");
                            Directory.CreateDirectory(d);
                            string probe = Path.Combine(d, ".write-test");
                            File.WriteAllText(probe, "x");
                            File.Delete(probe);
                            _dir = d;
                            return d;
                        }
                        catch { System.Threading.Thread.Sleep(120); }
                    }
                }

                try { _dir = Path.Combine(System.IO.Path.GetTempPath(), "LensPop"); Directory.CreateDirectory(_dir); } catch { _dir = Path.Combine(bases[0], "LensPop"); }
                return _dir;
            }
        }

        [ScriptIgnore]
        public static string FilePath
        {
            get { return Path.Combine(Dir, "config.json"); }
        }

        [ScriptIgnore]
        public static string BackupPath
        {
            get { return Path.Combine(Dir, "config.bak.json"); }
        }

        /// <summary>从指定文件读一份配置（导入用）；读不出来返回 null</summary>
        public static AppConfig LoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var cfg = ser.Deserialize<AppConfig>(json);
                if (cfg == null) return null;
                cfg.Normalize();
                return cfg;
            }
            catch
            {
                return null;
            }
        }

        public static AppConfig Load()
        {
            var cfg = LoadFrom(FilePath);
            if (cfg == null)
            {
                // 主配置坏了/被删了，退回上一次的自动备份
                cfg = LoadFrom(BackupPath);
                if (cfg != null) Program.LogLine("主配置不可用，已从 config.bak.json 恢复");
            }
            if (cfg == null) cfg = new AppConfig();
            cfg.Normalize();
            return cfg;
        }

        public void Normalize()
        {
            if (Providers == null) Providers = new List<ProviderDef>();
            if (Pool == null) Pool = new List<PoolItem>();
            if (Roles == null) Roles = new Dictionary<string, string>();
            if (Settings == null) Settings = new Settings();
            if (Settings.Prompts == null) Settings.Prompts = new Prompts();
            if (Settings.TimeoutSec < 5) Settings.TimeoutSec = 120;
            if (Settings.MaxTokens < 0) Settings.MaxTokens = 0;
            if (string.IsNullOrEmpty(Settings.AskLevel)) Settings.AskLevel = "off";
            if (string.IsNullOrEmpty(Settings.SearchEngine)) Settings.SearchEngine = "bing";
            if (string.IsNullOrEmpty(Settings.TargetLang)) Settings.TargetLang = "中文（简体）";

            string[] roleKeys = new string[] { "translate", "ocr", "ask" };
            foreach (string r in roleKeys)
            {
                if (!Roles.ContainsKey(r)) Roles[r] = "";
                if (!string.IsNullOrEmpty(Roles[r]) && FindPool(Roles[r]) == null) Roles[r] = "";
            }
        }

        public void Save()
        {
            try
            {
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string json = ser.Serialize(this);

                // 先留一份上一次的配置，防止写坏或误删后一无所有
                try
                {
                    if (File.Exists(FilePath))
                    {
                        string old = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
                        if (!string.IsNullOrEmpty(old) && old.Length > 20)
                            File.WriteAllText(BackupPath, old, new System.Text.UTF8Encoding(false));
                    }
                }
                catch { }

                File.WriteAllText(FilePath, json, new System.Text.UTF8Encoding(false));
            }
            catch
            {
                // 存不下就算了，别把界面卡死
            }
        }

        public ProviderDef FindProvider(string id)
        {
            foreach (var p in Providers) if (p.Id == id) return p;
            return null;
        }

        public PoolItem FindPool(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in Pool) if (m.Id == id) return m;
            return null;
        }

        public Target ResolveRole(string role)
        {
            string id;
            if (!Roles.TryGetValue(role, out id)) return null;
            var item = FindPool(id);
            if (item == null) return null;
            var prov = FindProvider(item.ProviderId);
            if (prov == null) return null;
            var t = new Target();
            t.ProviderId = prov.Id;
            t.PresetId = prov.PresetId;
            t.ProviderName = prov.Name;
            t.BaseUrl = prov.BaseUrl;
            t.ApiKey = prov.ApiKey;
            t.ExtraBody = prov.ExtraBody;
            t.Model = item.Model;
            return t;
        }
    }
}
