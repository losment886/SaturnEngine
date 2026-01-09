using SaturnEngine.Global;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.Asset
{
    public static class SELanguageManager
    {
        public static string CurrentLanguage { get; private set; } = "zh-CN";
        public static string CurrentUserLanguage { get; private set; } = "zh-CN";
        public static Encoding EngineResEncoding {  get; set; } = Encoding.UTF8;
        public static Encoding UserResEncoding { get; set; } = Encoding.UTF8;
        static LRL? engl;
        static LRL? usel;
        public static void LoadLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) { return; }
            if(File.Exists($"{GVariables.LanguageResPath}/{language}.lrl"))
            {
                if (engl != null)
                    engl.Close();
                engl = new LRL();
                engl.LoadFromFile($"{GVariables.LanguageResPath}/{language}.lrl");
            }
        }
        public static string GetString(this byte[] key)
        {
            //return key.GetInCurrLang();

            return EngineResEncoding.GetString(key);
        }
        public static string GetUserString(this byte[] key)
        {
            //return key.GetInCurrLang();

            return UserResEncoding.GetString(key);
        }
        /// <summary>
        /// 获取当前语言的翻译文本（引擎语言资源）
        /// </summary>
        /// <param name="key">文本</param>
        /// <returns>翻译文本</returns>
        /// <remarks>在没加载或文本不存在时会返还原句</remarks>
        public static string GetInCurrLang(this string key)
        {
            if (engl != null)
            {
                int i = engl.SearchByName(key);
                if(i >= 0)
                {
                    return engl.BKs[i].data.ReadAllInBytes().GetString();
                }
            }
            return key;
        }
        /// <summary>
        /// 获取当前语言的翻译文本（用户自定义语言资源）
        /// </summary>
        /// <param name="key">文本</param>
        /// <returns>翻译文本</returns>
        /// <remarks>在没加载或文本不存在时会返还原句</remarks>
        public static string GetInCurrUserLang(this string key)
        {
            if (usel != null)
            {
                int i = usel.SearchByName(key);
                if (i >= 0)
                {
                    return usel.BKs[i].data.ReadAllInBytes().GetUserString();
                }
            }
            return key;
        }
        public static void CreateLanguageFile(string language,string savedir,Dictionary<string,string> map)
        {

        }
    }
}
