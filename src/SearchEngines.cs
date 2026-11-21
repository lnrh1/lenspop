using System;

namespace LensPop
{
    public static class SearchEngines
    {
        public static readonly string[] Ids = new string[] { "bing", "google", "ddg", "baidu", "sogou", "custom" };
        public static readonly string[] Names = new string[] { "Bing", "Google", "DuckDuckGo", "百度", "搜狗", "自定义模板" };
        public static readonly string[] Urls = new string[]
        {
            "https://www.bing.com/search?q=%s",
            "https://www.google.com/search?q=%s",
            "https://duckduckgo.com/?q=%s",
            "https://www.baidu.com/s?wd=%s",
            "https://www.sogou.com/web?query=%s",
            ""
        };

        public static readonly string[] Langs = new string[]
        {
            "中文（简体）", "繁體中文", "English", "日本語", "한국어", "Français",
            "Deutsch", "Español", "Русский", "Português", "Italiano", "العربية",
            "ไทย", "Tiếng Việt"
        };

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return i;
            return 0;
        }

        public static string BuildUrl(Settings s, string text)
        {
            string tpl = "";
            if (s != null && s.SearchEngine == "custom") tpl = s.SearchUrlTpl;
            if (string.IsNullOrEmpty(tpl))
            {
                int i = IndexOf(s == null ? "bing" : s.SearchEngine);
                tpl = Urls[i];
            }
            if (string.IsNullOrEmpty(tpl)) tpl = Urls[0];
            string q = Uri.EscapeDataString(text == null ? "" : text);
            if (tpl.IndexOf("%s") >= 0) return tpl.Replace("%s", q);
            return tpl + q;
        }
    }
}
