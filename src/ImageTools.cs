using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace LensPop
{
    public class TextLine
    {
        public int X, Y, W, H;
        public string Text = "";
        public string Translation = "";
        public string Color = "";
    }

    public static class ImageTools
    {
        public const int MaxSide = 1600;
        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        /* ---------- 取图 ---------- */

        public static Bitmap LoadFromPath(string path)
        {
            if (path == null) throw new Exception("没有路径");
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = path.IndexOf(',');
                if (comma < 0) throw new Exception("图片数据不对");
                byte[] bytes = Convert.FromBase64String(path.Substring(comma + 1));
                using (var ms = new MemoryStream(bytes)) return new Bitmap(Image.FromStream(ms));
            }
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (var ms = new MemoryStream(bytes)) return new Bitmap(Image.FromStream(ms));
            }
            return Download(path);
        }

        public static Bitmap Download(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 20000;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) LensPop/1.0";
            req.Referer = url;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    ms.Position = 0;
                    return new Bitmap(Image.FromStream(ms));
                }
            }
            catch (Exception e)
            {
                throw new Exception("下载图片失败：" + Api.Humanize(e));
            }
        }

        public static Bitmap Downscale(Bitmap src, int maxSide)
        {
            int w = src.Width, h = src.Height;
            if (w <= maxSide && h <= maxSide) return src;
            double scale = (double)maxSide / Math.Max(w, h);
            int nw = Math.Max(1, (int)Math.Round(w * scale));
            int nh = Math.Max(1, (int)Math.Round(h * scale));
            var dst = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, nw, nh));
            }
            src.Dispose();
            return dst;
        }

        public static string ToDataUrl(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                var codec = GetCodec("image/jpeg");
                if (codec == null)
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                }
                var ps = new System.Drawing.Imaging.EncoderParameters(1);
                ps.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
                bmp.Save(ms, codec, ps);
                ps.Dispose();
                return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }

        static ImageCodecInfo GetCodec(string mime)
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (string.Equals(c.MimeType, mime, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        /* ---------- 解析模型返回的行框 ---------- */

        public static List<TextLine> ParseLines(string raw, out int logicalW, out int logicalH)
        {
            logicalW = 0;
            logicalH = 0;
            var result = new List<TextLine>();
            if (string.IsNullOrEmpty(raw)) return result;

            object[] arr = ExtractArray(raw, ref logicalW, ref logicalH);
            if (arr == null) arr = ExtractTopArray(raw);
            if (arr == null) return result;

            foreach (object o in arr)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                object boxObj = Get(d, "box");
                if (boxObj == null) boxObj = Get(d, "bbox");
                if (boxObj == null) boxObj = Get(d, "rect");
                if (boxObj == null) boxObj = Get(d, "boundingBox");
                if (boxObj == null) boxObj = Get(d, "region");
                var box = boxObj as object[];
                if (box == null || box.Length < 4) continue;

                double x = ToD(box[0]), y = ToD(box[1]), w = ToD(box[2]), h = ToD(box[3]);
                if (w <= 0 || h <= 0) continue;

                string text = ToS(Get(d, "text"));
                if (text.Length == 0) text = ToS(Get(d, "content"));
                if (text.Length == 0) continue;

                var line = new TextLine();
                line.X = (int)Math.Round(x);
                line.Y = (int)Math.Round(y);
                line.W = (int)Math.Round(w);
                line.H = (int)Math.Round(h);
                line.Text = text.Trim();
                line.Translation = ToS(Get(d, "translation")).Trim();
                if (line.Translation.Length == 0) line.Translation = ToS(Get(d, "translated")).Trim();
                line.Color = NormalizeColor(ToS(Get(d, "color")));
                if (line.Color.Length == 0) line.Color = NormalizeColor(ToS(Get(d, "fill")));
                result.Add(line);
            }
            return result;
        }

        static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v)) return v;
            return null;
        }

        static object[] ExtractArray(string raw, ref int lw, ref int lh)
        {
            string s = StripFence(raw);
            int a = s.IndexOf('{');
            int b = s.LastIndexOf('}');
            if (a < 0 || b <= a) return null;
            string body = s.Substring(a, b - a + 1);
            Dictionary<string, object> root = null;
            try { root = Ser.DeserializeObject(body) as Dictionary<string, object>; } catch { return null; }
            if (root == null) return null;
            object v;
            if (root.TryGetValue("width", out v)) lw = (int)ToD(v);
            if (root.TryGetValue("height", out v)) lh = (int)ToD(v);
            object[] arr = null;
            if (root.TryGetValue("lines", out v)) arr = v as object[];
            if (arr == null && root.TryGetValue("rows", out v)) arr = v as object[];
            if (arr == null && root.TryGetValue("blocks", out v)) arr = v as object[];
            return arr;
        }

        static object[] ExtractTopArray(string raw)
        {
            string s = StripFence(raw);
            int a = s.IndexOf('[');
            int b = s.LastIndexOf(']');
            if (a < 0 || b <= a) return null;
            try { return Ser.DeserializeObject(s.Substring(a, b - a + 1)) as object[]; }
            catch { return null; }
        }

        public static string StripFence(string s)
        {
            if (s == null) return "";
            s = s.Trim();
            string fence = new string((char)96, 3);
            int i = s.IndexOf(fence);
            if (i >= 0)
            {
                int j = s.IndexOf(fence, i + 3);
                if (j > i)
                {
                    string inner = s.Substring(i + 3, j - i - 3);
                    int nl = inner.IndexOf('\n');
                    if (nl >= 0) inner = inner.Substring(nl + 1);
                    return inner.Trim();
                }
            }
            return s;
        }

        static double ToD(object o)
        {
            if (o == null) return 0;
            try { return Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        static string ToS(object o)
        {
            if (o == null) return "";
            string s = o as string;
            if (s != null) return s;
            return Convert.ToString(o);
        }

        public static string NormalizeColor(string s)
        {
            if (s == null) return "";
            s = s.Trim();
            if (s.Length == 0) return "";
            int i = s.IndexOf('#');
            if (i >= 0 && s.Length >= i + 7)
            {
                string hex = s.Substring(i + 1, 6);
                int val;
                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out val)) return "#" + hex;
            }
            if (s.Length == 7 && s[0] == '#') return s;
            return "";
        }

        /* ---------- 译文排版：就地替换原文，不外扩 ---------- */

        public static Bitmap Compose(Bitmap src, List<TextLine> lines, int logicalW, int logicalH)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                double sx = (logicalW > 0 && Math.Abs(logicalW - src.Width) > 1) ? (double)src.Width / logicalW : 1.0;
                double sy = (logicalH > 0 && Math.Abs(logicalH - src.Height) > 1) ? (double)src.Height / logicalH : 1.0;

                // 归一化坐标（0~1）兜底
                double maxCoord = 0;
                foreach (var l in lines)
                {
                    maxCoord = Math.Max(maxCoord, Math.Max(Math.Max(l.X, l.Y), Math.Max(l.X + l.W, l.Y + l.H)));
                }
                if (maxCoord > 0 && maxCoord <= 1.001)
                {
                    sx = src.Width;
                    sy = src.Height;
                }

                var family = PickFont();
                foreach (var l in lines)
                {
                    string text = string.IsNullOrEmpty(l.Translation) ? l.Text : l.Translation;
                    if (string.IsNullOrEmpty(text)) continue;

                    int x = (int)Math.Round(l.X * sx);
                    int y = (int)Math.Round(l.Y * sy);
                    int w = (int)Math.Round(l.W * sx);
                    int h = (int)Math.Round(l.H * sy);
                    if (w < 4 || h < 5) continue;
                    x = Math.Max(0, x); y = Math.Max(0, y);
                    if (x >= bmp.Width || y >= bmp.Height) continue;
                    w = Math.Min(w, bmp.Width - x);
                    h = Math.Min(h, bmp.Height - y);
                    if (w < 4 || h < 5) continue;

                    var rect = new Rectangle(x, y, w, h);

                    // 模型给的框常常比原文窄，先按原文字号量一下真实宽度，多出来的原文也要擦干净
                    if (!string.IsNullOrEmpty(l.Text))
                    {
                        int measureFs = Math.Max(8, (int)Math.Round(rect.Height * 0.98));
                        using (var mf = new Font(family, measureFs, FontStyle.Regular, GraphicsUnit.Pixel))
                        {
                            SizeF need = MeasureNoWrap(g, l.Text, mf);
                            int needW = (int)Math.Ceiling(need.Width) + 6;
                            if (needW > rect.Width)
                            {
                                int cap = Math.Min(bmp.Width - x, (int)(rect.Width * 2.5));
                                rect = new Rectangle(x, y, Math.Max(rect.Width, Math.Min(needW, cap)), h);
                            }
                        }
                    }

                    Color bg = SampleRing(src, rect);
                    Color fg = ParseColor(l.Color, bg);

                    using (var brush = new SolidBrush(bg)) g.FillRectangle(brush, rect);
                    DrawFitted(g, text, rect, fg, family);
                }
            }
            return bmp;
        }

        static FontFamily PickFont()
        {
            string[] names = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Segoe UI" };
            foreach (string n in names)
            {
                try { return new FontFamily(n); } catch { }
            }
            return FontFamily.GenericSansSerif;
        }

        static Color ParseColor(string hex, Color bg)
        {
            if (!string.IsNullOrEmpty(hex) && hex.Length >= 7)
            {
                int v;
                if (int.TryParse(hex.Substring(1, 6), System.Globalization.NumberStyles.HexNumber, null, out v))
                {
                    var c = Color.FromArgb(255, (v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff);
                    // 和背景太接近就翻一下，保证看得见
                    if (Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G) + Math.Abs(c.B - bg.B) < 60)
                        return (bg.R + bg.G + bg.B) > 380 ? Color.FromArgb(34, 34, 34) : Color.White;
                    return c;
                }
            }
            return (bg.R + bg.G + bg.B) > 380 ? Color.FromArgb(34, 34, 34) : Color.White;
        }

        static SizeF MeasureNoWrap(Graphics g, string text, Font font)
        {
            var sf = new StringFormat(StringFormatFlags.NoWrap);
            sf.Trimming = StringTrimming.None;
            var size = g.MeasureString(text, font, new PointF(0, 0), sf);
            sf.Dispose();
            return size;
        }

        static Color SampleRing(Bitmap src, Rectangle r)
        {
            int pad = Math.Max(2, Math.Min(10, r.Height / 3));
            var rs = new List<int>();
            var gs = new List<int>();
            var bs = new List<int>();
            int x0 = Math.Max(0, r.Left - pad);
            int x1 = Math.Min(src.Width - 1, r.Right + pad);
            int y0 = Math.Max(0, r.Top - pad);
            int y1 = Math.Min(src.Height - 1, r.Bottom + pad);
            for (int x = x0; x <= x1; x += 2)
            {
                AddPx(src, x, r.Top - pad, rs, gs, bs);
                AddPx(src, x, r.Bottom + pad, rs, gs, bs);
            }
            for (int y = y0; y <= y1; y += 2)
            {
                AddPx(src, r.Left - pad, y, rs, gs, bs);
                AddPx(src, r.Right + pad, y, rs, gs, bs);
            }
            if (rs.Count == 0) return Color.White;
            rs.Sort(); gs.Sort(); bs.Sort();
            int m = rs.Count / 2;
            return Color.FromArgb(255, rs[m], gs[m], bs[m]);
        }

        static void AddPx(Bitmap src, int x, int y, List<int> rs, List<int> gs, List<int> bs)
        {
            if (x < 0 || y < 0 || x >= src.Width || y >= src.Height) return;
            var c = src.GetPixel(x, y);
            rs.Add(c.R); gs.Add(c.G); bs.Add(c.B);
        }

        static void DrawFitted(Graphics g, string text, Rectangle rect, Color fg, FontFamily family)
        {
            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Near;
            sf.LineAlignment = StringAlignment.Center;
            sf.Trimming = StringTrimming.None;
            sf.FormatFlags = StringFormatFlags.NoClip;

            float maxW = Math.Max(8, rect.Width - 4);
            Font best = null;
            int start = Math.Max(8, (int)Math.Round(rect.Height * 0.94));
            for (int f = start; f >= 8; f--)
            {
                var font = new Font(family, f, FontStyle.Regular, GraphicsUnit.Pixel);
                SizeF size = g.MeasureString(text, font, new SizeF(maxW, 10000f), sf);
                if (size.Height <= rect.Height + 1)
                {
                    best = font;
                    break;
                }
                font.Dispose();
            }
            if (best == null) best = new Font(family, 8, FontStyle.Regular, GraphicsUnit.Pixel);

            using (var brush = new SolidBrush(fg))
            {
                var state = g.Save();
                g.SetClip(rect);
                g.DrawString(text, best, brush, new RectangleF(rect.X + 2, rect.Y, maxW, rect.Height), sf);
                g.Restore(state);
            }
            best.Dispose();
        }
    }
}
