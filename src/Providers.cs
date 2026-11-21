using System;
using System.Collections.Generic;

namespace LensPop
{
    public class Preset
    {
        public string Id;
        public string Name;
        public string BaseUrl;
        public string KeyUrl;
        public string Note;

        public Preset(string id, string name, string baseUrl, string keyUrl, string note)
        {
            Id = id;
            Name = name;
            BaseUrl = baseUrl;
            KeyUrl = keyUrl;
            Note = note;
        }
    }

    public static class Providers
    {
        public static readonly List<Preset> All = new List<Preset>
        {
            new Preset("deepseek", "DeepSeek 深度求索", "https://api.deepseek.com/v1", "https://platform.deepseek.com/api_keys",
                "deepseek-flash 便宜又快，翻译首选；纯文本，不能读图。"),
            new Preset("kimi", "Kimi · 月之暗面", "https://api.moonshot.cn/v1", "https://platform.moonshot.cn/console/api-keys",
                "Kimi 的视觉模型（moonshot-v1-*-vision-preview / kimi-latest）能读图。"),
            new Preset("zhipu", "智谱 GLM", "https://open.bigmodel.cn/api/paas/v4", "https://bigmodel.cn/usercenter/proj-mgmt/apikeys",
                "glm-4v 系列读图；glm-4-flash 翻译几乎免费。"),
            new Preset("dashscope", "阿里云百炼 · 通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1", "https://bailian.console.aliyun.com/",
                "qwen-vl-max / qwen-vl-plus 适合定位与识别图片文字。"),
            new Preset("siliconflow", "硅基流动 SiliconFlow", "https://api.siliconflow.cn/v1", "https://cloud.siliconflow.cn/account/ak",
                "聚合开源模型，Qwen2.5-VL 等做 OCR 便宜。"),
            new Preset("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", "https://openrouter.ai/keys",
                "一个 Key 用多家模型；聚合服务不做思考档位适配。"),
            new Preset("ark", "火山方舟 · 豆包", "https://ark.cn-beijing.volces.com/api/v3", "https://console.volcengine.com/ark",
                "模型名填推理接入点 ep-xxxx 或豆包模型名，doubao-vision 可读图。"),
            new Preset("hunyuan", "腾讯混元", "https://api.hunyuan.cloud.tencent.com/v1", "https://console.cloud.tencent.com/hunyuan/api-key",
                "hunyuan-vision 可读图；hunyuan-lite 翻译够用。"),
            new Preset("qianfan", "百度千帆", "https://qianfan.baidubce.com/v2", "https://console.bce.baidu.com/qianfan/",
                "OpenAI 兼容模式，模型名填千帆上的服务名。"),
            new Preset("openai", "OpenAI", "https://api.openai.com/v1", "https://platform.openai.com/api-keys",
                "gpt-4o-mini 便宜且能读图，两个角色都行。"),
            new Preset("gemini", "Google Gemini（OpenAI 兼容）", "https://generativelanguage.googleapis.com/v1beta/openai", "https://aistudio.google.com/app/apikey",
                "gemini 系列有免费额度，读图强。"),
            new Preset("ollama", "Ollama 本地", "http://127.0.0.1:11434/v1", "",
                "全本地免费；API Key 随便填。OCR 需要本地视觉模型。"),
            new Preset("custom", "自定义（OpenAI 兼容）", "", "",
                "任何提供 /models 与 /chat/completions 的服务、中转站、自建网关。")
        };

        public static Preset Find(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return All[All.Count - 1];
        }

        public static bool LooksVision(string model)
        {
            if (string.IsNullOrEmpty(model)) return false;
            string m = model.ToLowerInvariant();
            string[] hints = new string[] { "vl", "vision", "vlm", "omni", "gpt-4o", "gpt-4.1", "gpt-5",
                "gemini", "claude", "llava", "minicpm-v", "internvl", "pixtral", "moondream", "kimi-latest", "4v" };
            foreach (string h in hints) if (m.IndexOf(h) >= 0) return true;
            return false;
        }
    }
}
