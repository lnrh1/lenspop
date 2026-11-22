using System;

namespace LensPop
{
    /// <summary>判断划到的内容是不是一整份 HTML 文档</summary>
    public static class HtmlDoc
    {
        public static bool IsHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.TrimStart();
            if (t.Length < 32) return false;
            string head = t.Length > 200 ? t.Substring(0, 200) : t;
            if (head.IndexOf("<!doctype html", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (head.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) return true;
            // 没有 <html> 头但整段都是标签结构，也算
            if (t.IndexOf("</html>", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
