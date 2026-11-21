using System;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace LensPop
{
    /// <summary>UI Automation 取词：不依赖剪贴板，浏览器 / Electron / Office 都支持</summary>
    public static class UiaGrab
    {
        public static string TryGetSelection(int timeoutMs)
        {
            string result = null;
            var t = new Thread(delegate()
            {
                try
                {
                    var fe = AutomationElement.FocusedElement;
                    if (fe == null) return;

                    object pattern;
                    if (!fe.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) return;

                    var tp = (TextPattern)pattern;
                    var ranges = tp.GetSelection();
                    if (ranges == null || ranges.Length == 0) return;

                    var sb = new StringBuilder();
                    for (int i = 0; i < ranges.Length; i++)
                    {
                        string piece = ranges[i].GetText(-1);
                        if (!string.IsNullOrEmpty(piece)) sb.Append(piece);
                    }
                    string s = sb.ToString().Trim();
                    if (s.Length > 0) result = s;
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            t.Join(timeoutMs);
            return result;
        }
    }
}
