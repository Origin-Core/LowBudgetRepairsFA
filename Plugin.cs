using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LowBudgetRepairsPersianFix
{
    [BepInPlugin("com.farsiorigin.persianlang", "Low Budget Repairs Persian Native Injection", "3.0.0")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger = null!;
        private static readonly string LangFilePath = Path.Combine(Paths.GameRootPath, "BepInEx", "plugins", "fa.json");
        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>();

        public override void Load()
        {
            Logger = Log;
            Logger.LogInfo("Persian Native Language System v3.0.0 Loaded.");

            LoadLanguageFile();

            var harmony = new Harmony("com.farsiorigin.persianlang");

            Type? tmpType = AccessTools.TypeByName("TMPro.TMP_Text");
            if (tmpType != null)
            {
                PropertyInfo? textProp = AccessTools.Property(tmpType, "text");
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
                __0 = ProcessRichTextAndFix(__0);
            }
        }

        private static string ProcessRichTextAndFix(string input)
        {
            // ایزوله کردن تگ‌های HTML / Rich Text جهت جلوگیری از معکوس شدن آن‌ها
            string pattern = @"(<[^>]+>)";
            string[] parts = Regex.Split(input, pattern);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!Regex.IsMatch(parts[i], pattern) && ContainsPersian(parts[i]))
                {
                    parts[i] = PersianShaper.Fix(parts[i]);
                }
            }

            return string.Join("", parts);
        }

        private static void LoadLanguageFile()
        {
            try
            {
                if (!File.Exists(LangFilePath))
                {
                    File.WriteAllText(LangFilePath, "{\n  \"Find your house\": \"خانه خود را پیدا کنید\"\n}", Encoding.UTF8);
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
                                Translations[key] = ProcessRichTextAndFix(val);
                            }
                        }
                    }
                }
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

    public static class PersianShaper
    {
        public static string Fix(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            char[] chars = str.ToCharArray();
            StringBuilder output = new StringBuilder();

            for (int i = 0; i < chars.Length; i++)
            {
                output.Append(chars[i]);
            }

            char[] result = output.ToString().ToCharArray();
            Array.Reverse(result);
            return new string(result);
        }
    }
}
