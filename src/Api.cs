using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LensPop
{
    public class ChatMsg
    {
        public string Role;
        public object Content;
        public ChatMsg(string role, object content) { Role = role; Content = content; }
    }

    public class ApiException : Exception
    {
        public int Status;
        public ApiException(string msg, int status) : base(msg) { Status = status; }
    }

    public static class Api
    {
        public static Action<string> Trace;
        static void T(string s) { if (Trace != null) Trace(s); }

        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };

        public static string NormalizeBase(string s)
        {
            if (s == null) return "";
            s = s.Trim();
            s = Regex.Replace(s, @"/+$", "");
            s = Regex.Replace(s, @"/chat/completions$", "", RegexOptions.IgnoreCase);
            return s;
        }

        public static string Shorten(string s, int n)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }

        /* ---------------- 模型列表 ---------------- */

        public static async Task<List<string>> ListModelsAsync(string baseUrl, string key, int timeoutSec)
        {
            string b = NormalizeBase(baseUrl);
            if (b.Length == 0) throw new Exception("请先填写 Base URL");

            var urls = new List<string>();
            urls.Add(b + "/models");
            if (!Regex.IsMatch(b, @"/v\d+[a-z]*$", RegexOptions.IgnoreCase)) urls.Add(b + "/v1/models");
            urls.Add(Regex.Replace(b, @"/v1$", "", RegexOptions.IgnoreCase) + "/api/tags");

            Exception last = null;
            foreach (string u in urls)
            {
                try
                {
                    string txt = await HttpGet(u, key, timeoutSec);
                    var list = ParseModels(txt);
                    if (list.Count > 0) return list;
                }
                catch (Exception e)
                {
                    last = e;
                }
            }
            if (last != null) throw new Exception("获取模型列表失败：" + last.Message);
            throw new Exception("没能拿到模型列表");
        }

        static async Task<string> HttpGet(string url, string key, int timeoutSec)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = Math.Max(5000, timeoutSec * 1000);
            req.UserAgent = "LensPop/1.0";
            req.ServicePoint.Expect100Continue = false;
            if (!string.IsNullOrEmpty(key)) req.Headers["Authorization"] = "Bearer " + key;
            try
            {
                using (var resp = (HttpWebResponse)(await req.GetResponseAsync()))
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return await sr.ReadToEndAsync();
            }
            catch (WebException we)
            {
                throw new Exception(ErrorText(we));
            }
        }

        public static List<string> ParseModels(string json)
        {
            var list = new List<string>();
            object root;
            try { root = Ser.DeserializeObject(json); } catch { return list; }

            IEnumerable src = null;
            var arr = root as object[];
            if (arr != null) src = arr;
            else
            {
                var dict = root as Dictionary<string, object>;
                if (dict != null)
                {
                    object d;
                    if (dict.TryGetValue("data", out d) && d is object[]) src = (object[])d;
                    else if (dict.TryGetValue("models", out d) && d is object[]) src = (object[])d;
                }
            }
            if (src == null) return list;

            foreach (object o in src)
            {
                string s = o as string;
                if (s != null) { list.Add(s); continue; }
                var od = o as Dictionary<string, object>;
                if (od == null) continue;
                string[] keys = new string[] { "id", "name", "model" };
                foreach (string k in keys)
                {
                    object v;
                    if (od.TryGetValue(k, out v) && v != null)
                    {
                        list.Add(Convert.ToString(v));
                        break;
                    }
                }
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /* ---------------- 对话 ---------------- */

        public static async Task<string> ChatAsync(Target t, List<ChatMsg> msgs, Dictionary<string, object> patch,
            bool stream, Action<string> onDelta, int timeoutSec, double temperature, int maxTokens)
        {
            ApiException first = null;
            try
            {
                return await ChatAttempt(t, msgs, patch, stream, onDelta, timeoutSec, temperature, maxTokens);
            }
            catch (ApiException e)
            {
                first = e;
            }
            // 服务不认思考参数：去掉再试一次
            if (patch != null && patch.Count > 0 && (first.Status == 400 || first.Status == 422))
                return await ChatAttempt(t, msgs, new Dictionary<string, object>(), stream, onDelta, timeoutSec, temperature, maxTokens);
            throw first;
        }

        static async Task<string> ChatAttempt(Target t, List<ChatMsg> msgs, Dictionary<string, object> patch,
            bool stream, Action<string> onDelta, int timeoutSec, double temperature, int maxTokens)
        {
            string b = NormalizeBase(t.BaseUrl);
            if (b.Length == 0) throw new Exception("还没配置 Base URL");
            if (string.IsNullOrEmpty(t.Model)) throw new Exception("还没选择模型");

            var body = new Dictionary<string, object>();
            body["model"] = t.Model;
            var arr = new List<object>();
            foreach (var m in msgs)
            {
                var one = new Dictionary<string, object>();
                one["role"] = m.Role;
                one["content"] = m.Content;
                arr.Add(one);
            }
            body["messages"] = arr;
            body["stream"] = stream;
            if (temperature >= 0) body["temperature"] = temperature;
            if (maxTokens > 0) body["max_tokens"] = maxTokens;

            if (!string.IsNullOrEmpty(t.ExtraBody) && t.ExtraBody.Trim().Length > 0)
            {
                try
                {
                    var extra = Ser.DeserializeObject(t.ExtraBody) as Dictionary<string, object>;
                    if (extra != null) foreach (var kv in extra) body[kv.Key] = kv.Value;
                }
                catch
                {
                    throw new Exception("附加请求参数不是合法 JSON");
                }
            }
            if (patch != null) foreach (var kv in patch) body[kv.Key] = kv.Value;

            int ms = Math.Max(10000, timeoutSec * 1000);
            var req = (HttpWebRequest)WebRequest.Create(b + "/chat/completions");
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = ms;
            req.ReadWriteTimeout = ms;
            req.UserAgent = "LensPop/1.0";
            req.ServicePoint.Expect100Continue = false;
            req.AllowReadStreamBuffering = false;
            req.KeepAlive = false;
            if (!string.IsNullOrEmpty(t.ApiKey)) req.Headers["Authorization"] = "Bearer " + t.ApiKey;

            T("序列化请求体");
            byte[] data = Encoding.UTF8.GetBytes(Ser.Serialize(body));
            req.ContentLength = data.Length;
            T("取请求流 " + b);
            using (var rs = await req.GetRequestStreamAsync())
            {
                T("写入 " + data.Length + " 字节");
                await rs.WriteAsync(data, 0, data.Length);
            }
            T("等响应");

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)(await req.GetResponseAsync());
            }
            catch (WebException we)
            {
                var hr = we.Response as HttpWebResponse;
                int code = hr != null ? (int)hr.StatusCode : 0;
                throw new ApiException(ErrorText(we), code);
            }

            using (resp)
            {
                string ct = resp.ContentType == null ? "" : resp.ContentType.ToLowerInvariant();
                if (!stream || ct.IndexOf("text/event-stream") < 0)
                {
                    string all;
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        all = await sr.ReadToEndAsync();
                    string text = ExtractText(all);
                    if (!string.IsNullOrEmpty(text) && onDelta != null) onDelta(text);
                    return text;
                }
                return await ReadSse(resp.GetResponseStream(), onDelta);
            }
        }

        static async Task<string> ReadSse(Stream s, Action<string> onDelta)
        {
            var sb = new StringBuilder();
            using (var sr = new StreamReader(s, Encoding.UTF8))
            {
                while (true)
                {
                    string line = await sr.ReadLineAsync();
                    if (line == null) break;
                    if (line.Length == 0) continue;
                    if (line[0] == ':') continue;
                    if (!line.StartsWith("data:")) continue;
                    string payload = line.Substring(5).Trim();
                    if (payload.Length == 0 || payload == "[DONE]") continue;
                    string delta = ExtractDelta(payload);
                    if (string.IsNullOrEmpty(delta)) continue;
                    sb.Append(delta);
                    if (onDelta != null) onDelta(delta);
                }
            }
            return sb.ToString();
        }

        static string ExtractDelta(string json)
        {
            Dictionary<string, object> root;
            try { root = Ser.DeserializeObject(json) as Dictionary<string, object>; }
            catch { return null; }
            if (root == null) return null;
            object choicesObj;
            if (!root.TryGetValue("choices", out choicesObj)) return null;
            var choices = choicesObj as object[];
            if (choices == null || choices.Length == 0) return null;
            var c0 = choices[0] as Dictionary<string, object>;
            if (c0 == null) return null;
            object msgObj;
            if (!c0.TryGetValue("delta", out msgObj)) c0.TryGetValue("message", out msgObj);
            var msg = msgObj as Dictionary<string, object>;
            if (msg == null) return null;
            object content;
            if (!msg.TryGetValue("content", out content)) return null;
            return ContentToString(content);
        }

        static string ExtractText(string body)
        {
            Dictionary<string, object> root;
            try { root = Ser.DeserializeObject(body) as Dictionary<string, object>; }
            catch { return ""; }
            if (root == null) return "";
            object choicesObj;
            if (!root.TryGetValue("choices", out choicesObj)) return "";
            var choices = choicesObj as object[];
            if (choices == null || choices.Length == 0) return "";
            var c0 = choices[0] as Dictionary<string, object>;
            if (c0 == null) return "";
            object msgObj;
            if (!c0.TryGetValue("message", out msgObj)) c0.TryGetValue("delta", out msgObj);
            var msg = msgObj as Dictionary<string, object>;
            if (msg == null) return "";
            object content;
            if (!msg.TryGetValue("content", out content)) return "";
            return ContentToString(content);
        }

        static string ContentToString(object content)
        {
            if (content == null) return "";
            string s = content as string;
            if (s != null) return s;
            var parts = content as object[];
            if (parts == null) return "";
            var sb = new StringBuilder();
            foreach (object p in parts)
            {
                var d = p as Dictionary<string, object>;
                if (d == null) continue;
                object t;
                if (d.TryGetValue("text", out t) && t != null) sb.Append(Convert.ToString(t));
            }
            return sb.ToString();
        }

        public static string ErrorText(WebException we)
        {
            string detail = "";
            try
            {
                if (we.Response != null)
                {
                    using (var rs = we.Response.GetResponseStream())
                    using (var sr = new StreamReader(rs, Encoding.UTF8))
                        detail = Shorten(sr.ReadToEnd(), 400);
                }
            }
            catch { }
            int code = 0;
            string desc = "";
            var hr = we.Response as HttpWebResponse;
            if (hr != null) { code = (int)hr.StatusCode; desc = hr.StatusDescription; }
            string head = code > 0 ? ("HTTP " + code + " " + desc) : we.Status.ToString();
            return head + (detail.Length > 0 ? " · " + detail : "");
        }

        public static string Humanize(Exception e)
        {
            string msg = e == null ? "未知错误" : (e.Message == null ? "未知错误" : e.Message.Trim());
            if (msg.Length == 0) msg = "未知错误";
            string low = msg.ToLowerInvariant();
            if (low.IndexOf("timed out") >= 0 || low.IndexOf("timeout") >= 0 || low.IndexOf("超时") >= 0)
                return "请求超时了，可以到设置里把「超时」调大，或者换一个更快的模型。";
            if (low.IndexOf("name resolution") >= 0 || low.IndexOf("actively refused") >= 0 || low.IndexOf("unable to connect") >= 0)
                return "连不上这个 API：检查网络/代理，以及 Base URL 是否正确（很多服务要带 /v1）。\n\n" + msg;
            if (low.IndexOf("http 401") >= 0 || low.IndexOf("http 403") >= 0)
                return "API Key 无效、过期，或者没有这个模型的权限。\n\n" + msg;
            if (low.IndexOf("http 404") >= 0)
                return "路径或模型不存在：Base URL 少/多了 /v1，或者模型名不对。\n\n" + msg;
            if (low.IndexOf("http 429") >= 0)
                return "被限流了，或者余额不足。\n\n" + msg;
            if (low.IndexOf("image") >= 0 && low.IndexOf("http 400") >= 0)
                return "这个模型不支持读图，OCR / 图片翻译请换视觉模型（名字里常带 vl / vision / 4v）。\n\n" + msg;
            return msg;
        }
    }
}
