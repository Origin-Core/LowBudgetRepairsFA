using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LowBudgetRepairsPersianFix
{
    [BepInPlugin("com.farsiorigin.persianlang", "Low Budget Repairs Persian Language Support", "2.0.0")]
    public class Plugin : BasePlugin
    {
        private static string langFilePath = Path.Combine(Paths.GameRootPath, "BepInEx", "plugins", "fa.json");
        private static Dictionary<string, string> Translations = new Dictionary<string, string>();
        private static HashSet<string> MissingKeys = new HashSet<string>();

        public override void Load()
        {
            Log.LogInfo("Persian Language Localization System v2.0.0 Loaded.");

            LoadLanguageFile();

            var harmony = new Harmony("com.farsiorigin.persianlang");

            // ۱. هوک کردن TextMeshPro
            Type tmpType = AccessTools.TypeByName("TMPro.TMP_Text");
            if (tmpType != null)
            {
                PropertyInfo textProp = AccessTools.Property(tmpType, "text");
                if (textProp?.SetMethod != null)
                {
                    MethodInfo prefix = typeof(Plugin).GetMethod(nameof(GenericTextSetter_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(textProp.SetMethod, prefix: new HarmonyMethod(prefix));
                }
            }

            // ۲. هوک کردن UI Text قدیمی یونتی برای پوشش کامل دیالوگ‌ها
            Type uiTextType = AccessTools.TypeByName("UnityEngine.UI.Text");
            if (uiTextType != null)
            {
                PropertyInfo textProp = AccessTools.Property(uiTextType, "text");
                if (textProp?.SetMethod != null)
                {
                    MethodInfo prefix = typeof(Plugin).GetMethod(nameof(GenericTextSetter_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(textProp.SetMethod, prefix: new HarmonyMethod(prefix));
                }
            }
        }

        private static void GenericTextSetter_Prefix(ref string __0)
        {
            if (string.IsNullOrEmpty(__0)) return;

            string cleanKey = __0.Trim();

            // اگر ترجمه فارسی در فایل زبان وجود داشت، آن را جایگزین و اصلاح کن
            if (Translations.TryGetValue(cleanKey, out string translatedText))
            {
                __0 = translatedText;
            }
            else if (ContainsPersian(__0))
            {
                // اگر متن خودش فارسی بود فقط RTL/Shaping کن
                __0 = PersianShaper.Fix(__0);
            }
            else
            {
                // اگر انگلیسی بود و ترجمه نداشت، در لیست متون جدید برای ترجمه ذخیره کن
                RegisterMissingKey(cleanKey);
            }
        }

        private static void LoadLanguageFile()
        {
            try
            {
                if (!File.Exists(langFilePath))
                {
                    File.WriteAllText(langFilePath, "{\n  \"Go to Zbyszek\": \"برو پیش زبیشک\"\n}", Encoding.UTF8);
                }

                string jsonContent = File.ReadAllText(langFilePath, Encoding.UTF8);
                // پارس ساده JSON بدون نیاز به نیوتون‌سافت
                string[] lines = jsonContent.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains(":") && line.Contains("\""))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Replace("\"", "").Trim();
                            string val = parts[1].Replace("\"", "").Replace(",", "").Trim();

                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                            {
                                Translations[key] = PersianShaper.Fix(val);
                            }
                        }
                    }
                }
                Log.LogInfo($"[Persian Language] Loaded {Translations.Count} translated lines.");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to load fa.json: " + ex.Message);
            }
        }

        private static void RegisterMissingKey(string key)
        {
            if (key.Length < 2 || MissingKeys.Contains(key)) return;
            MissingKeys.Add(key);

            try
            {
                string dumpPath = Path.Combine(Paths.GameRootPath, "BepInEx", "plugins", "missing_strings.txt");
                File.AppendAllText(dumpPath, key + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        private static bool ContainsPersian(string input)
        {
            foreach (char c in input)
            {
                if (c >= 0x0600 && c <= 0x06FF) return true;
            }
            return false;
        }
    }

    // الگوریتم شکل‌دهی و اتصال حروف فارسی
    public static class PersianShaper
    {
        public static string Fix(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            char[] letters = str.ToCharArray();
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < letters.Length; i++)
            {
                char c = letters[i];
                if (c >= 0x0600 && c <= 0x06FF)
                {
                    c = MapGlyph(c);
                }
                result.Append(c);
            }

            char[] arr = result.ToString().ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        private static char MapGlyph(char ch)
        {
            return ch switch
            {
                'آ' => 'ﺁ', 'ا' => 'ﺍ', 'ب' => 'ﺏ', 'پ' => 'ﭖ', 'ت' => 'ﺕ',
                'ث' => 'ﺙ', 'ج' => 'ﺝ', 'چ' => 'ﭺ', 'ح' => 'ﺡ', 'خ' => 'ﺥ',
                'د' => 'ﺩ', 'ذ' => 'ﺫ', 'ر' => 'ﺭ', 'ز' => 'ﺯ', 'ژ' => 'ﮊ',
                'س' => 'ﺱ', 'ش' => 'ﺵ', 'ص' => 'ﺹ', 'ض' => 'ﺽ', 'ط' => 'ﻁ',
                'ظ' => 'ﻅ', 'ع' => 'ﻉ', 'غ' => 'ﻍ', 'ف' => 'ﻑ', 'ق' => 'ﻕ',
                'ک' => 'ﮎ', 'گ' => 'ﮒ', 'ل' => 'ﻝ', 'م' => 'ﻡ', 'ن' => 'ﻥ',
                'و' => 'ﻭ', 'ه' => 'ﻩ', 'ی' => 'ﯼ', _ => ch
            };
        }
    }
}
