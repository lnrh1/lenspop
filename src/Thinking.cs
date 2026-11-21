using System;
using System.Collections.Generic;

namespace LensPop
{
    /// <summary>
    /// 思考档位适配。
    /// 翻译 / OCR 一律尽量关掉思考；询问可在窗口里选，默认「关闭」，也支持「默认」沿用各家自己的设置。
    /// 聚合类（OpenRouter / 硅基流动 / 自定义 / Ollama）不猜参数，只留「默认」。
    /// </summary>
    public class ThinkFamily
    {
        public string Id;
        public string Label;
        public string Note;
        public string[] Levels;
        public bool CanDisable;
    }

    public static class Thinking
    {
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "auto", "默认" },
            { "off", "关闭" },
            { "on", "开启" },
            { "minimal", "极简" },
            { "low", "低" },
            { "medium", "中" },
            { "high", "高" },
            { "max", "最高" }
        };

        static ThinkFamily F(string id, string label, string note, string[] levels, bool canDisable)
        {
            var f = new ThinkFamily();
            f.Id = id; f.Label = label; f.Note = note; f.Levels = levels; f.CanDisable = canDisable;
            return f;
        }

        public static string PickFamily(string presetId, string model)
        {
            string p = (presetId == null ? "" : presetId).ToLowerInvariant();
            string m = (model == null ? "" : model).ToLowerInvariant();

            if (p == "openrouter" || p == "siliconflow" || p == "custom" || p == "ollama") return "generic";

            if (p == "deepseek" || m.StartsWith("deepseek")) return "deepseek";
            if (p == "kimi" || m.StartsWith("kimi") || m.StartsWith("moonshot")) return "kimi";
            if (p == "zhipu" || m.StartsWith("glm"))
            {
                if (m.IndexOf("glm-5") >= 0 || m.IndexOf("glm-4.7") >= 0 || m.IndexOf("glm-4.8") >= 0 || m.IndexOf("glm-4.9") >= 0) return "glmEffort";
                return "glmToggle";
            }
            if (p == "dashscope" || m.StartsWith("qwen")) return "qwen";
            if (p == "openai" || m.StartsWith("gpt-5") || m.StartsWith("gpt-4.1") || (m.Length > 1 && m[0] == 'o' && char.IsDigit(m[1]))) return "openai";
            if (p == "gemini" || m.IndexOf("gemini") >= 0) return "gemini";
            if (p == "ark" || m.StartsWith("doubao")) return "ark";
            return "generic";
        }

        public static ThinkFamily Get(string presetId, string model)
        {
            string id = PickFamily(presetId, model);
            switch (id)
            {
                case "deepseek":
                    return F("deepseek", "DeepSeek",
                        "DeepSeek 默认开启思考、effort=high；这里可切 none/low/high/max，关掉最快。",
                        new string[] { "auto", "off", "low", "high", "max" }, true);
                case "kimi":
                    return F("kimi", "Kimi",
                        "Kimi K3 系列始终思考，只能调 reasoning_effort（low/high/max），关不掉。",
                        new string[] { "auto", "low", "high", "max" }, false);
                case "glmEffort":
                    return F("glmEffort", "GLM 5.x",
                        "GLM-5.x 始终思考，只能调 reasoning_effort（low/high/max），关不掉。",
                        new string[] { "auto", "low", "high", "max" }, false);
                case "glmToggle":
                    return F("glmToggle", "GLM 4.x",
                        "GLM-4.x 只支持开或关思考（thinking.type）。",
                        new string[] { "auto", "off", "on" }, true);
                case "qwen":
                    return F("qwen", "通义千问",
                        "Qwen3 用 enable_thinking 开关思考。",
                        new string[] { "auto", "off", "on" }, true);
                case "openai":
                    return F("openai", "OpenAI 推理模型",
                        "GPT-5 / o 系列用 reasoning_effort（minimal/low/medium/high）。",
                        new string[] { "auto", "minimal", "low", "medium", "high" }, true);
                case "gemini":
                    return F("gemini", "Gemini",
                        "Gemini 思考模型在 OpenAI 兼容接口里用 reasoning_effort。",
                        new string[] { "auto", "minimal", "low", "medium", "high" }, true);
                case "ark":
                    return F("ark", "豆包 Seed",
                        "豆包 Seed 用 thinking.type 开关，可用 reasoning_effort 调档。",
                        new string[] { "auto", "off", "low", "high" }, true);
                default:
                    return F("generic", "通用（聚合/自定义）",
                        "聚合或自定义服务不做档位猜测，直接用模型自身默认设置。",
                        new string[] { "auto" }, false);
            }
        }

        public static List<string> LevelsFor(string presetId, string model)
        {
            var f = Get(presetId, model);
            return new List<string>(f.Levels);
        }

        public static string Clamp(string presetId, string model, string level)
        {
            var f = Get(presetId, model);
            string lvl = level == null ? "auto" : level;
            foreach (string l in f.Levels) if (l == lvl) return lvl;
            if (lvl == "off") return Has(f.Levels, "low") ? "low" : "auto";
            if (lvl == "on") return "auto";
            return Has(f.Levels, "high") ? "high" : "auto";
        }

        static bool Has(string[] arr, string v)
        {
            foreach (string s in arr) if (s == v) return true;
            return false;
        }

        public static string Label(string level)
        {
            string v;
            if (Labels.TryGetValue(level == null ? "auto" : level, out v)) return v;
            return level;
        }

        public static Dictionary<string, object> Build(string presetId, string model, string level)
        {
            var f = Get(presetId, model);
            string lvl = Clamp(presetId, model, level);
            var patch = new Dictionary<string, object>();
            if (lvl == "auto") return patch;
            switch (f.Id)
            {
                case "deepseek":
                    if (lvl == "off")
                    {
                        patch["thinking"] = new Dictionary<string, object> { { "type", "disabled" } };
                        patch["reasoning_effort"] = "none";
                    }
                    else
                    {
                        patch["thinking"] = new Dictionary<string, object> { { "type", "enabled" } };
                        patch["reasoning_effort"] = lvl;
                    }
                    break;
                case "kimi":
                    patch["reasoning_effort"] = lvl;
                    break;
                case "glmEffort":
                    patch["thinking"] = new Dictionary<string, object> { { "type", "enabled" } };
                    patch["reasoning_effort"] = lvl;
                    break;
                case "glmToggle":
                    patch["thinking"] = new Dictionary<string, object> { { "type", lvl == "off" ? "disabled" : "enabled" } };
                    break;
                case "qwen":
                    patch["enable_thinking"] = (lvl != "off");
                    break;
                case "openai":
                case "gemini":
                    patch["reasoning_effort"] = lvl;
                    break;
                case "ark":
                    if (lvl == "off") patch["thinking"] = new Dictionary<string, object> { { "type", "disabled" } };
                    else
                    {
                        patch["thinking"] = new Dictionary<string, object> { { "type", "enabled" } };
                        patch["reasoning_effort"] = lvl;
                    }
                    break;
            }
            return patch;
        }

        /// <summary>翻译/OCR 用的「关掉思考」参数；关不掉的就取最低档。</summary>
        public static Dictionary<string, object> Disable(string presetId, string model)
        {
            var f = Get(presetId, model);
            var patch = new Dictionary<string, object>();
            switch (f.Id)
            {
                case "deepseek":
                    patch["thinking"] = new Dictionary<string, object> { { "type", "disabled" } };
                    patch["reasoning_effort"] = "none";
                    break;
                case "kimi":
                case "glmEffort":
                    patch["reasoning_effort"] = "low";
                    break;
                case "glmToggle":
                    patch["thinking"] = new Dictionary<string, object> { { "type", "disabled" } };
                    break;
                case "qwen":
                    patch["enable_thinking"] = false;
                    break;
                case "openai":
                case "gemini":
                    patch["reasoning_effort"] = "minimal";
                    break;
                case "ark":
                    patch["thinking"] = new Dictionary<string, object> { { "type", "disabled" } };
                    break;
            }
            return patch;
        }
    }
}
