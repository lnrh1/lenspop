using System;

namespace LensPop
{
    /// <summary>粗粒度语言判断：中文占比高就译成英文，否则译成目标语言</summary>
    public static class LangDetect
    {
        public static string TargetFor(string text, string fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            int cjk = 0;
            int latin = 0;
            int limit = Math.Min(text.Length, 2000);
            for (int i = 0; i < limit; i++)
            {
                char c = text[i];
                if (c >= 0x4E00 && c <= 0x9FFF) cjk++;
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) latin++;
            }
            if (cjk == 0 && latin == 0) return fallback;
            // 中日韩字符占多数 → 译成英文；否则按设置里的目标语言
            if (cjk * 3 >= (cjk + latin)) return "English";
            return fallback;
        }
    }
}
