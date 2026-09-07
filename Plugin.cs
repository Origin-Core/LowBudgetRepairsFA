using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LowBudgetRepairsPersianFix
{
    [BepInPlugin("com.farsiorigin.persianlang", "Low Budget Repairs Persian Language Support", "2.1.0")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger = null!;
        private static readonly string LangFilePath = Path.Combine(Paths.GameRootPath, "BepInEx", "plugins", "fa.json");
        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>();
        private static readonly HashSet<string> MissingKeys = new HashSet<string>();

        public override void Load()
        {
            Logger = Log;
            Logger.LogInfo("Persian Language System v2.1.0 (Advanced Shaping) Loaded.");

            LoadLanguageFile();

            var harmony = new Harmony("com.farsiorigin.persianlang");

            // هوک کردن متد Setter برای TextMeshPro
            Type? tmpType = AccessTools.TypeByName("TMPro.TMP_Text");
            if (tmpType != null)
            {
                PropertyInfo? textProp = AccessTools.Property(tmpType, "text");
                if (textProp?.SetMethod != null)
                {
                    MethodInfo? prefix = typeof(Plugin).GetMethod(nameof(TextSetter_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    if (prefix != null) harmony.Patch(textProp.SetMethod, prefix: new HarmonyMethod(prefix));
                }

                // هوک کردن متد SetText متغیر
                MethodInfo? setTextMethod = AccessTools.Method(tmpType, "SetText", new[] { typeof(string), typeof(bool) });
                if (setTextMethod != null)
                {
                    MethodInfo? prefix = typeof(Plugin).GetMethod(nameof(TextSetter_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    if (prefix != null) harmony.Patch(setTextMethod, prefix: new HarmonyMethod(prefix));
                }
            }

            // هوک کردن UnityEngine.UI.Text
            Type? uiTextType = AccessTools.TypeByName("UnityEngine.UI.Text");
            if (uiTextType != null)
            {
                PropertyInfo? textProp = AccessTools.Property(uiTextType, "text");
                if (textProp?.SetMethod != null)
                {
                    MethodInfo? prefix = typeof(Plugin).GetMethod(nameof(TextSetter_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    if (prefix != null) harmony.Patch(textProp.SetMethod, prefix: new HarmonyMethod(prefix));
                }
            }
        }

        private static void TextSetter_Prefix(ref string __0)
        {
            if (string.IsNullOrEmpty(__0)) return;

            string cleanKey = __0.Trim();

            if (Translations.TryGetValue(cleanKey, out string? translatedText))
            {
                __0 = translatedText;
            }
            else if (ContainsPersian(__0))
            {
                __0 = PersianShaper.Fix(__0);
            }
            else
            {
                RegisterMissingKey(cleanKey);
            }
        }

        private static void LoadLanguageFile()
        {
            try
            {
                if (!File.Exists(LangFilePath))
                {
                    File.WriteAllText(LangFilePath, "{\n  \"Go to Zbyszek\": \"برو پیش زبیشک\"\n}", Encoding.UTF8);
                }

                string jsonContent = File.ReadAllText(LangFilePath, Encoding.UTF8);
                string[] lines = jsonContent.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains(':') && line.Contains('"'))
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
                Logger.LogInfo($"Loaded {Translations.Count} translations successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load fa.json: " + ex.Message);
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

    // الگوریتم شکل‌دهی ۴ حالته حروف (Isolated, Initial, Medial, Final)
    public static class PersianShaper
    {
        private class CharForms
        {
            public char Isolated, Final, Initial, Medial;
            public bool CanConnectBefore, CanConnectAfter;

            public CharForms(char iso, char fin, char ini, char med, bool connBefore = true, bool connAfter = true)
            {
                Isolated = iso; Final = fin; Initial = ini; Medial = med;
                CanConnectBefore = connBefore; CanConnectAfter = connAfter;
            }
        }

        private static readonly Dictionary<char, CharForms> Map = new Dictionary<char, CharForms>
        {
            {'آ', new CharForms('ﺁ', 'ﺂ', 'ﺁ', 'ﺂ', true, false)},
            {'ا', new CharForms('ﺍ', 'ﺎ', 'ﺍ', 'ﺎ', true, false)},
            {'ب', new CharForms('ﺏ', 'ﺐ', 'ﺑ', 'ﺒ')},
            {'پ', new CharForms('ﭖ', 'ﭗ', 'ﭘ', 'ﭙ')},
            {'ت', new CharForms('ﺕ', 'ﺖ', 'ﺗ', 'ﺘ')},
            {'ث', new CharForms('ﺙ', 'ﺚ', 'ﺛ', 'ﺜ')},
            {'ج', new CharForms('ﺝ', 'ﺞ', 'ﺟ', 'ﺠ')},
            {'چ', new CharForms('ﭺ', 'ﭻ', 'ﭼ', 'ﭽ')},
            {'ح', new CharForms('ﺡ', 'ﺢ', 'ﺣ', 'ﺤ')},
            {'خ', new CharForms('ﺥ', 'ﺦ', 'ﺧ', 'ﺨ')},
            {'د', new CharForms('ﺩ', 'ﺪ', 'ﺩ', 'ﺪ', true, false)},
            {'ذ', new CharForms('ﺫ', 'ﺬ', 'ﺫ', 'ﺬ', true, false)},
            {'ر', new CharForms('ﺭ', 'ﺮ', 'ﺭ', 'ﺮ', true, false)},
            {'ز', new CharForms('ﺯ', 'ﺰ', 'ﺯ', 'ﺰ', true, false)},
            {'ژ', new CharForms('ﮊ', 'ﮋ', 'ﮊ', 'ﮋ', true, false)},
            {'س', new CharForms('ﺱ', 'ﺲ', 'ﺳ', 'ﺴ')},
            {'ش', new CharForms('ﺵ', 'ﺶ', 'ﺷ', 'ﺸ')},
            {'ص', new CharForms('ﺹ', 'ﺺ', 'ﺻ', 'ﺼ')},
            {'ض', new CharForms('ﺽ', 'ﺾ', 'ﺿ', 'ﻀ')},
            {'ط', new CharForms('ﻁ', 'ﻂ', 'ﻃ', 'ﻄ')},
            {'ظ', new CharForms('ﻅ', 'ﻆ', 'ﻇ', 'ﻈ')},
            {'ع', new CharForms('ﻉ', 'ﻊ', 'ﻋ', 'ﻌ')},
            {'غ', new CharForms('ﻍ', 'ﻎ', 'ﻏ', 'ﻐ')},
            {'ف', new CharForms('ﻑ', 'ﻒ', 'ﻓ', 'ﻔ')},
            {'ق', new CharForms('ﻕ', 'ﻖ', 'ﻗ', 'ﻘ')},
            {'ک', new CharForms('ﮎ', 'ﮏ', 'ﮐ', 'ﮑ')},
            {'گ', new CharForms('ﮒ', 'ﮕ', 'ﮔ', 'ﮕ')},
            {'ل', new CharForms('ﻝ', 'ﻞ', 'ﻟ', 'ﻠ')},
            {'م', new CharForms('ﻡ', 'ﻢ', 'ﻣ', 'ﻤ')},
            {'ن', new CharForms('ﻥ', 'ﻦ', 'ﻧ', 'ﻨ')},
            {'و', new CharForms('ﻭ', 'ﻮ', 'ﻭ', 'ﻮ', true, false)},
            {'ه', new CharForms('ﻩ', 'ﻪ', 'ﻫ', 'ﻬ')},
            {'ی', new CharForms('ﯼ', 'ﯽ', 'ﯾ', 'ﯿ')}
        };

        public static string Fix(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            char[] chars = str.ToCharArray();
            StringBuilder output = new StringBuilder();

            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];

                if (Map.TryGetValue(current, out var forms))
                {
                    bool prevConnects = i > 0 && Map.TryGetValue(chars[i - 1], out var prev) && prev.CanConnectAfter;
                    bool nextConnects = i < chars.Length - 1 && Map.TryGetValue(chars[i + 1], out var next) && next.CanConnectBefore;

                    if (prevConnects && nextConnects && forms.CanConnectAfter)
                        output.Append(forms.Medial);
                    else if (prevConnects)
                        output.Append(forms.Final);
                    else if (nextConnects && forms.CanConnectAfter)
                        output.Append(forms.Initial);
                    else
                        output.Append(forms.Isolated);
                }
                else
                {
                    output.Append(current);
                }
            }

            char[] result = output.ToString().ToCharArray();
            Array.Reverse(result);
            return new string(result);
        }
    }
}
