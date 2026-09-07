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
    [BepInPlugin("com.farsiorigin.persianrtlfix", "Low Budget Repairs Persian RTL Fix", "1.1.0")]
    public class Plugin : BasePlugin
    {
        private static string dumpFilePath = Path.Combine(Paths.GameRootPath, "ExtractedText.txt");
        private static readonly HashSet<string> ExtractedLines = new HashSet<string>();

        public override void Load()
        {
            Log.LogInfo("Low Budget Repairs Persian RTL Fix v1.1.0 loaded.");

            var harmony = new Harmony("com.farsiorigin.persianrtlfix");

            // پیدا کردن Dynamic نوع TMP_Text در حافظه بازی
            Type tmpType = AccessTools.TypeByName("TMPro.TMP_Text");
            if (tmpType != null)
            {
                PropertyInfo textProperty = AccessTools.Property(tmpType, "text");
                if (textProperty != null && textProperty.SetMethod != null)
                {
                    MethodInfo prefixMethod = typeof(Plugin).GetMethod(nameof(TMP_Text_SetText_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(textProperty.SetMethod, prefix: new HarmonyMethod(prefixMethod));
                    Log.LogInfo("Successfully hooked TMPro.TMP_Text.text setter!");
                }
                else
                {
                    Log.LogWarning("Could not find property 'text' on TMPro.TMP_Text.");
                }
            }
            else
            {
                Log.LogWarning("Could not find type 'TMPro.TMP_Text'.");
            }
        }

        private static void TMP_Text_SetText_Prefix(ref string __0)
        {
            if (string.IsNullOrEmpty(__0)) return;

            ExtractText(__0);

            if (ContainsPersian(__0))
            {
                __0 = ArabicFixer.Fix(__0);
            }
        }

        private static bool ContainsPersian(string input)
        {
            foreach (char c in input)
            {
                if (c >= 0x0600 && c <= 0x06FF) return true;
            }
            return false;
        }

        private static void ExtractText(string text)
        {
            try
            {
                string cleanText = text.Replace("\r", "").Replace("\n", "\\n");
                if (ExtractedLines.Contains(cleanText)) return;

                ExtractedLines.Add(cleanText);
                string entry = cleanText + "=" + cleanText + Environment.NewLine;
                File.AppendAllText(dumpFilePath, entry, Encoding.UTF8);
            }
            catch { }
        }
    }

    public static class ArabicFixer
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
                    c = MapPersianGlyph(c);
                }
                result.Append(c);
            }

            char[] arr = result.ToString().ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        private static char MapPersianGlyph(char ch)
        {
            return ch switch
            {
                'آ' => 'ﺁ',
                'ا' => 'ﺍ',
                'ب' => 'ﺏ',
                'پ' => 'ﭖ',
                'ت' => 'ﺕ',
                'ث' => 'ﺙ',
                'ج' => 'ﺝ',
                'چ' => 'ﭺ',
                'ح' => 'ﺡ',
                'خ' => 'ﺥ',
                'د' => 'ﺩ',
                'ذ' => 'ﺫ',
                'ر' => 'ﺭ',
                'ز' => 'ﺯ',
                'ژ' => 'ﮊ',
                'س' => 'ﺱ',
                'ش' => 'ﺵ',
                'ص' => 'ﺹ',
                'ض' => 'ﺽ',
                'ط' => 'ﻁ',
                'ظ' => 'ﻅ',
                'ع' => 'ﻉ',
                'غ' => 'ﻍ',
                'ف' => 'ﻑ',
                'ق' => 'ﻕ',
                'ک' => 'ﮎ',
                'گ' => 'ﮒ',
                'ل' => 'ﻝ',
                'م' => 'ﻡ',
                'ن' => 'ﻥ',
                'و' => 'ﻭ',
                'ه' => 'ﻩ',
                'ی' => 'ﯼ',
                _ => ch
            };
        }
    }
}
